namespace InscryptionVR
{
    internal enum ControllerInputContext { None, World, Dialogue }

    // One gesture belongs to one context. Context changes always require a neutral trigger.
    internal sealed class ControllerInputRouter
    {
        private readonly ControllerTriggerState _trigger = new ControllerTriggerState();
        internal ControllerInputContext Context { get; private set; }

        internal int Sample(ControllerInputContext context, bool digital, float analog)
        {
            if (context == ControllerInputContext.None) { Cancel(); return 0; }
            if (context != Context)
            {
                _trigger.Cancel();
                Context = context;
            }
            return _trigger.Sample(_trigger.IsPressed(digital, analog));
        }

        internal void Cancel()
        {
            Context = ControllerInputContext.None;
            _trigger.Cancel();
        }
    }
}
