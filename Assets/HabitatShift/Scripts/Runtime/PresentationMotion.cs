using UnityEngine;
using UnityEngine.UI;

namespace HabitatShift.Runtime
{
    public sealed class HabitatMotion : MonoBehaviour
    {
        SpriteRenderer source, visual, shadow;
        Vector3 basePosition, baseScale;
        bool dragging, enabledMotion = true;
        Vector2 direction;
        public void SetPose(SpriteRenderer renderer, Vector3 position, Vector3 scale, bool isDragging, Vector2 dragDirection, bool motionEnabled)
        {
            EnsureVisual(renderer);
            basePosition = position; baseScale = scale; dragging = isDragging; direction = dragDirection; enabledMotion = motionEnabled;
            transform.position = basePosition;
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            visual.sprite = source.sprite; visual.color = source.color; visual.sortingOrder = source.sortingOrder;
            shadow.sprite = source.sprite; shadow.sortingOrder = source.sortingOrder - 1;
        }
        void EnsureVisual(SpriteRenderer renderer)
        {
            if (source != null) return;
            source = renderer;
            source.enabled = false;
            visual = Child("Habitat Visual", source.sortingOrder);
            shadow = Child("Habitat Shadow", source.sortingOrder - 1);
            shadow.color = new Color(.18f, .12f, .08f, .16f);
        }
        SpriteRenderer Child(string name, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
        void LateUpdate()
        {
            if (source == null) return;
            // The root is authoritative and never receives presentation offsets.
            transform.position = basePosition;
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            if (!enabledMotion)
            {
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = baseScale;
                visual.transform.localRotation = Quaternion.identity;
                shadow.enabled = false;
                return;
            }
            var lift = dragging ? .065f : 0f;
            visual.transform.localPosition = Vector3.up * lift;
            visual.transform.localScale = baseScale * (dragging ? 1.06f : 1f);
            var tilt = dragging ? Mathf.Clamp(-direction.x * 8f, -7f, 7f) : 0f;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            shadow.enabled = dragging;
            shadow.transform.localPosition = new Vector3(0f, -.045f, 0f);
            shadow.transform.localScale = baseScale * 1.04f;
            shadow.transform.localRotation = Quaternion.identity;
        }
    }

    public sealed class UiModalPop : MonoBehaviour
    {
        public bool ReducedMotion;
        CanvasGroup group;
        float shownAt;
        void Awake()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            shownAt = Time.unscaledTime;
            transform.localScale = ReducedMotion ? Vector3.one : Vector3.one * .94f;
            group.alpha = 0f;
        }
        public void Configure(bool reduced)
        {
            ReducedMotion = reduced;
            if (reduced) transform.localScale = Vector3.one;
        }
        void Update()
        {
            var t = Mathf.Clamp01((Time.unscaledTime - shownAt) / (ReducedMotion ? .08f : .18f));
            group.alpha = t;
            if (!ReducedMotion) transform.localScale = Vector3.Lerp(Vector3.one * .94f, Vector3.one, 1f - Mathf.Pow(1f - t, 3f));
        }
    }
}


