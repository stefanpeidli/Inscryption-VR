using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

namespace InscryptionVR
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Inscryption.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.inscryption.vr";
        public const string PluginName = "Inscryption VR Prototype";
        public const string PluginVersion = "0.3.4";

        private ConfigEntry<bool> _enableLegacyXr;
        private ConfigEntry<bool> _enableOpenVrProbe;
        private ConfigEntry<bool> _enableStereoRenderer;
        private ConfigEntry<bool> _enableControllerPointer;
        private ConfigEntry<float> _worldUnitsPerMeter;
        private ConfigEntry<float> _renderScale;
        private ConfigEntry<float> _tableTiltDegrees;
        private ConfigEntry<bool> _enableVrSubtitles;
        private ConfigEntry<bool> _autoMapOverview;
        private ConfigEntry<bool> _logRenderTiming;
        private ConfigEntry<string> _deviceName;
        private ConfigEntry<KeyCode> _recenterKey;
        private ConfigEntry<bool> _cameraDiagnostics;
        private bool _ownsXrEnable;
        private bool _xrApiAvailable;
        private string _lastCameraSignature;
        private float _nextCameraScan;

        private void Awake()
        {
            _autoMapOverview = Config.Bind("Rendering", "AutoMapOverview", true,
                "Use a raised overview during the map phase and return to the seated view on leaving the map. T selects top-down; S selects seated for the current phase.");
            _logRenderTiming = Config.Bind("Diagnostics", "LogRenderTiming", true,
                "Read SteamVR compositor timing/counters periodically without changing rendering or runtime settings.");
            _enableVrSubtitles = Config.Bind("UI", "EnableVrSubtitles", true,
                "Mirror dialogue text into a stereo world-space panel above the center of the headset view.");
            _tableTiltDegrees = Config.Bind("Rendering", "TableTiltDegrees", 10f,
                new ConfigDescription("Downward viewing angle in degrees, bringing the tabletop into a more comfortable view. F7 toggles it; zero restores the previous level view. Requires restart.",
                    new AcceptableValueRange<float>(0f, 25f)));
            _enableControllerPointer = Config.Bind("Input", "EnableControllerPointer", false,
                "Experimental right-controller ray and trigger selection in supported battle views. " +
                "Requires stereo rendering. Desktop keyboard/menus remain available; F10 toggles controller input.");
            _enableStereoRenderer = Config.Bind("Experimental", "EnableStereoRenderer", false,
                "Start the experimental native OpenVR table renderer in Part1_Cabin. Requires SteamVR and a connected headset. " +
                "F8 recenters, F9 stops rendering. Controller pointing is a separate option; menus and image effects remain unfinished.");
            _worldUnitsPerMeter = Config.Bind("Rendering", "WorldUnitsPerMeter", 1f,
                new ConfigDescription("Game world units corresponding to one tracked meter. Initial scale requires headset verification.",
                    new AcceptableValueRange<float>(0.1f, 20f)));
            _renderScale = Config.Bind("Rendering", "RenderScale", 0.65f,
                new ConfigDescription("Fraction of SteamVR's recommended eye resolution; conservative for this 32-bit player.",
                    new AcceptableValueRange<float>(0.25f, 1f)));
            _enableOpenVrProbe = Config.Bind("Experimental", "EnableOpenVRProbe", false,
                "Run one native OpenVR Background connection probe at startup. Requires restarting the game. " +
                "Install and start SteamVR yourself first. This checks connectivity only and does not render VR.");
            _enableLegacyXr = Config.Bind("Experimental", "EnableLegacyXR", false,
                "Attempt the VR provider already included in the game build. Requires restarting the game. " +
                "This is a rendering capability experiment, not a playable VR conversion.");
            _deviceName = Config.Bind("Experimental", "LegacyDeviceName", "OpenVR",
                "Provider to request only when it appears in XRSettings.supportedDevices. " +
                "OpenVR is intended for SteamVR. Requires restarting the game.");
            _recenterKey = Config.Bind("Input", "RecenterKey", KeyCode.F8,
                "Keyboard fallback for runtime recentering when XR is active. None disables the key. " +
                "The runtime may ignore legacy recentering. The native stereo prototype uses F8.");
            _cameraDiagnostics = Config.Bind("Diagnostics", "LogCameraChanges", true,
                "Log camera name, stereo settings and projection when the active camera set changes.");

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Unity " + Application.unityVersion +
                "; process " + (IntPtr.Size * 8) + "-bit; game " + Application.version + ".");
            Logger.LogInfo("Stereo table renderer with optional controller pointer; stereo UI and complete gameplay coverage remain experimental.");
            Logger.LogInfo("Graphics API: " + SystemInfo.graphicsDeviceType + "; GPU: " + SystemInfo.graphicsDeviceName + ".");
            LogXrState("Startup");
            LogSubsystemDescriptors();
            if (_enableStereoRenderer.Value)
            {
                if (_enableLegacyXr.Value || _enableOpenVrProbe.Value)
                    Logger.LogWarning("Stereo renderer requires EnableLegacyXR and EnableOpenVRProbe to be false; skipping conflicting experiments.");
                else
                {
                    var stereo = gameObject.AddComponent<StereoRenderer>();
                    stereo.Initialize(Logger, _worldUnitsPerMeter.Value, _renderScale.Value, _tableTiltDegrees.Value, _autoMapOverview.Value);
                    if (_logRenderTiming.Value) gameObject.AddComponent<RenderTimingDiagnostics>().Initialize(Logger, stereo);
                    var dialogue = gameObject.AddComponent<VrDialogue>();
                    dialogue.Initialize(Logger, stereo, _enableVrSubtitles.Value);
                    stereo.SetDialogue(dialogue);
                    if (_enableControllerPointer.Value)
                        gameObject.AddComponent<ControllerPointer>().Initialize(Logger, stereo, dialogue);
                }
            }
            else if (_enableOpenVrProbe.Value)
            {
                if (_enableLegacyXr.Value)
                    Logger.LogWarning("OpenVR probe skipped while EnableLegacyXR is true; select one experiment at a time.");
                else
                    OpenVrProbe.Run(Logger);
            }
            if (_enableLegacyXr.Value && _xrApiAvailable)
                StartCoroutine(InitializeLegacyXr());
            else if (!_enableLegacyXr.Value)
                Logger.LogInfo("EnableLegacyXR is false. XR settings will remain untouched.");
        }

        private IEnumerator InitializeLegacyXr()
        {
            string requested = (_deviceName.Value ?? string.Empty).Trim();
            string loaded = string.Empty;
            bool shouldLoad = false;
            bool accepted = false;
            try
            {
                loaded = XRSettings.loadedDeviceName ?? string.Empty;
                if (string.IsNullOrEmpty(requested))
                {
                    Logger.LogWarning("No legacy XR provider requested; skipping initialization.");
                }
                else if (!string.IsNullOrEmpty(loaded) &&
                         !string.Equals(loaded, requested, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("XR provider '" + loaded + "' is already loaded. Refusing to replace it.");
                }
                else
                {
                    string supportedName = FindSupportedDevice(requested, XRSettings.supportedDevices);
                    if (supportedName == null)
                    {
                        Logger.LogWarning("Requested XR provider '" + requested + "' was not included in this game build. " +
                            "Legacy initialization cannot add native providers. A separate VR backend is required.");
                    }
                    else
                    {
                        requested = supportedName;
                        accepted = true;
                        shouldLoad = string.IsNullOrEmpty(loaded);
                        if (shouldLoad)
                        {
                            Logger.LogInfo("Requesting legacy XR provider '" + requested + "'.");
                            XRSettings.LoadDeviceByName(requested);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Legacy XR initialization unavailable: " + exception.Message);
                yield break;
            }

            if (!accepted)
                yield break;

            // Unity completes LoadDeviceByName at the beginning of the next frame.
            if (shouldLoad)
                yield return null;

            try
            {
                loaded = XRSettings.loadedDeviceName ?? string.Empty;
                if (!string.IsNullOrEmpty(requested) &&
                    string.Equals(loaded, requested, StringComparison.OrdinalIgnoreCase))
                {
                    if (!XRSettings.enabled)
                    {
                        XRSettings.enabled = true;
                        _ownsXrEnable = true;
                    }
                    Logger.LogInfo("XR provider loaded. Headset activity and stereo rendering still require in-game verification.");
                }
                else if (shouldLoad)
                    Logger.LogWarning("The requested legacy XR provider failed to load; desktop mode remains available.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Could not enable legacy XR: " + exception.Message);
            }
            yield return null;
            LogXrState("After initialization");
        }

        internal static string FindSupportedDevice(string requested, string[] supported)
        {
            if (supported == null)
                return null;
            foreach (string device in supported)
                if (string.Equals(device, requested, StringComparison.OrdinalIgnoreCase))
                    return device;
            return null;
        }

        private void LogXrState(string phase)
        {
            try
            {
                string[] supported = XRSettings.supportedDevices ?? new string[0];
                Logger.LogInfo(phase + " XR: supported=[" + string.Join(", ", supported) +
                    "], loaded='" + XRSettings.loadedDeviceName + "', enabled=" + XRSettings.enabled +
                    ", active=" + XRSettings.isDeviceActive + ", eye=" + XRSettings.eyeTextureWidth +
                    "x" + XRSettings.eyeTextureHeight + ".");
                _xrApiAvailable = true;
                if (supported.Length == 0)
                    Logger.LogWarning("No legacy XR providers reported. XR module DLLs alone do not supply VR support.");
            }
            catch (Exception exception)
            {
                _xrApiAvailable = false;
                Logger.LogWarning("XR diagnostics unavailable: " + exception.Message);
            }
        }

        private void LogSubsystemDescriptors()
        {
            try
            {
                var displays = new List<XRDisplaySubsystemDescriptor>();
                var inputs = new List<XRInputSubsystemDescriptor>();
                SubsystemManager.GetSubsystemDescriptors(displays);
                SubsystemManager.GetSubsystemDescriptors(inputs);
                Logger.LogInfo("XR subsystem descriptors: display=" + displays.Count + ", input=" + inputs.Count + ".");
                foreach (XRDisplaySubsystemDescriptor display in displays)
                    Logger.LogInfo("XR display descriptor: '" + display.id + "'.");
                foreach (XRInputSubsystemDescriptor input in inputs)
                    Logger.LogInfo("XR input descriptor: '" + input.id + "'.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning("XR subsystem discovery unavailable: " + exception.Message);
            }
        }

        private void Update()
        {
            if (_xrApiAvailable && _recenterKey.Value != KeyCode.None && Input.GetKeyDown(_recenterKey.Value))
                RequestRecenter();
            if (_cameraDiagnostics.Value && Time.unscaledTime >= _nextCameraScan)
            {
                _nextCameraScan = Time.unscaledTime + 2f;
                LogCameraChanges();
            }
        }

        /// <summary>Input-independent entry point for a future controller adapter.</summary>
        public void RequestRecenter()
        {
            try
            {
                if (!_xrApiAvailable || !XRSettings.enabled || !XRSettings.isDeviceActive)
                {
                    Logger.LogInfo("Recenter skipped: no active XR headset.");
                    return;
                }
                // This experiment uses the legacy provider API, so use its matching recenter entry point.
#pragma warning disable 618
                InputTracking.Recenter();
#pragma warning restore 618
                Logger.LogInfo("Runtime recenter requested. The active runtime controls whether this takes effect.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Runtime recenter unavailable: " + exception.Message);
            }
        }

        private void LogCameraChanges()
        {
            Camera[] cameras = Camera.allCameras;
            Array.Sort(cameras, delegate(Camera left, Camera right)
            {
                return left.GetInstanceID().CompareTo(right.GetInstanceID());
            });
            string sceneName = SceneManager.GetActiveScene().name;
            string signature = sceneName + ";";
            foreach (Camera camera in cameras)
                signature += camera.GetInstanceID() + ":" + camera.name + ":" + camera.stereoEnabled + ":" +
                    camera.stereoTargetEye + ":" + camera.orthographic + ":" + camera.fieldOfView + ";";
            if (signature == _lastCameraSignature)
                return;
            _lastCameraSignature = signature;
            Logger.LogInfo("Scene '" + sceneName + "', active cameras: " + cameras.Length + ".");
            foreach (Camera camera in cameras)
                Logger.LogInfo("Camera '" + camera.name + "': stereo=" + camera.stereoEnabled +
                    ", eyes=" + camera.stereoTargetEye + ", orthographic=" + camera.orthographic +
                    ", FOV=" + camera.fieldOfView + ", near=" + camera.nearClipPlane +
                    ", far=" + camera.farClipPlane + ", depth=" + camera.depth + ".");
        }

        private void OnDestroy()
        {
            if (!_ownsXrEnable)
                return;
            try
            {
                XRSettings.enabled = false;
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Could not restore XR enabled state: " + exception.Message);
            }
        }
    }
}
