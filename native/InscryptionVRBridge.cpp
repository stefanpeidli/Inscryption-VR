// OpenVR v2.12.14 C function tables. Exported ABI is cdecl; table ABI is stdcall.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <unknwn.h>
#include <cstring>
// Valve's generated C header typedefs `bool` when __WIN32 is defined even in C++.
// Keep _WIN32 for its stdcall ABI selection; use the standard C++ bool branch.
#ifdef __WIN32
#undef __WIN32
#define IVR_RESTORE_WIN32_ALIAS
#endif
#include "openvr_capi.h"
#ifdef IVR_RESTORE_WIN32_ALIAS
#define __WIN32 1
#undef IVR_RESTORE_WIN32_ALIAS
#endif

#define IVR_EXPORT extern "C" __declspec(dllexport)
namespace {
using InitFn = uint32_t (__cdecl *)(int*, int, const char*);
using ShutdownFn = void (__cdecl *)();
using InterfaceFn = void* (__cdecl *)(const char*, int*);
SRWLOCK gate = SRWLOCK_INIT;
struct Lock { Lock() { AcquireSRWLockExclusive(&gate); } ~Lock() { ReleaseSRWLockExclusive(&gate); } };
HMODULE apiModule = nullptr;
ShutdownFn shutdownApi = nullptr;
VR_IVRSystem_FnTable* systemApi = nullptr;
VR_IVRCompositor_FnTable* compositor = nullptr;
int state = 0; // 0 unopened, 1 running, 2 cleanup acknowledged, <0 bridge error
int leftError = 0, rightError = 0;
uint32_t frames = 0;
bool shutdownRequested = false, frameValid = false;
HmdMatrix34_t sampledPose = {};
constexpr unsigned QueueCapacity = 8;
struct PendingFrame { IUnknown* left; IUnknown* right; HmdMatrix34_t pose; };
PendingFrame pending[QueueCapacity] = {};
unsigned head = 0, count = 0;

void releaseFrame(PendingFrame& frame) {
    if (frame.left) frame.left->Release();
    if (frame.right) frame.right->Release();
    frame = {};
}
void cleanup() {
    // Called only by a render event after startup, or before any event during failed startup.
    while (count) {
        releaseFrame(pending[head]);
        head = (head + 1) % QueueCapacity;
        --count;
    }
    systemApi = nullptr;
    compositor = nullptr;
    if (shutdownApi) shutdownApi();
    shutdownApi = nullptr;
    if (apiModule) FreeLibrary(apiModule);
    apiModule = nullptr;
    frameValid = false;
    shutdownRequested = false;
}

void __stdcall renderEvent(int eventId) {
    Lock lock;
    if (eventId == 2 || shutdownRequested) {
        cleanup();
        state = 2;
        return;
    }
    if (eventId != 1 || state != 1 || !compositor || !count) return;
    PendingFrame frame = pending[head];
    pending[head] = {};
    head = (head + 1) % QueueCapacity;
    --count;

    // Preserve the user-verified 0.2.0 render-thread pacing path.
    // The predicted-before-render experiment was withdrawn after a headset crash.
    TrackedDevicePose_t predicted[k_unMaxTrackedDeviceCount] = {};
    auto waitError = compositor->WaitGetPoses(predicted, k_unMaxTrackedDeviceCount, nullptr, 0);
    if (waitError != EVRCompositorError_VRCompositorError_None) {
        leftError = rightError = static_cast<int>(waitError);
        releaseFrame(frame);
        return;
    }

    // Submit the original system-poll pose used by the eyes, not the newer WaitGetPoses pose.
    // The baseline deliberately waits after rendering; its latency remains under investigation.
    VRTextureWithPose_t left = {}, right = {};
    left.handle = frame.left;
    right.handle = frame.right;
    left.eType = right.eType = ETextureType_TextureType_DirectX;
    // Eye RenderTextures contain Unity's final display color; managed side uses sRGB targets.
    left.eColorSpace = right.eColorSpace = EColorSpace_ColorSpace_Auto;
    left.mDeviceToAbsoluteTracking = right.mDeviceToAbsoluteTracking = frame.pose;
    // Unity Camera->RenderTexture normalizes to OpenGL-like bottom-up UVs even on D3D11.
    // OpenVR consumes native D3D textures with top-down UVs, so invert the submitted V bounds.
    VRTextureBounds_t bounds = { 0.0f, 1.0f, 1.0f, 0.0f };
    leftError = static_cast<int>(compositor->Submit(EVREye_Eye_Left,
        reinterpret_cast<Texture_t*>(&left), &bounds, EVRSubmitFlags_Submit_TextureWithPose));
    rightError = static_cast<int>(compositor->Submit(EVREye_Eye_Right,
        reinterpret_cast<Texture_t*>(&right), &bounds, EVRSubmitFlags_Submit_TextureWithPose));
    compositor->PostPresentHandoff();
    if (!leftError && !rightError) ++frames;
    releaseFrame(frame);
}
}

