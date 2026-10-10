# Rotation distortion investigation

The user reports that rotating their head makes the table and Leshy appear to tilt or bend slightly. Dialogue and battle controls work. This is more specific than the earlier report of generic movement warping, but does not yet establish its cause.

The geometric audit found no confirmed defect in the OpenVR-to-Unity pose conversion, eye composition, or asymmetric projection tangent mapping. The seated anchor, recenter yaw and optional fixed viewing tilt form a world transform that cancels when computing relative head rotation. Altering the raw pose submitted to the compositor to include virtual tilt would introduce a mismatch.

The native renderer remains the exact user-verified 0.2.0 binary. It samples an unpredicted pose before rendering, then calls the compositor wait during submission. It submits the matching original pose without depth. Pose age, runtime reprojection or motion smoothing remain plausible explanations, not confirmed findings. The earlier attempt to change native timing crashed and remains withdrawn.

## Read-only evidence

`RenderTimingDiagnostics` samples the already-active compositor once every five seconds. It does not initialize or shut down OpenVR, wait for poses, or change runtime settings. Failure disables diagnostics only. The official generated Windows structures are size-checked against the pinned header: frame timing 192 bytes and cumulative statistics 112 bytes, with Windows default packing.

Logs distinguish the most recent frame from cumulative application counters. Last-frame flags describe only that sampled frame; they cannot establish how often motion smoothing occurred between samples. Bit `0x08` indicates motion smoothing triggered for that frame. Bits `0x01` and `0x02` indicate CPU/GPU reprojection reasons. Bit `0x04` alone means asynchronous reprojection mode is active, not that an image was reused. Multiple presents also indicate image reuse.

Cumulative counters are interpreted only when their process ID matches the current game process. Interval deltas are reported only when the counters have not reset. They capture dropped and reprojected counts between observations, but do not independently identify motion smoothing as the cause. The log also includes frame interval, render GPU time, submission time, transport latency and runtime smoothing support/enabled status. A false enabled value when unsupported is not a measurement of the headset's streaming implementation.

Future headset comparison should check slow yaw, pitch and small translations separately, and whether the effect is momentary during movement or remains at the new angle. F7 can compare the fixed viewing tilt. Those observations and the logs should guide a narrowly scoped next change; they do not justify replacing the stable renderer on their own.
