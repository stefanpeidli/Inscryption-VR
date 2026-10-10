# First runtime test

This package contains BepInEx 5.4.23.2 x86 and the Inscryption VR diagnostic plugin. It does not yet render the game in VR.

## Collect the capability log

1. Close Inscryption. This package is intended for the verified Epic x86 build. If your installation already has a mod loader, do not overwrite it with this package; install only the InscryptionVR plugin DLL into its plugin directory.
2. Extract the archive contents into the folder containing `Inscryption.exe`, currently `C:\Program Files\Epic Games\InscryptionWkwPQ`. Windows may require administrator permission. `winhttp.dll` should sit beside the game executable, and the plugin belongs at `BepInEx/plugins/InscryptionVR/InscryptionVR.dll`.
3. Launch Inscryption normally through Epic. Open Kaycee's Mod and reach a battle if it is already unlocked; otherwise enter an Act 1 table scene. VR probing is disabled by default and no progress is unlocked automatically.
4. Exit the game. The diagnostic log should be at `BepInEx/LogOutput.log`. Look for `Inscryption VR Prototype`, `Startup XR`, `XR subsystem descriptors`, `Graphics API`, and camera entries. A missing plugin banner means loader startup must be fixed before continuing.
5. Provide the log in the development chat, or let the agent read it from the installed game directory.

SteamVR and the headset are not required for this initial inventory. A zero-provider report is useful: it directs development to the native backend instead of blindly enabling XR.

## Conditional headset probe

Only if the log lists OpenVR as supported: start SteamVR, connect Quest 3 using Steam Link, and close Inscryption before editing `BepInEx/config/local.inscryption.vr.cfg`. Set `EnableLegacyXR = true` and leave `LegacyDeviceName = OpenVR`, then restart through Epic. Record the resulting log and what the headset shows. F8 requests recentering only if the runtime reports an active headset.

If the probe fails, set `EnableLegacyXR = false` before another normal run. A loaded provider or active headset report does not prove correct stereo, scale, or tracking; those require headset verification.

## Removing this test

For an installation that had no prior BepInEx loader, remove the package-added `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, and `BepInEx` directory after closing the game. Preserve any collected logs first. If a loader was already present, remove only `BepInEx/plugins/InscryptionVR` and this plugin's config file. Do not remove unrelated mods.

This diagnostic package does not patch `boot.config`, replace game assemblies, or edit save files. Running the game can still save progress normally.
