# Controller input roadmap

Verified by reading metadata and IL in the installed Epic `Assembly-CSharp.dll` on 2026-10-08. This is design research, not headset validation. Scope is Act 1 and Kaycee's Mod in `Part1_Cabin`; other acts and desktop menus retain desktop input. Do not change the working native renderer to add these features.

## Suggested mapping and order

| Priority | Controller action | Intended behavior | Status |
| --- | --- | --- | --- |
| 1 | Right trigger, release after press | Main ray selection | Battle pointing confirmed in 0.3.2; context routing extended in 0.3.3 |
| 2 | Right A, press and release | Alternate ray interaction, including ability/stat explanations | Hooks verified; not implemented |
| 3 | Right stick up/down, neutral before another step | Move through the game's current allowed views | Managed helper staged; not wired or enabled |
| 4 | Right stick left/right | Use current alternate view transition entries | Managed helper staged; not wired or enabled |
| 5 | Right trigger release while an input dialogue is visible | Advance one dialogue message, without selecting a world object | Implemented with headset panel in 0.3.3; needs headset validation |
| Later | Right B | Contextual back / close rulebook | Further context audit required |

Use separate edge states for each button, with release-to-arm on reconnect, phase entry, and toggle. Stick gestures should use a dead zone and neutral rearming; game keyboard repeat behavior need not become uncontrolled VR repeat. Preserve F10 mouse fallback, F8 recenter, and F9 VR stop.

## Alternate selection and card inspection

`DiskCardGame.InteractionCursor.UpdateAltInput()` is separate from `UpdateMainInput()`. It stores `currentAltInteractable` of type `AlternateInputInteractable`. Its ray update excludes the `NonInteractable` layer, whereas the main path uses its own exclusions. A future adapter must mirror each mask separately, not cast once and assume both component types are equivalent.

The alternate path runs `InteractionDisabledWith(target)` before `CursorStay()` and before input dispatch. On `InputButtons.GetButtonDown(Button.AltSelect)` it invokes `AlternateSelectStart()`. On button up it invokes `AlternateSelectEnd()`. Both paths call `ProgressionData.SetMechanicLearned(MechanicsConcept)` with enum value 11. `AltSelect` is Button value 7.

`AlternateSelectStart()` and `AlternateSelectEnd()` invoke their delegate property, then the corresponding overridable `OnAlternateSelectStarted/Ended()` method. Use those public wrappers, preserving target Enabled/activity checks and cursor-owned fields; calling only the override omits registered gameplay callbacks.

Verified consumers:

- `AbilityIconInteractable.OnAlternateSelectStarted()` calls `RuleBookController.OpenToAbilityPage(Ability.ToString(), card, false)`.
- `StatIconInteractable.OnAlternateSelectStarted()` calls `OpenToStatIconPage(statIcon.ToString(), card)`.
- `BoonIconInteractable.OnAlternateSelectStarted()` calls `OpenToBoonPage(Boon.ToString())`.
- Consumable item slots register alternate-selection callbacks when an item is created. Their full callback behavior still needs an audit before broad support.

Card inspection is not universally an alternate click: `SelectableCard.OnCursorEnter()` invokes its registered `cardInspected` callback. The existing main ray's hover path can already trigger that behavior for reachable, permitted cards. Do not add a second direct inspection call on hover, which would duplicate it.

Releasing an alternate button after losing focus or eligibility must cancel the controller gesture without dispatching to a newly hovered object. Rulebook interaction needs its own coverage: the current narrow battle-view pointer is not proof that every rulebook control is reachable.

## View navigation

`ViewController.ManagedLateUpdate()` returns when `LockState == ViewLockState.Locked` (value 1). Otherwise it reads the private `allowedViews` list and current view. If the current view is in that list:

- `LookDown` (Button 4) decrements the index, clamped at zero.
- `LookUp` (Button 3) increments the index, clamped at count minus one.
- If the rulebook is shown, LookUp closes it using `RuleBookController.SetShown(false, true)` and returns instead of changing view.

If the rulebook exists and is not shown, it also processes private `altTransitionInputs`. Each entry contains `from`, `to`, and `input`; matching current view and input can override the target. Finally a changed target is passed to `ViewManager.SwitchToView(target, false, false)`.

`CurrentControlModeAllowsView(View)` only checks membership in `allowedViews`. It does not check locks or alternate transitions. Calling that method alone before a fixed view change would therefore be incomplete. `SwitchToControlMode()` is a gameplay configuration operation that changes the allowed view lists; the VR adapter must not call it to gain access to views.

Normal CardBattle control mode configures `[Hand(0), Default(1), Board(4), OpponentQueue(6)]` and copies the battle alternate transition list. Verified battle alternate transitions:

| From | Button | To |
| --- | --- | --- |
| Default, Board, OpponentQueue | LookRight (6) | Consumables (32) |
| Consumables | LookLeft (5), LookDown (4) | Default |
| Consumables | LookUp (3) | OpponentQueue |
| Default, Board, OpponentQueue | LookLeft (5) | Scales (7) |
| Scales | LookRight (6), LookDown (4) | Default |
| Scales | LookUp (3) | OpponentQueue |

Read the live lists rather than hardcoding this table: draw, sacrifice, map, and scripted control modes use different lists. Suspend pointer presses before a view change, wait for the new view, then rearm. Keep existing pause, scene, tracking, and playable-phase gates; do not override LockState.

