using HabitatShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HabitatShift.Runtime
{
    // Catalog coordinates are top-left/Y-down. The camera stays full-screen so the garden fills the display.
    public static class BoardViewport
    {
        public const float MarginCells = .35f;
        const float HeaderAt1080 = 168f;
        const float ControlsAt1080 = 360f;

        public static Vector3 ToWorld(Vector2 board, float z = 0f) => new Vector3(board.x, -board.y, z);

        public static Vector2 FromScreen(Camera camera, Vector2 screen)
        {
            var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Vector2(world.x, -world.y);
        }

        public static void Fit(Camera camera, LevelDto level)
        {
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var safe = Screen.safeArea;
            var scale = width / 1080f;
            var available = Rect.MinMaxRect(safe.xMin, safe.yMin + ControlsAt1080 * scale,
                safe.xMax, safe.yMax - HeaderAt1080 * scale);
            if (available.height < height * .35f)
                available = Rect.MinMaxRect(safe.xMin, safe.yMin + safe.height * .12f,
                    safe.xMax, safe.yMax - safe.height * .12f);

            var boardWidth = level.cols + 2f * MarginCells;
            var boardHeight = level.rows + 2f * MarginCells;
            var sizeForWidth = boardWidth * height / (2f * Mathf.Max(1f, available.width));
            var sizeForHeight = boardHeight * height / (2f * Mathf.Max(1f, available.height));
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);

            var unitsPerPixel = 2f * camera.orthographicSize / height;
            var offset = available.center - new Vector2(width * .5f, height * .5f);
            var center = ToWorld(new Vector2(level.cols * .5f, level.rows * .5f));
            camera.transform.position = new Vector3(center.x - offset.x * unitsPerPixel,
                center.y - offset.y * unitsPerPixel, -10f);
        }
    }

    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        int lastWidth, lastHeight;
        void OnEnable() => Apply();
        public void Refresh() => Apply();
        void Update()
        {
            if (last != Screen.safeArea || lastWidth != Screen.width || lastHeight != Screen.height) Apply();
        }
        void Apply()
        {
            last = Screen.safeArea;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            var rect = GetComponent<RectTransform>();
            var min = last.position;
            var max = last.position + last.size;
            rect.anchorMin = new Vector2(min.x / Mathf.Max(1, lastWidth), min.y / Mathf.Max(1, lastHeight));
            rect.anchorMax = new Vector2(max.x / Mathf.Max(1, lastWidth), max.y / Mathf.Max(1, lastHeight));
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    // Layout runs in canvas units; Screen.height is never assigned to a RectTransform.
    public sealed class ModalCardFitter : MonoBehaviour
    {
        RectTransform card, safeArea;
        float designHeight;

        public void Configure(float height)
        {
            designHeight = height;
            card = (RectTransform)transform;
            safeArea = (RectTransform)transform.parent;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (card == null || safeArea == null) return;
            var available = safeArea.rect.size - new Vector2(48f, 48f);
            var size = new Vector2(Mathf.Max(1f, Mathf.Min(820f, available.x)),
                Mathf.Max(1f, Mathf.Min(designHeight, available.y)));
            if (card.sizeDelta != size) card.sizeDelta = size;
        }
    }

    // ScrollRect content has explicit geometry; no ContentSizeFitter/GridLayoutGroup feedback loop.
    public sealed class LevelGridFitter : MonoBehaviour
    {
        RectTransform viewport, content;
        ScrollRect scroll;
        float lastWidth = -1f;
        int lastChildren = -1;
        bool logged;

        public void Configure(RectTransform grid)
        {
            viewport = (RectTransform)transform;
            content = grid;
            scroll = GetComponent<ScrollRect>();
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (viewport == null || content == null) return;
            var width = viewport.rect.width;
            // Cho layout cua card chay xong truoc, neu khong cell se bi tinh sai theo rect = 0.
            if (width < 120f || viewport.rect.height < 60f) return;
            if (Mathf.Abs(width - lastWidth) < .5f && content.childCount == lastChildren) return;
            lastWidth = width;
            lastChildren = content.childCount;
            const float gap = 18f;
            const float inset = 8f;
            var cell = Mathf.Min(192f, Mathf.Max(48f, (width - 2f * inset - 2f * gap) / 3f));
            var left = (width - 3f * cell - 2f * gap) * .5f;
            var rows = Mathf.CeilToInt(content.childCount / 3f);
            content.sizeDelta = new Vector2(0f, rows * cell + Mathf.Max(0, rows - 1) * gap + 2f * inset);
            content.anchoredPosition = Vector2.zero;
            for (var i = 0; i < content.childCount; i++)
            {
                var card = (RectTransform)content.GetChild(i);
                card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
                card.pivot = new Vector2(.5f, .5f);
                card.sizeDelta = new Vector2(cell, cell);
                card.anchoredPosition = new Vector2(left + cell * .5f + (i % 3) * (cell + gap),
                    -inset - cell * .5f - (i / 3) * (cell + gap));
            }
            scroll.verticalNormalizedPosition = 1f;
            if (Debug.isDebugBuild && !logged)
            {
                logged = true;
                Debug.Log("Level Select layout: viewport=" + viewport.rect.size +
                    ", grid=" + content.rect.size + ", cards=" + content.childCount +
                    ", cell=" + cell, this);
            }
        }
    }

    // Places the two action rows directly below the projected board card.
    public sealed class GameplayActionFitter : MonoBehaviour
    {
        Camera worldCamera;
        Canvas canvas;
        RectTransform safeArea, assistRow, utilityRow, cancelButton;
        LevelDto level;

        public void Configure(Camera camera, Canvas owner, RectTransform root,
            RectTransform assist, RectTransform utility, RectTransform cancel, LevelDto board)
        {
            worldCamera = camera; canvas = owner; safeArea = root;
            assistRow = assist; utilityRow = utility; cancelButton = cancel; level = board;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (worldCamera == null || canvas == null || safeArea == null ||
                assistRow == null || utilityRow == null || level == null) return;

            var scale = canvas.scaleFactor;
            var boardBottom = worldCamera.WorldToScreenPoint(
                BoardViewport.ToWorld(new Vector2(level.cols * .5f, level.rows + .17f)));
            var assistY = boardBottom.y - (22f + assistRow.rect.height * .5f) * scale;
            var utilityY = assistY -
                (assistRow.rect.height * .5f + 18f + utilityRow.rect.height * .5f) * scale;
            var minUtilityY = Screen.safeArea.yMin + (utilityRow.rect.height * .5f + 20f) * scale;
            if (utilityY < minUtilityY)
            {
                var adjustment = minUtilityY - utilityY;
                assistY += adjustment; utilityY += adjustment;
            }
            var centerX = Screen.safeArea.center.x;
            SetScreenCenter(assistRow, centerX, assistY);
            SetScreenCenter(utilityRow, centerX, utilityY);
            if (cancelButton != null)
                SetScreenCenter(cancelButton, centerX + (assistRow.rect.width * .5f + 48f) * scale, assistY);
        }

        void SetScreenCenter(RectTransform row, float x, float y)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeArea, new Vector2(x, y), null, out var local))
                row.anchoredPosition = local;
        }
    }
}

