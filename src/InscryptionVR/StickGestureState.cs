using System;

namespace InscryptionVR
{
    internal enum StickGesture { None, Up, Down, Left, Right }

    // One step per deliberate deflection; activation/recovery always requires neutral first.
    internal sealed class StickGestureState
    {
        private bool _armed;
        internal StickGesture Sample(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
            { Cancel(); return StickGesture.None; }
            float horizontal = Math.Abs(x), vertical = Math.Abs(y);
            float magnitude = Math.Max(horizontal, vertical);
            if (magnitude <= 0.25f) { _armed = true; return StickGesture.None; }
            if (!_armed || magnitude < 0.65f) return StickGesture.None;
            _armed = false;
            // Vertical wins exact diagonal ties, matching ordinary view-list navigation.
            if (vertical >= horizontal) return y > 0f ? StickGesture.Up : StickGesture.Down;
            return x > 0f ? StickGesture.Right : StickGesture.Left;
        }
        internal void Cancel() { _armed = false; }
    }
}
