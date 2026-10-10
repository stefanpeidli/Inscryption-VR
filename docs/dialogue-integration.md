# VR dialogue integration

`VrDialogue` mirrors the active cabin `TextDisplayer.textMesh` into its own world-space Unity UI canvas. It uses the game's localized, parsed message and font, with high-contrast white text on a dark panel. The panel follows the current headset pose 1.2 metres forward and 0.25 metres above the forward centre. Automatic and input-driven messages both appear. This copies the message, rather than the desktop letter animation. The desktop text remains untouched.

The subtitle objects use dedicated layer 30, with no colliders and `raycastTarget = false`. Stereo eye cameras must include that layer; the installed source camera's mask excludes it. `RefreshForRender()` runs after the current head pose is available and before the two manual eye renders, and forces the UI geometry to refresh. The canvas uses a separate UI material with depth testing set to Always, so cabin geometry cannot obscure its text. No OpenVR session or render bridge changes are involved.

## Input contract and safeguards

Inspected the installed Epic `Assembly-CSharp.dll` with Mono.Cecil. `TextDisplayer.ManagedUpdate()` writes private Boolean `continuePressed` when the ordinary Select button is released. `ShowUntilInput` sets `CurrentAdvanceMode` to Input (1), disables world interaction, shows its message, waits 0.2 seconds, then resets `continuePressed` before waiting for the player's input. `Clear()` hides the original text object. `Interrupt()` also sets `skipToEnd` and is deliberately unused.

`IsInputDialogue` is true whenever the active message is visible and uses Input mode, including its initial wait. Controller integration must treat this as an exclusive context: cancel card/world selection, obtain a fresh neutral trigger state, and route a newly armed release to `TryAdvance(true)`. The adapter never polls a controller itself. It sets only `continuePressed`, and only after both scaled and real elapsed time reach 0.25 seconds from observing the message. One pulse is allowed until the message hides or changes. A rejected early release is consumed by the dialogue context; it must never fall through to a card action.

Pause, unpausing, an active pause cursor, unsupported scenes, missing headset tracking, and disabled/missing source text all suppress the adapter. Losing those conditions resets readiness. Auto-mode messages retain the game's original timing. The code never changes `skipToEnd`, cursor interaction flags, message content, or coroutine state.

If two identical messages appear with no observed hidden frame between them, the next message coroutine clearing `continuePressed` acknowledges the old pulse and rearms readiness. The old waiting loop itself never clears that field. This avoids permanently blocking a repeated line without repeatedly submitting a pulse to the same waiting message.

## Integration API

- `Initialize(ManualLogSource logger, StereoRenderer renderer, bool showSubtitles)` resolves the narrow reflection contract and optionally creates the independent canvas. Disabling subtitle rendering preserves dialogue input.
- `IsInputDialogue` identifies the exclusive controller context.
- `TryAdvance(bool deliberateRelease)` returns true only when it submitted a pulse.
- `RefreshForRender()` updates visibility, text and placement for both stereo eyes using `StereoRenderer.TryGetHeadPose`.
- `SubtitleLayer` is 30 and must be included in each eye camera mask.

Compilation and metadata checks cannot prove headset readability or actual dialogue progression. In-headset checks should cover ordinary Leshy lines, a timed automatic message, consecutive messages, pause/resume, controller disconnect while held, and returning from dialogue to card selection without an accidental click. Long localized text may shrink to fit; readability and panel placement remain user-test items.
