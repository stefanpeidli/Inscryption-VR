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
    check(argc == 3, "usage: BridgeSmoke.exe bridge.dll MockOpenVR.dll");
    wchar_t bridgePath[MAX_PATH], mockPath[MAX_PATH];
    check(MultiByteToWideChar(CP_UTF8, 0, argv[1], -1, bridgePath, MAX_PATH) != 0, "bridge path conversion");
    check(MultiByteToWideChar(CP_UTF8, 0, argv[2], -1, mockPath, MAX_PATH) != 0, "mock path conversion");
    HMODULE mock = LoadLibraryW(mockPath), bridge = LoadLibraryW(bridgePath);
    check(mock != nullptr && bridge != nullptr, "load libraries");
    auto start = load<Start>(bridge, "ivr_start");
    auto frame = load<Frame>(bridge, "ivr_get_frame");
    auto textures = load<SetTextures>(bridge, "ivr_set_textures");
    auto status = load<Status>(bridge, "ivr_get_submit_status");
    auto shutdown = load<Shutdown>(bridge, "ivr_shutdown");
    auto event = reinterpret_cast<Event>(load<GetEvent>(bridge, "ivr_get_render_event_func")());
    auto config = load<Config>(mock, "mock_config");
    auto stats = load<Stats>(mock, "mock_stats");
    check(event != nullptr, "render callback");
    uint32_t width = 0, height = 0, frames = 0;
    int leftError = 0, rightError = 0;
    check(start(mockPath, &width, &height) == 0 && width == 1200 && height == 1400, "Scene start/recommended dimensions");
    MockStats recorded = {}; stats(&recorded);
    check(recorded.applicationType == 1 && recorded.trackingSpace == 0, "Scene application/seated origin");
    check(recorded.interfaceSystem == 1 && recorded.interfaceCompositor == 1, "pinned FnTable versions");
    check(start(mockPath, &width, &height) == -1002, "duplicate startup rejected");
    float head[12] = {}, left[12] = {}, right[12] = {}, lf[4] = {}, rf[4] = {};
    check(frame(nullptr, left, right, lf, rf, .1f, 50) == -2, "null frame buffer rejected");
    check(frame(head, left, right, lf, rf, .1f, 50) == 1, "valid frame");
    stats(&recorded);
    check(recorded.waitCalls == 1 && recorded.submitCalls == 0 && recorded.pollCalls == 0, "compositor prediction waits before rendering; system polling never used");
    check(equal(head[0], 1) && equal(head[5], 1) && equal(head[10], 1) && equal(head[3], 11) && equal(head[7], 2) && equal(head[11], -3), "3x4 pose structure layout");
    check(equal(left[3], -.032f) && equal(right[3], .032f) && equal(left[10], 1), "stdcall aggregate eye-transform return");
    check(equal(lf[0], -1.1f) && equal(rf[0], -1.2f) && equal(lf[2], -.9f) && equal(lf[3], 1.4f), "projection raw pointer ordering");
    Texture l, r;
    textures(&l, &r); check(l.refs == 2 && r.refs == 2, "queued textures AddRef");
    config(1, 0, 0, 0, 22);
    check(frame(head, left, right, lf, rf, .1f, 50) == 2, "pending submission defers new pose sampling");
    stats(&recorded);
    check(recorded.waitCalls == 1 && equal(head[3], 11), "backpressure prevents WaitGetPoses overwriting render pose");
    textures(&l, &r); check(l.refs == 2 && r.refs == 2, "deferred prediction invalidates texture registration until next valid pose");
    event(1); stats(&recorded);
    check(recorded.submitCalls == 2 && recorded.waitCalls == 1 && recorded.handoffs == 1, "left/right submission and handoff");
    check(recorded.eyes[0] == 0 && recorded.eyes[1] == 1 && recorded.handles[0] == &l && recorded.handles[1] == &r, "eye/texture routing");
    check(recorded.flags[0] == 8 && recorded.textureTypes[0] == 0 && recorded.colorSpaces[0] == 0, "TextureWithPose/DirectX/Auto fields");
    check(equal(recorded.bounds[0][0], 0) && equal(recorded.bounds[0][1], 1) && equal(recorded.bounds[0][2], 1) && equal(recorded.bounds[0][3], 0) && equal(recorded.bounds[1][1], 1) && equal(recorded.bounds[1][3], 0), "both eye submission bounds flip Unity render texture Y");
    check(equal(recorded.poseX[0], 11) && equal(recorded.poseX[1], 11), "queued render pose survives runtime pose change");
    check(l.refs == 1 && r.refs == 1, "submission releases frame");
    check(frame(head, left, right, lf, rf, .1f, 50) == 1 && equal(head[3], 22), "new prediction available after submission completes");
    textures(&l, &r);
    event(1); stats(&recorded);
    check(equal(recorded.poseX[2], 22) && equal(recorded.poseX[3], 22) && l.refs == 1 && r.refs == 1 && recorded.waitCalls == 2, "second render pose/release; callback performs no extra wait");
    check(status(&leftError, &rightError, &frames) == 1 && frames == 2 && leftError == 0 && rightError == 0, "successful frame status");
    config(1, 0, 102, 103, 33); frame(head, left, right, lf, rf, .1f, 50); textures(&l, &r); event(1);
    check(status(&leftError, &rightError, &frames) == 1 && leftError == 102 && rightError == 103 && frames == 2 && l.refs == 1 && r.refs == 1, "submit failures reported/references released");
    config(1, FocusError, 0, 0, 44);
    check(frame(head, left, right, lf, rf, .1f, 50) == 2, "focus loss defers rendering instead of failing bridge");
    textures(&l, &r);
    check(status(&leftError, &rightError, &frames) == 1 && leftError == FocusError && rightError == FocusError && l.refs == 1 && r.refs == 1, "focus-loss wait records errors without queuing textures");
    config(1, 0, 0, 0, 44);
    check(frame(head, left, right, lf, rf, .1f, 50) == 1 && status(&leftError, &rightError, &frames) == 1 && leftError == 0 && rightError == 0, "focus recovery clears stale wait errors");
    textures(&l, &r); event(1);
    check(status(&leftError, &rightError, &frames) == 1 && frames == 3, "rendering resumes after wait focus recovery");
    config(1, 0, FocusError, FocusError, 45); frame(head, left, right, lf, rf, .1f, 50); textures(&l, &r); event(1);
    check(status(&leftError, &rightError, &frames) == 1 && leftError == FocusError && rightError == FocusError && frames == 3 && l.refs == 1 && r.refs == 1, "submit focus loss releases texture refs without counting frame");
    config(1, 0, 0, 0, 46);
    check(frame(head, left, right, lf, rf, .1f, 50) == 1 && status(&leftError, &rightError, &frames) == 1 && leftError == 0 && rightError == 0, "successful prediction retries submit-focus loss");
    textures(&l, &r); event(1);
    check(status(&leftError, &rightError, &frames) == 1 && frames == 4, "rendering resumes after submit focus recovery");
    config(0, 0, 0, 0, 55);
    check(frame(head, left, right, lf, rf, .1f, 50) == 0, "invalid tracking rejected");
    textures(&l, &r); check(l.refs == 1 && r.refs == 1, "invalid tracking queues no textures");
    config(1, 0, 0, 0, 66); frame(head, left, right, lf, rf, .1f, 50); textures(&l, &r);
    shutdown(); check(l.refs == 2 && r.refs == 2, "shutdown request retains pending textures until render event");
    check(frame(head, left, right, lf, rf, .1f, 50) == -1, "shutdown blocks new poses");
    event(2); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == 2 && recorded.shutdownCalls == 1 && l.refs == 1 && r.refs == 1, "render shutdown acknowledges/reclaims pending references");
    check(l.adds == l.releases && r.adds == r.releases, "balanced texture ownership");
    event(1); event(2); stats(&recorded);
    check(recorded.shutdownCalls == 1, "post-shutdown callbacks are harmless");
    check(start(mockPath, &width, &height) == 0, "session can restart after acknowledged cleanup");
    config(1, 0, 0, 0, 77); frame(head, left, right, lf, rf, .1f, 50);
    for (int index = 0; index < 8; ++index) textures(&l, &r);
    check(l.refs == 9 && r.refs == 9, "queue retains exactly eight frames");
    textures(&l, &r);
    check(status(&leftError, &rightError, &frames) == -1010 && l.refs == 9 && r.refs == 9, "overflow stops bridge without retaining ninth frame");
    event(1); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == 2 && recorded.shutdownCalls == 2 && l.refs == 1 && r.refs == 1, "overflow cleanup drains all pending references on render callback");
    check(l.adds == l.releases && r.adds == r.releases, "all lifetime paths balanced");
    check(start(mockPath, &width, &height) == 0, "restart for fatal wait-error lifecycle");
    stats(&recorded); int previousSubmits = recorded.submitCalls;
    config(1, 100, 0, 0, 44);
    check(frame(head, left, right, lf, rf, .1f, 50) == -1011, "fatal prediction wait error stops frame acquisition");
    textures(&l, &r); event(1); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == -1011 && leftError == 100 && rightError == 100 && frames == 0 && recorded.submitCalls == previousSubmits && l.refs == 1 && r.refs == 1, "fatal wait error queues no textures or submission");
    check(frame(head, left, right, lf, rf, .1f, 50) == -1, "fatal bridge rejects subsequent frame acquisition");
    shutdown(); event(2); stats(&recorded);
    check(status(&leftError, &rightError, &frames) == 2 && recorded.shutdownCalls == 3 && l.adds == l.releases && r.adds == r.releases, "fatal wait-error shutdown acknowledges cleanup");
    check(recorded.pollCalls == 0, "all rendering poses obtained from compositor prediction");
    FreeLibrary(bridge); FreeLibrary(mock);
    std::printf("PASS: %d native bridge ABI/lifetime assertions (mock runtime only).\n", assertions);
    return 0;
}
