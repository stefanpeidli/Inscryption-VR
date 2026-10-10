# Runtime test 05: controller prototype awaiting headset test

Installed managed plugin: **0.3.1.0**. Controller pointer and stereo rendering are enabled; legacy XR and background probes are disabled. The native renderer remains the exact working 0.2.0 binary with SHA-256 `EA4C2B919B19F668D0B8CAD0DB1E6220A0AE6D93357432B5307EC3171D91642E`.

The user was away during development, so no live controller or new renderer test has occurred. Offline compilation and input checks passed. The 49 native baseline mock checks passed earlier; the unchanged native binary was not rebuilt for controls.

Installed controls:

- Right-controller ray and trigger through game pointer callbacks in supported battle/map phases.
- F10 restores mouse control; F8 recenters and cancels a press; F9 stops VR and cancels a press.
- Startup/recovery requires trigger release. Hover departure cancels drag once. Tracking loss, right-device replacement, pause and unsupported phases relinquish input.
- State-change and selection diagnostics are logged without per-frame hover logging.

Thumbstick view navigation and 23 gesture assertions are staged in source but are **not wired into gameplay**. Alternate interaction, dialogue advance, cabin traversal and complete UI coverage remain unfinished. Their known hooks are recorded in `docs/input-roadmap.md`.

Next live test: enter a player-turn battle, observe ray alignment and card highlighting, then test trigger selection and F10 mouse fallback. Follow `docs/controller-test.md`. Read logs directly or run `scripts/collect-runtime.ps1`; no user file extraction is needed. If necessary, the pinned 0.2.0 baseline can be restored with `scripts/restore-renderer-baseline.ps1` after closing the game.

Installation backup: `artifacts/installed-backups/20261009-035904-6758`. Post-install snapshot: `artifacts/runtime-snapshots/20261009-035932-4838`.
