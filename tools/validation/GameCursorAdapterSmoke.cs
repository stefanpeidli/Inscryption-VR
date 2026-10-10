using System;
using System.IO;
using System.Reflection;

internal static class GameCursorAdapterSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Provide game managed, BepInEx core and candidate plugin paths.");
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request)
        {
            string name = new AssemblyName(request.Name).Name + ".dll";
            foreach (string directory in new[] { args[0], args[1] })
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Assembly.LoadFrom(Path.Combine(args[0], "Assembly-CSharp.dll"));
        Assembly plugin = Assembly.LoadFrom(args[2]);
        // Executes the production reflection lookups, including visibility flags.
        // Does not create Unity objects, initialize OpenVR or launch the game.
        foreach (string name in new[] { "InscryptionVR.GameCursorAdapter", "InscryptionVR.VrDialogueBindings", "InscryptionVR.SpecialEventInput" })
        {
            Type adapter = plugin.GetType(name, true);
            object instance = Activator.CreateInstance(adapter, true);
            if (instance == null) throw new Exception("Adapter constructor returned null.");
        }
        Console.WriteLine("PASS: production cursor/dialogue/event binding constructors resolved runtime members on the installed assemblies.");
        return 0;
    }
}
