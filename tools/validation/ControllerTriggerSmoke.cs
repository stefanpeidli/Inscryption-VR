using System;

namespace InscryptionVR
{
    internal static class ControllerTriggerSmoke
    {
        private static int _checks;
        private static void Check(bool result, string label)
        {
            _checks++;
            if (!result) throw new Exception(label);
        }
        private static void Main()
        {
            var trigger = new ControllerTriggerState();
            Check(trigger.Sample(false) == 0, "Idle activation must not release");
            Check(trigger.Sample(true) == 1, "First deliberate press");
            Check(trigger.Sample(true) == 0, "Held trigger must not repeat selection");
            Check(trigger.Held, "Held state");
            Check(trigger.Sample(false) == -1, "Single release");
            Check(trigger.Sample(false) == 0, "Release must not repeat");
            trigger.Cancel();
            Check(trigger.Sample(true) == 0, "Held trigger during recovery must not select");
            Check(trigger.Sample(true) == 0, "Recovery hold must remain blocked");
            Check(trigger.Sample(false) == 0, "Unarmed release must not dispatch selection end");
            Check(trigger.Sample(true) == 1, "Press after recovery release");
            trigger.Cancel();
            Check(!trigger.Held, "Cancellation clears held state");
            Check(trigger.Sample(false) == 0, "Idle recovery must not invent release");
            Check(trigger.Sample(true) == 1, "New press after idle recovery");
            Check(trigger.Sample(false) == -1, "New deliberate release");
            trigger.Cancel();
            Check(trigger.Sample(trigger.IsPressed(false, 0.5f)) == 0, "Partially held trigger on recovery must stay unarmed");
            Check(trigger.Sample(trigger.IsPressed(false, 0.7f)) == 0, "Squeezing further without releasing must not select");
            Check(trigger.Sample(trigger.IsPressed(false, 0.4f)) == 0, "Recovery hysteresis must not falsely arm");
            Check(trigger.Sample(trigger.IsPressed(false, 0.1f)) == 0, "Full recovery release only arms");
            Check(trigger.Sample(trigger.IsPressed(false, 0.5f)) == 0, "Armed analog press must cross high threshold");
            Check(trigger.Sample(trigger.IsPressed(false, 0.7f)) == 1, "Intentional analog squeeze selects once");
            Check(trigger.Sample(trigger.IsPressed(false, 0.4f)) == 0, "Normal hold below press threshold stays held");
            Check(trigger.Sample(trigger.IsPressed(false, 0.1f)) == -1, "Analog release crosses low threshold once");
            Check(!trigger.IsPressed(false, float.NaN), "Invalid analog sample cannot select");
            Check(!trigger.IsPressed(false, float.PositiveInfinity), "Infinite analog sample cannot select");
            Check(trigger.IsPressed(true, 0f), "Digital trigger fallback remains available");
            Console.WriteLine("PASS: " + _checks + " controller trigger assertions.");
        }
    }
}
