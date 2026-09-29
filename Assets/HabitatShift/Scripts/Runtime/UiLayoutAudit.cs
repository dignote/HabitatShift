using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatShift.Runtime
{
    // Development-only checks of the rendered rectangles, sampled without per-frame logging.
    public sealed class UiLayoutAudit : MonoBehaviour
    {
        const float Tolerance = 4f;
        readonly HashSet<string> reported = new HashSet<string>();
        readonly Dictionary<Button, int> firstSeenFrame = new Dictionary<Button, int>();
        readonly List<Button> staleButtons = new List<Button>();
        int lastFrame = -120;

        void LateUpdate()
        {
            if (!Debug.isDebugBuild || Time.frameCount - lastFrame < 60) return;
            lastFrame = Time.frameCount;
            Canvas.ForceUpdateCanvases();
            foreach (var button in GetComponentsInChildren<Button>(false)) AuditButton(button);
            if (firstSeenFrame.Count > 128)
            {
                staleButtons.Clear();
                foreach (var entry in firstSeenFrame) if (entry.Key == null) staleButtons.Add(entry.Key);
                foreach (var stale in staleButtons) firstSeenFrame.Remove(stale);
            }
            AuditPanels();
            AuditHud();
        }

        void AuditButton(Button button)
        {
            var rect = button.transform as RectTransform;
            if (rect == null || rect.rect.width < 1f || rect.rect.height < 1f) return;
            if (!firstSeenFrame.TryGetValue(button, out var firstFrame))
            {
                firstSeenFrame[button] = Time.frameCount;
                return;
            }
            if (Time.frameCount - firstFrame < 2) return;
            var key = Path(button);
            if (rect.rect.width < 48f || rect.rect.height < 48f)
                Report(key + "/size", "hit target below 48 reference units", button);

            var content = button.transform.Find("Content") as RectTransform;
            if (content == null) { Report(key + "/content", "missing Content", button); return; }
            if (!Fits(rect, content)) Report(key + "/content", "Content outside button", content);
            foreach (RectTransform child in content)
            {
                if (!Fits(content, child))
                    Report(key + "/" + child.name,
                        "outside Content (content " + content.rect.size + ", child " + child.rect.size + ")", child);
                var text = child.GetComponent<Text>();
                if (text != null)
                {
                    if (text.cachedTextGenerator.lineCount > 1)
                        Report(key + "/" + child.name + "/wrap", "button text wraps", text);
                    if (text.preferredWidth > child.rect.width + Tolerance ||
                        text.preferredHeight > child.rect.height + Tolerance)
                        Report(key + "/" + child.name + "/glyph",
                            "text glyphs clip (preferred " + text.preferredWidth.ToString("F1") + "x" +
                            text.preferredHeight.ToString("F1") + ", rect " + child.rect.size + ")", text);
                }
            }

            var toggle = content.Find("Toggle") as RectTransform;
            if (toggle != null)
            {
                if (content.rect.xMax - content.InverseTransformPoint(toggle.TransformPoint(toggle.rect.center)).x > 100f)
                    Report(key + "/toggle", "toggle is not in the right column", toggle);
                var thumb = toggle.Find("Thumb") as RectTransform;
                var state = content.Find("State")?.GetComponent<Text>();
                if (thumb == null || state == null ||
                    (state.text == "ON" && thumb.anchoredPosition.x <= 0f) ||
                    (state.text == "OFF" && thumb.anchoredPosition.x >= 0f))
                    Report(key + "/toggle-state", "toggle thumb disagrees with state", toggle);
            }
        }

        void AuditPanels()
        {
            foreach (var safe in GetComponentsInChildren<SafeAreaFitter>(false))
            {
                if (safe.name != "Safe Area") continue;
                var safeRect = (RectTransform)safe.transform;
                foreach (RectTransform child in safeRect)
                    if (!Fits(safeRect, child)) Report(Path(child) + "/safe", "card outside safe area", child);
            }
            var levelCard = transform.Find("Levels Backdrop/Safe Area/Levels") as RectTransform;
            if (levelCard != null && levelCard.childCount > 0)
            {
                var footer = levelCard.GetChild(levelCard.childCount - 1) as RectTransform;
                if (footer != null && !Fits(levelCard, footer))
                    Report("Levels/footer", "HOME footer outside card", footer);
                var viewport = levelCard.Find("Level Scroll") as RectTransform;
                var grid = viewport != null ? viewport.Find("Level Grid") as RectTransform : null;
                if (viewport == null || grid == null || grid.childCount != HabitatShift.Core.CatalogLoader.ExpectedLevelCount ||
                    viewport.rect.width < 48f || viewport.rect.height < 48f || grid.rect.height < 48f)
                    Report("Levels/grid",
                        "viewport=" + (viewport == null ? "null" : viewport.rect.size.ToString()) +
                        " grid=" + (grid == null ? "null" : grid.rect.size + "@" + grid.anchoredPosition) +
                        " cards=" + (grid == null ? -1 : grid.childCount) +
                        " first=" + (grid == null || grid.childCount == 0
                            ? "-" : ((RectTransform)grid.GetChild(0)).rect.size + "@" + ((RectTransform)grid.GetChild(0)).anchoredPosition),
                        levelCard);
                else
                {
                    var first = grid.GetChild(0) as RectTransform;
                    if (first != null && !Fits(grid, first))
                        Report("Levels/first", "first level card outside grid", first);
                    var visible = false;
                    for (var i = 0; i < grid.childCount; i++)
                    {
                        var card = grid.GetChild(i) as RectTransform;
                        if (card != null && Overlaps(viewport, card)) { visible = true; break; }
                    }
                    if (!visible) Report("Levels/visible", "no level card intersects the viewport", viewport);
                }
            }
            var homeCard = transform.Find("Habitat Shift Backdrop/Safe Area/Habitat Shift") as RectTransform;
            if (homeCard != null)
            {
                var continueButton = homeCard.Find("CONTINUE Button") as RectTransform;
                var levelsButton = homeCard.Find("LEVELS Button") as RectTransform;
                var settingsButton = homeCard.Find("SETTINGS Button") as RectTransform;
                if (continueButton != null && levelsButton != null && settingsButton != null &&
                    (Vector2.Distance(continueButton.rect.size, levelsButton.rect.size) > Tolerance ||
                     Vector2.Distance(continueButton.rect.size, settingsButton.rect.size) > Tolerance))
                    Report("Home/buttons", "primary navigation buttons have different sizes", homeCard);
            }
        }

        void AuditHud()
        {
            var assist = transform.Find("HUD/Assist Row") as RectTransform;
            var utility = transform.Find("HUD/Utility Row") as RectTransform;
            if (assist == null || utility == null) return;
            if (assist.position.y <= utility.position.y)
                Report("HUD/order", "assist row must be above utility row", assist);
            var safe = transform.Find("HUD") as RectTransform;
            if (safe != null && (!Fits(safe, assist) || !Fits(safe, utility)))
                Report("HUD/safe", "action row outside safe area", assist);
            var card = GameObject.Find("card");
            var sprite = card != null ? card.GetComponent<SpriteRenderer>() : null;
            var camera = Camera.main;
            if (sprite == null || camera == null) return;
            var boardBottom = camera.WorldToScreenPoint(sprite.bounds.min).y;
            var corners = new Vector3[4]; assist.GetWorldCorners(corners);
            var assistTop = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
            var scale = GetComponent<Canvas>().scaleFactor;
            var gap = (boardBottom - assistTop) / Mathf.Max(.01f, scale);
            if (gap < -Tolerance) Report("HUD/board", "assist overlaps board", assist);
            if (gap > 120f) Report("HUD/gap", "assist too far from board", assist);
        }

        static bool Fits(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var rect = parent.rect;
            foreach (var world in corners)
            {
                var local = parent.InverseTransformPoint(world);
                if (local.x < rect.xMin - Tolerance || local.x > rect.xMax + Tolerance ||
                    local.y < rect.yMin - Tolerance || local.y > rect.yMax + Tolerance) return false;
            }
            return true;
        }

        static bool Overlaps(RectTransform a, RectTransform b)
        {
            var ac = new Vector3[4]; var bc = new Vector3[4];
            a.GetWorldCorners(ac); b.GetWorldCorners(bc);
            return ac[0].x < bc[2].x && ac[2].x > bc[0].x &&
                ac[0].y < bc[2].y && ac[2].y > bc[0].y;
        }

        static string Path(Component component)
        {
            var names = new List<string>();
            for (var node = component.transform; node != null; node = node.parent) names.Add(node.name);
            names.Reverse(); return string.Join("/", names);
        }

        void Report(string key, string detail, Object context)
        {
            if (reported.Add(key)) Debug.LogError("UI audit: " + key + " " + detail + ".", context);
        }
    }
}
