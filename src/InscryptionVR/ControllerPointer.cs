using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

namespace InscryptionVR
{
    /// <summary>Right-controller ray for audited cabin table views. No OpenVR session ownership.</summary>
    public sealed class ControllerPointer : MonoBehaviour
    {
        private ManualLogSource _logger;
        private StereoRenderer _renderer;
        private VrDialogue _dialogue;
        private ControllerVisual _visual;
        private GameCursorAdapter _game;
        private bool _failed;
        private bool _userEnabled = true;
        private bool _ownsCursor;
        private bool _originalCursorEnabled;
        private readonly ControllerInputRouter _inputRouter = new ControllerInputRouter();
        private int _suspendedView = -1;
        private uint _controllerIndex = uint.MaxValue;
        private int _triggerAxis = -1;
        private Component _hover;
        private Component _down;
        private readonly float[] _controllerMatrix = new float[12];
        private string _lastStatus;
        private int _seatRevision = -1;

        public void Initialize(ManualLogSource logger, StereoRenderer renderer, VrDialogue dialogue)
        {
            _logger = logger;
            _renderer = renderer;
            _dialogue = dialogue;
            try
            {
                _game = new GameCursorAdapter();
                _visual = new ControllerVisual(transform);
                _logger.LogInfo("Right-controller input enabled. Trigger selects world targets or advances visible dialogue. F10 toggles controller input.");
            }
            catch (Exception exception) { Fail(exception); }
        }

        private void Update()
        {
            if (_logger == null || _failed) return;
            try
            {
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    _userEnabled = !_userEnabled;
                    Relinquish();
                    _logger.LogInfo("Controller pointer " + (_userEnabled ? "enabled" : "disabled; desktop cursor restored") + ".");
                }
                if (!_userEnabled) { Status("disabled by F10"); return; }
                if (Input.GetKeyDown(KeyCode.F7) || Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.F9) ||
                    Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.S))
                {
                    Relinquish();
                    Status("VR keyboard tilt/recenter/stop requested; current press cancelled");
                    return;
                }
                if (_renderer != null && _seatRevision != _renderer.SeatRevision)
                {
                    Relinquish();
                    _seatRevision = _renderer.SeatRevision;
                }
                Vector3 anchor, origin;
                Quaternion anchorYaw, trackingYaw;
                float scale;
                if (_renderer == null || !_renderer.TryGetTrackingSpace(out anchor, out anchorYaw, out origin, out trackingYaw, out scale))
                {
                    Relinquish();
                    Status("waiting for VR tracking and recenter anchor");
                    return;
                }
                if (SceneManager.GetActiveScene().name != "Part1_Cabin" || Time.timeScale == 0f || !_game.Resolve() || _game.IsPaused)
                {
                    Relinquish();
                    Status("desktop input: unsupported scene, pause, transition or gameplay phase");
                    return;
                }
                bool dialogueActive = _dialogue != null && _dialogue.IsInputDialogue;
                int view = _game.CurrentView;
                if (!dialogueActive && (!_game.IsPlayablePhase || (!_game.IsSpecialEvent && !IsSupportedView(view))))
                { Relinquish(); Status("desktop input: unsupported gameplay phase or view " + view); return; }
                if (!dialogueActive && _suspendedView >= 0)
                {
                    if (_suspendedView == view) { Relinquish(); Status("desktop cursor requested by game in view " + view); return; }
                    _suspendedView = -1;
                }
                if (!dialogueActive && _ownsCursor && _game.Cursor.enabled)
                {
                    _suspendedView = view;
                    Relinquish();
                    _logger.LogInfo("Game re-enabled its desktop cursor; controller takeover suspended until the table view changes.");
                    return;
                }
                CVRSystem system = OpenVR.System;
                if (system == null || !system.IsInputAvailable()) { Relinquish(); Status("SteamVR input unavailable or overlay owns input"); return; }
                uint index = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
                if (index == OpenVR.k_unTrackedDeviceIndexInvalid) { Relinquish(); Status("waiting for a right-hand controller"); return; }
                var state = new VRControllerState_t();
                var pose = new TrackedDevicePose_t();
                if (!system.GetControllerStateWithPose(ETrackingUniverseOrigin.TrackingUniverseSeated, index, ref state,
                        (uint)Marshal.SizeOf(typeof(VRControllerState_t)), ref pose) || !pose.bDeviceIsConnected || !pose.bPoseIsValid)
                { Relinquish(); Status("right-controller state or tracking unavailable"); return; }
                if (_controllerIndex != index)
                {
                    Relinquish();
                    _controllerIndex = index;
                    _triggerAxis = FindTriggerAxis(system, index);
                    _logger.LogInfo("Right controller detected: device " + index + ", trigger axis " + _triggerAxis +
                        ", state size " + Marshal.SizeOf(typeof(VRControllerState_t)) + " bytes.");
                }
                CopyMatrix(pose.mDeviceToAbsoluteTracking, _controllerMatrix);
                if (!StereoPose.IsPoseValid(_controllerMatrix)) { Relinquish(); Status("invalid right-controller pose"); return; }
                if (dialogueActive)
                {
                    Relinquish(false);
                    Status("dialogue: release right trigger to advance");
                    int dialogueEdge = SampleTrigger(state, ControllerInputContext.Dialogue);
                    if (dialogueEdge == -1 && _dialogue.TryAdvance(true))
                        _logger.LogInfo("Controller dialogue advance accepted.");
                    return; // This gesture must never reach card/bell callbacks.
                }
                if (!_ownsCursor)
                {
                    if (!_game.Cursor.enabled || _game.Down != null || Input.GetMouseButton(0))
                    { _visual.SetVisible(false); Status("waiting for desktop cursor to finish its interaction"); return; }
                    // A held trigger when tracking resumes must first be released before a new click.
                    _inputRouter.Cancel();
                    _originalCursorEnabled = _game.Cursor.enabled;
                    _game.Cursor.enabled = false;
                    _ownsCursor = true;
                    Component desktopHover = _game.Current;
                    if (desktopHover != null) _game.Dispatch(desktopHover, "CursorExit");
                    _game.Current = null;
                }
                if (_hover != null && _game.Current != _hover)
                    _hover = null; // A script already exited/cleared the cursor during an interaction lock.
                if (_down != null && _game.Down != _down) _down = null;
                Status("active in view " + view);

