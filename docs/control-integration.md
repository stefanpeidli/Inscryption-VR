# Controller integration against the installed game

## Recommended first implementation

Use the existing OpenVR Scene session to acquire a right-hand pose and trigger, transform the hand through the same stationary anchor/recenter/world-scale calculation as the eyes, then dispatch the game's **pointer lifecycle wrappers** to the first 3D hit. Do not reimplement card play, sacrifice, bell permissions, map progression, or inventory rules.

The local BepInEx logs repeatedly report `Supports SRE: False`. That flag does not by itself prove every Harmony approach will fail, but it makes dynamic patch generation an unverified dependency. **The first controller prototype can avoid Harmony entirely**: `InteractionCursor.SetEnabled(bool)` only writes `Behaviour.enabled`, and the inspected `FrameLoopManager.ExecuteIteration` checks both that flag and `gameObject.activeInHierarchy` before invoking `ManagedUpdate`. Temporarily disable the ordinary cursor component, keep its GameObject/singleton alive, and implement the pointer lifecycle through reflection/public wrappers. Restore desktop control on takeover release.

This is a replacement of pointer dispatch, not game rules. The first deliverable should cover table cards, sacrifices, the bell, and map nodes; dialogue and flat pause menus remain separate integration work. Keep the proven renderer timing path unchanged while testing controls.

## Evidence

Read-only Mono.Cecil inspection used the installed `Inscryption_Data/Managed/Assembly-CSharp.dll`, fingerprint recorded in `docs/game-integration.md`. The full temporary IL dump is `artifacts/control-il.txt`. No game assembly or native/source file was edited for this audit.

| Contract | Verified behavior |
| --- | --- |
| `DiskCardGame.InteractionCursor.ManagedUpdate()` | Position update in mouse mode, cursor visuals, main update, alternate update, drag update in that order. |
| `UpdatePosition()` | Converts `Input.mousePosition` through `rayCamera.ScreenToWorldPoint`, retains XY and uses `rayCamera.nearClipPlane` as world Z. It is a screen cursor transform, not a controller-world hit point. |
| `RaycastForInteractable<T>(int, Vector3)` | Converts cursor world position back through `rayCamera.WorldToScreenPoint`, then uses **`Camera.main.ScreenPointToRay`**. Calls 3D `Physics.Raycast(ray, out hit, 1000, mask)`, then `hit.transform.GetComponent<T>()`. It does not search ancestors, penetrate a blocker, or call Physics2D. |
| Main exclusion mask | `~LayerMask.GetMask(cursor.excludedLayers.ToArray())`; constructor defaults exclude `AlternateInteractable` and `NonInteractable`. Read the live list rather than assuming defaults remain unchanged. |
| Alternate exclusion mask | `~LayerMask.GetMask("NonInteractable")`; distinct from the main mask. |
| `InteractionDisabledWith(InteractableBase)` | Private instance method. If `InteractionDisabled` is true, a target is permitted only if in `DisabledInteractionExceptions`. It does not itself check the target collider's Enabled state. |
| `InteractableBase.Enabled` | Reads the attached Collider or Collider2D enabled flag according to `CollisionIs2D`. It does not mean MonoBehaviour.enabled. |
| `InteractionDisabled` setter | On setting true, exits and clears both current hover fields, restores default cursor type, then records the flag. A controller adapter must reconcile its hover state if the game clears those fields. |
| Main wrapper methods | `CursorEnter()`, `CursorStay()`, `CursorExit()`, `CursorSelectStart()`, `CursorSelectEnd()`, `CursorDragOff()`, all zero parameters. Main wrappers invoke callback delegates and virtual OnCursor hooks, with enabled parent propagation where applicable. |
| Alternate wrapper methods | `AlternateSelectStart()` and `AlternateSelectEnd()`, zero parameters. They invoke delegate plus virtual alternate callback. |

