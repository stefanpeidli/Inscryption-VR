# Runtime test 07: full battle confirmed; dialogue/UI follow-up installed

The user confirmed 0.3.2 substantially improved positioning and card visibility, with working rays for card use, movement/selection and ending the turn. An entire fight was playable; advancing dialogue still required mouse input, and desktop subtitles were missing in VR. This is user-confirmed battle evidence, not proof of complete Act 1 or Kaycee coverage.

The successful session logs were preserved in `artifacts/runtime-snapshots/20261009-052344-8867`. Before installing the follow-up, the exact working plugin/config/log were backed up to `artifacts/installed-backups/20261009-052550-4282`.

## Installed 0.3.3 changes

- Right trigger advances visible Input-mode dialogue on a fresh deliberate release. World interaction is relinquished during that context; a gesture cannot spill across dialogue and world selection.
- An independent world-space subtitle panel mirrors the game's text/font above the headset's forward center. It refreshes before both manual eye renders, including changed-text canvas rebuilds. Desktop dialogue UI is retained.
- A 10-degree downward viewing bias brings the tabletop higher into the field of view. F7 toggles it and recenters; `Rendering.TableTiltDegrees` can be set from 0 to 25. The eyes and controller ray share the rotated basis. Gameplay geometry and physics are unchanged.
- `UI.EnableVrSubtitles` controls the panel independently of dialogue input. A panel failure disables subtitles while leaving dialogue input available.

The native renderer remains the exact verified 0.2.0 binary. No compositor wait/submission, prediction, projection or native lifetime changes were made.

## Offline validation

Compilation passed. Trigger tests passed 25 assertions; context routing passed 25 assertions covering held gestures and preventing cross-context releases. OpenVR ABI checks passed 12 assertions; staged stick tests passed 23. Metadata checks verified 38 game members. The actual production cursor and dialogue binding constructors also executed successfully against installed assemblies. Independent source/IL review confirmed the dialogue coroutine/reset semantics and coherent tilted tracking mapping.

No headset test of 0.3.3 has occurred yet. Next check: read a Leshy dialogue in VR, press/release right trigger to advance consecutive messages, return to a card without an accidental click, and compare tilt on/off with F7. F8 recenters, F10 restores mouse control, F9 stops VR. Verify panel readability and timed automatic messages separately.