                Quaternion basis = anchorYaw * Quaternion.Inverse(trackingYaw);
                Vector3 position = anchor + basis * ((StereoPose.Position(_controllerMatrix) - origin) * scale);
                Vector3 direction = basis * StereoPose.Rotation(_controllerMatrix) * Vector3.forward;
                RaycastHit hit;
                bool hitSomething = Physics.Raycast(position, direction, out hit, 10f * scale, _game.RaycastMask, QueryTriggerInteraction.UseGlobal);
                Component target = hitSomething ? hit.transform.GetComponent(_game.InteractableType) : null;
                if (target != null && !_game.CanDispatch(target)) target = null;
                ChangeHover(target);
                if (_hover != null) _game.Dispatch(_hover, "CursorStay");
                _visual.Draw(position, hitSomething ? hit.point : position + direction * (2f * scale), _hover != null, scale);

                int edge = SampleTrigger(state, ControllerInputContext.World);
                if (edge == 1 && _hover != null)
                {
                    _down = _hover;
                    _game.Down = _down;
                    _game.Dispatch(_hover, "CursorSelectStart");
                    _logger.LogInfo("Controller select start: " + TargetLabel(_hover));
                }
                if (edge == -1)
                {
                    // The game's release is routed to the hovered object, even after a drag cancellation.
                    if (_hover != null)
                    {
                        _game.Dispatch(_hover, "CursorSelectEnd");
                        _logger.LogInfo("Controller select end: " + TargetLabel(_hover));
                    }
                    if (_game.Down == _down) _game.Down = null;
                    _down = null;
                }
                // Match UpdateDragInput: run after main press/release, cancel once, and clear down state.
                if (_down != null && _down != _hover)
                {
                    _game.Dispatch(_down, "CursorDragOff");
                    if (_game.Down == _down) _game.Down = null;
                    _down = null;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private void ChangeHover(Component target)
        {
            if (_hover == target) return;
            if (_hover != null)
            {
                _game.Dispatch(_hover, "CursorExit");
            }
            _hover = target;
            _game.Current = target;
            if (_hover != null) _game.Dispatch(_hover, "CursorEnter");
        }

        private int SampleTrigger(VRControllerState_t state, ControllerInputContext context)
        {
            bool digital = (state.ulButtonPressed & (1UL << (int)EVRButtonId.k_EButton_SteamVR_Trigger)) != 0;
            float analog = 0f;
            switch (_triggerAxis)
            {
                case 0: analog = state.rAxis0.x; break;
                case 1: analog = state.rAxis1.x; break;
                case 2: analog = state.rAxis2.x; break;
                case 3: analog = state.rAxis3.x; break;
                case 4: analog = state.rAxis4.x; break;
            }
            return _inputRouter.Sample(context, digital, analog);
        }

        private static int FindTriggerAxis(CVRSystem system, uint index)
        {
            for (int axis = 0; axis < 5; axis++)
            {
                ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
                int type = system.GetInt32TrackedDeviceProperty(index, (ETrackedDeviceProperty)(3002 + axis), ref error);
                if (error == ETrackedPropertyError.TrackedProp_Success && type == 3) return axis;
            }
            return -1;
        }

        private static void CopyMatrix(HmdMatrix34_t matrix, float[] values)
        {
            values[0] = matrix.m0; values[1] = matrix.m1; values[2] = matrix.m2; values[3] = matrix.m3;
            values[4] = matrix.m4; values[5] = matrix.m5; values[6] = matrix.m6; values[7] = matrix.m7;
            values[8] = matrix.m8; values[9] = matrix.m9; values[10] = matrix.m10; values[11] = matrix.m11;
        }

        private static bool IsSupportedView(int view)
        {
            switch (view)
            {
                case 0: case 1: case 4: case 5: case 6: case 7: case 11: case 12: case 13:
                case 32: case 34: case 37: return true;
                default: return false;
            }
        }

        private void Relinquish(bool cancelInput = true)
        {
            if (cancelInput) _inputRouter.Cancel();
            if (_visual != null) _visual.SetVisible(false);
            if (!_ownsCursor) return;
            try
            {
                if (_down != null) _game.Dispatch(_down, "CursorDragOff");
                if (_hover != null && _game.Current == _hover) _game.Dispatch(_hover, "CursorExit");
            }
            catch (Exception exception)
            {
                if (_logger != null) _logger.LogWarning("Controller cursor cleanup callback failed: " + exception.Message);
            }
            finally
            {
                if (_game.Current == _hover) _game.Current = null;
                if (_game.Down == _down) _game.Down = null;
                if (_game.Cursor != null) _game.Cursor.enabled = _originalCursorEnabled;
                _ownsCursor = false;
                _hover = null;
                _down = null;
            }
        }

        private void Fail(Exception exception)
        {
            _failed = true;
            try { Relinquish(); } catch { /* Always restore the cursor even if a game callback throws. */
                if (_game != null && _game.Cursor != null && _ownsCursor) _game.Cursor.enabled = _originalCursorEnabled;
                _ownsCursor = false;
            }
            if (_logger != null) _logger.LogWarning("Controller pointer stopped: " + exception);
        }

        private void Status(string status)
        {
            if (_lastStatus == status) return;
            _lastStatus = status;
            _logger.LogInfo("Controller pointer: " + status + ".");
        }

        private static string TargetLabel(Component target)
        {
            return target == null ? "destroyed target" : target.GetType().Name + " (" + target.name + ")";
        }

        private void OnDisable() { if (_game != null) Relinquish(); }
        private void OnDestroy()
        {
            if (_game != null) Relinquish();
            if (_visual != null) _visual.Destroy();
        }
    }

