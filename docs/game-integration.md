# Inscryption integration: inspected Epic build

This document records read-only inspection of `C:\Program Files\Epic Games\InscryptionWkwPQ`. No game files, saves, or launcher settings were changed. Findings describe this installed build and must be rechecked after game updates.

## Verified runtime facts

| Property | Evidence | Result |
| --- | --- | --- |
| Unity version | Serialized header of `Inscryption_Data/globalgamemanagers`; executable version | `2019.4.24f1`; file version `2019.4.24.6137584` |
| CPU architecture | `Inscryption.exe` PE COFF machine field | `0x014c` / x86, **32-bit** |
| Scripting runtime | `MonoBleedingEdge` directory and `Inscryption_Data/Managed/Assembly-CSharp.dll` | Mono managed C# build |
| Native plugin folder | `Inscryption_Data/Plugins/x86` | Galaxy, Rewired DirectInput, Steam API DLLs; no OpenVR native plugin observed |
| Initial VR configuration | `Inscryption_Data/boot.config` | `vr-enabled=0` |
| Managed XR APIs | Managed directory | `UnityEngine.VRModule.dll`, `UnityEngine.XRModule.dll`, `UnityEngine.SubsystemsModule.dll` present |
| Assembly fingerprint | SHA-256 of `Assembly-CSharp.dll` | `7A3883350821B3D91C186B903799EFDCE1C6D0830A2B8ADD40D7D60E745FEB72` |

The XR modules establish API availability only. They do not establish an installed provider, successful headset initialization, compositor submission, or usable stereo rendering. A 64-bit-only native dependency cannot load in this process. The backend proof must use a matching 32-bit native component and demonstrate actual runtime availability before changing gameplay cameras.

The Epic launcher manifest reports `AppVersionString` as `1.10WIN`. This is launcher metadata; prefer the assembly fingerprint and runtime diagnostics for compatibility checks.

## Verified managed hooks

Signatures below come from reflection metadata in the installed `Assembly-CSharp.dll`, not guesses from community examples. Except where noted, types are in `DiskCardGame`.

| Area | Types and members | Proposed use |
| --- | --- | --- |
| View changes | `ViewManager.SwitchToView(View, bool, bool)`, `GetViewInfo(View)`, `CurrentView`, `CameraParent`; fields `cameraParent`, `pixelCamera`, `controller` | Observe intended views and map them to comfortable rig anchors while preserving logical view state and events. |
| Scripted camera offsets | `ViewManager.SetCameraWorldPosition(Vector3, float)`, `OffsetPosition(Vector3, float)`, `OffsetRotation(Vector3, float)`, `OffsetFOV(float, float, bool)` | Audit camera writers before headset control. Suppress or translate visual offsets only while VR is active. |
| View control locks | `ViewController.ManagedLateUpdate()`, `CurrentControlModeAllowsView(View)`, `SwitchToControlMode(ControlMode, bool)`, `LockState`; `ViewLockState.Unlocked` / `Locked` | Preserve view permissions and script locks instead of globally unlocking the table. |
| Pointer routing | `InteractionCursor.UpdatePosition()`, `UpdateMainInput()`, `UpdateAltInput()`, `UpdateDragInput()`, `RaycastForInteractable<T>(int, Vector3)`, `CastForInteractableAtWorldPosition(Vector3)`, `MatchWorldPosition(Vector3)`; fields `rayCamera`, `currentMainInputInteractable`, `cursorDownInteractable` | Use controller ray/trigger through the same event lifecycle. Audit 2D and 3D collider handling and press/release state before replacing cursor updates. |
| Interaction gates | `InteractionCursor.InteractionDisabled`, `DisabledInteractionExceptions`, `InteractionDisabledWith(InteractableBase)`; `InteractableBase.Enabled`, `CollisionIs2D`, `SetEnabled(bool)` | Respect disabled interactions, exceptions, and interactable enable state. |
| Pointer events | `MainInputInteractable.CursorEnter()`, `CursorStay()`, `CursorExit()`, `CursorSelectStart()`, `CursorSelectEnd()`, `CursorDragOff()`; alternate-input sibling exists | Invoke public lifecycle methods so delegates, parent routing, and game state remain involved. Calling only a subclass `OnCursor...` callback can skip wrapper behavior. |
| Abstract input | **Global namespace** `InputButtons.GetButton(Button)`, `GetButtonDown(Button)`, `GetButtonUp(Button)`, `GetButtonRepeating(Button)`, `GetAxis(Button)`; field `rewiredPlayer` | Candidate for VR button overlays after mapping the nested button enum. Preserve keyboard/gamepad fallback. |
| Cards | `PlayerHand.OnCardSelected(PlayableCard)`, `AllowCardInHandSelection(PlayableCard)`, `PlayingLocked`, `InspectingLocked`, `SelectSlotForCard(PlayableCard)`; `CardSlot.OnCursorSelectStart()`, `Chooseable` | Keep original selection and sacrifice coroutines. Controller selection should act like pointer input rather than directly relocating cards. |
| Battle logic | `BoardManager.ChooseSlot(List<CardSlot>, bool)`, `ChooseSacrificesForCard(List<CardSlot>, PlayableCard)`, `TurnManager.PlayerCanInitiateCombat`, `OnCombatBellRang()` | Observe state for UX; avoid rewriting rules for the first prototype. |
| Bell | `CombatBell.PressingAllowed()`, `Pressable`, `OnCursorSelectStart()`, `OnBellPressed()` | Trigger via input lifecycle and existing permission checks; do not call turn completion unconditionally. |
| Map | `MapNode.OnCursorSelectEnd()`, `OnCursorEnter()`, `OnCursorExit()`, `SetActive(bool)` | Controller release must reach existing map selection behavior. |
| Cabin navigation | `FirstPersonController.SetZone(NavigationZone3D, bool)`, `LookAtDirection(LookDirection, bool)`, `UpdateRotation()`, `UpdateNavigationEvents()`, `MoveLocked`, `LookLocked`, `ReturnToTableView()` | Later use fade/teleport around zone changes; preserve puzzle locks and zone events. |
| Act 1 flow | `Part1GameFlowManager.SceneSpecificTransitionTo(GameState, bool)`, `CanTransitionToFirstPerson()`, `SceneSpecificInitialization()` | Anchor lifecycle and phase gating; avoid applying table rig during other acts. |
| Pixel rendering | **Global namespace** `PixelCamera.LateUpdate()`, `OnRenderImage(RenderTexture, RenderTexture)`; fields `cam`, `renderWidth`, `renderHeight`, `actualWidth`, `actualHeight` | Investigate rendering path before using native stereo; pixel resolution logic can conflict with per-eye textures and readable cards. |
| Camera effects | `CameraEffects.Shake(float, float)`, `StopShake()`, fisheye, tilt shift, chromatic aberration, blur, oil painting, fog setters | Audit stereo compatibility. Disable artificial camera shake and optical distortion by VR configuration while preserving story logic. |
| Pause | **Global namespace** `PauseMenu.ActivePauseCursor`, `SetGamePaused(bool)`, `LateUpdate()` | Pause owns a separate cursor and needs its own controller route. |

