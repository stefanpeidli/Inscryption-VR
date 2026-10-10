using System;
using BepInEx.Logging;

namespace InscryptionVR
{
    internal static class OpenVrProbeSmoke
    {
        private static void Main()
        {
            Console.WriteLine("Probe smoke test process: " + (IntPtr.Size * 8) + "-bit");
            var logger = new ManualLogSource("OpenVR smoke test");
            logger.LogEvent += delegate(object sender, LogEventArgs args)
            {
                Console.WriteLine(args.Level + ": " + args.Data);
            };
            OpenVrProbe.Run(logger);
        }
    }
}
