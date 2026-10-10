using UnityEngine;

namespace InscryptionVR
{
    internal sealed class ControllerVisual
    {
        private readonly GameObject _root;
        private readonly LineRenderer _line;
        private readonly GameObject _marker;
        private readonly Material _material;
        private readonly Material _markerMaterial;

        internal ControllerVisual(Transform parent)
        {
            _root = new GameObject("InscryptionVR Right Controller Ray");
            _root.transform.SetParent(parent, false);
            _root.layer = 2; // Ignore Raycast. Visuals must never become pointer hit targets.
            _line = _root.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new System.InvalidOperationException("No shader available for controller ray");
            _material = new Material(shader);
            _line.sharedMaterial = _material;
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "InscryptionVR Controller Target";
            _marker.transform.SetParent(_root.transform, false);
            _marker.layer = 2;
            Collider collider = _marker.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }
            _markerMaterial = new Material(shader);
            _marker.GetComponent<Renderer>().sharedMaterial = _markerMaterial;
            SetVisible(false);
        }

        internal void Draw(Vector3 start, Vector3 end, bool interactable, float scale)
        {
            SetVisible(true);
            Color color = interactable ? new Color(0.3f, 1f, 0.45f, 0.9f) : new Color(0.3f, 0.75f, 1f, 0.6f);
            _line.startColor = color;
            _line.endColor = color;
            _line.startWidth = 0.002f * scale;
            _line.endWidth = 0.001f * scale;
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
            _marker.transform.position = end;
            _marker.transform.localScale = Vector3.one * (0.012f * scale);
            _markerMaterial.color = color;
        }

        internal void SetVisible(bool visible) { if (_root != null) _root.SetActive(visible); }

        internal void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            if (_material != null) Object.Destroy(_material);
            if (_markerMaterial != null) Object.Destroy(_markerMaterial);
        }
    }
}
