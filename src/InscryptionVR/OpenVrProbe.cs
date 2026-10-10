using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;

namespace InscryptionVR
{
    /// <summary>One-shot connection diagnostics, without a compositor or rendering session.</summary>
    internal static class OpenVrProbe
    {
        // Valve OpenVR v2.12.14 headers/openvr.h: exported API functions are Cdecl.
        // Function-table methods use a different calling convention; none are used here.
        private const int BackgroundApplication = 3;
        private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
        private const uint LoadLibrarySearchDefaultDirs = 0x00001000;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool QueryBoolean();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint Initialize(ref int error, int applicationType, IntPtr startupInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Shutdown();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr DescribeError(int error);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string fileName, IntPtr reserved, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        internal static void Run(ManualLogSource logger)
        {
            if (IntPtr.Size != 4)
            {
                logger.LogWarning("OpenVR probe skipped: packaged native provider requires an x86 process.");
                return;
            }

            IntPtr library = IntPtr.Zero;
            Shutdown shutdown = null;
            bool connectionOwned = false;
            try
            {
                // Do not initialize or shut down another mod's shared OpenVR session.
                if (GetModuleHandleW("openvr_api.dll") != IntPtr.Zero)
                {
                    logger.LogWarning("OpenVR probe skipped: openvr_api.dll is already loaded in this process.");
                    return;
                }
                string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string libraryPath = Path.GetFullPath(Path.Combine(pluginDirectory, "openvr_api.dll"));
                if (!File.Exists(libraryPath))
                {
                    logger.LogWarning("OpenVR probe skipped: native provider missing at '" + libraryPath + "'.");
                    return;
                }
                library = LoadLibraryExW(libraryPath, IntPtr.Zero,
                    LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirs);
                if (library == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load packaged OpenVR provider");

                var runtimeInstalled = Export<QueryBoolean>(library, "VR_IsRuntimeInstalled");
                var hmdPresent = Export<QueryBoolean>(library, "VR_IsHmdPresent");
                var initialize = Export<Initialize>(library, "VR_InitInternal2");
                shutdown = Export<Shutdown>(library, "VR_ShutdownInternal");
                var describeError = Export<DescribeError>(library, "VR_GetVRInitErrorAsEnglishDescription");

                bool installed = runtimeInstalled();
                bool present = hmdPresent();
                logger.LogInfo("OpenVR connection probe: runtime installed=" + installed + ", HMD present=" + present + ".");
                if (!installed)
                {
                    logger.LogWarning("OpenVR connection probe requires a SteamVR runtime installation.");
                    return;
                }
                logger.LogInfo("Attempting one OpenVR Background connection; this application type does not start SteamVR.");
                int error = 0;
                uint token = initialize(ref error, BackgroundApplication, IntPtr.Zero);
                connectionOwned = error == 0;
                string description = Marshal.PtrToStringAnsi(describeError(error)) ?? "Unknown error";
                if (error == 0 && token != 0)
                    logger.LogInfo("OpenVR Background connection succeeded; token=" + token + ". " +
                        "No stereo rendering, tracking adapter, or controller input is implemented by this probe.");
                else if (error == 0)
                    logger.LogWarning("OpenVR initialization returned success with a zero token; connection is unverified.");
                else
                    logger.LogWarning("OpenVR Background connection failed: error=" + error + " (" + description + ").");
            }
            catch (Exception exception)
            {
                logger.LogWarning("OpenVR connection probe unavailable: " + exception.Message);
            }
            finally
            {
                if (connectionOwned && shutdown != null)
                {
                    try
                    {
                        shutdown();
                        logger.LogInfo("OpenVR connection probe shutdown completed.");
                    }
                    catch (Exception exception)
                    {
                        logger.LogWarning("OpenVR probe shutdown failed: " + exception.Message);
                    }
                }
                if (library != IntPtr.Zero && !FreeLibrary(library))
                    logger.LogWarning("OpenVR probe library release failed: " + Marshal.GetLastWin32Error() + ".");
            }
        }

        private static T Export<T>(IntPtr library, string name) where T : class
        {
            IntPtr address = GetProcAddress(library, name);
            if (address == IntPtr.Zero)
                throw new EntryPointNotFoundException("Packaged OpenVR provider lacks " + name + ".");
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }
    }
}