    internal sealed class GameCursorAdapter
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Type _cursorType;
        private readonly Type _viewType;
        private readonly Type _pauseType;
        internal readonly Type InteractableType;
        private readonly FieldInfo _current;
        private readonly FieldInfo _down;
        private readonly FieldInfo _excluded;
        private readonly MethodInfo _disabledWith;
        private readonly PropertyInfo _enabled;
        private readonly PropertyInfo _currentView;
        private readonly FieldInfo _pauseInstance;
        private readonly PropertyInfo _paused;
        private readonly PropertyInfo _unpausing;
        private readonly PropertyInfo _activePauseCursor;
        private readonly Type _flowType;
        private readonly Type _turnType;
        private readonly Type _mapType;
        private readonly Type _mapNodesType;
        private readonly PropertyInfo _gameState;
        private readonly PropertyInfo _transitioning;
        private readonly PropertyInfo _turnMainOrDraw;
        private readonly PropertyInfo _turnSetup;
        private readonly PropertyInfo _turnEnding;
        private readonly PropertyInfo _turnEnded;
        private readonly PropertyInfo _mapUnrolled;
        private readonly PropertyInfo _mapMoving;
        private readonly System.Collections.Generic.Dictionary<string, MethodInfo> _callbacks =
            new System.Collections.Generic.Dictionary<string, MethodInfo>();
        private UnityEngine.Object _view;
        private UnityEngine.Object _flow;
        private UnityEngine.Object _turn;
        private UnityEngine.Object _map;
        private UnityEngine.Object _mapNodes;
        private readonly SpecialEventInput _events = new SpecialEventInput();
        internal Behaviour Cursor;

