using System;
using System.Runtime.InteropServices;
using Valve.VR;

internal static class OpenVrControllerLayoutSmoke
{
    private static int _checks;
    private static void Check(bool result, string label)
    {
        _checks++;
        if (!result) throw new Exception(label);
    }

    private static void Main()
    {
        // Windows x86 OpenVR ABI. No runtime initialization or headset connection.
        Check(IntPtr.Size == 4, "Harness must execute as x86");
        Check(Marshal.SizeOf(typeof(VRControllerState_t)) == 64, "Windows controller state size");
        Check(Marshal.OffsetOf(typeof(VRControllerState_t), "ulButtonPressed").ToInt32() == 8, "Pressed mask alignment");
        Check(Marshal.OffsetOf(typeof(VRControllerState_t), "ulButtonTouched").ToInt32() == 16, "Touched mask alignment");
        Check(Marshal.OffsetOf(typeof(VRControllerState_t), "rAxis0").ToInt32() == 24, "First axis offset");
        Check(Marshal.OffsetOf(typeof(VRControllerState_t), "rAxis4").ToInt32() == 56, "Last axis offset");
        Check(Marshal.SizeOf(typeof(TrackedDevicePose_t)) == 80, "Tracked pose size");
        Check(Marshal.OffsetOf(typeof(TrackedDevicePose_t), "bPoseIsValid").ToInt32() == 76, "Pose validity byte offset");
        Check(Marshal.OffsetOf(typeof(TrackedDevicePose_t), "bDeviceIsConnected").ToInt32() == 77, "Pose connected byte offset");
        Check((int)EVRButtonId.k_EButton_SteamVR_Trigger == 33, "Legacy trigger mask bit");
        Check((int)ETrackedDeviceProperty.Prop_Axis0Type_Int32 == 3002, "First axis property");
        Check((int)EVRControllerAxisType.k_eControllerAxis_Trigger == 3, "Trigger axis type");
        Check(Marshal.SizeOf(typeof(Compositor_FrameTiming)) == 192, "Windows compositor timing size");
        Check(Marshal.SizeOf(typeof(Compositor_CumulativeStats)) == 112, "Windows cumulative statistics size");
        Check(Marshal.OffsetOf(typeof(Compositor_FrameTiming), "m_HmdPose").ToInt32() == 96, "Timing HMD pose offset");
        Check(Marshal.OffsetOf(typeof(Compositor_FrameTiming), "m_flTransferLatencyMs").ToInt32() == 184, "Timing transfer latency offset");
        Console.WriteLine("PASS: " + _checks + " Windows x86 OpenVR controller ABI assertions (no live runtime).");
    }
}
