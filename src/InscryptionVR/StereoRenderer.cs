using System;
using System.ComponentModel;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InscryptionVR
{
    /// <summary>Opt-in table-scene stereo experiment. Keeps desktop cameras and input intact.</summary>
    public sealed class StereoRenderer : MonoBehaviour
    {
        private ManualLogSource _logger;
        private StereoBridge _bridge;
        private Camera _source;
        private DiskCardGame.ViewManager _tableView;
        private Camera _leftCamera;
        private Camera _rightCamera;
        private RenderTexture _leftTexture;
        private RenderTexture _rightTexture;
        private Vector3 _anchorPosition;
        private Quaternion _anchorRotation;
        private Vector3 _trackingOrigin;
        private Quaternion _trackingRotation;
        private float _worldUnitsPerMeter;
        private float _renderScale;
        private float _tableTiltDegrees;
        private bool _tableTiltActive = true;
        private VrDialogue _dialogue;
        private readonly TableViewPolicy _viewPolicy = new TableViewPolicy();
        private DiskCardGame.Part1GameFlowManager _flow;
        private bool _autoMapOverview;
        public int SeatRevision { get; private set; }
        private bool _started;
        private bool _stopped;
        private bool _centered;
        private bool _trackingLost;
        private bool _focusLost;
        private bool _acknowledgedFrame;
        private string _waitingScene;
        private readonly float[] _head = new float[12];
        private readonly float[] _leftEye = new float[12];
        private readonly float[] _rightEye = new float[12];
        private readonly float[] _leftFrustum = new float[4];
        private readonly float[] _rightFrustum = new float[4];

        public void Initialize(ManualLogSource logger, float worldUnitsPerMeter, float renderScale, float tableTiltDegrees, bool autoMapOverview)
        {
            _logger = logger;
            _worldUnitsPerMeter = Mathf.Clamp(worldUnitsPerMeter, 0.1f, 20f);
            _renderScale = Mathf.Clamp(renderScale, 0.25f, 1f);
            _tableTiltDegrees = Mathf.Clamp(tableTiltDegrees, 0f, 25f);
            _autoMapOverview = autoMapOverview;
            _logger.LogInfo("Stereo table experiment enabled. Waiting for Part1_Cabin. F8 recenters; F9 stops stereo.");
        }

        public void SetDialogue(VrDialogue dialogue) { _dialogue = dialogue; }

        public bool TryGetHeadPose(out Vector3 position, out Quaternion rotation, out float unitsPerMeter)
        {
            unitsPerMeter = _worldUnitsPerMeter;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!_started || _stopped || !_centered || _trackingLost) return false;
            Quaternion basis = _anchorRotation * Quaternion.Inverse(_trackingRotation);
            position = _anchorPosition + basis * ((StereoPose.Position(_head) - _trackingOrigin) * unitsPerMeter);
            rotation = basis * StereoPose.Rotation(_head);
            return true;
        }

        public bool TryGetTrackingSpace(out Vector3 anchorPosition, out Quaternion anchorRotation,
            out Vector3 trackingOrigin, out Quaternion trackingYaw, out float unitsPerMeter)
        {
            anchorPosition = _anchorPosition;
            anchorRotation = _anchorRotation;
            trackingOrigin = _trackingOrigin;
            trackingYaw = _trackingRotation;
            unitsPerMeter = _worldUnitsPerMeter;
            return _started && !_stopped && _centered && !_trackingLost;
        }

        private void Update()
        {
            if (_logger == null || _stopped)
                return;
            if (_started)
            {
                if (_flow == null) _flow = UnityEngine.Object.FindObjectOfType<DiskCardGame.Part1GameFlowManager>();
                bool map = _flow != null && (int)_flow.CurrentGameState == 1;
                TableViewMode? requested = Input.GetKeyDown(KeyCode.T) ? (TableViewMode?)TableViewMode.TopDown :
                    Input.GetKeyDown(KeyCode.S) ? (TableViewMode?)TableViewMode.Seated : null;
                if (_viewPolicy.Update(map, _autoMapOverview, requested))
                {
                    _centered = false;
                    _logger.LogInfo("VR viewpoint changed to " + _viewPolicy.Current + "; held inputs will be cancelled.");
                }
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                StopStereo("F9 emergency stop");
                return;
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                _tableTiltActive = !_tableTiltActive;
                _centered = false;
                _logger.LogInfo("Table viewing tilt " + (_tableTiltActive ? "enabled" : "disabled") + "; recentering anchor.");
            }
            if (!_started)
            {
                string scene = SceneManager.GetActiveScene().name;
                if (scene != "Part1_Cabin")
                {
                    if (_waitingScene != scene)
                    {
                        _waitingScene = scene;
                        _logger.LogInfo("Stereo waiting: scene '" + scene + "' has no supported table view. Use desktop menus.");
                    }
                    return;
                }
                _source = FindTableCamera();
                if (_source != null)
                {
                    try { StartStereo(); }
                    catch (Exception exception) { StopStereo("Initialization failed: " + exception.Message); }
                }
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                _centered = false;
                _logger.LogInfo("Stereo recenter requested; next valid headset pose resets the table anchor.");
            }
        }

        private void StartStereo()
        {
            if (IntPtr.Size != 4 || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new NotSupportedException("This native stereo bridge requires x86 Direct3D11.");
            _bridge = new StereoBridge();
            uint width, height;
            int result = _bridge.Start(out width, out height);
            if (result != 0)
                throw new InvalidOperationException("Native OpenVR Scene initialization error " + result + ". Start SteamVR and connect the headset first.");
            _started = true;
            if (width == 0 || height == 0 || width > 16384 || height > 16384)
                throw new InvalidOperationException("Runtime returned invalid eye texture dimensions.");
            int eyeWidth = Mathf.Clamp(Mathf.RoundToInt(width * _renderScale), 256, 2048);
            int eyeHeight = Mathf.Clamp(Mathf.RoundToInt(height * _renderScale), 256, 2048);
            _leftTexture = MakeTexture("InscryptionVR Left Eye", eyeWidth, eyeHeight);
            _rightTexture = MakeTexture("InscryptionVR Right Eye", eyeWidth, eyeHeight);
            _leftCamera = MakeCamera("InscryptionVR Left Eye Camera", _leftTexture);
            _rightCamera = MakeCamera("InscryptionVR Right Eye Camera", _rightTexture);
            _logger.LogInfo("Stereo source: culling mask=" + _source.cullingMask + ", clear flags=" + _source.clearFlags +
                ", source target=" + (_source.targetTexture == null ? "desktop" : _source.targetTexture.name) +
                "; hierarchy=" + CameraPath(_source.transform) + "; eye targets=" + _leftTexture.name + "/" + _rightTexture.name +
                ", near=" + _leftCamera.nearClipPlane + ".");
            _logger.LogInfo("Stereo Scene session started from camera '" + _source.name + "'; eyes=" + eyeWidth + "x" + eyeHeight +
                ", world units/metre=" + _worldUnitsPerMeter + ". Desktop input and menus remain necessary.");
            StartCoroutine(RenderFrames());
        }

        private RenderTexture MakeTexture(string textureName, int width, int height)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            texture.name = textureName;
            texture.antiAliasing = 1;
            texture.useMipMap = false;
            if (!texture.Create())
            {
                Destroy(texture);
                throw new InvalidOperationException("Could not allocate an eye render texture.");
            }
            return texture;
        }

        private Camera MakeCamera(string cameraName, RenderTexture texture)
        {
            var eyeObject = new GameObject(cameraName);
            eyeObject.transform.SetParent(transform, false);
            var eyeCamera = eyeObject.AddComponent<Camera>();
            eyeCamera.CopyFrom(_source);
            eyeCamera.enabled = false;
            eyeCamera.stereoTargetEye = StereoTargetEyeMask.None;
            eyeCamera.targetTexture = texture;
            eyeCamera.rect = new Rect(0f, 0f, 1f, 1f);
            eyeCamera.cullingMask |= 1 << VrDialogue.SubtitleLayer;
            eyeCamera.nearClipPlane = 0.03f * _worldUnitsPerMeter;
            // Only Camera settings are copied. PixelCamera/postprocessing scripts are deliberately absent.
            return eyeCamera;
        }

        private IEnumerator RenderFrames()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (!_stopped)
            {
                // Cabin helper cameras produce textures earlier in the frame; sample them after desktop rendering.
                yield return endOfFrame;
                RenderFrame();
            }
        }

        private void RenderFrame()
        {
            if (!_started || _stopped)
                return;
            try
            {
                if (_source == null || SceneManager.GetActiveScene().name != "Part1_Cabin")
                {
                    StopStereo("Supported table scene or source camera exited. Restart the game to start stereo again.");
                    return;
                }
                int leftError, rightError;
                uint frames;
                int status = _bridge.GetStatus(out leftError, out rightError, out frames);
                // Pinned OpenVR: DoNotHaveFocus=101 is temporary; InvalidTexture=102 is a real error.
                const int focusError = 101;
                if (status < 0 || (leftError != 0 && leftError != focusError) || (rightError != 0 && rightError != focusError))
                    throw new InvalidOperationException("Compositor submission failed: state=" + status + ", left=" + leftError + ", right=" + rightError + ".");
                bool focusLost = leftError == focusError || rightError == focusError;
                if (focusLost != _focusLost)
                {
                    _focusLost = focusLost;
                    _logger.LogInfo(focusLost ? "Stereo waiting for SteamVR focus; render events continue so it can recover."
                        : "SteamVR focus recovered.");
                }
                int valid = _bridge.GetFrame(_head, _leftEye, _rightEye, _leftFrustum, _rightFrustum,
                    _source.nearClipPlane, _source.farClipPlane);
                if (valid < 0)
                    throw new InvalidOperationException("Native pose query failed: " + valid + ".");
                if (valid == 0)
                {
                    if (!_trackingLost)
                    {
                        _trackingLost = true;
                        _logger.LogWarning("Headset tracking unavailable; eye rendering/submission paused.");
                    }
                    return;
                }
                if (!StereoPose.IsPoseValid(_head) || !StereoPose.IsPoseValid(_leftEye) || !StereoPose.IsPoseValid(_rightEye) ||
                    !StereoPose.IsFrustumValid(_leftFrustum) || !StereoPose.IsFrustumValid(_rightFrustum))
                    throw new InvalidOperationException("Runtime returned invalid pose or projection data.");
                _trackingLost = false;
                if (!_centered)
                {
                    CaptureTableSeat();
                    _trackingOrigin = StereoPose.Position(_head);
                    _trackingRotation = Quaternion.Euler(0f, StereoPose.Rotation(_head).eulerAngles.y, 0f);
                    _centered = true;
                    SeatRevision++;
                    _logger.LogInfo("Stereo table anchor captured at " + _anchorPosition.ToString("F3") +
                        "; desktop view=" + (_tableView == null ? "unknown" : _tableView.CurrentView.ToString()) +
                        ". Scripted desktop camera movement does not move the headset rig.");
                    var hand = UnityEngine.Object.FindObjectOfType<DiskCardGame.PlayerHand3D>();
                    if (hand != null) _logger.LogInfo("Hand origin=" + hand.transform.position.ToString("F3") +
                        "; anchor distance=" + Vector3.Distance(_anchorPosition, hand.transform.position).ToString("F3") + " game units.");
                }
                if (_dialogue != null) _dialogue.RefreshForRender();
                RenderEye(_leftCamera, _leftEye, _leftFrustum);
                RenderEye(_rightCamera, _rightEye, _rightFrustum);
                // Capture an immutable texture/pose pair for this particular render-thread event.
                _bridge.SetTextures(_leftTexture.GetNativeTexturePtr(), _rightTexture.GetNativeTexturePtr());
                GL.IssuePluginEvent(_bridge.RenderEvent, 1);
                if (frames > 0 && !_acknowledgedFrame)
                {
                    _acknowledgedFrame = true;
                    _logger.LogInfo("Stereo compositor has acknowledged a submitted frame; headset visual quality remains to be verified.");
                }
            }
            catch (Exception exception)
            {
                StopStereo("Stereo failed: " + exception.Message);
            }
        }

        private void RenderEye(Camera camera, float[] eye, float[] frustum)
        {
            Quaternion originInverse = Quaternion.Inverse(_trackingRotation);
            Quaternion headRotation = StereoPose.Rotation(_head);
            Vector3 headPosition = StereoPose.Position(_head);
            Vector3 localPosition = originInverse * (headPosition - _trackingOrigin + headRotation * StereoPose.Position(eye));
            camera.transform.position = _anchorPosition + _anchorRotation * (localPosition * _worldUnitsPerMeter);
            camera.transform.rotation = _anchorRotation * originInverse * headRotation * StereoPose.Rotation(eye);
            camera.cullingMask = _source.cullingMask | (1 << VrDialogue.SubtitleLayer);
            camera.projectionMatrix = StereoPose.Projection(frustum, camera.nearClipPlane, camera.farClipPlane);
            camera.Render();
        }

        private void CaptureTableSeat()
        {
            if (_tableView == null || _tableView.CameraParent == null)
                throw new InvalidOperationException("Table view camera parent unavailable for seated anchor.");
            Transform cameraParent = _tableView.CameraParent;
            DiskCardGame.View seatView = _viewPolicy.Current == TableViewMode.TopDown ? DiskCardGame.View.MapArial :
                _viewPolicy.Current == TableViewMode.MapOverview ? DiskCardGame.View.MapDefault : DiskCardGame.View.Default;
            DiskCardGame.ViewInfo seat = DiskCardGame.ViewManager.GetViewInfo(seatView);
            Vector3 cameraOffset = cameraParent.InverseTransformPoint(_source.transform.position);
            Vector3 localPosition = seat.camPosition + Quaternion.Euler(seat.camRotation) *
                Vector3.Scale(cameraOffset, cameraParent.localScale);
            _anchorPosition = cameraParent.parent == null ? localPosition : cameraParent.parent.TransformPoint(localPosition);
            float yaw = seat.camRotation.y + (cameraParent.parent == null ? 0f : cameraParent.parent.eulerAngles.y);
            // A small fixed downward view angle brings the tabletop up in the user's field of view.
            // Controllers use this same basis, so rays stay aligned without moving gameplay objects.
            float pitch = _viewPolicy.Current == TableViewMode.Seated ? (_tableTiltActive ? _tableTiltDegrees : 0f) : seat.camRotation.x;
            _anchorRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private Camera FindTableCamera()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type viewType = assembly.GetType("DiskCardGame.ViewManager", false);
                if (viewType == null)
                    continue;
                FieldInfo field = viewType.GetField("pixelCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    break;
                foreach (UnityEngine.Object view in UnityEngine.Object.FindObjectsOfType(viewType))
                {
                    Camera candidate = field.GetValue(view) as Camera;
                    if (candidate != null && !candidate.orthographic && candidate.gameObject.activeInHierarchy)
                    {
                        _tableView = view as DiskCardGame.ViewManager;
                        return candidate;
                    }
                }
                break;
            }
            return null;
        }

        private static string CameraPath(Transform cameraTransform)
        {
            string path = cameraTransform.name;
            while (cameraTransform.parent != null)
            {
                cameraTransform = cameraTransform.parent;
                path = cameraTransform.name + "/" + path;
            }
            return path;
        }

        public void StopStereo(string reason)
        {
            if (_stopped)
                return;
            _stopped = true;
            if (_logger != null)
                _logger.LogWarning("Stereo stopped: " + reason);
            if (_leftCamera != null) Destroy(_leftCamera.gameObject);
            if (_rightCamera != null) Destroy(_rightCamera.gameObject);
            if (_bridge != null && _started)
            {
                _bridge.RequestShutdown();
                GL.IssuePluginEvent(_bridge.RenderEvent, 2);
                var cleanupObject = new GameObject("InscryptionVR Stereo Cleanup");
                DontDestroyOnLoad(cleanupObject);
                cleanupObject.AddComponent<StereoCleanup>().Initialize(_bridge, _leftTexture, _rightTexture, _logger);
            }
            else
            {
                ReleaseTexture(_leftTexture);
                ReleaseTexture(_rightTexture);
            }
        }

        private void OnDestroy() { StopStereo("Renderer destroyed"); }

        internal static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }
    }

    internal sealed class StereoCleanup : MonoBehaviour
    {
        private StereoBridge _bridge;
        private RenderTexture _left;
        private RenderTexture _right;
        private ManualLogSource _logger;
        internal void Initialize(StereoBridge bridge, RenderTexture left, RenderTexture right, ManualLogSource logger)
        { _bridge = bridge; _left = left; _right = right; _logger = logger; }
        private void Update()
        {
            int leftError, rightError;
            uint frames;
            if (_bridge == null || _bridge.GetStatus(out leftError, out rightError, out frames) != 2) return;
            StereoRenderer.ReleaseTexture(_left);
            StereoRenderer.ReleaseTexture(_right);
            if (_logger != null) _logger.LogInfo("Render-thread stereo shutdown acknowledged; eye textures released.");
            Destroy(gameObject);
        }
    }

    internal sealed class StereoBridge
    {
        // Remains loaded until process exit: Unity may still hold queued render callback pointers.
        private static IntPtr _library;
        private readonly StartDelegate _start;
        private readonly ShutdownDelegate _shutdown;
        private readonly FrameDelegate _frame;
        private readonly TexturesDelegate _textures;
        private readonly StatusDelegate _status;
        internal readonly IntPtr RenderEvent;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StartDelegate([MarshalAs(UnmanagedType.LPWStr)] string path, out uint width, out uint height);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ShutdownDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FrameDelegate([Out] float[] head, [Out] float[] leftEye, [Out] float[] rightEye, [Out] float[] leftFrustum, [Out] float[] rightFrustum, float near, float far);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void TexturesDelegate(IntPtr left, IntPtr right);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr RenderEventDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StatusDelegate(out int left, out int right, out uint frames);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] private static extern IntPtr LoadLibraryExW(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        private readonly string _directory;
        internal StereoBridge()
        {
            _directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (_library == IntPtr.Zero)
            {
                string path = Path.GetFullPath(Path.Combine(_directory, "InscryptionVR.Native.dll"));
                if (!File.Exists(path)) throw new FileNotFoundException("Native stereo bridge missing", path);
                _library = LoadLibraryExW(path, IntPtr.Zero, 0x100 | 0x1000);
                if (_library == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Native stereo bridge load failed");
            }
            _start = Export<StartDelegate>("ivr_start");
            _shutdown = Export<ShutdownDelegate>("ivr_shutdown");
            _frame = Export<FrameDelegate>("ivr_get_frame");
            _textures = Export<TexturesDelegate>("ivr_set_textures");
            _status = Export<StatusDelegate>("ivr_get_submit_status");
            RenderEvent = Export<RenderEventDelegate>("ivr_get_render_event_func")();
            if (RenderEvent == IntPtr.Zero) throw new InvalidOperationException("Native bridge returned a null render callback");
        }
        internal int Start(out uint width, out uint height) { return _start(Path.GetFullPath(Path.Combine(_directory, "openvr_api.dll")), out width, out height); }
        internal int GetFrame(float[] head, float[] leftEye, float[] rightEye, float[] leftFrustum, float[] rightFrustum, float near, float far)
        { return _frame(head, leftEye, rightEye, leftFrustum, rightFrustum, near, far); }
        internal void SetTextures(IntPtr left, IntPtr right) { _textures(left, right); }
        internal int GetStatus(out int left, out int right, out uint frames) { return _status(out left, out right, out frames); }
        internal void RequestShutdown() { _shutdown(); }
        private static T Export<T>(string name) where T : class
        {
            IntPtr address = GetProcAddress(_library, name);
            if (address == IntPtr.Zero) throw new EntryPointNotFoundException("Native bridge lacks " + name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }
    }
}
