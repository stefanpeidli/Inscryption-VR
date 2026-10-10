# Direct OpenVR capability probe

The user's game diagnostics report `supported=[None]`, no Unity XR display/input subsystem descriptors, D3D11 and an RTX 5060 Ti. The next experiment connects directly to SteamVR using the official OpenVR client library. This probe does not render stereo images, track the game camera or implement controllers.

## Run the update

1. Close Inscryption and extract `InscryptionVR-openvr-probe-update-x86.zip` directly beside `Inscryption.exe`, accepting replacement of `BepInEx/plugins/InscryptionVR/InscryptionVR.dll`. This update contains plugin files, not a replacement mod loader.
2. Edit `BepInEx/config/local.inscryption.vr.cfg`. Under the existing `[Experimental]` section, add or change `EnableOpenVRProbe = true`. Keep `EnableLegacyXR = false`. Do not add a second copy of the section or setting.
3. Start SteamVR on the PC and connect the Quest 3 through Steam Link. Launch Inscryption through Epic, wait for the main menu, then exit. A battle is not needed for this connection test.
4. Let the development agent read `BepInEx/LogOutput.log`. It should contain `Inscryption VR Prototype 0.1.1` and `OpenVR connection probe` lines. The headset will not show a VR game scene yet.
5. Set `EnableOpenVRProbe = false` after the test to avoid repeating it on normal launches.

## Pinned dependency

- Official SDK tag: [ValveSoftware/openvr v2.12.14](https://github.com/ValveSoftware/openvr/releases/tag/v2.12.14).
- Win32 client DLL: [tag-pinned download](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/bin/win32/openvr_api.dll).
- SHA-256 recorded by the dependency download step: `FD100F4F6730B5FB01D644EDCA96C7280A09481F044F61695908FEBA2D9663C6`.
- Source signatures: [generated C# bindings](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr_api.cs), [C++ header](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr.h), [client-library implementation](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/src/openvr_api_public.cpp).

The local downloaded bindings at `.dependencies/OpenVR/openvr_api.cs`, lines 4775–4782, confirm the exported entry points and calling convention. Preserve the SDK's license when distributing its binary. Use the x86 binary for this x86 game regardless of the PC's OS architecture.

## Export ABI

The DLL exports below use **Cdecl**. This differs from the **StdCall** function-table delegates elsewhere in the generated bindings. Confusing those conventions on x86 can corrupt the stack.

| Export | Managed signature |
| --- | --- |
| `VR_InitInternal2` | `uint(ref int error, int applicationType, IntPtr startupInfo)` |
| `VR_ShutdownInternal` | `void()` |
| `VR_IsHmdPresent` | `bool()` with one-byte return marshaling |
| `VR_IsRuntimeInstalled` | `bool()` with one-byte return marshaling |
| `VR_GetVRInitErrorAsSymbol` | `IntPtr(int error)` |
| `VR_GetVRInitErrorAsEnglishDescription` | `IntPtr(int error)` |

`EVRInitError` and `EVRApplicationType` are 32-bit enums. Initialization returns a 32-bit token, not an interface pointer. Pass `IntPtr.Zero` for absent startup information; a string alternative needs LPStr marshaling. Native C++ bool exports should use `[return: MarshalAs(UnmanagedType.I1)]` to describe their ABI explicitly, even though Valve's generated declarations omit that attribute for these exports. Convert borrowed error-description pointers to strings; do not free them.

For an absolute-path native load followed by `GetProcAddress`, place `UnmanagedFunctionPointer(CallingConvention.Cdecl)` on each delegate and retain the DLL handle until all calls finish. Verify required exports before attempting initialization. Avoid changing the process-wide DLL search path.

## Background rather than Scene

The pinned C++ header defines `VRApplication_Background = 3`: it should neither start SteamVR nor keep it running after other applications quit. `VRApplication_Scene = 1` identifies a client submitting 3D frames. Use Background for this diagnostic connection. Reserve Scene for the later renderer experiment.

Start SteamVR and connect the Quest 3 through Steam Link before launching the game. A failed Background initialization with SteamVR stopped is an expected diagnostic result. Utility type 4 avoids loading drivers and is therefore insufficient for a hardware-connection probe.

## Lifecycle and interpretation

Keep the experiment opt-in and make one attempt per launch. Log runtime-installed and HMD-present checks, then the initialization error number and symbol/description. Those lightweight availability checks are callable before initialization. They do not guarantee initialization success or valid headset tracking.

If initialization succeeds, shut down the connection once in `finally` before releasing this probe's DLL handle. A nonzero token with error zero is the expected success result. The pinned client implementation cleans up its module when initialization fails; do not claim ownership of a connection after a failed call. Skip this probe when another VR backend is already active in the game process, since init/shutdown manages process-level client state.

Managed exception handling can report missing DLLs/exports and ordinary marshaling failures. It cannot guarantee recovery from a native stack mismatch or forcibly cancel a synchronous runtime call safely. A frame-based timeout does not solve that issue. Keep the ABI small and verified and avoid retries.

Success means **the x86 game process can establish and release a native SteamVR connection**. It does not establish compositor submission, working stereo rendering, head tracking, controller bindings or playable VR. The next experiment must separately verify eye textures and submission, coordinate conversion and render synchronization on the headset.