        internal GameCursorAdapter()
        {
            Assembly assembly = null;
            foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
                if (candidate.GetType("DiskCardGame.InteractionCursor", false) != null) { assembly = candidate; break; }
            if (assembly == null) throw new InvalidOperationException("Inscryption interaction assembly not loaded");
            _cursorType = assembly.GetType("DiskCardGame.InteractionCursor", true);
            _viewType = assembly.GetType("DiskCardGame.ViewManager", true);
            _pauseType = assembly.GetType("PauseMenu", true);
            InteractableType = assembly.GetType("DiskCardGame.MainInputInteractable", true);
            _current = RequiredField(_cursorType, "currentMainInputInteractable");
            _down = RequiredField(_cursorType, "cursorDownInteractable");
            _excluded = RequiredField(_cursorType, "excludedLayers");
            _disabledWith = _cursorType.GetMethod("InteractionDisabledWith", Members);
            _enabled = InteractableType.GetProperty("Enabled", Members);
            _currentView = _viewType.GetProperty("CurrentView", Members);
            _pauseInstance = RequiredField(_pauseType, "instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _paused = _pauseType.GetProperty("Paused", Members);
            _unpausing = _pauseType.GetProperty("Unpausing", Members);
            _activePauseCursor = _pauseType.GetProperty("ActivePauseCursor", Members);
            _flowType = assembly.GetType("DiskCardGame.Part1GameFlowManager", true);
            _turnType = assembly.GetType("DiskCardGame.TurnManager", true);
            _mapType = assembly.GetType("DiskCardGame.GameMap", true);
            _mapNodesType = assembly.GetType("DiskCardGame.MapNodeManager", true);
            _gameState = RequiredProperty(_flowType, "CurrentGameState");
            _transitioning = RequiredProperty(_flowType, "Transitioning");
            _turnMainOrDraw = RequiredProperty(_turnType, "IsMainOrDrawPhaseNotUpkeep");
            _turnSetup = RequiredProperty(_turnType, "IsSetupPhase");
            _turnEnding = RequiredProperty(_turnType, "GameEnding");
            _turnEnded = RequiredProperty(_turnType, "GameEnded");
            _mapUnrolled = RequiredProperty(_mapType, "FullyUnrolled");
            _mapMoving = RequiredProperty(_mapNodesType, "MovingNodes");
            if (_disabledWith == null || _enabled == null || _currentView == null || _pauseInstance == null ||
                _paused == null || _unpausing == null || _activePauseCursor == null)
                throw new MissingMemberException("Inscryption cursor/view/pause contract changed");
            foreach (string name in new[] { "CursorEnter", "CursorStay", "CursorExit", "CursorSelectStart", "CursorSelectEnd", "CursorDragOff" })
            {
                MethodInfo callback = InteractableType.GetMethod(name, Members, null, Type.EmptyTypes, null);
                if (callback == null) throw new MissingMethodException(InteractableType.FullName, name);
                _callbacks.Add(name, callback);
            }
        }

        internal bool Resolve()
        {
            if (Cursor == null) Cursor = UnityEngine.Object.FindObjectOfType(_cursorType) as Behaviour;
            if (_view == null) _view = UnityEngine.Object.FindObjectOfType(_viewType);
            return Cursor != null && Cursor.gameObject.activeInHierarchy && _view != null;
        }

        internal int CurrentView { get { return Convert.ToInt32(_currentView.GetValue(_view, null)); } }
        internal bool IsPlayablePhase
        {
            get
            {
                UnityEngine.Object flow = ResolveManager(ref _flow, _flowType);
                if (flow == null || (bool)_transitioning.GetValue(flow, null)) return false;
                int state = Convert.ToInt32(_gameState.GetValue(flow, null));
                if (state == 0)
                {
                    UnityEngine.Object turn = ResolveManager(ref _turn, _turnType);
                    return turn != null && (bool)_turnMainOrDraw.GetValue(turn, null) &&
                        !(bool)_turnSetup.GetValue(turn, null) && !(bool)_turnEnding.GetValue(turn, null) &&
                        !(bool)_turnEnded.GetValue(turn, null);
                }
                if (state == 1)
                {
                    UnityEngine.Object map = ResolveManager(ref _map, _mapType);
                    UnityEngine.Object nodes = ResolveManager(ref _mapNodes, _mapNodesType);
                    return map != null && nodes != null && (bool)_mapUnrolled.GetValue(map, null) && !(bool)_mapMoving.GetValue(nodes, null);
                }
                if (state == 3) return _events.IsAvailable(CurrentView);
                return false;
            }
        }
        internal bool IsSpecialEvent
        {
            get
            {
                UnityEngine.Object flow = ResolveManager(ref _flow, _flowType);
                return flow != null && Convert.ToInt32(_gameState.GetValue(flow, null)) == 3;
            }
        }
        internal Component Current { get { return Cursor == null ? null : _current.GetValue(Cursor) as Component; } set { if (Cursor != null) _current.SetValue(Cursor, value); } }
        internal Component Down { get { return Cursor == null ? null : _down.GetValue(Cursor) as Component; } set { if (Cursor != null) _down.SetValue(Cursor, value); } }
        internal int RaycastMask
        {
            get
            {
                var layers = _excluded.GetValue(Cursor) as System.Collections.Generic.List<string>;
                int mask = layers == null ? Physics.DefaultRaycastLayers : ~LayerMask.GetMask(layers.ToArray());
                return mask & ~(1 << 2);
            }
        }
        internal bool IsPaused
        {
            get
            {
                object pause = _pauseInstance.GetValue(null);
                if (pause == null) return false;
                if ((bool)_paused.GetValue(pause, null) || (bool)_unpausing.GetValue(pause, null)) return true;
                var pauseCursor = _activePauseCursor.GetValue(pause, null) as Behaviour;
                return pauseCursor != null && pauseCursor.enabled && pauseCursor.gameObject.activeInHierarchy;
            }
        }
        internal bool CanDispatch(Component target)
        {
            return target != null && Cursor != null && target.gameObject.activeInHierarchy &&
                (bool)_enabled.GetValue(target, null) && !(bool)_disabledWith.Invoke(Cursor, new object[] { target }) &&
                (!IsSpecialEvent || _events.AllowsTarget(target, CurrentView));
        }
        internal void Dispatch(Component target, string method)
        {
            if (!CanDispatch(target)) return;
            _callbacks[method].Invoke(target, null);
        }
        private static FieldInfo RequiredField(Type type, string name, BindingFlags flags = Members)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        private static PropertyInfo RequiredProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, Members);
            if (property == null) throw new MissingMemberException(type.FullName, name);
            return property;
        }

        private static UnityEngine.Object ResolveManager(ref UnityEngine.Object cached, Type type)
        {
            Component component = cached as Component;
            if (cached == null || (component != null && !component.gameObject.activeInHierarchy))
                cached = UnityEngine.Object.FindObjectOfType(type);
            return cached;
        }
    }
}
