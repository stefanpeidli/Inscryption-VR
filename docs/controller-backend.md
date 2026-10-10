# Controller input on the existing OpenVR session

This is a read-only investigation for adding managed controller pointing while preserving the known working 0.2.0 native renderer. No native/session initialization changes are required for the ordinary controller attachment path.

## Attach rather than initialize

The pinned generated bindings in `.dependencies/OpenVR/openvr_api.cs` expose `Valve.VR.OpenVR.System`. Its context checks the native init token, clears stale cached wrappers when that token changes, and requests `FnTable:IVRSystem_023` through `VR_GetGenericInterface`. That matches the native renderer's system interface. This property does not call `OpenVR.Init`, `InitInternal`, `InitInternal2`, or `Shutdown`.

Only read this property while the renderer reports an active initialized Scene session. Reacquire it each update rather than retaining a `CVRSystem` reference across shutdown. A null result means that input should be unavailable. Do not attempt to repair that condition by initializing a second session.

Controller code must not call `WaitGetPoses`, change compositor tracking space, recenter the runtime, or shut down OpenVR. Those operations belong to the renderer's lifecycle. Stop controller polling before requesting native shutdown and suppress input when the renderer has stopped or is reporting a fatal state. Token checks help invalidate cached wrappers; they are not a synchronization mechanism for arbitrary concurrent shutdown.

The official [pinned generated bindings](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr_api.cs) and [pinned native header](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr.h) are the primary sources. The complete local C# file compiled successfully with the Windows .NET compiler using `/langversion:5 /platform:x86 /target:library`, without modifying it. Compile without `OPENVR_XR_API`, which excludes the bindings. Unity-specific convenience helpers are optional and not needed for this input path.

## Exact calls and values

| Operation | Managed call / contract |
| --- | --- |
| Find hand | `uint GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole role)`; LeftHand = 1, RightHand = 2. Do not assume device indices 1/2. |
| Reject missing hand | Compare against `OpenVR.k_unTrackedDeviceIndexInvalid` (`0xFFFFFFFF`) and array bounds; require controller class and connectivity. |
| Read state | `bool GetControllerState(uint index, ref VRControllerState_t state, uint size)`; calculate size with `Marshal.SizeOf(typeof(VRControllerState_t))`. Windows layout is 64 bytes. |
| Read state and pose together | `bool GetControllerStateWithPose(ETrackingUniverseOrigin origin, uint index, ref VRControllerState_t state, uint size, ref TrackedDevicePose_t pose)`; useful for the first pointer adapter. |
| Sample all poses separately | `void GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin origin, float predictionSeconds, TrackedDevicePose_t[] poses)`; wrapper passes the array length automatically. Reuse an array of `OpenVR.k_unMaxTrackedDeviceCount` entries. |
| Input availability | `bool IsInputAvailable()`; require true before allowing clicks. |

The existing renderer uses **TrackingUniverseSeated = 0**. Use seated controller poses too. Standing poses would introduce a different origin and make the ray appear offset. Require `bDeviceIsConnected`, `bPoseIsValid` and finite/nondegenerate transform data. Neither role discovery nor a valid button state establishes valid tracking on its own.

The generated function-table delegates already declare StdCall and one-byte native bool results. Preserve the official structs and wrappers rather than hand-building a partial table. Windows uses the ordinary sequential `VRControllerState_t`; the file's packed variant is an explicit compatibility workaround for Linux/macOS.

## Trigger and axes

The legacy digital trigger bit is `1UL << (int)EVRButtonId.k_EButton_SteamVR_Trigger`, where the enum value is 33. Read `ulButtonPressed`, not `ulButtonTouched`, for activation. A rising edge produces one click; a held trigger must not repeatedly activate cards. Reset or baseline edge tracking on controller disconnect, invalid pose, focus loss, hand change and session stop. Regaining tracking while the trigger is already held should require release before another click.

For an analog fallback, discover which of the five axis properties reports `k_eControllerAxis_Trigger = 3`. Query `GetInt32TrackedDeviceProperty` with `Prop_Axis0Type_Int32` through `Prop_Axis4Type_Int32` (3002–3006), checking its property error. Read the corresponding `state.rAxisN.x`; do not assume the trigger is always `rAxis1`. Use separate press/release thresholds to avoid chatter.

Quest controllers supplied through Steam Link need an actual runtime check of these legacy mappings. Log role, discovered axis type, state-query success and trigger transitions. If the driver does not provide useful legacy state, SteamVR action input with a manifest and tested binding is a later fallback; adding another OpenVR initialization would not solve mapping availability.

## Pose-to-ray conversion

Reuse `StereoPose.Position` and `StereoPose.Rotation` with the 12 floats from `pose.mDeviceToAbsoluteTracking`; this applies the same OpenVR-to-Unity basis change used for the head. Then apply the renderer's captured tracking-origin inverse, anchor position/rotation and world-units-per-metre scale. The pointer and eye cameras must share that rig mapping and F8 recenter state.

The ray starts at the converted controller position and points along the converted controller rotation's Unity forward direction. Actual Quest grip-versus-aim orientation may need a small configurable local pitch offset after headset testing. Do not compensate for origin or handedness errors with an arbitrary positional offset.

Use a visible ray/hit marker and existing game interactable handlers. Keep input polling independent of stereo submission: reading controller state must not add compositor waits, submit frames, or mutate the desktop camera. Disable pointer activation in unsupported scenes, with no valid target, or while an overlay owns input.

## Validation scope

Check controller discovery and trigger edges first. Then verify ray alignment, F8 recenter consistency, lost-tracking recovery, focus/overlay suppression, hand switching and shutdown. The managed attachment itself is feasible from source inspection and C# 5 compilation; Quest mapping and game interactions remain runtime work.
