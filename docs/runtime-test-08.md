# Runtime test 08: dialogue confirmed; map and event support

The user confirmed 0.3.3 subtitles are visible and right-trigger dialogue advance works. F7 was not tested. They clarified that rotational distortion makes the table and Leshy appear to tilt/bend, and reported map-scroll occlusion plus unavailable input in reward and sacrifice-altar events. Logs were preserved in `artifacts/runtime-snapshots/20261010-172849-4129`.

## Installed 0.3.4

- Automatic raised map overview uses the game's canonical MapDefault camera geometry, independent of desktop camera tweens. It persists throughout Map state and restores the seated anchor on leaving Map. T chooses a fuller top-down MapArial viewpoint, S chooses seated; manual choice persists until map/non-map context changes. `Rendering.AutoMapOverview = false` disables automatic switching.
- Camera/hand tracking uses the same anchor basis. Viewpoint recapture increments a seat revision; pointer ownership and held gestures cancel before using the new basis.
- Special-card-phase input is narrowly available for owned, live card reward targets/reroll, and altar slots, selected cards, deck-selector cards and confirmation stone. Each target still passes the game's collider/activity/global interaction gates. View locks are preserved; they do not mean clicks are forbidden. Other special events remain unimplemented.
- Low-frequency read-only SteamVR timing samples record current frame data and process-specific cumulative presentation/drop/reprojection counters. No runtime settings, compositor waits or native submissions were changed. `Diagnostics.LogRenderTiming = false` disables polling.

The rotation math audit found no verified basis, eye-transform or projection defect. Timing/reprojection and Steam Link remain candidate causes of visible bending; no correction is claimed. See `docs/rotation-warping.md` for counter interpretation and limitations. The native DLL remains the exact verified 0.2.0 binary.

## Verification

Compilation passed. Input checks passed 25 trigger, 25 exclusive-context and 12 map/viewpoint-policy assertions. Windows x86 ABI checks passed 16 assertions including compositor timing/statistics layout. Staged stick checks passed 23 assertions. Metadata checks verified 49 game members; the production cursor/dialogue/event binding constructors executed successfully against installed assemblies. These checks do not validate live event targeting, map comfort or head-rotation appearance.

Installed managed version: 0.3.4.0. Confirmed 0.3.3 plugin/config/log backup: `artifacts/installed-backups/20261010-173527-4114`. Native SHA-256: `EA4C2B919B19F668D0B8CAD0DB1E6220A0AE6D93357432B5307EC3171D91642E`.

Next headset check: select a map node from the raised overview, select a reward card, complete both altar card slots and confirmation, and rotate the head gently during a 30-second seated view. Report viewpoint comfort and any blocked targets. The agent reads timing logs directly afterward.
