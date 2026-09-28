using System;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatShift.Runtime
{
    public enum HudControlStyle { Primary, Secondary, Circle, Action, Assist, Level, Disabled }
    public enum UiContentLayout { Modal, Setting, GameplayAction, LevelCard, Circle }
    public enum UiTextStyle { Display, Title, Heading, Body, Button, Hud, Caption, Badge }

    public readonly struct UiControlSpec
    {
        public readonly HudControlStyle Style;
        public readonly string Icon;
        public readonly UiContentLayout Layout;
        public readonly bool IsSetting;
        public UiControlSpec(HudControlStyle style, string icon, UiContentLayout layout, bool isSetting = false)
        { Style = style; Icon = icon; Layout = layout; IsSetting = isSetting; }
    }

    public static class HudTheme
    {
        const string IconRoot = "HabitatShift/UI/v1/";
        const string SurfaceRoot = "HabitatShift/UI/v2/";
        static readonly Color Cocoa = new Color(.17f, .095f, .055f, 1f);
        static readonly Color Ivory = new Color(1f, .965f, .86f, 1f);
        static readonly Color Disabled = new Color(.62f, .64f, .57f, .60f);
        static Font lilita, notoRegular, notoSemiBold, notoBold;

        static Font Load(ref Font cache, string key) { if (cache == null) cache = Resources.Load<Font>(key); return cache; }
        static bool IsLilita(UiTextStyle style) => style == UiTextStyle.Display || style == UiTextStyle.Title || style == UiTextStyle.Heading || style == UiTextStyle.Button || style == UiTextStyle.Hud || style == UiTextStyle.Badge;
        static Font FontFor(UiTextStyle style)
        {
            if (IsLilita(style)) return Load(ref lilita, "HabitatShift/Fonts/LilitaOne-Regular");
            if (style == UiTextStyle.Body) return Load(ref notoSemiBold, "HabitatShift/Fonts/NotoSans-SemiBold");
            if (style == UiTextStyle.Caption) return Load(ref notoBold, "HabitatShift/Fonts/NotoSans-Bold");
            return Load(ref notoRegular, "HabitatShift/Fonts/NotoSans-Regular");
        }
        public static void ApplyTypography(Text text, UiTextStyle style, Color color, TextAnchor alignment)
        {
            text.font = FontFor(style); text.fontStyle = FontStyle.Normal;
            text.fontSize = FontSize(style); text.alignment = alignment; text.color = color; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.supportRichText = false;
            var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.enabled = color.r > .8f;
            shadow.effectColor = color.r > .8f ? new Color(.16f,.08f,.04f,.40f) : new Color(1f,.94f,.80f,.10f);
            shadow.effectDistance = new Vector2(0f, -1f); shadow.useGraphicAlpha = true;
        }
        public static int FontSize(UiTextStyle style) { switch(style) { case UiTextStyle.Display:return 68; case UiTextStyle.Title:return 54; case UiTextStyle.Heading:return 44; case UiTextStyle.Body:return 26; case UiTextStyle.Button:return 28; case UiTextStyle.Hud:return 28; case UiTextStyle.Caption:return 18; default:return 18; } }

        public static Sprite Sprite(string key) => Resources.Load<Sprite>(IconRoot + key) ?? Resources.Load<Sprite>(SurfaceRoot + key);

        public static UiControlSpec Resolve(string label, bool disabled)
        {
            if (label == "LOCKED" || IsLevelLabel(label)) return new UiControlSpec(HudControlStyle.Level, label == "LOCKED" ? "icon_lock" : null, UiContentLayout.LevelCard);
            if (label == "BACK") return new UiControlSpec(HudControlStyle.Circle, "icon_back", UiContentLayout.Circle);
            if (label == "PAUSE") return new UiControlSpec(HudControlStyle.Circle, "icon_pause", UiContentLayout.Circle);
            if (label == "CLOSE") return new UiControlSpec(HudControlStyle.Circle, "icon_close", UiContentLayout.Circle);
            if (label == "BLOOM") return new UiControlSpec(disabled ? HudControlStyle.Disabled : HudControlStyle.Assist, "icon_assist_bloom", UiContentLayout.GameplayAction);
            if (label == "SHIFT") return new UiControlSpec(disabled ? HudControlStyle.Disabled : HudControlStyle.Assist, "icon_assist_shift", UiContentLayout.GameplayAction);
            if (label == "TRIM") return new UiControlSpec(disabled ? HudControlStyle.Disabled : HudControlStyle.Assist, "icon_assist_trim", UiContentLayout.GameplayAction);
            if (IsSetting(label)) return new UiControlSpec(HudControlStyle.Secondary, IconFor(label), UiContentLayout.Setting, true);
            if (label == "UNDO" || label == "RESTART" || label == "REPLAY") return new UiControlSpec(HudControlStyle.Action, IconFor(label), UiContentLayout.GameplayAction);
            if (disabled) return new UiControlSpec(HudControlStyle.Disabled, IconFor(label), UiContentLayout.Modal);
            var secondary = label == "LEVEL SELECT" || label == "LEVELS" || label == "SETTINGS" || label == "HOME";
            return new UiControlSpec(secondary ? HudControlStyle.Secondary : HudControlStyle.Primary, IconFor(label), UiContentLayout.Modal);
        }

        public static void StyleButton(GameObject target, HudControlStyle style, bool enabled)
        {
            var image = target.GetComponent<Image>();
            var key = style == HudControlStyle.Primary ? "ui_primary_9slice" :
                      style == HudControlStyle.Secondary || style == HudControlStyle.Disabled ? "ui_secondary_9slice" :
                      style == HudControlStyle.Level ? "ui_level_9slice" :
                      style == HudControlStyle.Assist || style == HudControlStyle.Action ? "ui_assist_9slice" : "ui_circle";
            image.sprite = Sprite(key); image.type = style == HudControlStyle.Circle ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = style == HudControlStyle.Circle; image.color = enabled ? Color.white : Disabled; image.raycastTarget = enabled;
            var shadow = target.GetComponent<Shadow>() ?? target.AddComponent<Shadow>();
            shadow.effectColor = new Color(.22f, .14f, .08f, enabled ? .18f : .08f); shadow.effectDistance = new Vector2(0f, -3f); shadow.useGraphicAlpha = true;
        }

        public static void StylePill(Image image)
        {
            image.sprite = Sprite("ui_secondary_9slice"); image.type = Image.Type.Sliced; image.preserveAspect = false; image.color = Color.white; image.raycastTarget = false;
        }

        public static string IconFor(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            if (label == "BACK") return "icon_back"; if (label == "PAUSE") return "icon_pause"; if (label == "CLOSE") return "icon_close";
            if (label.Contains("CONTINUE") || label.Contains("NEXT")) return "icon_play";
            if (label == "LEVELS" || label == "LEVEL SELECT") return "icon_levels";
            if (label.Contains("RESUME")) return "icon_resume"; if (label.Contains("UNDO")) return "icon_undo";
            if (label.Contains("RESTART") || label.Contains("REPLAY")) return "icon_restart";
            if (label == "HOME") return "icon_home"; if (label.StartsWith("SETTINGS")) return "icon_settings";
            if (label.StartsWith("MUSIC")) return "icon_music"; if (label.StartsWith("SFX")) return "icon_sfx";
            if (label.StartsWith("HAPTICS")) return "icon_haptics"; if (label.StartsWith("VFX")) return "icon_vfx";
            if (label.StartsWith("REDUCED")) return "icon_reduced_motion"; if (label == "BLOOM") return "icon_assist_bloom";
            if (label == "SHIFT") return "icon_assist_shift"; if (label == "TRIM") return "icon_assist_trim";
            if (label == "GOT IT") return "icon_check"; if (label == "LOCKED") return "icon_lock";
            return null;
        }

        public static void AddContent(GameObject button, string label, UiControlSpec spec, bool enabled, int fontSize)
        {
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(button.transform, false);
            var rect = content.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(12f, 9f); rect.offsetMax = new Vector2(-12f, -9f);
            switch (spec.Layout)
            {
                case UiContentLayout.Setting: AddSettingContent(content.transform, label, spec, enabled); break;
                case UiContentLayout.GameplayAction: AddGameplayContent(content.transform, label, spec, enabled); break;
                case UiContentLayout.LevelCard: AddLevelContent(content.transform, label, spec, enabled); break;
                case UiContentLayout.Circle: AddCircleContent(content.transform, spec); break;
                default: AddModalContent(content.transform, label, spec, enabled, fontSize); break;
            }
        }

        static void AddModalContent(Transform parent, string label, UiControlSpec spec, bool enabled, int fontSize)
        {
            if (!string.IsNullOrEmpty(spec.Icon)) AddImage(parent, "Icon", spec.Icon, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(52f, 0f), new Vector2(50f, 50f));
            var left = string.IsNullOrEmpty(spec.Icon) ? 14f : 86f;
            AddText(parent, "Label", label, fontSize, TextAnchor.MiddleCenter, TextColor(spec.Style, enabled), new Vector2(0f, 0f), Vector2.one, new Vector2(left, 0f), new Vector2(-16f, 0f));
        }

        static void AddSettingContent(Transform parent, string label, UiControlSpec spec, bool enabled)
        {
            AddImage(parent, "Icon", spec.Icon, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(48f, 0f), new Vector2(56f, 56f));
            AddText(parent, "Label", SettingName(label), 22, TextAnchor.MiddleLeft, Cocoa, new Vector2(0f,0f), Vector2.one, new Vector2(96f,0f), new Vector2(-176f,0f));
            AddText(parent, "State", label.EndsWith("ON", StringComparison.Ordinal) ? "ON" : "OFF",
                18, TextAnchor.MiddleRight, Cocoa, new Vector2(1f,.5f), new Vector2(1f,.5f),
                new Vector2(-152f,-20f), new Vector2(-104f,20f));
            AddToggle(parent, label.EndsWith("ON", StringComparison.Ordinal));
        }

        static void AddGameplayContent(Transform parent, string label, UiControlSpec spec, bool enabled)
        {
            if (!string.IsNullOrEmpty(spec.Icon)) AddImage(parent, "Icon", spec.Icon, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(0f,13f), new Vector2(58f,58f));
            AddText(parent, "Label", label, 17, TextAnchor.MiddleCenter, TextColor(spec.Style, enabled), new Vector2(0f,0f), new Vector2(1f,.40f), Vector2.zero, Vector2.zero);
        }

        static void AddLevelContent(Transform parent, string label, UiControlSpec spec, bool enabled)
        {
            if (!string.IsNullOrEmpty(spec.Icon)) { AddImage(parent, "Icon", spec.Icon, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(56f,56f)); return; }
            var number = label.Length >= 2 ? label.Substring(label.Length - 2) : label;
            AddText(parent, "Number", number, 30, TextAnchor.MiddleCenter, Cocoa, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        static void AddCircleContent(Transform parent, UiControlSpec spec)
        {
            AddImage(parent, "Icon", spec.Icon, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(56f,56f));
        }

        static void AddImage(Transform parent, string name, string key, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var item = new GameObject(name, typeof(Image)); item.transform.SetParent(parent, false); var image = item.GetComponent<Image>();
            image.sprite = Sprite(key); image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
            var rect = image.rectTransform; rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
        }

        static void AddText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var item = new GameObject(name, typeof(Text)); item.transform.SetParent(parent, false); var text = item.GetComponent<Text>();
            text.text = value; ApplyTypography(text, fontSize >= 46 ? UiTextStyle.Title : fontSize >= 26 ? UiTextStyle.Button : UiTextStyle.Caption, color, alignment); text.fontSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rect = text.rectTransform; rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        static Sprite toggleTrackOn, toggleTrackOff, toggleThumb;

        static Sprite ToggleSprite(bool thumb, bool isOn)
        {
            if (thumb && toggleThumb != null) return toggleThumb;
            if (!thumb && (isOn ? toggleTrackOn : toggleTrackOff) != null)
                return isOn ? toggleTrackOn : toggleTrackOff;
            var width = thumb ? 64 : 128;
            var height = thumb ? 64 : 72;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = thumb ? "Habitat Toggle Thumb" : isOn ? "Habitat Toggle On Track" : "Habitat Toggle Off Track";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            var border = new Color(.31f, .27f, .20f);
            var fill = thumb ? new Color(.98f, .96f, .84f) : isOn
                ? new Color(.48f, .65f, .37f) : new Color(.82f, .80f, .70f);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = Mathf.Abs(x + .5f - width * .5f) - (thumb ? 0f : 28f);
                var dy = Mathf.Abs(y + .5f - height * .5f) - (thumb ? 0f : 1f);
                var distance = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                    Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f) - (thumb ? 29f : 31f);
                var alpha = Mathf.Clamp01(.5f - distance);
                var interior = Mathf.Clamp01(-distance - 2f);
                var color = Color.Lerp(border, fill, interior);
                color.a = alpha;
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
            if (thumb) toggleThumb = sprite;
            else if (isOn) toggleTrackOn = sprite;
            else toggleTrackOff = sprite;
            return sprite;
        }

        static void AddToggle(Transform parent, bool isOn)
        {
            var item = new GameObject("Toggle", typeof(Image)); item.transform.SetParent(parent, false); var image = item.GetComponent<Image>();
            image.sprite = ToggleSprite(false, isOn); image.type = Image.Type.Simple; image.preserveAspect = false; image.raycastTarget = false;
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1f,.5f); rect.anchoredPosition = new Vector2(-48f,0f); rect.sizeDelta = new Vector2(76f,44f);
            var thumb = new GameObject("Thumb", typeof(Image)); thumb.transform.SetParent(item.transform, false); var dot = thumb.GetComponent<Image>();
            dot.sprite = ToggleSprite(true, isOn); dot.preserveAspect = true; dot.raycastTarget = false;
            var d = dot.rectTransform; d.anchorMin = d.anchorMax = new Vector2(.5f,.5f);
            d.anchoredPosition = new Vector2(isOn ? 18f : -18f, 0f); d.sizeDelta = new Vector2(32f,32f);
        }

        static string SettingName(string label) { var colon = label.IndexOf(':'); return colon > 0 ? label.Substring(0, colon) : label; }
        public static bool IsSetting(string label) => label.StartsWith("MUSIC") || label.StartsWith("SFX") || label.StartsWith("HAPTICS") || label.StartsWith("VFX") || label.StartsWith("REDUCED");
        static bool IsLevelLabel(string label) => label.Length == 8 && label.StartsWith("LEVEL ", StringComparison.Ordinal) && char.IsDigit(label[6]) && char.IsDigit(label[7]);
        public static Color TextColor(HudControlStyle style, bool enabled) => !enabled ? Cocoa : (style == HudControlStyle.Primary ? Ivory : Cocoa);
    }
}

