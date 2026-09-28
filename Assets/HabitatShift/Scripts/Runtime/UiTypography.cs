using UnityEngine;
using UnityEngine.UI;
namespace HabitatShift.Runtime
{
    // Semantic typography entry point for all runtime UI.
    public static class UiTypography
    {
        public static Text Create(Transform parent, string name, string value, UiTextStyle style, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.text = value; HudTheme.ApplyTypography(text, style, color, alignment);
            return text;
        }
    }
}
