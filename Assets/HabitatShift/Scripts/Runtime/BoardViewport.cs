using HabitatShift.Core;
using UnityEngine;

namespace HabitatShift.Runtime
{
    // Catalog coordinates are top-left/Y-down. The camera stays full-screen so the garden fills the display.
    public static class BoardViewport
    {
        public const float MarginCells = .35f;
        const float HeaderAt1080 = 168f;
        const float ControlsAt1080 = 224f;

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
}

