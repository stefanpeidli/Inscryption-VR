using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InscryptionVR
{
    /// <summary>Mirrors the game's current subtitle; owns neither input nor an OpenVR session.</summary>
    public sealed class VrDialogue : MonoBehaviour
    {
        public const int SubtitleLayer = 30;
        private ManualLogSource _logger;
        private StereoRenderer _renderer;
        private FieldInfo _textMesh;
        private FieldInfo _continuePressed;
        private FieldInfo _pauseInstance;
        private PropertyInfo _paused, _unpausing, _pauseCursor;
        private DiskCardGame.TextDisplayer _source;
        private GameObject _panel;
        private Text _subtitle;
        private Text _hint;
        private Material _material;
        private string _observedText;
        private bool _observedVisible;
        private bool _observedInput;
        private float _visibleSince;
        private float _visibleSinceRealtime;
        private bool _pulseSent;
        private bool _failed;
        private bool _showSubtitles;

        public void Initialize(ManualLogSource logger, StereoRenderer renderer, bool showSubtitles)
        {
            _logger = logger;
            _renderer = renderer;
            _showSubtitles = showSubtitles;
            try
            {
                var bindings = new VrDialogueBindings();
                _textMesh = bindings.TextMesh;
                _continuePressed = bindings.ContinuePressed;
                _pauseInstance = bindings.PauseInstance;
                _paused = bindings.Paused;
                _unpausing = bindings.Unpausing;
                _pauseCursor = bindings.PauseCursor;
                if (_showSubtitles)
                {
                    try { CreatePanel(); }
                    catch (Exception exception) { FailVisual(exception); }
                }
                _logger.LogInfo("VR dialogue input enabled; subtitles " + (_showSubtitles ? "enabled" : "disabled") +
                    ". Right trigger press/release advances input dialogue; automatic messages keep their original timing.");
            }
            catch (Exception exception) { Fail(exception); }
        }

        public bool IsInputDialogue
        {
            get
            {
                if (_failed || _logger == null) return false;
                try { Text text; return Observe(out text) && _observedInput; }
                catch (Exception exception) { Fail(exception); return false; }
            }
        }

        public bool TryAdvance(bool deliberateRelease)
        {
            if (!deliberateRelease || _failed || _logger == null) return false;
            try
            {
                Text text;
                if (!Observe(out text) || !_observedInput || _pulseSent) return false;
                // ShowUntilInput resets continuePressed after 0.2 seconds. Respect both its scaled
                // and realtime variants; never bypass that coroutine or its message clear.
                if (Time.time - _visibleSince < 0.25f || Time.realtimeSinceStartup - _visibleSinceRealtime < 0.25f)
                    return false;
                _continuePressed.SetValue(_source, true);
                _pulseSent = true;
                _logger.LogInfo("VR trigger advanced the current dialogue message.");
                return true;
            }
            catch (Exception exception) { Fail(exception); return false; }
        }

        /// <summary>Called once immediately before both stereo eye renders.</summary>
        public void RefreshForRender()
        {
            if (_failed || _logger == null) return;
            if (!_showSubtitles) { Hide(); return; }
            try
            {
                Text text;
                Vector3 position;
                Quaternion rotation;
                float scale;
                if (!Observe(out text) || !_renderer.TryGetHeadPose(out position, out rotation, out scale))
                { Hide(); return; }
                _panel.transform.position = position + rotation * new Vector3(0f, 0.25f, 1.2f) * scale;
                _panel.transform.rotation = rotation;
                _panel.transform.localScale = Vector3.one * (0.001f * scale);
                Font font = text.font != null ? text.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
                string hint = _observedInput ? "RIGHT TRIGGER: CONTINUE" : "";
                bool dirty = !_panel.activeSelf || _subtitle.font != font || _subtitle.text != text.text ||
                    _subtitle.supportRichText != text.supportRichText || _subtitle.fontStyle != text.fontStyle || _hint.text != hint;
                _subtitle.font = font;
                _hint.font = _subtitle.font;
                _subtitle.text = text.text;
                // Preserve the localized and parsed message but use consistent high contrast in VR.
                _subtitle.supportRichText = text.supportRichText;
                _subtitle.fontStyle = text.fontStyle;
                _hint.text = hint;
                _panel.SetActive(true);
                // Manual eye rendering runs after the ordinary UI rebuild stage.
                if (dirty) Canvas.ForceUpdateCanvases();
            }
            catch (Exception exception) { FailVisual(exception); }
        }

        private bool Observe(out Text text)
        {
            text = null;
            Vector3 position; Quaternion rotation; float scale;
            if (SceneManager.GetActiveScene().name != "Part1_Cabin" || Time.timeScale <= 0f || IsPaused() ||
                _renderer == null || !_renderer.TryGetHeadPose(out position, out rotation, out scale))
            { ResetObservation(); return false; }
            if (_source == null || !_source.gameObject.activeInHierarchy)
            {
                _source = UnityEngine.Object.FindObjectOfType<DiskCardGame.TextDisplayer>();
                ResetObservation();
            }
            if (_source == null) { ResetObservation(); return false; }
            text = _textMesh.GetValue(_source) as Text;
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy || !_source.Displaying ||
                String.IsNullOrEmpty(text.text))
            { ResetObservation(); return false; }
            bool input = Convert.ToInt32(_source.CurrentAdvanceMode) == 1;
            // If an identical next line appeared between observations, its coroutine's reset
            // acknowledges the old pulse. The old waiting loop itself never clears this field.
            bool acknowledged = _pulseSent && !(bool)_continuePressed.GetValue(_source);
            if (!_observedVisible || _observedText != text.text || _observedInput != input || acknowledged)
            {
                _visibleSince = Time.time;
                _visibleSinceRealtime = Time.realtimeSinceStartup;
                _pulseSent = false;
            }
            _observedVisible = true;
            _observedInput = input;
            _observedText = text.text;
            return true;
        }

        private bool IsPaused()
        {
            object pause = _pauseInstance.GetValue(null);
            if (pause == null) return false;
            if ((bool)_paused.GetValue(pause, null) || (bool)_unpausing.GetValue(pause, null)) return true;
            Behaviour cursor = _pauseCursor.GetValue(pause, null) as Behaviour;
            return cursor != null && cursor.enabled && cursor.gameObject.activeInHierarchy;
        }

        private void CreatePanel()
        {
            _panel = new GameObject("InscryptionVR Subtitle Panel", typeof(RectTransform), typeof(Canvas));
            _panel.layer = SubtitleLayer;
            _panel.transform.SetParent(transform, false);
            var rectangle = (RectTransform)_panel.transform;
            rectangle.sizeDelta = new Vector2(1050f, 250f);
            Canvas canvas = _panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;
            Shader shader = Shader.Find("UI/Default");
            if (shader == null) throw new InvalidOperationException("Unity UI shader unavailable");
            _material = new Material(shader);
            _material.name = "InscryptionVR Subtitle Material";
            _material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            GameObject background = MakeUiObject("Background", _panel.transform);
            RectTransform backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero; backgroundRect.offsetMax = Vector2.zero;
            Image image = background.AddComponent<Image>();
            image.color = new Color(0.015f, 0.015f, 0.015f, 0.88f);
            image.raycastTarget = false;
            image.material = _material;
            _subtitle = MakeText("Message", new Vector2(0f, 16f), new Vector2(990f, 184f), 36);
            _hint = MakeText("Continue Hint", new Vector2(0f, -99f), new Vector2(990f, 35f), 22);
            _hint.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            _panel.SetActive(false);
        }

        private Text MakeText(string name, Vector2 position, Vector2 size, int fontSize)
        {
            GameObject child = MakeUiObject(name, _panel.transform);
            RectTransform rectangle = (RectTransform)child.transform;
            rectangle.anchoredPosition = position;
            rectangle.sizeDelta = size;
            Text text = child.AddComponent<Text>();
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 22;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            text.material = _material;
            return text;
        }

        private static GameObject MakeUiObject(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = SubtitleLayer;
            child.transform.SetParent(parent, false);
            return child;
        }

        private void ResetObservation()
        { _observedVisible = false; _observedInput = false; _observedText = null; _pulseSent = false; }
        private void Hide() { if (_panel != null) _panel.SetActive(false); }
        private void Fail(Exception exception)
        {
            _failed = true;
            Hide();
            if (_logger != null) _logger.LogError("VR dialogue adapter disabled: " + exception);
        }
        private void FailVisual(Exception exception)
        {
            _showSubtitles = false;
            Hide();
            if (_logger != null) _logger.LogWarning("VR subtitles disabled; dialogue input remains available: " + exception);
        }
        private void OnDisable() { Hide(); ResetObservation(); }
        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
            if (_material != null) Destroy(_material);
        }
    }

    // Shared production/test lookup path; construction performs no Unity native calls.
    internal sealed class VrDialogueBindings
    {
        private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        internal readonly FieldInfo TextMesh, ContinuePressed, PauseInstance;
        internal readonly PropertyInfo Paused, Unpausing, PauseCursor;
        internal VrDialogueBindings()
        {
            Type type = typeof(DiskCardGame.TextDisplayer);
            TextMesh = RequiredField(type, "textMesh", InstanceMembers);
            ContinuePressed = RequiredField(type, "continuePressed", InstanceMembers);
            Type pause = type.Assembly.GetType("PauseMenu", true);
            PauseInstance = RequiredField(pause, "instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Paused = RequiredProperty(pause, "Paused");
            Unpausing = RequiredProperty(pause, "Unpausing");
            PauseCursor = RequiredProperty(pause, "ActivePauseCursor");
        }
        private static FieldInfo RequiredField(Type type, string name, BindingFlags flags)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        private static PropertyInfo RequiredProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, InstanceMembers);
            if (property == null) throw new MissingMemberException(type.FullName, name);
            return property;
        }
    }
}
