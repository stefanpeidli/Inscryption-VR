#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstring>
#ifdef __WIN32
#undef __WIN32
#endif
#include "openvr_capi.h"
#include "MockContract.h"
static_assert(FocusError == EVRCompositorError_VRCompositorError_DoNotHaveFocus, "focus enum must match pinned SDK");
#define EXPORT extern "C" __declspec(dllexport)
namespace {
MockStats stats = {};
bool valid = true;
int waitError = 0, leftError = 0, rightError = 0;
float sampleX = 11.0f;
VR_IVRSystem_FnTable systemTable = {};
VR_IVRCompositor_FnTable compositorTable = {};
HmdMatrix34_t pose(float x) {
    HmdMatrix34_t value = {};
    value.m[0][0] = value.m[1][1] = value.m[2][2] = 1;
    value.m[0][3] = x; value.m[1][3] = 2; value.m[2][3] = -3;
    return value;
}
void __stdcall size(uint32_t* width, uint32_t* height) { *width = 1200; *height = 1400; }
HmdMatrix34_t __stdcall eye(EVREye which) { return pose(which == EVREye_Eye_Left ? -.032f : .032f); }
void __stdcall projection(EVREye which, float* left, float* right, float* top, float* bottom) {
    *left = which == EVREye_Eye_Left ? -1.1f : -1.2f;
    *right = 1.3f; *top = -.9f; *bottom = 1.4f;
}
void __stdcall tracking(ETrackingUniverseOrigin origin, float, TrackedDevicePose_t* poses, uint32_t count) {
    ++stats.pollCalls;
    stats.trackingSpace = static_cast<int>(origin);
    if (count) {
        poses[0].bDeviceIsConnected = true;
        poses[0].bPoseIsValid = valid;
        poses[0].mDeviceToAbsoluteTracking = pose(999); // The renderer must use compositor-predicted poses instead.
    }
}
void __stdcall setSpace(ETrackingUniverseOrigin origin) { stats.trackingSpace = static_cast<int>(origin); }
EVRCompositorError __stdcall wait(TrackedDevicePose_t* poses, uint32_t count, TrackedDevicePose_t*, uint32_t) {
    ++stats.waitCalls;
    if (count) {
        poses[0].bDeviceIsConnected = true;
        poses[0].bPoseIsValid = valid;
        poses[0].mDeviceToAbsoluteTracking = pose(sampleX);
    }
    return static_cast<EVRCompositorError>(waitError);
}
EVRCompositorError __stdcall submit(EVREye which, Texture_t* texture, VRTextureBounds_t* bounds, EVRSubmitFlags flags) {
    int index = stats.submitCalls++;
    if (index < 32) {
        stats.eyes[index] = static_cast<int>(which);
        stats.flags[index] = static_cast<int>(flags);
        stats.textureTypes[index] = static_cast<int>(texture->eType);
        stats.colorSpaces[index] = static_cast<int>(texture->eColorSpace);
        stats.handles[index] = texture->handle;
        stats.poseX[index] = reinterpret_cast<VRTextureWithPose_t*>(texture)->mDeviceToAbsoluteTracking.m[0][3];
        if (bounds) {
            stats.bounds[index][0] = bounds->uMin; stats.bounds[index][1] = bounds->vMin;
            stats.bounds[index][2] = bounds->uMax; stats.bounds[index][3] = bounds->vMax;
        }
    }
    return static_cast<EVRCompositorError>(which == EVREye_Eye_Left ? leftError : rightError);
}
void __stdcall handoff() { ++stats.handoffs; }
void configureTables() {
    systemTable.GetRecommendedRenderTargetSize = &size;
    systemTable.GetEyeToHeadTransform = &eye;
    systemTable.GetProjectionRaw = &projection;
    systemTable.GetDeviceToAbsoluteTrackingPose = &tracking;
    compositorTable.SetTrackingSpace = &setSpace;
    compositorTable.WaitGetPoses = &wait;
    compositorTable.Submit = &submit;
    compositorTable.PostPresentHandoff = &handoff;
}
}
EXPORT uint32_t __cdecl VR_InitInternal2(int* error, int applicationType, const char*) {
    ++stats.initCalls; stats.applicationType = applicationType; *error = 0; configureTables(); return 7;
}
EXPORT void __cdecl VR_ShutdownInternal() { ++stats.shutdownCalls; }
EXPORT void* __cdecl VR_GetGenericInterface(const char* version, int* error) {
    *error = 0;
    if (!std::strcmp(version, "FnTable:IVRSystem_023")) { ++stats.interfaceSystem; return &systemTable; }
    if (!std::strcmp(version, "FnTable:IVRCompositor_029")) { ++stats.interfaceCompositor; return &compositorTable; }
    *error = 105; return nullptr;
}
EXPORT void __cdecl mock_config(int poseValid, int waitCode, int leftCode, int rightCode, float x) {
    valid = poseValid != 0; waitError = waitCode; leftError = leftCode; rightError = rightCode; sampleX = x;
}
EXPORT void __cdecl mock_stats(MockStats* output) { *output = stats; }
