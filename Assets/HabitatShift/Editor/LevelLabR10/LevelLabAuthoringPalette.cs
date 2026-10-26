#if UNITY_EDITOR
using HabitatShift.Runtime;
using UnityEngine;
namespace HabitatShift.LevelLabR10
{
    internal static class R10AuthoringPalette
    {
        // Runtime Preview and Authoring share this production palette. Authoring only changes opacity.
        public static Color ColorFor(string color) => GameplayFxController.ColorFor(color);
        public static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        public static string ConstraintGlyph(string value) => value == "HORIZONTAL_ONLY" ? "H" : value == "VERTICAL_ONLY" ? "V" : "F";
        public static string DirectionGlyph(string value) => value == "UP" ? "^" : value == "DOWN" ? "v" : value == "LEFT" ? "<" : value == "RIGHT" ? ">" : "?";
    }
}
#endif