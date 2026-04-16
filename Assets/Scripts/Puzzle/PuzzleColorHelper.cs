using UnityEngine;
using System.Collections.Generic;

namespace Puzzle
{
    public static class PuzzleColorHelper
    {
        private static readonly Dictionary<PuzzleColor, Color> colorMap = new()
        {
            { PuzzleColor.Red,    new Color(0.9f, 0.1f, 0.1f) },
            { PuzzleColor.Green,  new Color(0.1f, 0.8f, 0.1f) },
            { PuzzleColor.Blue,   new Color(0.1f, 0.3f, 0.9f) },
            { PuzzleColor.Yellow, new Color(0.95f, 0.85f, 0.1f) },
            { PuzzleColor.Purple, new Color(0.6f, 0.1f, 0.8f) }
        };

        public static Color GetColor(PuzzleColor color) =>
            colorMap.TryGetValue(color, out var c) ? c : Color.white;

        public static void ApplyColor(MeshRenderer mr, PuzzleColor color)
        {
            if (mr == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = GetColor(color);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", GetColor(color));

            mr.material = mat;
        }

        public static string GetColorName(PuzzleColor color) => color.ToString();
    }
}
