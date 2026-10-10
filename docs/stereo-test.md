# Stereo table test: retained 0.2.0 native renderer

The 0.3.1 managed controller prototype retains the exact native renderer from the user-verified 0.2.0 build. The 0.2.1 timing change caused a regression and crash and was withdrawn. Minor movement distortion remains unresolved.

1. Start SteamVR and connect Quest 3 through Steam Link.
2. Launch Inscryption through Epic and enter an Act 1 table scene or Kaycee's Mod battle. VR starts after the cabin camera is available.
3. Face forward naturally and press **F8** on the keyboard to recenter. Check upright text, depth, card readability, and gentle head rotation and translation.
4. Press **F9** to stop rendering if needed. Scene exit stops the session; restart the game for another rendering test.

The seating anchor stays stationary while the desktop camera changes scripted views. F8 captures a fresh camera position and yaw. World scale remains uncalibrated. Menus, some UI/effects, and story/cabin traversal remain unfinished. See [controller test instructions](controller-test.md) for the new pointer.

The managed build compiles against the installed game libraries. The exact retained x86 native DLL passes 49 mock ABI/lifetime assertions. These mocks use no real SteamVR session or GPU textures and do not establish visual quality or comfort. Eye textures remain alive until native render-thread cleanup is acknowledged, and the bridge stays loaded until process exit.