Calling subclass `OnCursorSelectStart` directly skips wrapper delegates/parent propagation. Calling wrappers without collider, cursor, scene, and phase checks can bypass dispatcher restrictions because the wrappers do not enforce all those gates themselves.

## Concrete lifecycle

1. Only acquire pointer ownership when the supported scene/phase is active, the renderer anchor is valid, the selected controller is connected/tracked, its trigger has first been observed released, and the original cursor component is enabled. Cache original state; disable only that component. Do not deactivate its GameObject or set `InteractionDisabled` to obtain ownership.
2. Raycast with the live main exclusion mask and a bounded practical controller ray distance. Use the **first** hit's exact Transform component, not `GetComponentInParent`, so scenery remains an occluder and collider/target semantics match the game. Require target GameObject active, `Enabled == true`, and `InteractionDisabledWith(target) == false`.
3. Compare new hover with `currentMainInputInteractable`. If changed, exit the former still-enabled target, enter the permitted new target, and set the cursor field to the new target. Call `CursorStay()` once per pointer update while permitted. Cache reflection metadata once, not on every frame.
4. On trigger-down, call `CursorSelectStart()` on the current permitted target and store it in `cursorDownInteractable`. Do not continuously repeat select while held.
5. On trigger-up, the inspected game dispatches `CursorSelectEnd()` to the **current hovered target**, then clears `cursorDownInteractable`. It does not universally release to the original pressed object. This matters because map nodes select on release.
6. In the drag step, if the permitted pressed target differs from current hover, call its `CursorDragOff()` and clear the down field. This wrapper maps to `OnCursorDrag`, not an unrestricted physical card drag. Preserve the game's ordering: hover/main select, alternate select, drag-off.
7. On tracking loss, ownership release, recenter, pause, scene exit, or destruction, clear controller pressed state and hover/down fields without synthesizing an unintended successful selection. Exit/cancel only still-valid permitted objects; restore desktop cursor state. Require a neutral trigger before resuming to prevent a held trigger creating an immediate click.

Synchronizing the actual current-hover field is required: verified readers include `CombatBell.set_Pressable`, `CardSlot.set_Chooseable`, `PlayerHand3D.SetCardPositions`, `CardSingleChoicesSequencer.OnCardFlipped`, `RulePaintingManager`, and talking-card logic. A private controller-only hover variable would leave these game systems unaware of the selected object.

## Ownership limits and scope

The game also calls `InteractionCursor.SetEnabled` during campfire/stat-boost sequences, victory-feast video, `VideoCameraRig`, and feedback/reporting UI. A lease that itself sets `enabled=false` cannot distinguish a later game request to disable the already-false component. Do not force it disabled every frame or declare that component state sufficient for all scripted locks. Restrict the first adapter to verified battle/map views, respect target and global interaction gates, relinquish on external re-enable, and audit special-node sequences before expanding support. This is a material limitation of a patch-free prototype.

An initial whitelist can use `Hand`, `Default`, `Board`, `CardPiles`, `OpponentQueue`, `Scales`, `MapCloseIn`, `MapDefault`, `MapArial`, `Consumables`, `BoardCentered`, and `ConsumablesOnly`. The enum spells `MapArial` exactly that way. `Choices` / `ChoicesCloser` require reward-node validation. Exclude `FirstPerson`, `StatBoostSlot`, cabin/story/video views, and unsupported acts. View names alone are insufficient because this enum also serves Act 3; require `Part1_Cabin` and the actual table/cursor hierarchy.

### Deterministic battle/map gating

Further IL inspection confirms **`DiskCardGame.GameFlowManager.CurrentGameState`** is a public instance property returning `DiskCardGame.GameState`: `CardBattle = 0`, `Map = 1`, `FirstPerson3D = 2`, `SpecialCardSequence = 3`. `GameFlowManager.Transitioning` is a public instance bool. Use the active `Part1GameFlowManager` subtype and reject missing managers, transitions, and any state other than CardBattle/Map before taking pointer ownership. This excludes campfire and special-node scripts even when they reuse a table camera/view.

