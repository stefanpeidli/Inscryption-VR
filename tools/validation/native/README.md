# Native bridge smoke harness

Run `scripts/test-native.ps1` against the retained user-verified 0.2.0 native artifact. Its default fixture is `BridgeBaselineSmoke.cpp`, with 49 baseline assertions covering x86 exports/callbacks, pose and frustum arrays, main-thread system polling, render-thread compositor wait, immutable texture/pose submission, flipped bounds, reference ownership, queue overflow and deferred shutdown.

The harness uses `MockOpenVR.dll` built from the pinned Valve C header and mock IUnknown textures. It does not start SteamVR, access a headset or submit real GPU textures. It cannot establish live Unity synchronization or headset appearance.

`-WithdrawnTimingExperiment` selects `BridgeSmoke.cpp`, the historical fixture for the failed 0.2.1 timing change. Its 63 assertions do not apply to the baseline binary. That experiment passed mocks but regressed the view and crashed in the headset. Do not redeploy it based on mock results.

Rebuild both test binaries after changing `MockContract.h`, which defines their shared capture layout. For controller work, preserve the exact baseline native binary rather than rebuilding the renderer. The controller installer checks its SHA-256 before copying files.