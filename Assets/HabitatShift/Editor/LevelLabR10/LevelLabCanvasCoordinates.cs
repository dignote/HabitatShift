#if UNITY_EDITOR
using UnityEngine;
namespace HabitatShift.LevelLabR10
{
    // Level data and production BoardViewport both use top-left origin / Y-down.
    public static class R10CanvasCoordinates
    {
        public static Rect BoardToCanvasRect(Rect boardRect, float cellSize, int x, int y)
        {
            return new Rect(boardRect.x + x * cellSize, boardRect.y + y * cellSize, cellSize, cellSize);
        }

        public static Vector2Int CanvasToBoard(Rect boardRect, float cellSize, Vector2 canvasPoint)
        {
            return new Vector2Int(Mathf.FloorToInt((canvasPoint.x - boardRect.x) / cellSize),
                Mathf.FloorToInt((canvasPoint.y - boardRect.y) / cellSize));
        }
    }
}
#endif