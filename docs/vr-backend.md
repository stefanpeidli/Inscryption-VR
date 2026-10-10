# VR backend investigation

Inspected the local Epic installation read-only on 2026-10-08. This document records feasibility findings, not a successful headset test.

## Local evidence

Installation: `C:\Program Files\Epic Games\InscryptionWkwPQ`.

| Item | Observed value | Implication |
| --- | --- | --- |
| Unity serialized-file header | `2019.4.24f1` in `Inscryption_Data/globalgamemanagers` | Match APIs and any native Unity provider to this engine generation. |
| UnityPlayer.dll PE machine | `0x014C` (Intel x86) | Native modules must be 32-bit. A 64-bit Windows OS does not make this game a 64-bit process. |
| Managed runtime | `MonoBleedingEdge` and managed game assemblies | A Mono-compatible BepInEx plugin is a reasonable entry point. |
| boot.config | `vr-enabled=0` | The installed build starts with VR disabled. |
| Managed VR APIs | `UnityEngine.XRModule.dll`, `UnityEngine.VRModule.dll` | API assemblies exist, but do not establish a working display provider. |
| Plugins/x86 | Galaxy.dll, GalaxyCSharpGlue.dll, Rewired_DirectInput.dll, steam_api.dll, steam_api64.dll | No OpenVR/XR display provider was found in this directory. |

The legacy Unity pipeline relies on devices selected when the player was built. `-vrmode OpenVR` and `XRSettings.LoadDeviceByName("OpenVR")` cannot manufacture an absent provider or compiled device configuration. Unity's [legacy VR overview](https://docs.unity.cn/2019.2/Documentation/Manual/VROverview.html) explains this restriction. The [2019.4 documentation](https://docs.unity3d.com/2019.4/Documentation/Manual/VROverview.html) instead recommends XR Management and platform providers. The exact viable path in this player remains unverified.

## Backend choice

Target the SteamVR runtime on the PC. The Quest runs Steam Link; the mod runs inside the Windows game. Epic ownership does not require an Epic-specific headset renderer. Valve describes OpenVR as a hardware-independent API whose runtime is SteamVR in its [SDK repository](https://github.com/ValveSoftware/openvr).

Use this decision order:

1. **Diagnostic plugin:** report architecture, graphics API, loaded Unity/game version, legacy supported/loaded XR devices, XR display/input subsystem descriptors, enabled state, active scene and camera inventory. This is the smallest useful deliverable and must not advertise stereo rendering.
2. **Legacy capability probe:** if `supportedDevices` actually includes OpenVR, offer an explicit opt-in load/enable attempt, wait the required frame boundary, and report the result. A successful load still requires headset verification. Do not repeatedly attempt initialization every frame.
3. **Unity XR provider experiment:** if legacy support is unavailable, test Valve's OpenVR XR provider in an isolated copy. This needs compatible managed loader dependencies, native provider registration/configuration, and x86 native libraries. Valve ships its prominent prebuilt provider under `Runtime/x64`; its repository also documents Win32 source builds. Copying the x64 DLL into this game will fail. See [Valve's provider repository and build instructions](https://github.com/ValveSoftware/unity-xr-plugin). Runtime injection into this prebuilt player is an experiment, not the normal editor installation procedure.
4. **Direct OpenVR rendering fallback:** use the official x86 `openvr_api.dll`, matched C# bindings or a small x86 native bridge, two eye textures, headset poses, eye transforms, projections and render-thread texture submission. Valve maintains a [Win32 SDK binary directory](https://github.com/ValveSoftware/openvr/tree/master/bin/win32). Its [compositor overview](https://github.com/ValveSoftware/openvr/wiki/IVRCompositor_Overview) specifies pose acquisition, per-eye rendering and compositor submission. This avoids assuming the game's Unity XR configuration, but makes us responsible for synchronization, graphics-device compatibility, lifecycle and camera effects. Prefer D3D11 for the first experiment after the actual graphics API is logged.

Neither fallback is implemented or validated yet. Keep backend details behind an interface so the Act 1 interaction code can survive changing the renderer.

## Framework candidates

| Candidate | Assessment |
| --- | --- |
| [Original VRGIN](https://github.com/Eusth/VRGIN) | Useful reference for camera/UI handling and seated modes. Its stated compatibility table covers Unity 5.0–5.6; this is not a supported drop-in for 2019.4. It also does not provide the plugin injection mechanism. |
| [VRGIN_OpenXR fork](https://github.com/ManlyMarco/VRGIN_OpenXR) | Maintainer reports use in Unity 2018/2019. Native x86 availability, dependencies, license/provenance and this game's renderer still need review before adoption. |
| [UnityVRMod](https://github.com/NewUnityModder/UnityVRMod) | Current README describes Windows 64-bit/D3D11 support. This local x86 player is outside that stated target. |
| Valve OpenVR XR provider | Primary-source integration path for Unity XR. Its editor instructions cannot be assumed to apply to runtime mod injection. Rendering and controller input are separate concerns. |

## Honest proof-of-concept exit criteria

The first rendering experiment should show a static seated table scene with distinct left/right views and tracked head rotation/translation, plus recentering. Keep keyboard/mouse input initially. Record initialization and shutdown behavior, loss of tracking, per-eye projection, scale, card readability and frame timing. Verify on the Quest 3 through Steam Link before calling it a working VR prototype.

After that, add controller pointing and battle actions in Kaycee's Mod. Act 1 cabin traversal, forced camera transitions, puzzles and cinematics come later. A plugin that logs XR availability is groundwork; a desktop side-by-side image is not evidence that the SteamVR compositor displays correctly.

No game files, save files, SteamVR configuration or installed runtime were changed during this investigation.
