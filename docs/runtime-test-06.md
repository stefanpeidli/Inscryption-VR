# Runtime test 06: controls did not initialize; card visibility poor

The user reported no controller response and barely visible hand cards with 0.3.1. The run was preserved in `artifacts/runtime-snapshots/20261009-044937-6583`.

## Confirmed input failure

The log shows `GameCursorAdapter` throwing `MissingMemberException` during plugin initialization, before OpenVR controller polling begins. `PauseMenu.instance` is **public static**, while the adapter requested only nonpublic static fields. The earlier metadata test established existence but did not execute those visibility-sensitive lookups.

Version 0.3.2 searches both public and nonpublic static fields. A new standalone harness executes the actual production adapter constructor against the installed assemblies, without creating Unity objects or initializing OpenVR. It reproduces the failure in installed 0.3.1 and passes with 0.3.2. Trigger, ABI, stick and game metadata checks also pass. This is a confirmed initialization fix; Quest controller mapping and actual clicks remain unverified.

## Seating correction

The initial rig captured the current desktop camera position, which can be a top-down board/map position. Game IL shows the hand is positioned independently of that camera. The board camera is `(0.475, 9.71, -1.36)`, while the default hand origin is `(1.15, 3.88, -5.43)`. Capturing that board camera can leave the hand behind and far below the headset seat.

The corrected anchor reconstructs the canonical Default table camera position from `ViewManager.GetViewInfo(Default)` in the live camera-parent coordinate system, preserving the source camera's child offset. HMD recenter remains yaw-only. Anchor, desktop view and available hand position/distance are logged on capture. This fixes the arbitrary-seat flaw; the failed run lacks enough geometry logs to prove it was the entire card-visibility problem. Blurriness, hand elevation and readability still require feedback.

The native renderer binary, compositor timing, eye projection and submission logic are unchanged. Independent review found the anchor position reconstruction correct for the observed cabin hierarchy.

Installed version: 0.3.2.0. Installation backup: `artifacts/installed-backups/20261009-045333-8560`. Next test: enter a battle, press F8, look down at the hand, and try the right trigger. F10 restores mouse control; F9 stops VR.
