#if UNITY_EDITOR
using HabitatShift.Runtime;
using NUnit.Framework;
using UnityEngine;
namespace HabitatShift.LevelLabR10.Tests
{
    public sealed class LevelLabCanvasCoordinateTests
    {
        [Test]
        public void AuthoringOrigin_MatchesRuntimeTopLeft()
        {
            var board = new Rect(10f, 20f, 300f, 300f);
            var cell = R10CanvasCoordinates.BoardToCanvasRect(board, 50f, 0, 0);
            Assert.AreEqual(new Vector2(10f, 20f), cell.position);
            Assert.AreEqual(new Vector2Int(0, 0), R10CanvasCoordinates.CanvasToBoard(board, 50f, cell.center));
        }
        [Test]
        public void CanvasBoardRoundTrip_PreservesBottomRightCell()
        {
            var board = new Rect(0f, 0f, 600f, 600f);
            var cell = R10CanvasCoordinates.BoardToCanvasRect(board, 100f, 5, 5);
            Assert.AreEqual(new Vector2Int(5, 5), R10CanvasCoordinates.CanvasToBoard(board, 100f, cell.center));
        }
        [TestCase("orange")][TestCase("blue")][TestCase("green")][TestCase("yellow")][TestCase("purple")]
        [TestCase("cyan")][TestCase("pink")][TestCase("red")][TestCase("white")][TestCase("brown")]
        public void AuthoringPalette_MatchesRuntimePalette(string color)
        {
            Assert.AreEqual(GameplayFxController.ColorFor(color), R10AuthoringPalette.ColorFor(color));
        }
    }
}
#endif