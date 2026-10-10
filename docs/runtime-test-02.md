# Runtime test 02: native OpenVR connection succeeded

The user started SteamVR, connected the Quest 3 through Steam Link, launched Inscryption through Epic to the main menu and exited. The installed `BepInEx/LogOutput.log` confirms:

- Inscryption VR Prototype 0.1.1 loaded in the 32-bit Unity 2019.4.24f1 process.
- Graphics API remains Direct3D11.
- `OpenVR connection probe: runtime installed=True, HMD present=True.`
- `OpenVR Background connection succeeded; token=1.`
- `OpenVR connection probe shutdown completed.`
- No Error/Fatal/Exception entries found by targeted search of this log.

## Meaning and next milestone

The packaged x86 OpenVR client can detect the connected headset, establish a SteamVR connection from inside the Epic game process and shut down cleanly. The game's empty Unity XR configuration does not prevent this native communication path.

This does not validate stereo rendering, camera tracking, compositor submission or motion-controller interaction. The next engineering milestone is a Scene-mode OpenVR renderer: query eye transforms/projection and headset poses, render one texture per eye, submit on an appropriate rendering boundary and verify a stable tracked table scene. PixelCamera and overlay/image-effect handling remain integration risks. Retain desktop input during the first rendering experiment.

The connection probe can now be disabled for normal launches. Preserve the successful log as evidence before another run overwrites it.
