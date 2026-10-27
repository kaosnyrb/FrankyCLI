using System;
using System.Collections.Generic;

namespace Retrograde.Grammar
{
    public static class ArcCurve
    {
        // Each curve: normalized position (0.0=start, 1.0=end) → tension (0.0=low, 1.0=max)
        // Six points per curve, linearly interpolated between them.
        private static readonly Dictionary<ArcShape, float[]> Curves = new Dictionary<ArcShape, float[]>
        {
            { ArcShape.RagsToRiches, new[] { 0.20f, 0.35f, 0.50f, 0.65f, 0.80f, 0.90f } },
            { ArcShape.Tragedy,      new[] { 0.80f, 0.70f, 0.55f, 0.40f, 0.25f, 0.15f } },
            { ArcShape.ManInAHole,   new[] { 0.30f, 0.50f, 0.80f, 0.85f, 0.50f, 0.20f } },
            { ArcShape.Icarus,       new[] { 0.70f, 0.50f, 0.20f, 0.15f, 0.50f, 0.85f } },
            { ArcShape.Cinderella,   new[] { 0.20f, 0.40f, 0.70f, 0.80f, 0.50f, 0.20f } },
            { ArcShape.Oedipus,      new[] { 0.70f, 0.40f, 0.20f, 0.30f, 0.70f, 0.90f } },
        };

        /// <summary>
        /// Sample tension at a normalized position (0.0 to 1.0) along the arc.
        /// </summary>
        public static float SampleTension(ArcShape shape, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            var curve = Curves[shape];
            int segments = curve.Length - 1;
            float scaledT = t * segments;
            int index = Math.Min((int)scaledT, segments - 1);
            float frac = scaledT - index;
            return curve[index] + (curve[index + 1] - curve[index]) * frac;
        }

        /// <summary>
        /// Map a tension value to a mood descriptor for prompt injection.
        /// </summary>
        public static string SampleMood(float tension)
        {
            if (tension < 0.2f) return "calm";
            if (tension < 0.4f) return "uneasy";
            if (tension < 0.6f) return "tense";
            if (tension < 0.8f) return "desperate";
            return "climactic";
        }
    }
}