In CardBattle, require a live `TurnManager`, `IsMainOrDrawPhaseNotUpkeep`, and false `IsSetupPhase`, `GameEnding`, and `GameEnded`. The inspected phase getter requires IsPlayerTurn, excludes upkeep, and allows Draw or Main. `TurnManager.PlayerTurnPhase` values are Draw 0, Main 1, Combat 2, End 3. Card sacrifice/slot-choice stays within the main phase and continues to use target/global interaction gates.

In Map, require a live `GameMap` with `FullyUnrolled == true` and a live `MapNodeManager` with `MovingNodes == false`. Map views can include Default, MapDefault, MapCloseIn, MapArial, and **MapDeckReview = 23** when deck-review pointer targets have been validated. Require valid map phase independently of a familiar view name.

`ViewManager.CurrentView` is the exact view property. `ViewManager.Controller` returns `ViewController`; `CurrentControlModeAllowsView(View)` simply tests its `allowedViews` list. `ViewController.LockState` controls view navigation, not all pointer interaction. Do **not** reject every Locked state: card-placement and sacrifice flows can lock the view while deliberately permitting target selections. The private `controlMode` enum has CardGameDefault 0, CardGameChoosingSlot 1, CardGameChooseDraw 2, Map 3, FirstPerson3D 4, and MapNoDeckReview 16, among other special-node modes. Phase gating plus target permission checks is preferable to guessing whether a locked camera permits a click.

## Buttons and pause

`InputButtons` and the **`Button` enum are global namespace types**. The enum is not nested in InputButtons. Methods `GetButton`, `GetButtonDown`, `GetButtonUp`, `GetButtonRepeating`, and `GetAxis` resolve player 0 then call Rewired by the enum's name string.

| Button | Value | Initial controller use |
| --- | --- | --- |
| `Select` | 2 | Right trigger, pointer dispatch only initially. |
| `AltSelect` | 7 | Later card inspection through alternate wrappers and its separate mask. |
| `Cancel` | 8 | Later explicit cancellation; do not map to arbitrary card removal. |
| `EndTurn` | 9 | Existing CombatBell also reads this action; pointing at the actual bell is sufficient for first prototype. |
| `Menu` | 14 | Pause toggle requires original pause gates, not only calling SetGamePaused. |
| `AltMenu` | 16 | Additional pause-menu dismiss action. |

Other values are None 0, ScrollWheel 1, LookUp/Down/Left/Right 3–6, DirUp/Down/Left/Right 10–13, Scroll 15. The trigger adapter does **not** populate Rewired's Select action, so `TextDisplayer.ManagedUpdate()`—which separately reads `InputButtons.GetButtonUp(Select)`—will not automatically advance dialogue. A future validated input overlay/custom-controller integration should supply held/down/up consistently and preserve desktop fallback; adding pointer wrappers alone cannot claim complete input support.

`PauseMenu` is global, with private static `instance` set in Awake; public instance properties `Paused`, `Unpausing`, and virtual `ActivePauseCursor`. The latter returns its separate `pauseCursor`. `PauseMenu3D.OnPausedChange` updates both main/pause cursor positions and orthographic UI visibility. `PauseMenu.SetGamePaused` disables frame-loop iteration and sets timeScale to zero, while PauseMenu itself continues via ordinary LateUpdate. Its pause/unpause checks include `pausingDisabled`, active main-cursor GameObject, `Unpausing`, menu scene transitions, and card transitions. Retain desktop pause UI for the first controller test rather than raycasting through those checks.

## Validation target

Compile with no Harmony requirement. Verify no duplicate pointer dispatch; single click/release; hover appearance and game-field synchronization; card selection, sacrifice cancellation and confirmation, bell phase permissions, release-selected map nodes, tracking-loss cancellation, recenter while held, script InteractionDisabled exceptions, and clean restoration of desktop control. A controller test must report the unsupported dialogue, pause, cabin and special-node scope explicitly. Avoid modifying saves or game rules to make a test appear successful.
