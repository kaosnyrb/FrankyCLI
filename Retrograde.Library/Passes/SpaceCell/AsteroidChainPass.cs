using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Places a long chain of asteroids that passes through the Origin Point
/// (se_CenterMarker01 — where the player's ship spawns).
///
/// The chain is centred on the origin so asteroids appear on both sides of
/// the player. It extends in a random direction with perpendicular scatter,
/// covering approximately twice the area of the vanilla source cell
/// (achieved via the Scale factor in SpaceCellState).
/// </summary>
public class AsteroidChainPass : ISpaceCellPass
{
    // Number of asteroids in the chain — enough to feel dense over the full length.
    private const int AsteroidCount = 150;

    // Scatter as a fraction of the vanilla radius — asteroids spread sideways.
    private const float ScatterFraction = 0.1f;

    // Per-asteroid scale variation: base ± half this value.
    private const float ScaleVariation = 2f;

    // Minimum distance between any two placed asteroids. Candidates closer than
    // this to an already-placed asteroid are skipped.
    private const float BufferRadius = 30f;

    public void RunPass(SpaceCellState state)
    {
        if (state.AsteroidPalette == null || state.AsteroidPalette.Count == 0)
        {
            Console.WriteLine("[AsteroidChainPass] No asteroid palette — skipping.");
            return;
        }

        var rng        = RandomProvider.Random;
        var targetMod  = RetrogradeContext.Current.TargetMod;

        // Pick a random unit direction vector (uniform distribution on the sphere).
        float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
        float phi   = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
        float dx    = MathF.Sin(phi) * MathF.Cos(theta);
        float dy    = MathF.Sin(phi) * MathF.Sin(theta);
        float dz    = MathF.Cos(phi);

        // Build two perpendicular vectors for scatter.
        (float px, float py, float pz) = Perp(dx, dy, dz);
        float qx = dy * pz - dz * py;
        float qy = dz * px - dx * pz;
        float qz = dx * py - dy * px;

        float chainLength  = state.VanillaRadius * state.Scale;
        float scatterRange = state.VanillaRadius * ScatterFraction;

        Console.WriteLine($"[AsteroidChainPass] dir=({dx:F2},{dy:F2},{dz:F2}) " +
                          $"chainLength={chainLength:F0} scatter={scatterRange:F0} " +
                          $"count={AsteroidCount} palette={state.AsteroidPalette.Count}");

        float bufferSq = BufferRadius * BufferRadius;
        var placedPositions = new List<P3Float>(AsteroidCount);
        int placedCount = 0;

        for (int i = 0; i < AsteroidCount; i++)
        {
            // Centre the chain on the origin so it passes through the player spawn point.
            // t runs -0.5 → +0.5, dist runs -chainLength/2 → +chainLength/2.
            float t      = (float)i / (AsteroidCount - 1) - 0.5f;
            float dist   = chainLength * t;

            // Perpendicular scatter.
            float sA = (float)(rng.NextDouble() * 2.0 - 1.0) * scatterRange;
            float sB = (float)(rng.NextDouble() * 2.0 - 1.0) * scatterRange;

            float x = dx * dist + px * sA + qx * sB;
            float y = dy * dist + py * sA + qy * sB;
            float z = dz * dist + pz * sA + qz * sB;

            // Skip if within BufferRadius of any already-placed asteroid.
            bool tooClose = false;
            foreach (var pp in placedPositions)
            {
                float ex = x - pp.X, ey = y - pp.Y, ez = z - pp.Z;
                if (ex * ex + ey * ey + ez * ez < bufferSq) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Random rotation (full 360° on each axis, in radians).
            float rx = (float)(rng.NextDouble() * Math.PI * 2.0);
            float ry = (float)(rng.NextDouble() * Math.PI * 2.0);
            float rz = (float)(rng.NextDouble() * Math.PI * 2.0);

            // Random scale.
            float scale = 1.0f + (float)(rng.NextDouble() - 0.5) * ScaleVariation;

            // Pick a random asteroid from the palette.
            var baseKey = state.AsteroidPalette[rng.Next(state.AsteroidPalette.Count)];

            var placed = new PlacedObject(targetMod)
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

        Console.WriteLine($"[AsteroidChainPass] Placed {placedCount}/{AsteroidCount} asteroids " +
                          $"(buffer={BufferRadius}).");
    }

    // Returns a unit vector perpendicular to (dx, dy, dz).
    private static (float, float, float) Perp(float dx, float dy, float dz)
    {
        // Cross with world-up (0,0,1) unless direction is nearly parallel to it.
        float px, py, pz;
        if (MathF.Abs(dz) < 0.9f)
        {
            px = -dy; py = dx; pz = 0f;
        }
        else
        {
            px = 0f; py = -dz; pz = dy;
        }
        float len = MathF.Sqrt(px * px + py * py + pz * pz);
        return (px / len, py / len, pz / len);
    }
}
