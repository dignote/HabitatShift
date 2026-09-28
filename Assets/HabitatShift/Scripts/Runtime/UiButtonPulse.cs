using UnityEngine;
using UnityEngine.EventSystems;

namespace HabitatShift.Runtime
{
    public sealed class UiButtonPulse : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public static bool ReducedMotion;
        Transform content;
        Vector3 baseScale = Vector3.one;
        bool pressed;
        void Awake() { content = transform.Find("Content"); if (content != null) baseScale = content.localScale; }
        public void OnPointerDown(PointerEventData eventData) { pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }
        public void OnPointerExit(PointerEventData eventData) { pressed = false; }
        void Update()
        {
            if (content == null) { content = transform.Find("Content"); if (content == null) return; baseScale = content.localScale; }
            content.localScale = ReducedMotion ? baseScale : Vector3.Lerp(content.localScale, baseScale * (pressed ? .94f : 1f), Time.unscaledDeltaTime * 18f);
        }
    }
}