### Staged navigation API

`ControllerViewNavigation` is a plain managed helper, not a MonoBehaviour. `Bind(ViewManager, RuleBookController)` supplies scene references and resets stick state; there are no game-object searches or automatic input subscriptions. `TryNavigate(Vector2 axis, bool callerReady)` processes one neutral-armed gesture and returns true only if it closes the rulebook or changes view. `Cancel()` requires a neutral sample before another gesture. The caller must pass false on tracking loss, pointer toggle, pause, unsupported phase, dialogue, transition, or pending pointer press; it must bind new live objects after scene changes and protect invocation with the existing managed input error fallback. These gates must be reevaluated every sample, not remembered as a one-time authorization. The helper additionally rejects inactive manager/controller, locked controller, non-cabin scenes, and zero timescale.

The pure `StickGestureState` uses maximum component magnitude with a 0.25 neutral threshold and 0.65 actuation threshold. The dominant axis determines direction, with vertical winning an exact tie. Holding or rolling the stick cannot repeat a step until neutral. Invalid floating-point values cancel arming. `StickGestureSmoke` validates 23 assertions covering boundaries, all directions, held activation, cancel/recovery, diagonal dominance, and invalid samples. Runtime object behavior and visible camera transitions remain untested.

Compilation uses C# 5 and the installed game's `mscorlib.dll`, `System.dll`, `System.Core.dll`, Unity assemblies, `Assembly-CSharp.dll`, and **`netstandard.dll`** facade. The existing build reference list must include the facade before this typed game adapter is integrated. Do not ship any of these game assemblies.

Additional exact metadata contracts for integration checks:

| Type | Member | Required shape |
| --- | --- | --- |
| `DiskCardGame.ViewManager` | `Controller` | Readable property returning `ViewController` |
| `DiskCardGame.ViewManager` | `CurrentView` | Readable property returning `View` |
| `DiskCardGame.ViewManager` | `SwitchToView` | `(View, Boolean, Boolean)` instance method |
| `DiskCardGame.ViewController` | `LockState` | Readable property returning `ViewLockState` |
| `DiskCardGame.ViewController` | `allowedViews` | Instance field, `List<View>` |
| `DiskCardGame.ViewController` | `altTransitionInputs` | Instance field, `List<ViewTransitionInput>` |
| `DiskCardGame.ViewController/ViewTransitionInput` | `from`, `to`, `input` | Instance fields returning `View`, `View`, and global `Button` |
| `DiskCardGame.RuleBookController` | `Shown` | Readable Boolean property |
| `DiskCardGame.RuleBookController` | `SetShown` | `(Boolean, Boolean)` instance method |
| `DiskCardGame.ViewLockState` | `Locked` | Enum constant 1 |
| global `Button` | `LookUp`, `LookDown`, `LookLeft`, `LookRight` | Enum constants 3, 4, 5, 6 |

The standalone helper compiles against these local contracts, but the integration should retain metadata checks so future game updates fail explicitly. Exceptions must disable only the optional feature and release controller ownership, never alter native renderer state.

## Dialogue advance

`TextDisplayer.ManagedUpdate()` sets private `continuePressed = true` when `InputButtons.GetButtonUp(Button.Select)` fires; Select is enum value 2. There is no ordinary public single-message advance method. `Interrupt()` is unsuitable: it sets both `continuePressed` and `skipToEnd`, so it also changes dialogue-event behavior.

The `ShowUntilInput` coroutine:

1. Sets `CurrentAdvanceMode = MessageAdvanceMode.Input` (value 1; Auto is 0).
2. Sets the interaction cursor's `InteractionDisabled = true`.
3. Prepares effects and text, waits 0.2 seconds after showing the message, then resets `continuePressed = false`.
4. Waits for `continuePressed`, clears the text, and eventually restores interaction.

`Displaying` reports the text object's `activeSelf`; it is not a dedicated readiness flag. A premature pulse may be discarded by the coroutine's reset, exactly as a desktop click can be. This reset must not be removed or bypassed.

A narrow future adapter can set `continuePressed` once on a newly armed release when the active TextDisplayer reports Displaying and CurrentAdvanceMode Input. It must consume that gesture exclusively, prevent main/alternate selection on the same frame, and remain disabled on pause or tracking loss. Whether a visible prompt is genuinely ready requires runtime validation. Avoid globally patching `InputButtons.GetButtonUp(Select)`: the same button is used across unrelated gameplay systems, so global injection can activate them together.

Text currently renders through `UnityEngine.UI.Text`. Having a correct input hook does not establish that the dialogue canvas is readable in both VR eyes; UI placement is a separate requirement before claiming complete dialogue support.

## Validation before enabling more coverage

Keep source changes managed-only and the native DLL identical to the known working baseline. Add meaningful checks for release-to-arm, exclusive dialogue/world dispatch, alternate target cancellation, stick neutral rearm, locked views, and rulebook-close behavior. Metadata checks can establish member compatibility but cannot prove Unity object lifetime, ray collider placement, rendering visibility, or gameplay sequence correctness. Extend the headset checklist for ability explanations, rulebook exit, map branches, card choices, transitions, and controller disconnect; retain desktop fallback for unvalidated phases.
