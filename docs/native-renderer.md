# Native stereo rendering experiment

`native/InscryptionVRBridge.cpp` implements an x86 Windows bridge to the pinned OpenVR v2.12.14 SDK. It is a rendering experiment for the existing Unity 2019.4 Mono player. Compilation and compositor acceptance do not establish comfortable or playable VR; the Quest 3 must be checked through Steam Link.

## Dependencies and build

The source consumes the official `.dependencies/OpenVR/openvr_capi.h` and dynamically loads the official x86 `openvr_api.dll` supplied beside the managed plugin. It requests `FnTable:IVRSystem_023` and `FnTable:IVRCompositor_029`, avoiding compiler-specific C++ virtual object calls. SDK DLL exports use Cdecl; the pinned C function-table entries use StdCall on Windows. Unity's render-event callback is also StdCall. These distinctions matter for x86 stack cleanup.

Root's native build script uses the official portable Zig 0.13.0 toolchain targeting `x86-windows-gnu`, Windows headers, and a static runtime. Output must be named `InscryptionVR.Native.dll` and packaged beside `InscryptionVR.dll` and `openvr_api.dll`. No OpenVR import library is required. The generated C header has a `__WIN32` branch that redeclares `bool` in C++; the bridge temporarily removes that alias while including it, retaining `_WIN32` and therefore the correct StdCall selection. The vendor header is unchanged.

Official references: [pinned C header](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr_capi.h), [native API implementation](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/src/openvr_api_public.cpp), and [compositor sequence](https://github.com/ValveSoftware/openvr/wiki/IVRCompositor_Overview).

## Managed/native contract

All exports below are Cdecl. Arrays contain contiguous floats. Matrices are OpenVR row-major, right-handed, and measured in metres.

| Export | Contract |
| --- | --- |
| `int ivr_start(const wchar_t* apiPath, uint32_t* width, uint32_t* height)` | LPWStr absolute DLL path. Initializes Scene application type 1. Returns 0 on success, positive OpenVR error, or negative bridge error. Reports per-eye recommended dimensions. |
| `int ivr_get_frame(float* head12, float* leftEye12, float* rightEye12, float* leftFrustum4, float* rightFrustum4, float near, float far)` | Returns 1 for a valid connected HMD pose, 0 for missing tracking, negative for unavailable or invalid request. Outputs 3x4 head/eye transforms and raw tangents in left/right/top/bottom order. Clip arguments are retained in the ABI but unused for raw tangents. |
| `void ivr_set_textures(void* left, void* right)` | Call each frame after eye rendering, before render event 1. Pointers must be Unity D3D11 native eye textures. Queues an immutable texture pair and captured pose, with COM AddRef on each resource. |
| `void* ivr_get_render_event_func()` | Returns the StdCall callback pointer used with `GL.IssuePluginEvent`. Event 1 submits the oldest pending frame. Event 2 cleans up. |
| `void ivr_shutdown()` | Requests cleanup; does not free rendering resources on the main thread. Follow with render event 2. |
| `int ivr_get_submit_status(int* leftError, int* rightError, uint32_t* successfulFrames)` | Returns 0 unopened, 1 running, 2 cleanup acknowledged, or negative bridge state. Eye errors are raw compositor enums. Count increments only when both eyes are accepted. |

Keep the native bridge DLL loaded until process exit: Unity may retain callback pointers in queued render commands. Keep Unity eye RenderTextures alive until status 2. Native cleanup releases queued COM references, shuts down its owned OpenVR client and unloads the OpenVR API module on the render thread. The bridge rejects an already loaded `openvr_api.dll` to avoid disturbing another mod's shared client state.

## Render sequence and timing

1. The main-thread pose call samples seated tracking with `GetDeviceToAbsoluteTrackingPose` and a zero prediction horizon, then obtains eye transforms and raw projection tangents.
2. Managed code renders both eye cameras, registers an immutable texture/pose pair and issues native event 1.
3. Unity's render-thread callback calls `WaitGetPoses`, then submits the queued images with their original sampled pose using `VRTextureWithPose_t` and flag 8. It performs handoff and records results.
4. The bounded queue retains texture COM references until submission or render-thread cleanup.

This is the sequence used by the user-verified 0.2.0 native binary, retained unchanged for the 0.3.1 controller prototype. It has unresolved latency and movement distortion. The 0.2.1 attempt to move the compositor wait before rendering caused a headset regression and crash and was withdrawn. Its mock tests did not validate live Unity synchronization.
The managed caller must require x86/D3D11, check pose/projection validity, and stop on reported compositor failures. No display-driver changes, Unity boot configuration edits, controller bindings, UI conversion, image effects, Act 1 camera patches, or save writes are part of this bridge.

Submission uses explicit vertically reversed bounds (`uMin=0, vMin=1, uMax=1, vMax=0`). Unity's [2019.4 graphics-platform documentation](https://docs.unity3d.com/2019.4/Documentation/Manual/SL-PlatformDifferences.html) states that rendering into RenderTextures on Direct3D internally flips the image to the OpenGL-like convention. OpenVR consumes the native Direct3D texture, so the bounds correct that orientation for these eye cameras, which have no image effects or MSAA. Headset verification must still check upright text and asymmetric scene landmarks.

Compositor error 101 (`DoNotHaveFocus`) is transient: another scene app has focus and the runtime throttles `WaitGetPoses` to 10 Hz. Keep issuing frame events so the bridge can acquire focus again; report the pause once instead of permanently stopping. Error 102 means `InvalidTexture`, not focus loss. Other texture/device/ABI failures warrant a stop and diagnosis. The pinned [OpenVR C++ header](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr.h) documents this distinction.

## Verification required

Verify the produced DLL's PE machine, exports and native runtime dependencies. Then use the actual headset to check initialization, distinct eyes, projection direction, orientation, eye separation, translation scale, card visibility, recentering, frame timing, tracking loss, scene exit and F9 cleanup. At shutdown, confirm native status 2 before destroying eye textures. Logs claiming a submitted frame should still state that visual quality awaits headset verification.
