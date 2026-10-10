// Regression fixture for the user-proven 0.2.0 native binary, not withdrawn 0.2.1 timing.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <unknwn.h>
#include <cstdio>
#include <cstdlib>
#include <cmath>
#include "MockContract.h"
namespace {
int assertions = 0;
void check(bool success, const char* label) {
    ++assertions;
    if (!success) { std::fprintf(stderr, "FAIL: %s\n", label); std::exit(1); }
}
bool equal(float a, float b) { return std::fabs(a - b) < .00001f; }
template<typename T> T load(HMODULE module, const char* name) {
    FARPROC proc = GetProcAddress(module, name); check(proc != nullptr, name); return reinterpret_cast<T>(proc);
}
struct Texture : IUnknown {
    ULONG refs = 1, adds = 0, releases = 0;
    HRESULT __stdcall QueryInterface(REFIID, void** result) override { if (result) *result = nullptr; return E_NOINTERFACE; }
    ULONG __stdcall AddRef() override { ++adds; return ++refs; }
    ULONG __stdcall Release() override { ++releases; return --refs; }
};
using Start = int (__cdecl *)(const wchar_t*, uint32_t*, uint32_t*);
using Frame = int (__cdecl *)(float*, float*, float*, float*, float*, float, float);
using SetTextures = void (__cdecl *)(void*, void*);
using Status = int (__cdecl *)(int*, int*, uint32_t*);
using Shutdown = void (__cdecl *)();
using Event = void (__stdcall *)(int);
using GetEvent = void* (__cdecl *)();
using Config = void (__cdecl *)(int, int, int, int, float);
using Stats = void (__cdecl *)(MockStats*);
}
int main(int argc, char** argv) {
    check(sizeof(void*) == 4, "x86 harness");
    check(argc == 3, "usage: BridgeBaselineSmoke.exe golden-bridge.dll MockOpenVR.dll");
    wchar_t bridgePath[MAX_PATH], mockPath[MAX_PATH];
    check(MultiByteToWideChar(CP_UTF8, 0, argv[1], -1, bridgePath, MAX_PATH) != 0, "bridge path conversion");
    check(MultiByteToWideChar(CP_UTF8, 0, argv[2], -1, mockPath, MAX_PATH) != 0, "mock path conversion");
    HMODULE mock = LoadLibraryW(mockPath), bridge = LoadLibraryW(bridgePath);
    check(mock != nullptr && bridge != nullptr, "load mock and baseline libraries");
    auto start = load<Start>(bridge, "ivr_start");
    auto frame = load<Frame>(bridge, "ivr_get_frame");
    auto textures = load<SetTextures>(bridge, "ivr_set_textures");
    auto status = load<Status>(bridge, "ivr_get_submit_status");
    auto shutdown = load<Shutdown>(bridge, "ivr_shutdown");
    auto event = reinterpret_cast<Event>(load<GetEvent>(bridge, "ivr_get_render_event_func")());
    auto config = load<Config>(mock, "mock_config");
    auto stats = load<Stats>(mock, "mock_stats");
    check(event != nullptr, "stdcall render callback");
    uint32_t width = 0, height = 0, frames = 0;
    int leftError = 0, rightError = 0;
    check(start(mockPath, &width, &height) == 0 && width == 1200 && height == 1400, "Scene startup and eye dimensions");
    MockStats recorded = {}; stats(&recorded);
    check(recorded.applicationType == 1 && recorded.trackingSpace == 0 && recorded.interfaceSystem == 1 && recorded.interfaceCompositor == 1, "Scene/seated/pinned FnTables");
    check(start(mockPath, &width, &height) == -1002, "duplicate startup rejected");
    float head[12] = {}, left[12] = {}, right[12] = {}, lf[4] = {}, rf[4] = {};
    check(frame(nullptr, left, right, lf, rf, .1f, 50) == -2, "null frame output rejected");
    config(1, 0, 0, 0, 11);
    check(frame(head, left, right, lf, rf, .1f, 50) == 1, "baseline valid sampled frame");
    stats(&recorded);
    check(recorded.pollCalls == 1 && recorded.waitCalls == 0 && recorded.submitCalls == 0, "baseline polls before render without compositor wait");
    check(equal(head[0], 1) && equal(head[5], 1) && equal(head[10], 1) && equal(head[3], 999) && equal(head[7], 2) && equal(head[11], -3), "baseline returns system-poll matrix layout");
    check(equal(left[3], -.032f) && equal(right[3], .032f) && equal(left[10], 1), "stdcall 48-byte aggregate eye return");
    check(equal(lf[0], -1.1f) && equal(rf[0], -1.2f) && equal(lf[2], -.9f) && equal(lf[3], 1.4f), "per-eye raw frustum pointer layout");
    Texture l, r;
    textures(&l, &r); check(l.refs == 2 && r.refs == 2, "first queued textures retained");
    config(1, 0, 0, 0, 22);
    check(frame(head, left, right, lf, rf, .1f, 50) == 1, "baseline permits sampling with pending callback");
    textures(&l, &r); check(l.refs == 3 && r.refs == 3, "independent second frame references");
    event(1); stats(&recorded);
    check(recorded.waitCalls == 1 && recorded.submitCalls == 2 && recorded.handoffs == 1, "render callback waits then submits both eyes");
    check(recorded.eyes[0] == 0 && recorded.eyes[1] == 1 && recorded.handles[0] == &l && recorded.handles[1] == &r, "eye texture routing");
    check(recorded.flags[0] == 8 && recorded.textureTypes[0] == 0 && recorded.colorSpaces[0] == 0, "TextureWithPose DirectX Auto ABI fields");
    check(equal(recorded.poseX[0], 999) && equal(recorded.poseX[1], 999), "queued image uses sampled pose and ignores later WaitGetPoses pose22");
    check(equal(recorded.bounds[0][0], 0) && equal(recorded.bounds[0][1], 1) && equal(recorded.bounds[0][2], 1) && equal(recorded.bounds[0][3], 0) && equal(recorded.bounds[1][1], 1) && equal(recorded.bounds[1][3], 0), "both eyes vertically flipped submit bounds");
    check(l.refs == 2 && r.refs == 2, "only submitted frame references released");
    config(1, 0, 0, 0, 33); event(1); stats(&recorded);
    check(recorded.waitCalls == 2 && recorded.submitCalls == 4 && equal(recorded.poseX[2], 999) && equal(recorded.poseX[3], 999) && l.refs == 1 && r.refs == 1, "second queued capture and FIFO release ignore changed runtime prediction");
    check(status(&leftError, &rightError, &frames) == 1 && leftError == 0 && rightError == 0 && frames == 2, "successful baseline frame count");
    config(1, 0, 102, 103, 44); frame(head, left, right, lf, rf, .1f, 50); textures(&l, &r); event(1);
    check(status(&leftError, &rightError, &frames) == 1 && leftError == 102 && rightError == 103 && frames == 2 && l.refs == 1 && r.refs == 1, "submit error reporting and release");
    config(1, 100, 0, 0, 55);
    check(frame(head, left, right, lf, rf, .1f, 50) == 1, "baseline sampling precedes wait error");
    textures(&l, &r); event(1); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == 1 && leftError == 100 && rightError == 100 && frames == 2 && recorded.submitCalls == 6 && l.refs == 1 && r.refs == 1, "wait error skips submit and releases queued textures");
    config(0, 0, 0, 0, 66);
    check(frame(head, left, right, lf, rf, .1f, 50) == 0, "invalid polling pose pauses rendering");
    textures(&l, &r); check(l.refs == 1 && r.refs == 1, "invalid pose retains no textures");
    config(1, 0, 0, 0, 77); frame(head, left, right, lf, rf, .1f, 50); textures(&l, &r);
    shutdown(); check(l.refs == 2 && r.refs == 2, "shutdown request defers native resource release");
    check(frame(head, left, right, lf, rf, .1f, 50) == -1, "shutdown rejects new frames");
    event(2); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == 2 && recorded.shutdownCalls == 1 && l.refs == 1 && r.refs == 1, "render-thread cleanup acknowledgement");
    check(l.adds == l.releases && r.adds == r.releases, "normal and error lifetime balanced");
    event(1); event(2); stats(&recorded); check(recorded.shutdownCalls == 1, "callbacks harmless after cleanup");
    check(start(mockPath, &width, &height) == 0, "restart after cleanup");
    config(1, 0, 0, 0, 88); frame(head, left, right, lf, rf, .1f, 50);
    for (int index = 0; index < 8; ++index) textures(&l, &r);
    check(l.refs == 9 && r.refs == 9, "queue capacity retains eight frames");
    textures(&l, &r);
    check(status(&leftError, &rightError, &frames) == -1010 && l.refs == 9 && r.refs == 9, "overflow rejects ninth reference pair");
    event(1); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == 2 && recorded.shutdownCalls == 2 && l.refs == 1 && r.refs == 1, "overflow cleanup drains queued resources");
    check(l.adds == l.releases && r.adds == r.releases, "all reference paths balanced");
    FreeLibrary(bridge); FreeLibrary(mock);
    std::printf("PASS: %d baseline 0.2.0 native ABI/lifetime assertions (mock runtime only).\n", assertions);
    return 0;
}
