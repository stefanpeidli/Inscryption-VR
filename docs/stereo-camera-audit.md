# First stereo camera experiment

## Evidence and boundary

The installed assembly was inspected read-only with Mono.Cecil from the already-downloaded BepInEx bundle. `tools/inspection/inspect-camera-il.ps1` reproduces the selected IL inspection. Runtime camera observations below come from the first user-run diagnostic log preserved at `artifacts/installed-backups/20261009-030305-5312/LogOutput.log`, lines 47–57.

The first experiment should run only in **`Part1_Cabin`** with a valid `DiskCardGame.ViewManager` and its perspective `pixelCamera` field resolved. This is a stereo table rendering experiment, not complete UI, story, or controller support. Do not automatically select the first active camera, `Camera.main`, or a name containing `Pixel`.

## Cameras actually observed

| Camera | Projection / initial FOV | Depth | Near / far | Interpretation |
| --- | --- | --- | --- | --- |
| `Pixel Camera` | Perspective / 60 | -1 | 0.3 / 50 | Primary table candidate; runtime FOV changes with game views. Confirm identity against `ViewManager.pixelCamera`. |
| `Effects Camera` | Perspective / 60 | 3 | 0.3 / 40 | Separate effects layer; cloning only the table camera will not prove the composed desktop image is complete. |
| `PerspectiveUICamera` | Perspective / 60 | 2 | 1 / 1000 | Separate perspective UI layer. |
| `OrthographicUICamera` | Orthographic | 3 | 0.3 / 1000 | Separate flat UI layer, including pause-related state. |
| `BackCamera` | Orthographic | -100 | 0 / 0.11 | Background layer; unsuitable as tracking source. |
| `CardRenderCamera_Live(Clone)`, `CardSnapshotCamera`, `EmissionRenderCamera` | Orthographic | 0 | 0.3 / 1000 | Card/emission helper cameras. Retain their original render-to-texture work. |
| `MapMarkingsCamera` | Orthographic | 0 | 0.3 / 1000 | Map helper camera. |

A separate `PixelCamera` without a space, orthographic with depth 2, also appears during a later transition. The Start scene uses `PixelCamera` and `EffectsCamera` without spaces and orthographic projection. These names are not interchangeable with the table's perspective `Pixel Camera`.

The existing diagnostic did **not** log culling masks, target texture names, hierarchy paths, or component lists. Therefore exact serialized masks, the physical camera hierarchy, and which GameObject hosts each compositor component remain unverified. Do not hard-code an inferred mask. Resolve the managed reference at runtime and log source path, parent path, mask (hex), target texture dimensions/format, clear flags, rendering path, and attached components once on attachment.

## Confirmed method behavior

### `PixelCamera` component

`Awake` stores its attached `Camera`. `LateUpdate` calculates render dimensions from `Screen.width`, `Screen.height`, and `ReferenceHeight`; writes the camera's `orthographicSize`; and sets a centered viewport rectangle. `OnRenderImage` obtains a temporary texture at that pixel resolution, sets point filtering, blits source to temporary and then temporary to destination, and releases it.

This code is desktop/pixel-resolution dependent. **Copy camera settings onto new GameObjects containing only Camera components** for the initial experiment. Instantiating the original GameObject can carry `PixelCamera`, light compositors, event scripts, and image effects that overwrite render targets or viewport settings. The component and the `ViewManager.pixelCamera` field share terminology but are not proof of the same role.

### Other rendering components

`LightCameraRenderer.OnPreRender` overwrites its attached camera's `targetTexture` with its private `_texture`. `OnRenderImage` then sets `targetTexture` to null, blits source to destination, and makes a second material blit to the screen. It cannot be copied blindly onto an eye camera expected to render to a fixed compositor texture.

`ClearCameraRenderTextures.OnDisable` can clear camera render targets. `CardRenderCamera.TryCreateCameraForLiveRender` and `StopLiveRenderCard` assign/clear helper-camera target textures. Those helpers must keep running; broadly disabling all desktop cameras will break card surfaces even when the headset scene camera itself renders correctly.

`CameraEffects.ShowLayer` and `HideLayer` mutate the culling mask of the Camera on the effects manager's GameObject. Eye masks copied only at startup will not track those changes if that camera is later included as an effects pass. Separate fog code also creates/manages cameras and render textures. Full effect composition remains a later audit.

### `ViewManager` writes and side effects

