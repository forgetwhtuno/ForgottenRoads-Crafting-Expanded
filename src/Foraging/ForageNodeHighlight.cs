using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Mod-owned footprint contour. It uses one cached material shared only by these per-node
    // LineRenderers; vegetation materials are never mutated and no material is allocated per frame.
    internal sealed class ForageNodeHighlightView : MonoBehaviour
    {
        private LineRenderer _outer;
        private LineRenderer _inner;
        private bool _available = true;
        private bool _hovered;
        private bool _selected;
        private bool _ready;

        internal void Initialize(LineRenderer outer, LineRenderer inner)
        {
            _outer = outer;
            _inner = inner;
            ApplyState();
        }

        internal void SetAvailable(bool available)
        {
            if (_available == available) return;
            _available = available;
            ApplyState();
        }

        internal void SetInteractionState(bool hovered, bool selected, bool ready)
        {
            if (_hovered == hovered && _selected == selected && _ready == ready) return;
            _hovered = hovered;
            _selected = selected;
            _ready = ready;
            ApplyState();
        }

        private void ApplyState()
        {
            bool showSelected = _available && _selected;
            bool showHover = _available && !_selected && _hovered;
            if (_outer != null) _outer.enabled = showSelected;
            if (_inner != null) _inner.enabled = showSelected || showHover;
            if (!_available) return;

            float mult = ForageHighlightPolicy.WidthMultiplier(_hovered, _selected, _ready);
            if (_outer != null)
            {
                _outer.widthMultiplier = ForageHighlightPolicy.OuterWidth * mult;
                Color outer = new Color(0.055f, 0.032f, 0.008f, _ready ? 0.72f : 0.62f);
                _outer.startColor = outer; _outer.endColor = outer;
            }
            if (_inner != null)
            {
                _inner.widthMultiplier = ForageHighlightPolicy.InnerWidth * mult;
                Color inner = showSelected
                    ? new Color(1f, 0.78f, 0.16f, _ready ? 0.86f : 0.74f)
                    : new Color(0.92f, 0.72f, 0.20f, 0.25f);
                _inner.startColor = inner; _inner.endColor = inner;
            }
        }

        private void OnDestroy() { _outer = null; _inner = null; }
    }

    internal static class ForageNodeHighlight
    {
        private static Material _sharedMaterial;

        internal static GameObject Create(Bounds bounds, string nodeId, out ForageNodeHighlightView view)
        {
            view = null;
            Material material = ResolveMaterial();
            if (material == null) return null;
            GameObject root = null;
            try
            {
                root = new GameObject("ForageHighlight_" + (nodeId ?? string.Empty));
                root.layer = 2;
                root.transform.position = bounds.center;
                float radiusX = ForageHighlightPolicy.CalculateRadius(bounds.size.x);
                float radiusZ = ForageHighlightPolicy.CalculateRadius(bounds.size.z);
                float y = bounds.min.y + 0.035f;
                LineRenderer outer = CreateRing(root.transform, "Outer", material, radiusX, radiusZ, y);
                LineRenderer inner = CreateRing(root.transform, "Inner", material, radiusX, radiusZ, y + 0.004f);
                view = root.AddComponent<ForageNodeHighlightView>();
                view.Initialize(outer, inner);
                return root;
            }
            catch
            {
                try { if (root != null) UnityEngine.Object.Destroy(root); } catch { }
                view = null;
                return null;
            }
        }

        internal static void Shutdown()
        {
            Material material = _sharedMaterial;
            _sharedMaterial = null;
            try { if (material != null) UnityEngine.Object.Destroy(material); } catch { }
        }

        private static Material ResolveMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;
            try
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) return null;
                _sharedMaterial = new Material(shader);
                _sharedMaterial.name = "ForgottenRoads_ForageHighlight_Material";
                return _sharedMaterial;
            }
            catch { return null; }
        }

        private static LineRenderer CreateRing(Transform parent, string name, Material material, float radiusX, float radiusZ, float y)
        {
            GameObject go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = ForageHighlightPolicy.SegmentCount;
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 1;
            line.numCapVertices = 0;
            Vector3 center = parent.position;
            for (int i = 0; i < ForageHighlightPolicy.SegmentCount; i++)
            {
                float angle = (float)(System.Math.PI * 2.0 * i / ForageHighlightPolicy.SegmentCount);
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radiusX, y, center.z + Mathf.Sin(angle) * radiusZ));
            }
            return line;
        }
    }
}
