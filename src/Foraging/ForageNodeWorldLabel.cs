using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorCraftingExpanded
{
    // The world label is deliberately not transform-parented to vegetation because native plant
    // hierarchies can carry negative/non-uniform scale that mirrors UI. This mod-owned component
    // gives the detached label the same destruction lifetime as the mechanical node root anyway.
    internal sealed class ForageNodePresentationOwner : MonoBehaviour
    {
        private GameObject _worldLabel;

        internal void OwnWorldLabel(GameObject worldLabel)
        {
            if (_worldLabel != null && _worldLabel != worldLabel)
            {
                try { UnityEngine.Object.Destroy(_worldLabel); } catch { }
            }
            _worldLabel = worldLabel;
        }

        private void OnDestroy()
        {
            GameObject label = _worldLabel;
            _worldLabel = null;
            try { if (label != null) UnityEngine.Object.Destroy(label); } catch { }
        }
    }


    // Cached, allocation-free runtime presentation handle for the existing resource bar. The
    // controller drives this directly during Gathering/completion instead of searching the UI
    // hierarchy every frame.
    internal sealed class ForageNodeWorldLabelView : MonoBehaviour
    {
        private Image _fill;
        private RectTransform _fillRect;
        private Vector3 _fillBaseScale = Vector3.one;
        private CanvasGroup _group;
        private Vector3 _baseScale = Vector3.one;
        private float _distanceScale = 1f;
        private float _feedbackScale = 1f;
        private Image _back;
        private TextMeshProUGUI _text;
        private static readonly Color NormalText = new Color(1f, 0.84f, 0.08f, 1f);
        private static readonly Color TargetText = new Color(1f, 0.91f, 0.18f, 1f);
        private static readonly Color ReadyText = new Color(1f, 0.97f, 0.38f, 1f);
        private static readonly Color NormalBack = new Color(0.055f, 0.008f, 0.008f, 0.98f);
        private static readonly Color ReadyBack = new Color(0.085f, 0.012f, 0.008f, 1f);

        internal void Initialize(Image fill, Image back, TextMeshProUGUI text)
        {
            _fill = fill;
            _back = back;
            _text = text;
            _fillRect = fill == null ? null : fill.rectTransform;
            if (_fillRect != null) _fillBaseScale = _fillRect.localScale;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _baseScale = transform.localScale;
            ResetAvailable();
        }

        internal void ResetAvailable()
        {
            SetFillFraction(1f);
            if (_group != null) _group.alpha = 1f;
            _feedbackScale = 1f;
            ApplyScale();
        }

        internal void SetGatherProgress(float progress01)
        {
            SetFillFraction(ForagePresentationPolicy.ResourceBarFill(progress01));
            if (_group != null) _group.alpha = 1f;
            _feedbackScale = 1f;
            ApplyScale();
        }

        internal void SetCompletionFeedback(float normalizedRemaining)
        {
            SetFillFraction(0f);
            if (_group != null) _group.alpha = ForagePresentationPolicy.CompletionFeedbackAlpha(normalizedRemaining);
            _feedbackScale = ForagePresentationPolicy.CompletionFeedbackScale(normalizedRemaining);
            ApplyScale();
        }

        internal void SetInteractionState(bool targeted, bool ready)
        {
            if (_text != null) _text.color = ready ? ReadyText : (targeted ? TargetText : NormalText);
            if (_back != null) _back.color = ready ? ReadyBack : NormalBack;
        }

        internal void SetDistanceScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 1f) scale = 1f;
            if (scale > ForagePresentationPolicy.LabelMaximumDistanceScale) scale = ForagePresentationPolicy.LabelMaximumDistanceScale;
            if (Mathf.Abs(_distanceScale - scale) < 0.002f) return;
            _distanceScale = scale;
            ApplyScale();
        }

        private void ApplyScale()
        {
            transform.localScale = _baseScale * (_distanceScale * _feedbackScale);
        }

        private void SetFillFraction(float fraction)
        {
            if (_fillRect == null) return;
            if (float.IsNaN(fraction) || float.IsInfinity(fraction)) fraction = 1f;
            fraction = Mathf.Clamp01(fraction);
            Vector3 scale = _fillBaseScale;
            scale.x *= fraction;
            _fillRect.localScale = scale;
        }

        private void OnDestroy()
        {
            _fill = null;
            _fillRect = null;
            _group = null;
            _back = null;
            _text = null;
        }
    }

    // One-sided, screen-aligned world label. Canvas/TMP's readable face is local -Z, so the root
    // copies the active gameplay camera rotation: local +Z follows the camera view direction and
    // local -Z faces the player. Unlike pointing at the camera's world position, this keeps the bar
    // parallel to the screen while orbiting 360 degrees. LateUpdate allocates no managed objects.
    internal sealed class ForageNodeLabelBillboard : MonoBehaviour
    {
        private const float MissingCameraProbeInterval = 0.25f;
        private Camera _camera;
        private float _nextCameraProbeTime;
        private ForageNodeWorldLabelView _view;

        private void OnEnable()
        {
            _nextCameraProbeTime = 0f;
            _view = GetComponent<ForageNodeWorldLabelView>();
            UpdateFacing();
        }

        private void LateUpdate()
        {
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            try
            {
                float now = Time.unscaledTime;
                if (_camera == null || !_camera.isActiveAndEnabled || now >= _nextCameraProbeTime)
                {
                    _nextCameraProbeTime = now + MissingCameraProbeInterval;
                    Camera previous = _camera;
                    Camera resolved = ForageGameplayCameraResolver.Resolve(_camera, gameObject.layer);
                    if (resolved != null) _camera = resolved;
                    if (_camera == null) return;
                    if (previous != _camera || ForageNodeWorldLabel.LastBillboardCameraSummary == "camera=(unbound)")
                        ForageNodeWorldLabel.LastBillboardCameraSummary = ForageGameplayCameraResolver.Describe(_camera);
                }

                // Exact camera rotation gives a true screen-aligned nameplate. This deliberately
                // does not use labelPosition-cameraPosition LookRotation: that creates a world sign
                // aimed at a point rather than a native-style overlay parallel to the view plane.
                transform.rotation = _camera.transform.rotation;
                if (_view != null)
                {
                    float distance = Vector3.Distance(_camera.transform.position, transform.position);
                    _view.SetDistanceScale(ForagePresentationPolicy.CalculateLabelDistanceScale(distance));
                }
                ForageNodeWorldLabel.UpdateFacingDiagnostic(_camera, transform);
            }
            catch { }
        }

        private void OnDisable()
        {
            _camera = null;
            _nextCameraProbeTime = 0f;
        }

        private void OnDestroy()
        {
            _camera = null;
            _nextCameraProbeTime = 0f;
        }
    }

    internal static class ForageNodeWorldLabel
    {
        private const float VerticalGap = 0.20f;
        private const float FacingDiagnosticInterval = 1.0f;
        private static float _nextFacingDiagnosticAt;
        internal static string LastBillboardCameraSummary = "camera=(unbound)";
        internal static string LastFacingDiagnostic = "facing=(unbound)";

        internal static GameObject Create(GameObject nodeRoot, string displayName, Bounds? rendererAnchor)
        {
            if (nodeRoot == null || string.IsNullOrEmpty(displayName)) return null;
            GameObject root = null;
            try
            {
                Vector3 worldPosition = ResolveWorldPosition(nodeRoot, rendererAnchor);
                root = new GameObject("ForageResourceLabel", typeof(RectTransform), typeof(Canvas));
                // Do not parent this to vegetation. Native plant transforms can carry negative or
                // non-uniform scale/rotation; the label remains a mod-owned world-space object with
                // a stable positive scale and independent camera-facing orientation.
                root.transform.position = worldPosition;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one * ForagePresentationPolicy.LabelWorldScale;

                RectTransform rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(ForagePresentationPolicy.LabelWidth, ForagePresentationPolicy.LabelHeight);

                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 40;

                // Native Mineral Deposit is the visual reference: the yellow resource name sits
                // inside/over the red availability bar instead of floating as a separate label.
                // This is mod-owned presentation only; no MiningNode gameplay/component is copied.
                RectTransform barBack = CreateRect("ResourceBarBack", rect);
                barBack.anchorMin = new Vector2(
                    ForagePresentationPolicy.BarLeftFraction,
                    ForagePresentationPolicy.BarBottomFraction);
                barBack.anchorMax = new Vector2(
                    ForagePresentationPolicy.BarRightFraction,
                    ForagePresentationPolicy.BarTopFraction);
                barBack.offsetMin = Vector2.zero;
                barBack.offsetMax = Vector2.zero;
                Image back = barBack.gameObject.AddComponent<Image>();
                back.color = new Color(0.055f, 0.008f, 0.008f, 0.98f);
                back.raycastTarget = false;
                Outline barOutline = barBack.gameObject.AddComponent<Outline>();
                barOutline.effectColor = new Color(0f, 0f, 0f, 1f);
                barOutline.effectDistance = new Vector2(2.4f, -2.4f);
                barOutline.useGraphicAlpha = false;

                RectTransform barFill = CreateRect("ResourceBar", barBack);
                Stretch(barFill, 1.5f, 1.5f, 1.5f, 1.5f);
                barFill.pivot = new Vector2(0f, 0.5f);
                Image fill = barFill.gameObject.AddComponent<Image>();
                fill.color = new Color(0.78f, 0.025f, 0.018f, 1f);
                fill.raycastTarget = false;
                // RectTransform scale is the fill authority because an Image without an assigned
                // sprite does not reliably honor Image.fillAmount across Unity UI code paths.
                fill.type = Image.Type.Simple;

                // Create text after the fill so it renders over the bar in this Canvas hierarchy.
                RectTransform textRect = CreateRect("Name", barBack);
                Stretch(textRect, 4f, 0f, 4f, 0f);
                TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = displayName;
                text.fontSize = ForagePresentationPolicy.LabelFontSize;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1f, 0.84f, 0.08f, 1f);
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
                Outline textOutline = textRect.gameObject.AddComponent<Outline>();
                textOutline.effectColor = new Color(0f, 0f, 0f, 1f);
                textOutline.effectDistance = new Vector2(1.6f, -1.6f);
                textOutline.useGraphicAlpha = false;

                ForageNodeWorldLabelView view = root.AddComponent<ForageNodeWorldLabelView>();
                view.Initialize(fill, back, text);

                // Add the billboard only after the root has its final world position/scale and UI
                // hierarchy, so OnEnable can establish a correct front face immediately.
                root.AddComponent<ForageNodeLabelBillboard>();
                ForageNodePresentationOwner owner = nodeRoot.GetComponent<ForageNodePresentationOwner>();
                if (owner == null) owner = nodeRoot.AddComponent<ForageNodePresentationOwner>();
                owner.OwnWorldLabel(root);
                return root;
            }
            catch
            {
                try { if (root != null) UnityEngine.Object.Destroy(root); } catch { }
                return null;
            }
        }

        internal static void UpdateFacingDiagnostic(Camera camera, Transform canvasTransform)
        {
            if (camera == null || canvasTransform == null) return;
            float now;
            try { now = Time.unscaledTime; } catch { return; }
            if (now < _nextFacingDiagnosticAt) return;
            _nextFacingDiagnosticAt = now + FacingDiagnosticInterval;
            try
            {
                Vector3 cameraForward = camera.transform.forward;
                Vector3 canvasForward = canvasTransform.forward;
                Vector3 localScale = canvasTransform.localScale;
                Vector3 worldScale = canvasTransform.lossyScale;
                string parentScale = canvasTransform.parent == null
                    ? "detached"
                    : FormatVector(canvasTransform.parent.lossyScale);
                LastFacingDiagnostic =
                    "camera={" + camera.name + "}" +
                    " depth=" + camera.depth.ToString("F1") +
                    " rect=" + camera.pixelRect.x.ToString("F0") + "," + camera.pixelRect.y.ToString("F0") + "," +
                        camera.pixelRect.width.ToString("F0") + "x" + camera.pixelRect.height.ToString("F0") +
                    " cameraRot=" + FormatQuaternion(camera.transform.rotation) +
                    " canvasRot=" + FormatQuaternion(canvasTransform.rotation) +
                    " cameraForward=" + FormatVector(cameraForward) +
                    " canvasForward=" + FormatVector(canvasForward) +
                    " forwardDot=" + Vector3.Dot(canvasForward.normalized, cameraForward.normalized).ToString("F4") +
                    " parentScale=" + parentScale +
                    " canvasLocalScale=" + FormatVector(localScale) +
                    " canvasWorldScale=" + FormatVector(worldScale);
            }
            catch { LastFacingDiagnostic = "facing=(unavailable)"; }
        }

        internal static string DescribePresentation()
        {
            return "labelBillboard=screen-aligned-camera-rotation nameInBar=yes " + LastBillboardCameraSummary +
                " labelScale=" + ForagePresentationPolicy.LabelWorldScale.ToString("F4") +
                " distanceScaleMax=" + ForagePresentationPolicy.LabelMaximumDistanceScale.ToString("F2") +
                " labelWorld=" + ForagePresentationPolicy.LabelWorldWidth().ToString("F2") + "x" +
                    ForagePresentationPolicy.LabelWorldHeight().ToString("F2") +
                " barScale=" + ForagePresentationPolicy.BarWorldWidth().ToString("F2") + "x" +
                    ForagePresentationPolicy.BarWorldHeight().ToString("F2");
        }

        internal static void ResetDiagnostics()
        {
            _nextFacingDiagnosticAt = 0f;
            LastBillboardCameraSummary = "camera=(unbound)";
            LastFacingDiagnostic = "facing=(unbound)";
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F3") + "," + value.y.ToString("F3") + "," + value.z.ToString("F3") + ")";
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return "(" + value.x.ToString("F3") + "," + value.y.ToString("F3") + "," + value.z.ToString("F3") + "," + value.w.ToString("F3") + ")";
        }

        private static Vector3 ResolveWorldPosition(GameObject nodeRoot, Bounds? rendererAnchor)
        {
            if (rendererAnchor.HasValue)
            {
                Bounds anchor = rendererAnchor.Value;
                return new Vector3(anchor.center.x, anchor.max.y + VerticalGap, anchor.center.z);
            }
            Vector3 fallback = nodeRoot.transform.position + Vector3.up * 1.35f;
            try
            {
                Renderer[] renderers = nodeRoot.GetComponentsInChildren<Renderer>(true);
                bool haveBounds = false;
                Bounds combined = new Bounds();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (!haveBounds) { combined = renderer.bounds; haveBounds = true; }
                    else combined.Encapsulate(renderer.bounds);
                }
                if (!haveBounds) return fallback;
                return new Vector3(combined.center.x, combined.max.y + VerticalGap, combined.center.z);
            }
            catch { return fallback; }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
