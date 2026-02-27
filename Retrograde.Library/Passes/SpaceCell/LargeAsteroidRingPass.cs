using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Randomly places a single oversized asteroid on the outer edge of the cell,
/// biased toward the equatorial plane so it's visible from the player spawn point.
///
/// The hero asteroid is chosen from the XXLarge size bucket so its mass is
/// apparent from the model itself — no scale inflation needed.
/// It is surrounded by a tilted disk of Small ring asteroids. Each ring asteroid
/// is slightly displaced off the disk plane (the "wobble"), giving the ring a
/// natural three-dimensional look.
/// </summary>
public class LargeAsteroidRingPass : ISpaceCellPass
{
    // Probability this pass fires for any given space cell.
    private const float RingChance = 0.4f;

    // Number of asteroids in the orbiting ring.
    private const int RingCount = 30;

    // Distance from the hero asteroid centre to the ring asteroids.
    private const float RingRadius = 400f;

    // Max displacement of each ring asteroid perpendicular to the disk plane.
    private const float WobbleRange = 80f;

    // Max additional radial scatter per ring asteroid (thickens the ring slightly).
    private const float RadialScatter = 50f;

    // Minimum distance from the hero asteroid centre to any ring asteroid.
    // Prevents ring asteroids from visually intersecting the hero.
    private const float BufferRadius = 200f;

    public void RunPass(SpaceCellState state)
    {
        if (state.AsteroidPalette == null || state.AsteroidPalette.Count == 0)
        {
            Console.WriteLine("[LargeAsteroidRingPass] No asteroid palette — skipping.");
            return;
        }

        var rng = RandomProvider.Random;

        if ((float)rng.NextDouble() >= RingChance)
        {
            Console.WriteLine("[LargeAsteroidRingPass] Skipped (chance roll).");
            return;
        }

        var targetMod = RetrogradeContext.Current.TargetMod;
        var groups    = AsteroidPaletteHelper.GroupBySize(
            state.AsteroidPalette, RetrogradeContext.Current.StarfieldMod);

        // ── Pick direction biased toward the equatorial plane ─────────────────────
        // phi ranges 60°–120° from zenith so the asteroid sits in front of the
        // player rather than directly above or below the spawn point.
        float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
        float phi   = (float)(Math.PI / 3.0 + rng.NextDouble() * Math.PI / 3.0);
        float dx    = MathF.Sin(phi) * MathF.Cos(theta);
        float dy    = MathF.Sin(phi) * MathF.Sin(theta);
        float dz    = MathF.Cos(phi);

        // ── Place hero asteroid at the outer edge ─────────────────────────────────
        float edgeDist = state.VanillaRadius * state.Scale;
        float ax = dx * edgeDist;
        float ay = dy * edgeDist;
        float az = dz * edgeDist;

        var heroKey = AsteroidPaletteHelper.Pick(groups, AsteroidSize.XXLarge, rng);
        if (heroKey != FormKey.Null)
            PlaceAsteroid(targetMod, state.Cell, heroKey, ax, ay, az, rng);

        // ── Build ring disk ───────────────────────────────────────────────────────
        // Pick a random disk normal (the plane the ring lies in).
        float nt = (float)(rng.NextDouble() * Math.PI * 2.0);
        float np = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
        float nx = MathF.Sin(np) * MathF.Cos(nt);
        float ny = MathF.Sin(np) * MathF.Sin(nt);
        float nz = MathF.Cos(np);

        // Two orthonormal vectors spanning the disk plane.
        (float ux, float uy, float uz) = Perp(nx, ny, nz);
        float vx = ny * uz - nz * uy;
        float vy = nz * ux - nx * uz;
        float vz = nx * uy - ny * ux;

        for (int i = 0; i < RingCount; i++)
        {
            float angle  = (float)(i * Math.PI * 2.0 / RingCount);
            float cos    = MathF.Cos(angle);
            float sin    = MathF.Sin(angle);
            float ringR  = MathF.Max(BufferRadius, RingRadius + (float)(rng.NextDouble() * 2.0 - 1.0) * RadialScatter);
            float wobble = (float)(rng.NextDouble() * 2.0 - 1.0) * WobbleRange;

            float rx = ax + ringR * (cos * ux + sin * vx) + wobble * nx;
            float ry = ay + ringR * (cos * uy + sin * vy) + wobble * ny;
            float rz = az + ringR * (cos * uz + sin * vz) + wobble * nz;

            var ringKey = AsteroidPaletteHelper.Pick(groups, AsteroidSize.Small, rng);
            if (ringKey != FormKey.Null)
                PlaceAsteroid(targetMod, state.Cell, ringKey, rx, ry, rz, rng);
        }

        Console.WriteLine(
            $"[LargeAsteroidRingPass] Hero asteroid at ({ax:F0},{ay:F0},{az:F0}), " +
            $"edge dist={edgeDist:F0}, ring={RingCount} asteroids.");
    }

    private static void PlaceAsteroid(StarfieldMod targetMod, Cell cell, FormKey baseKey,
                                      float x, float y, float z, Random rng)
    {
        float rx = (float)(rng.NextDouble() * Math.PI * 2.0);
        float ry = (float)(rng.NextDouble() * Math.PI * 2.0);
        float rz = (float)(rng.NextDouble() * Math.PI * 2.0);

        var placed = new PlacedObject(targetMod)
        {
            Position = new P3Float(x, y, z),
            Rotation = new P3Float(rx, ry, rz),
            Scale    = AsteroidPaletteHelper.SizeNoise(rng),
        };
        placed.XALG = 8uL;
        placed.Base.SetTo(baseKey);

        cell.Temporary.Add(placed);
    }

    // Returns a unit vector perpendicular to (dx, dy, dz).
    private static (float, float, float) Perp(float dx, float dy, float dz)
    {
        float px, py, pz;
        if (MathF.Abs(dz) < 0.9f) { px = -dy; py = dx;  pz = 0f; }
        else                        { px =  0f; py = -dz; pz = dy; }
        float len = MathF.Sqrt(px * px + py * py + pz * pz);
        return (px / len, py / len, pz / len);
    }
}
