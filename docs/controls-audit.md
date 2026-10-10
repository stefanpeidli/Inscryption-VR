# Offline controller audit and follow-up: 0.3.1

The 0.3.0 controller implementation was reviewed against the installed game's IL before another headset test. The native renderer remains the exact user-verified 0.2.0 binary. No renderer timing changes were made.

## Corrections

- Match the game's drag cancellation: after hover and main input processing, dispatch `CursorDragOff` once when the pressed target differs from hover, then clear the pressed field. The initial prototype retained it and could repeat cancellation callbacks.
- Match release routing: a deliberately armed trigger release invokes `CursorSelectEnd` on the hovered target, including after drag cancellation. A press held during activation or tracking recovery remains suppressed until released.
- Cancel ownership and press state explicitly on F8/F9 so Unity component update order cannot let a held press survive recentering or VR stop.
- Require a full analog trigger release on activation/recovery, including when first observed partially squeezed. Reject non-finite analog samples and preserve press/release hysteresis.
- Cancel the previous interaction when the right-hand tracked device index changes.
- Cache callback metadata and active manager references, avoiding repeated method lookup and scene searches during ordinary pointer updates. Destroyed or inactive manager references are resolved again.
- Log state changes, discovered device/axis, selection targets and full exceptions. No continuous pose or per-frame hover logging is enabled.

A second independent source review found no concrete new regression. One conservative difference remains: the adapter clears a cancelled press even if the game currently blocks the drag-off callback, whereas the original cursor can retain it until permitted.

Before installation, the renderer, deferred cleanup, native bridge and pose-helper managed IL were compared against installed 0.3.0 and matched exactly. The retained native DLL also matched its pinned SHA-256. Managed plugin 0.3.1 was then installed with hashes verified and the prior plugin/config/log backed up under `artifacts/installed-backups/20261009-035904-6758`. No live controller test was performed while the user was away.

## Validation and recovery

`scripts/test-controls.ps1` checks 25 trigger assertions, 12 Windows x86 OpenVR layout/constants, 23 staged stick gestures and 34 game member contracts. Native baseline validation covers 49 mock assertions. These checks do not validate live Unity callbacks, Quest mapping, pointer alignment, or headset comfort. Thumbstick view navigation is compiled as staged code but is not called by the installed pointer.

`scripts/collect-runtime.ps1` preserves local game/plugin logs, mod configuration, installed versions and hashes in `artifacts/runtime-snapshots`. It does not collect saves. Snapshots taken while the game is running may contain incomplete logs.

`scripts/restore-renderer-baseline.ps1 -VerifyOnly` verifies availability of the pinned rollback files without touching the game. Without that switch, it backs up the current installation and restores the exact 0.2.0 plugin, native renderer and configuration. The game must be closed for restoration.

See [the input roadmap](input-roadmap.md) for verified future alternate selection, view navigation and dialogue hooks. Research and offline tests do not establish complete gameplay coverage.
