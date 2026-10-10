# Tracking and reprojection review: withdrawn 0.2.1 experiment

The user reports that 0.2.0 displays the correct location, responds to head movement and recenters, with minor apparent space warping during movement. That establishes a useful working baseline. It does not identify a single cause: rendering latency, missed compositor frames, runtime motion smoothing and stream behavior can produce similar symptoms.

The timing proposal below is historical. Version 0.2.1 caused a headset regression and crash and was withdrawn. Its cause remains unconfirmed; the 0.3.1 controller prototype retains the exact working 0.2.0 native binary. Do not treat this proposal as an approved fix.

## Timing hypothesis and attempted change

The original native path called `GetDeviceToAbsoluteTrackingPose` with prediction horizon zero before drawing the eyes. It then called `WaitGetPoses` inside the submission callback, after the eye images were rendered, and discarded that call's predicted poses. Consequently, a fresh compositor frame began using images from an earlier tracking sample. Explicit pose metadata made that internally honest but did not remove the delay or supply missing positional depth.

Valve's [compositor overview](https://github.com/ValveSoftware/openvr/wiki/IVRCompositor_Overview) gives the sequence pose acquisition, eye rendering, then submission. The pinned [OpenVR header](https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/openvr.h) specifies that scene applications acquire render poses immediately before starting rendering. The focused fix follows that sequence:

1. `ivr_get_frame` first rejects a still-queued prior frame with return 2. It does not wait for a Unity callback.
2. With no pending frame, it calls `WaitGetPoses` on the game thread and uses the returned predicted HMD pose for both cameras.
3. Managed code renders the eyes, registers that immutable pose/texture pair and issues render event 1.
4. The render callback submits the images and performs handoff. It does not call `WaitGetPoses` again.

`WaitGetPoses` does not issue Unity graphics commands. The submission and handoff operations remain on the graphics/render thread. The native lock ensures a callback already executing completes before the next pose call enters. The pending-frame check ensures a callback waiting in Unity's command queue can execute before another compositor wait begins. Neither thread synchronously waits on a manually queued Unity event.

Focus error 101 is transient: the pose call records it and returns 2. Error 102 means invalid texture. A later successful pose acquisition clears those errors. Missing tracking returns 0. Other wait errors retain their compositor enum values and set bridge state -1011. Shutdown still runs through render event 2 and retains textures until acknowledgment.

## Pose and projection metadata

The pinned header defines `VRTextureWithPose_t` as the actual pose used to render the images and flag 8 as the indication that submission receives that extended texture structure. The existing frame snapshot and head-pose metadata are appropriate; replacing them with a newer pose that did not render those pixels would be incorrect.

The managed conversion uses `S*M*S`, flipping the OpenVR Z axis for Unity, preserves eye-to-head transforms and builds per-eye projections from the runtime's raw tangents. Submission continues to use the matching raw OpenVR HMD pose. Projection or IPD changes should not be mixed into this timing experiment without evidence of a geometric defect. Upright image orientation and meaningful head tracking have already been observed on the headset.

## Verification and next evidence

Native mock validation should prove that the WaitGetPoses pose reaches the managed camera arrays, zero-horizon system polling is unused, no additional wait occurs during submission, queued prior frames defer without waiting, and focus errors recover. Retain tests for immutable pose ownership and render-thread texture release.

Compare the corrected build against 0.2.0 using slow head rotation, then small seated translations, at the same render scale and runtime settings. Record compositor frame timing and whether the issue affects the whole scene or only edges/objects. Improvement would support the timing explanation; unchanged distortion would justify investigating stream/runtime smoothing, projection and scale separately. This change does not guarantee removal of every warping artifact.
