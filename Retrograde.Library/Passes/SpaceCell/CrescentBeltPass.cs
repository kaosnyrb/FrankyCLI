using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Randomly places a crescent-shaped asteroid belt along the outer edge of the cell.
///
/// The crescent is a circular arc spanning ArcAngle degrees on the surface of
/// a sphere of radius VanillaRadius * Scale. Asteroids are largest at the
/// midpoint and taper to their smallest size at both tips, driven by a
/// half-cosine scale curve.
///
/// AngularJitter offsets each asteroid from its evenly-spaced slot, breaking
/// the mechanical regularity of the spacing. ScaleNoise adds a random
/// multiplier on top of the smooth cosine taper so adjacent asteroids
/// vary in size rather than sitting on a perfect gradient.
///
/// A small amount of radial scatter gives the belt physical depth.
/// A BufferRadius prevents adjacent asteroids from overlapping.
/// </summary>
public class CrescentBeltPass : ISpaceCellPass
{
    // Probability this pass fires for any given space cell.
    private const float CrescentChance = 1.35f;

    // Total angular span of the crescent in radians (150°).
    private const float ArcAngle = 2.618f;

    // Number of asteroids distributed along the arc.
    private const int AsteroidCount = 50;

    // Scale at the crescent midpoint (largest asteroids).
    private const float MaxScale = 3.0f;

    // Scale at the crescent tips (smallest asteroids).
    private const float MinScale = 0.4f;

    // Max radial offset from the edge sphere — gives the belt physical depth.
    private const float RadialScatter = 200f;

    // Minimum distance between any two placed belt asteroids.
    private const float BufferRadius = 60f;

    // Max random angular offset from each evenly-spaced arc slot (radians, ~4.6°).
    // Breaks the mechanical regularity of asteroid spacing along the arc.
    private const float AngularJitter = 0.08f;

    // Random scale multiplier applied on top of the cosine taper: ±this fraction.
    // Gives adjacent asteroids unpredictable size variation rather than a smooth gradient.
    private const float ScaleNoise = 0.4f;

    public void RunPass(SpaceCellState state)
    {
        if (state.AsteroidPalette == null || state.AsteroidPalette.Count == 0)
        {
            Console.WriteLine("[CrescentBeltPass] No asteroid palette — skipping.");
            return;
        }

        var rng = RandomProvider.Random;

        if ((float)rng.NextDouble() >= CrescentChance)
        {
            Console.WriteLine("[CrescentBeltPass] Skipped (chance roll).");
            return;
        }

        var targetMod = RetrogradeContext.Current.TargetMod;

        // ── Define the arc ────────────────────────────────────────────────────────
        // d   = midpoint direction of the crescent (random, biased equatorial)
        // up  = the axis along which the arc sweeps (perpendicular to d)
        // For angle a in [-ArcAngle/2, +ArcAngle/2]:
        //   pos = edgeDist * (cos(a) * d + sin(a) * up)

        float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
        float phi   = (float)(Math.PI / 3.0 + rng.NextDouble() * Math.PI / 3.0); // 60°-120° from zenith
        float dx    = MathF.Sin(phi) * MathF.Cos(theta);
        float dy    = MathF.Sin(phi) * MathF.Sin(theta);
        float dz    = MathF.Cos(phi);

        // Pick a random "up" vector in the arc plane (perpendicular to d).
        // Rotate the natural Perp(d) by a random roll so the crescent is not always
        // oriented the same way in its plane.
        (float px, float py, float pz) = Perp(dx, dy, dz);
        float qx = dy * pz - dz * py;
        float qy = dz * px - dx * pz;
        float qz = dx * py - dy * px;

        float roll = (float)(rng.NextDouble() * Math.PI * 2.0);
        float ux   = MathF.Cos(roll) * px + MathF.Sin(roll) * qx;
        float uy   = MathF.Cos(roll) * py + MathF.Sin(roll) * qy;
        float uz   = MathF.Cos(roll) * pz + MathF.Sin(roll) * qz;

        float edgeDist  = state.VanillaRadius * state.Scale;
        float bufferSq  = BufferRadius * BufferRadius;
        float halfArc   = ArcAngle * 0.5f;

        var placedPositions = new List<P3Float>(AsteroidCount);
        int placedCount = 0;

        for (int i = 0; i < AsteroidCount; i++)
        {
            // Distribute evenly from -halfArc to +halfArc, then jitter.
            float a       = -halfArc + ArcAngle * i / (AsteroidCount - 1)
                            + (float)(rng.NextDouble() * 2.0 - 1.0) * AngularJitter;
            float scatter = (float)(rng.NextDouble() * 2.0 - 1.0) * RadialScatter;
            float r       = edgeDist + scatter;

            float x = r * (MathF.Cos(a) * dx + MathF.Sin(a) * ux);
            float y = r * (MathF.Cos(a) * dy + MathF.Sin(a) * uy);
            float z = r * (MathF.Cos(a) * dz + MathF.Sin(a) * uz);

            // Skip if within BufferRadius of any already-placed belt asteroid.
            bool tooClose = false;
            foreach (var pp in placedPositions)
            {
                float ex = x - pp.X, ey = y - pp.Y, ez = z - pp.Z;
                if (ex * ex + ey * ey + ez * ez < bufferSq) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Half-cosine scale curve: MaxScale at centre (a=0), MinScale at tips.
            // Clamp t in case jitter pushed a outside the arc bounds.
            float t     = Math.Clamp(a / halfArc, -1f, 1f);   // -1 → +1 across the arc
            float scale = MinScale + (MaxScale - MinScale) * MathF.Cos(t * MathF.PI * 0.5f);
            scale      *= 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * ScaleNoise;
            scale       = MathF.Max(scale, MinScale * 0.5f);   // prevent degenerate tiny values

            float rx = (float)(rng.NextDouble() * Math.PI * 2.0);
            float ry = (float)(rng.NextDouble() * Math.PI * 2.0);
            float rz = (float)(rng.NextDouble() * Math.PI * 2.0);

            var baseKey = state.AsteroidPalette[rng.Next(state.AsteroidPalette.Count)];
            var placed  = new PlacedObject(targetMod)
            {
                Position = new P3Float(x, y, z),
                Rotation = new P3Float(rx, ry, rz),
                Scale    = scale,
            };
            placed.Base.SetTo(baseKey);

            state.Cell.Temporary.Add(placed);
            placedPositions.Add(new P3Float(x, y, z));
            placedCount++;
        }

        Console.WriteLine(
            $"[CrescentBeltPass] Placed {placedCount}/{AsteroidCount} asteroids " +
            $"arc={ArcAngle * 180f / MathF.PI:F0}° edge={edgeDist:F0} " +
            $"scale={MinScale:F1}→{MaxScale:F1}.");
    }

    private static (float, float, float) Perp(float dx, float dy, float dz)
    {
        float px, py, pz;
        if (MathF.Abs(dz) < 0.9f) { px = -dy; py = dx;  pz = 0f; }
        else                        { px =  0f; py = -dz; pz = dy; }
        float len = MathF.Sqrt(px * px + py * py + pz * pz);
        return (px / len, py / len, pz / len);
    }
}
