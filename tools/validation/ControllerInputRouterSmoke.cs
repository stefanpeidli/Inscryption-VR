using System;
using InscryptionVR;

internal static class ControllerInputRouterSmoke
{
    private static int _checks;
    private static void Expect(int actual, int expected, string label)
    {
        _checks++;
        if (actual != expected) throw new Exception(label + ": " + actual + " instead of " + expected);
    }
    private static void Main()
    {
        var router = new ControllerInputRouter();
        Expect(router.Sample(ControllerInputContext.World, false, 0f), 0, "World neutral arms");
        Expect(router.Sample(ControllerInputContext.World, true, 0f), 1, "World press");
        Expect(router.Sample(ControllerInputContext.Dialogue, true, 0f), 0, "Dialogue opening cancels held world press");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, 0f), 0, "Cancelled world release cannot advance dialogue");
        Expect(router.Sample(ControllerInputContext.Dialogue, true, 0f), 1, "Fresh dialogue press");
        Expect(router.Sample(ControllerInputContext.Dialogue, true, 0f), 0, "Dialogue hold does not repeat");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, 0f), -1, "Dialogue release advances once");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, 0f), 0, "No duplicate dialogue advance");
        Expect(router.Sample(ControllerInputContext.World, false, 0f), 0, "Dialogue completion cannot release on a card");
        Expect(router.Sample(ControllerInputContext.World, true, 0f), 1, "Fresh world press after dialogue");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, 0f), 0, "Dialogue transition on release suppresses world click and advance");
        Expect(router.Sample(ControllerInputContext.Dialogue, true, 0f), 1, "New deliberate dialogue press");
        Expect(router.Sample(ControllerInputContext.World, true, 0f), 0, "Dialogue disappears while held");
        Expect(router.Sample(ControllerInputContext.World, false, 0f), 0, "Held dialogue release cannot click world");
        Expect(router.Sample(ControllerInputContext.World, true, 0f), 1, "World rearmed after neutral");
        router.Cancel();
        Expect(router.Sample(ControllerInputContext.Dialogue, true, 0f), 0, "Tracking recovery cancels held input");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, 0f), 0, "Recovery release cannot advance");
        Expect(router.Sample(ControllerInputContext.Dialogue, true, 0f), 1, "Deliberate press after recovery");
        Expect(router.Sample(ControllerInputContext.None, false, 0f), 0, "Pause removes input context");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, 0f), 0, "Unpause must not invent release");
        Expect(router.Sample(ControllerInputContext.Dialogue, false, .7f), 1, "Analog dialogue press");
        Expect(router.Sample(ControllerInputContext.World, false, .5f), 0, "Partially held analog context transition");
        Expect(router.Sample(ControllerInputContext.World, false, .7f), 0, "Analog context transition still requires release");
        Expect(router.Sample(ControllerInputContext.World, false, .1f), 0, "Neutral arms new analog context");
        Expect(router.Sample(ControllerInputContext.World, false, .7f), 1, "Fresh analog world press");
        Console.WriteLine("PASS: " + _checks + " controller context/exclusive gesture assertions.");
    }
}