`SwitchToView` tweens the 3D hand position/rotation, tweens `cameraParent` **local** position/rotation, tweens `pixelCamera.fieldOfView`, invokes `ViewChanged(newView, oldView)`, updates control hints, sets `CurrentView`, and optionally locks its controller. This rules out replacing the whole method with an early return simply to stop camera movement.

`SetCameraWorldPosition` tweens `cameraParent` world position. `OffsetPosition` and `OffsetRotation` tween its local pose relative to the current view's authored pose. `OffsetFOV` writes `pixelCamera`. The tracking rig must not be a child that inherits these animated movements throughout gameplay.

## Recommended initial construction

1. Resolve `ViewManager.pixelCamera` by exact type/field and confirm it is active, perspective, and belongs to `Part1_Cabin`. If unavailable, leave desktop rendering intact and wait; do not fall back to auxiliary cameras.
2. At explicit entry/recenter, capture the **actual source camera's world position**, including any local offset beneath `cameraParent`. Use that pose as a stationary root reference. `cameraParent` is useful for identifying intended logical anchors, but its world pose alone can omit the child's transform and inherits authored camera pitch/roll.
3. Keep the stereo root outside the game's camera hierarchy. Use a comfortable yaw-only seating orientation; retain table-relative eye height and headset positional motion. Treat the source's pitch as a diagnostic/initial gaze reference, not an artificial head tilt that persists during tracking. Provide manual recentering. World scale remains an explicit calibration value.
4. Create bare left/right cameras. Copy source settings, then explicitly set the target textures, full viewport rect, enabled=false (manual render), perspective projection, no native Unity stereo targeting, and per-eye projection/view transforms from OpenVR. Do not keep re-copying source transforms/FOV after every view change.
5. Copy the source's actual culling mask and refresh it if the source changes. Keep helper rendering cameras alive. Leave existing desktop cameras active for the first test so cards and monitor input still work.
6. Render both eye textures after the game has completed its scene updates and helper camera work, then enqueue the native render-thread submission event in order. Ensure both frames use one coherent pose and that texture ownership outlives the queued submission. Scene unload, stop, or resize must drain/detach submission before releasing textures.
7. Treat missing UI/effects as a known experiment limitation. Do not claim playable VR based only on the visible table; dialogue, card inspection, pause, and map selection may depend on other layers/cameras.

## Validation before asking for another headset test

Build both x86 components; test native export contracts and managed matrix conversion without initializing a second game. Verify cleanup and missing-camera paths. At the next user-run test log the resolved source and eye texture metadata, backend init/compositor failures, and submission results. The actual user test must confirm distinct stereo views, coherent head tracking, stable table placement during view changes, recenter behavior, and no unintended movement when pausing or returning to Start.

No serialized scene parser was available in the bundled environment. A confined UnityPy package download attempt could not resolve the package host; no package was installed. Consequently this audit does not invent target textures or layer-mask values absent from the log. Runtime instrumentation is the remaining direct route to those facts.

## Initial managed renderer review

The initial `StereoRenderer.cs` / `StereoPose.cs` draft was reviewed read-only. The coordinate conversion implements `S*M*S` for an OpenVR right-handed pose, and the raw-frustum conversion places OpenVR's downward-positive bottom at Unity's negative-Y frustum boundary. The independent rig, bare cameras, per-eye projections, desktop preservation, and deferred texture release after native shutdown acknowledgement fit the experiment.

Before packaging, address these concrete points:

- Render in an end-of-frame coroutine after normal helper-camera rendering, rather than in `LateUpdate`. Unity's automatic camera rendering follows LateUpdate; the first draft can sample stale/uninitialized live card/map/emission textures in its eyes.
- Capture yaw-only anchor and recenter rotations. Capturing the entire authored source rotation and initial headset rotation imposes desktop camera pitch/roll on the player and rotates positional tracking axes. The inspected Board view uses approximately 79.9 degrees of authored pitch, making this particularly unsuitable as a physical seated frame.
- Refresh the source culling mask when rendering. A one-time `CopyFrom` misses script layer changes.
- Use the actual `ViewManager.pixelCamera` reference as the selection gate. A name-only fallback should not silently attach to an unverified camera hierarchy.
- Review the native shutdown code alongside the managed deferred cleanup: acknowledgement must follow all preceding submission events before managed textures are released. Library handles retained through process exit avoid dangling queued callback addresses. This review cannot establish that guarantee without the native implementation.
