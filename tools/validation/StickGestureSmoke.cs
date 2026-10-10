using System;
using InscryptionVR;

internal static class StickGestureSmoke
{
    private static int _checks;
    private static void Expect(StickGesture actual, StickGesture expected, string label)
    {
        _checks++;
        if (actual != expected) throw new Exception(label + ": " + actual + " instead of " + expected);
    }
    public static int Main()
    {
        StickGestureState state = new StickGestureState();
        Expect(state.Sample(0f, 1f), StickGesture.None, "Held on activation");
        Expect(state.Sample(0f, 0f), StickGesture.None, "Neutral arms");
        Expect(state.Sample(0f, .64f), StickGesture.None, "Below actuation");
        Expect(state.Sample(0f, .65f), StickGesture.Up, "Actuation threshold");
        Expect(state.Sample(0f, 1f), StickGesture.None, "No held repeat");
        Expect(state.Sample(1f, 0f), StickGesture.None, "Direction change requires neutral");
        Expect(state.Sample(.26f, 0f), StickGesture.None, "Outside neutral does not arm");
        Expect(state.Sample(-1f, 0f), StickGesture.None, "Still disarmed");
        Expect(state.Sample(.25f, -.25f), StickGesture.None, "Neutral boundary arms");
        Expect(state.Sample(-1f, .8f), StickGesture.Left, "Dominant horizontal");
        state.Cancel();
        Expect(state.Sample(0f, -1f), StickGesture.None, "Cancel requires neutral");
        Expect(state.Sample(0f, 0f), StickGesture.None, "Rearm after cancel");
        Expect(state.Sample(0f, -1f), StickGesture.Down, "Down");
        Expect(state.Sample(0f, 0f), StickGesture.None, "Neutral");
        Expect(state.Sample(1f, .1f), StickGesture.Right, "Right");
        Expect(state.Sample(0f, 0f), StickGesture.None, "Neutral before diagonal");
        Expect(state.Sample(.8f, .8f), StickGesture.Up, "Diagonal tie uses vertical");
        Expect(state.Sample(0f, 0f), StickGesture.None, "Neutral before invalid");
        Expect(state.Sample(float.NaN, 0f), StickGesture.None, "NaN cancels");
        Expect(state.Sample(1f, 0f), StickGesture.None, "Invalid sample disarms");
        Expect(state.Sample(0f, 0f), StickGesture.None, "Neutral before infinity");
        Expect(state.Sample(0f, float.PositiveInfinity), StickGesture.None, "Infinity cancels");
        Expect(state.Sample(0f, 1f), StickGesture.None, "Infinity requires rearm");
        Console.WriteLine("PASS: " + _checks + " stick gesture assertions.");
        return 0;
    }
}