IVR_EXPORT int __cdecl ivr_start(const wchar_t* absoluteApiPath, uint32_t* width, uint32_t* height) {
    Lock lock;
    if (!absoluteApiPath || !width || !height) return -1001;
    *width = *height = 0;
    if (state == 1 || apiModule) return -1002;
    // Do not disrupt another mod's process-level OpenVR client session.
    if (GetModuleHandleW(L"openvr_api.dll") != nullptr) return -1003;
    state = 0;
    shutdownRequested = frameValid = false;
    head = count = frames = 0;
    leftError = rightError = 0;
    apiModule = LoadLibraryExW(absoluteApiPath, nullptr,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!apiModule) { state = -1004; return state; }
    auto init = reinterpret_cast<InitFn>(GetProcAddress(apiModule, "VR_InitInternal2"));
    auto getInterface = reinterpret_cast<InterfaceFn>(GetProcAddress(apiModule, "VR_GetGenericInterface"));
    auto shutdown = reinterpret_cast<ShutdownFn>(GetProcAddress(apiModule, "VR_ShutdownInternal"));
    if (!init || !getInterface || !shutdown) {
        cleanup(); state = -1005; return state;
    }
    int error = 0;
    uint32_t token = init(&error, EVRApplicationType_VRApplication_Scene, nullptr);
    if (error || !token) {
        // Failed initialization cleans up inside Valve's library; no session is owned.
        cleanup(); state = error ? -error : -1006; return error ? error : -1006;
    }
    shutdownApi = shutdown;
    // FnTable prefix requests C ABI tables, not compiler-specific C++ objects.
    systemApi = static_cast<VR_IVRSystem_FnTable*>(getInterface("FnTable:IVRSystem_023", &error));
    if (error || !systemApi) {
        cleanup(); state = -1007; return error ? error : state;
    }
    compositor = static_cast<VR_IVRCompositor_FnTable*>(getInterface("FnTable:IVRCompositor_029", &error));
    if (error || !compositor) {
        cleanup(); state = -1008; return error ? error : state;
    }
    systemApi->GetRecommendedRenderTargetSize(width, height);
    if (!*width || !*height || *width > 8192 || *height > 8192) {
        cleanup(); state = -1009; return state;
    }
    compositor->SetTrackingSpace(ETrackingUniverseOrigin_TrackingUniverseSeated);
    state = 1;
    return 0;
}

IVR_EXPORT int __cdecl ivr_get_frame(float* hmd12, float* leftEye12, float* rightEye12,
    float* leftFrustum4, float* rightFrustum4, float nearClip, float farClip) {
    (void)nearClip; (void)farClip; // Raw projection tangents do not depend on clip planes.
    Lock lock;
    if (state != 1 || !systemApi || shutdownRequested) return -1;
    if (!hmd12 || !leftEye12 || !rightEye12 || !leftFrustum4 || !rightFrustum4) return -2;
    TrackedDevicePose_t poses[k_unMaxTrackedDeviceCount] = {};
    systemApi->GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin_TrackingUniverseSeated,
        0.0f, poses, k_unMaxTrackedDeviceCount);
    const auto& hmd = poses[k_unTrackedDeviceIndex_Hmd];
    frameValid = hmd.bDeviceIsConnected && hmd.bPoseIsValid;
    if (!frameValid) return 0;
    sampledPose = hmd.mDeviceToAbsoluteTracking;
    auto leftEye = systemApi->GetEyeToHeadTransform(EVREye_Eye_Left);
    auto rightEye = systemApi->GetEyeToHeadTransform(EVREye_Eye_Right);
    std::memcpy(hmd12, &sampledPose, sizeof(sampledPose));
    std::memcpy(leftEye12, &leftEye, sizeof(leftEye));
    std::memcpy(rightEye12, &rightEye, sizeof(rightEye));
    systemApi->GetProjectionRaw(EVREye_Eye_Left, leftFrustum4, leftFrustum4 + 1,
        leftFrustum4 + 2, leftFrustum4 + 3);
    systemApi->GetProjectionRaw(EVREye_Eye_Right, rightFrustum4, rightFrustum4 + 1,
        rightFrustum4 + 2, rightFrustum4 + 3);
    return 1;
}

IVR_EXPORT void __cdecl ivr_set_textures(void* left, void* right) {
    Lock lock;
    if (state != 1 || shutdownRequested || !frameValid || !left || !right) return;
    if (count == QueueCapacity) { state = -1010; shutdownRequested = true; return; }
    PendingFrame& frame = pending[(head + count) % QueueCapacity];
    frame.left = static_cast<IUnknown*>(left);
    frame.right = static_cast<IUnknown*>(right);
    frame.pose = sampledPose;
    frame.left->AddRef();
    frame.right->AddRef();
    ++count;
}

IVR_EXPORT void* __cdecl ivr_get_render_event_func() { return reinterpret_cast<void*>(&renderEvent); }

IVR_EXPORT void __cdecl ivr_shutdown() {
    Lock lock;
    if (state == 1 || apiModule) shutdownRequested = true;
    else state = 2;
}

IVR_EXPORT int __cdecl ivr_get_submit_status(int* left, int* right, uint32_t* submittedFrames) {
    Lock lock;
    if (left) *left = leftError;
    if (right) *right = rightError;
    if (submittedFrames) *submittedFrames = frames;
    return state;
}
