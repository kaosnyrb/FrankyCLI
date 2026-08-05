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
/// a sphere of radius VanillaRadius * Scale. Asteroid size is largest at the
/// midpoint and tapers to the smallest at both tips, driven by
/// AsteroidPaletteHelper.SizeFromT — selecting Large at centre, Medium in the
/// mid-sections, and Small at the tips.
///
/// AngularJitter offsets each asteroid from its evenly-spaced slot, breaking
/// the mechanical regularity of the spacing. SizeNoise adds a ±15% scale
/// multiplier on top so adjacent asteroids vary naturally in size.
///
/// RadialScatter gives the belt depth along the sphere surface.
/// PlaneScatter adds out-of-plane scatter perpendicular to the arc disk,
/// turning the flat arc into a volumetric 3D cloud.
/// A BufferRadius prevents adjacent asteroids from overlapping.
/// </summary>
public class CrescentBeltPass : ISpaceCellPass
{
    // Probability this pass fires for any given space cell, 0..1.
    // The gate below is `NextDouble() >= CrescentChance`, and NextDouble() returns [0,1),
    // so 1.0 already means "always". This was 1.35f, which behaved identically but read as
    // a tunable 135% — the danger being that dialling it "back a bit" to 0.9 looks like a
    // third off and is actually a tenth. The belt is this design's main feature and is meant
    // to fire every time; say that with 1.0 rather than with an out-of-range number.
    private const float CrescentChance = 1.0f;

    // Total angular span of the crescent in radians (150°).
    private const float ArcAngle = 2.618f;

    // Number of asteroids distributed along the arc.
    private const int AsteroidCount = 50;

    // Max radial offset from the edge sphere — gives the belt depth along the sphere.
    private const float RadialScatter = 400f;

    // Max scatter perpendicular to the arc plane — lifts the belt into a 3D cloud
    // rather than a flat arc lying in a single disk.
    private const float PlaneScatter = 350f;

    // Minimum distance between any two placed belt asteroids.
    private const float BufferRadius = 60f;

    // Max random angular offset from each evenly-spaced arc slot (radians, ~8.6°).
    // Higher value creates natural density clumping rather than even bead-on-string spacing.
    private const float AngularJitter = 0.15f;

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
        var groups    = AsteroidPaletteHelper.GroupBySize(
            state.AsteroidPalette, RetrogradeContext.Current.StarfieldMod);

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

        // Normal to the arc plane (d × u). Scatter along this axis lifts the
        // belt out of its plane so it reads as a volumetric cloud rather than
        // a drawn arc. Derived analytically: d × u = cos(roll)*q - sin(roll)*p.
        float wx = MathF.Cos(roll) * qx - MathF.Sin(roll) * px;
        float wy = MathF.Cos(roll) * qy - MathF.Sin(roll) * py;
        float wz = MathF.Cos(roll) * qz - MathF.Sin(roll) * pz;

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
            float scatter    = (float)(rng.NextDouble() * 2.0 - 1.0) * RadialScatter;
            float planeScat  = (float)(rng.NextDouble() * 2.0 - 1.0) * PlaneScatter;
            float r          = edgeDist + scatter;

            float x = r * (MathF.Cos(a) * dx + MathF.Sin(a) * ux) + wx * planeScat;
            float y = r * (MathF.Cos(a) * dy + MathF.Sin(a) * uy) + wy * planeScat;
            float z = r * (MathF.Cos(a) * dz + MathF.Sin(a) * uz) + wz * planeScat;

            // Skip if within BufferRadius of any already-placed belt asteroid.
            bool tooClose = false;
            foreach (var pp in placedPositions)
            {
                float ex = x - pp.X, ey = y - pp.Y, ez = z - pp.Z;
                if (ex * ex + ey * ey + ez * ez < bufferSq) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Size taper: Large at centre (|t|=0), Small at tips (|t|=1).
            // Clamp t in case jitter pushed a outside the arc bounds.
            float t           = Math.Clamp(a / halfArc, -1f, 1f);
            AsteroidSize size = AsteroidPaletteHelper.SizeFromT(MathF.Abs(t));

            var baseKey = AsteroidPaletteHelper.Pick(groups, size, rng);
            if (baseKey == FormKey.Null) continue;

            float rx = (float)(rng.NextDouble() * Math.PI * 2.0);
            float ry = (float)(rng.NextDouble() * Math.PI * 2.0);
            float rz = (float)(rng.NextDouble() * Math.PI * 2.0);

            var placed = new PlacedObject(targetMod)
            {
                Position = new P3Float(x, y, z),
                Rotation = new P3Float(rx, ry, rz),
                Scale    = AsteroidPaletteHelper.SizeNoise(rng, state.AsteroidScale),
            };
            placed.XALG = 8uL;
            placed.Base.SetTo(baseKey);

            state.Cell.Temporary.Add(placed);
            placedPositions.Add(new P3Float(x, y, z));
            placedCount++;
        }

        Console.WriteLine(
            $"[CrescentBeltPass] Placed {placedCount}/{AsteroidCount} asteroids " +
            $"arc={ArcAngle * 180f / MathF.PI:F0}° edge={edgeDist:F0}.");
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