## Scope detection

`SaveManager` and `SaveFile` are global namespace types, not `DiskCardGame.SaveManager` / `DiskCardGame.SaveFile`. Verified members are `SaveManager.SaveFile`, `SaveFile.IsPart1`, `SaveFile.IsAscension`, `SaveFile.Part1NotLeshyFinale`, and `SaveFile.currentScene`. `SaveFile` also holds `ascensionActive` and `ascensionData`; `DiskCardGame.AscensionSaveData.Data` exists.

Use explicit configuration plus runtime phase checks and the presence of the expected view hierarchy. Do not infer active table gameplay from an ascension save flag alone: menus and transitions can have that flag too. Do not change saves or unlock Kaycee's Mod automatically.

## First integration experiment

1. Load a minimal 32-bit Mono plugin; log Unity/process architecture, scene transitions, provider availability, and the discovered camera hierarchy.
2. In a repeatable Kaycee battle, capture cameras' target textures, culling masks, depth, projection, and image-effect components. Establish which camera renders the table and which renders overlays.
3. Initialize the selected VR backend and prove independent eye images, headset pose, and compositor output before taking control of `ViewManager.CameraParent`.
4. Keep logical `SwitchToView` behavior running. Drive the headset camera from a separate tracking child/root arrangement; audit tweens and offsets so scripts cannot animate the HMD pose.
5. Add one controller ray with hover, press, release, and drag cancellation. Validate card selection, sacrifice, bell, map, and pause against the original permission gates.
6. Handle view changes with stable table placement and explicit focus behavior. Add cabin zone teleports and story effects only after the entire Kaycee loop is usable.

Do not call this a working VR mod until an actual Quest/Steam Link test proves stereo, tracking, recentering, and input. Compilation and managed XR availability are insufficient.

## Inspection limitations and reproduction

Run the included `tools/inspection/inspect-game.ps1` with **Windows PowerShell 5.1** (`powershell.exe`), not PowerShell 7: reflection-only loading is unavailable in .NET Core. It reads assembly metadata and resolves referenced managed DLLs from the game directory. The script reports available types even when unrelated Unity-era types fail reflection loading on the host CLR. Current observed failures include a legacy `System.Core` version mismatch and an explicit-layout `OptionValue` type; the listed hooks were successfully inspected.

This establishes member existence and signatures, not implementation semantics. Method bodies, serialized scene references, event ordering, coroutine side effects, and stereo shaders still need targeted inspection and runtime instrumentation. No headset test was performed.
