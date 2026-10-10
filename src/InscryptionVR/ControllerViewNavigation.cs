using System;
using System.Collections;
using System.Reflection;
using DiskCardGame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InscryptionVR
{
    // Staged feature: no component, input subscription, or game lookup of its own.
    // Caller binds live scene objects and supplies readiness on EVERY sample.
    internal sealed class ControllerViewNavigation
    {
        private readonly StickGestureState _stick = new StickGestureState();
        private readonly FieldInfo _allowedViews;
        private readonly FieldInfo _alternateTransitions;
        private readonly FieldInfo _from;
        private readonly FieldInfo _to;
        private readonly FieldInfo _input;
        private ViewManager _manager;
        private RuleBookController _rulebook;

        internal ControllerViewNavigation()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type controller = typeof(ViewController);
            _allowedViews = RequireField(controller, "allowedViews", flags);
            _alternateTransitions = RequireField(controller, "altTransitionInputs", flags);
            Type transition = controller.GetNestedType("ViewTransitionInput", BindingFlags.Public | BindingFlags.NonPublic);
            if (transition == null) throw new MissingMemberException(controller.FullName, "ViewTransitionInput");
            _from = RequireField(transition, "from", flags);
            _to = RequireField(transition, "to", flags);
            _input = RequireField(transition, "input", flags);
        }

        internal void Bind(ViewManager manager, RuleBookController rulebook)
        {
            _manager = manager;
            _rulebook = rulebook;
            Cancel();
        }

        // callerReady includes tracking, user toggle, pause, playable phase, transitions,
        // and dialogue/selection ownership. A false sample discards a held gesture.
        internal bool TryNavigate(Vector2 axis, bool callerReady)
        {
            if (!callerReady || SceneManager.GetActiveScene().name != "Part1_Cabin" || Time.timeScale <= 0f ||
                _manager == null || !_manager.isActiveAndEnabled || _manager.Controller == null ||
                !_manager.Controller.isActiveAndEnabled || _manager.Controller.LockState == ViewLockState.Locked)
            { Cancel(); return false; }

            StickGesture gesture = _stick.Sample(axis.x, axis.y);
            if (gesture == StickGesture.None) return false;
            ViewController controller = _manager.Controller;
            IList allowed = _allowedViews.GetValue(controller) as IList;
            IList alternate = _alternateTransitions.GetValue(controller) as IList;
            if (allowed == null || alternate == null) throw new InvalidOperationException("Missing live view navigation lists.");
            View current = _manager.CurrentView, target = current;
            Button button = ToButton(gesture);
            int index = allowed.IndexOf(current);
            if (index >= 0)
            {
                if (gesture == StickGesture.Down) index = Math.Max(0, index - 1);
                if (gesture == StickGesture.Up)
                {
                    if (_rulebook != null && _rulebook.Shown)
                    { _rulebook.SetShown(false, true); return true; }
                    index = Math.Min(allowed.Count - 1, index + 1);
                }
                target = (View)allowed[index];
            }
            // Original code only evaluates alternate entries when a rulebook exists and is closed.
            if (_rulebook != null && !_rulebook.Shown)
            {
                foreach (object entry in alternate)
                {
                    if (entry != null && (View)_from.GetValue(entry) == current && (Button)_input.GetValue(entry) == button)
                        target = (View)_to.GetValue(entry);
                }
            }
            if (target == current) return false;
            _manager.SwitchToView(target, false, false);
            return true;
        }

        internal void Cancel() { _stick.Cancel(); }

        private static Button ToButton(StickGesture gesture)
        {
            switch (gesture)
            {
                case StickGesture.Up: return Button.LookUp;
                case StickGesture.Down: return Button.LookDown;
                case StickGesture.Left: return Button.LookLeft;
                case StickGesture.Right: return Button.LookRight;
                default: return Button.None;
            }
        }

        private static FieldInfo RequireField(Type type, string name, BindingFlags flags)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return field;
        }
    }
}
