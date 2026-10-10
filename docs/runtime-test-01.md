# Runtime test 01: diagnostic plugin loaded

Read the installed Epic game's `BepInEx/LogOutput.log` after the user completed the first test on 2026-10-08. Log timestamps are host-generated and are not used as the test date.

- BepInEx 5.4.23.2 loaded Inscryption VR Prototype 0.1.0.
- Runtime: Unity 2019.4.24f1, 32-bit process.
- Graphics: Direct3D11, NVIDIA GeForce RTX 5060 Ti.
- Legacy XR: `supported=[None]`, empty loaded device, enabled=false, active=false, eye=0x0.
- XR subsystem descriptors: display=0, input=0.
- Scene traversal observed: Start, Loading, Part1_Cabin, return to Start.
- Table-scene cameras include Pixel Camera, Effects Camera, PerspectiveUICamera, OrthographicUICamera, card render and map render cameras.
- No Error/Fatal/Exception entries found by targeted log search. This is not a statement about every possible gameplay issue.

## Decision

The existing player's legacy XR configuration exposes no usable provider. Do not ask the user to enable the legacy OpenVR setting. Proceed with a separate x86 OpenVR runtime connection experiment. Direct3D11 is a suitable target for investigating a native compositor bridge, but its rendering and synchronization have not been implemented.

Next test: use the official x86 OpenVR SDK in Background application mode to report runtime installation, headset presence and initialization error/success. This requires the user to run SteamVR and connect the Quest 3 through Steam Link first. A successful connection only establishes runtime communication; it does not render stereo images or implement head tracking.

## Native probe development validation

Plugin 0.1.1 compiles cleanly. A standalone x86 smoke harness loaded the official DLL, resolved the probe exports and executed the availability calls without crashing. It reported runtime=false and HMD=false while running inside the tool's sandbox, whose redirected OpenVR registry path was absent. This validates the negative path and native ABI entry points; it does not determine whether SteamVR is installed in the user's normal session. In-game initialization still requires the next user-run test.
