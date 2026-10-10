#pragma once
#include <cstdint>
constexpr int FocusError = 101; // Pinned OpenVR EVRCompositorError_DoNotHaveFocus.
struct MockStats {
    int initCalls, shutdownCalls, trackingSpace, waitCalls, submitCalls, handoffs;
    int applicationType, interfaceSystem, interfaceCompositor, pollCalls;
    int eyes[32], flags[32], textureTypes[32], colorSpaces[32];
    void* handles[32];
    float poseX[32];
    float bounds[32][4];
};
