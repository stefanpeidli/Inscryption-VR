# Runtime test 03: stereo table scene verified on Quest 3

The user tested plugin 0.2.0 through Steam Link. Their report confirms that entering Act 1 opened the scene in VR, initial camera placement was correct, centering/recentering worked, and the view responded to head movement. They reported a minor issue: space appeared to warp incorrectly during head movement.

The installed log confirms a native stereo Scene session, accepted compositor submission, two F8 recenter requests, F9 stop and render-thread shutdown acknowledgment. The resolved source is `FirstPersonController/PixelCameraParent/Pixel Camera`, culling mask 269320725, desktop target, and Skybox clear. Eye textures were 1342x1435 at one world unit per metre, with 0.03 near clipping. No Error/Fatal/Exception entries appeared in the targeted log search.

This satisfies the first headset rendering milestone, with a tracked table view and working recenter. It does not establish complete gameplay, controller input, stereo menus/UI, all story sequences, or comfort across prolonged play.

## Tracking issue under investigation

The current renderer samples an unpredicted pose before rendering, then calls WaitGetPoses during submission after rendering. It preserves the rendered pose for reprojection, but this sequence adds avoidable pose age. The user's description could indicate pose timing/reprojection, positional scale or projection mismatch; it is not yet a confirmed diagnosis. Clarify whether motion causes temporary wobble, incorrect lean distance, or persistent shape distortion before attributing the issue solely to latency.

## Refinement 0.2.1

The timing sequence now uses the predicted WaitGetPoses result before rendering. Submission remains on the render thread and performs no second wait. Pending submissions defer the next pose call so a frame's texture contents cannot be overwritten early. Pinned focus error 101 is transient; invalid texture 102 remains fatal. Both components compiled and 63 isolated native ABI/lifecycle assertions passed, including focus recovery and predicted-pose ordering. This is an engineering fix for the observed timing defect, not yet confirmation that the user's perceived distortion is gone.
