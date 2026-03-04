using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Randomly places a comet-tail streamer through the cell.
///
/// The comet consists of:
///   - A dense "coma" cluster of XLarge asteroids at the head
///   - A long streaming tail that fans out and shrinks away from the head
///
/// The tail is modelled as a sinusoidally-wandering parabolic arc. A slow wave
/// in the p–q plane makes the tail axis gently drift side-to-side so it reads as
/// a natural stream rather than a ruler-straight tube. On top of that, a parabolic
/// bend curves the whole stream as if deflected by a stellar wind. Perpendicular
/// scatter grows from tight (HeadScatter) to diffuse (TailScatter), giving the
/// tail a natural conical spread.
///
/// TJitter offsets each asteroid from its evenly-spaced t-slot so density
/// varies along the tail (clumps and gaps) rather than being perfectly uniform.
/// SizeNoise adds ±15% variation within each size class.
/// </summary>
public class CometTailPass : ISpaceCellPass
{
    // Probability this pass fires for any given space cell.
    private const float CometChance = 0.45f;

    // Asteroids distributed along the streaming tail.
    private const int TailCount = 80;

    // Extra asteroids packed into the dense coma at the head.
    private const int HeadCount = 15;

    // Tail length as a fraction of the edge distance (VanillaRadius * Scale).
    private const float TailLengthFraction = 0.8f;

    // Parabolic bend of the tail as a fraction of edge distance.
    // The tail curves in a fixed perpendicular direction (solar wind deflection).
    private const float CurvatureFraction = 0.12f;

    // Perpendicular scatter width at the head (tight coma).
    private const float HeadScatter = 80f;

    // Perpendicular scatter width at the tail tip (wide, diffuse).
    private const float TailScatter = 350f;

    // Minimum distance between any two placed asteroids.
    private const float BufferRadius = 25f;

    // Max random offset added to each asteroid's evenly-spaced t-slot (0–1 range).
    // Each slot is ~0.013 wide, so ±0.02 is about 1.5 slots of wiggle.
    private const float TJitter = 0.02f;

    // Sinusoidal wander of the tail axis — makes the stream drift side-to-side
    // rather than running in a perfectly straight line.
    // WaveAmplitude: max lateral offset of the axis centre (units).
    // WaveFrequency: full oscillations over the tail length (p and q use coprime
    //                frequencies so the 3D path never forms a flat figure-8).
    private const float WaveAmplitude  = 160f;
    private const float WaveFrequencyP = 1.5f;   // oscillations in p over full tail
    private const float WaveFrequencyQ = 1.1f;   // oscillations in q over full tail

    public void RunPass(SpaceCellState state)
    {
        if (state.AsteroidPalette == null || state.AsteroidPalette.Count == 0)
        {
            Console.WriteLine("[CometTailPass] No asteroid palette — skipping.");
            return;
        }

        var rng = RandomProvider.Random;

        if ((float)rng.NextDouble() >= CometChance)
        {
            Console.WriteLine("[CometTailPass] Skipped (chance roll).");
            return;
        }

        var targetMod = RetrogradeContext.Current.TargetMod;
        var groups    = AsteroidPaletteHelper.GroupBySize(
            state.AsteroidPalette, RetrogradeContext.Current.StarfieldMod);

        // ── Comet travel direction (tail streams in the -d direction) ──────────
        // Uniform sphere distribution — comet can travel in any direction.
        float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
        float phi   = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
        float dx    = MathF.Sin(phi) * MathF.Cos(theta);
        float dy    = MathF.Sin(phi) * MathF.Sin(theta);
        float dz    = MathF.Cos(phi);

        // Two perpendicular vectors spanning the scatter / bend plane.
        (float px, float py, float pz) = Perp(dx, dy, dz);
        float qx = dy * pz - dz * py;
        float qy = dz * px - dx * pz;
        float qz = dx * py - dy * px;

        // ── Head position: inner portion of the cell ───────────────────────────
        // Placed within 20% of edgeDist from the origin so the coma is
        // near the player spawn point and the tail sweeps past them.
        float edgeDist = state.VanillaRadius * state.Scale;
        float headR    = edgeDist * 0.2f * (float)rng.NextDouble();
        float ht       = (float)(rng.NextDouble() * Math.PI * 2.0);
        float hp       = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
        float hx       = headR * MathF.Sin(hp) * MathF.Cos(ht);
        float hy       = headR * MathF.Sin(hp) * MathF.Sin(ht);
        float hz       = headR * MathF.Cos(hp);

        float tailLength = edgeDist * TailLengthFraction;
        float curvature  = edgeDist * CurvatureFraction;
        float bufferSq   = BufferRadius * BufferRadius;

        var placedPositions = new List<P3Float>(TailCount + HeadCount);
        int placedCount = 0;

        // ── Tail: t = 0 at head, t = 1 at tail tip ────────────────────────────
        // Main axis:  -d  (tail streams backward from travel direction)
        // Wave:       sinusoidal drift of the axis in the p–q plane
        // Bend:       parabolic offset along p (grows as t²)
        // Scatter:    fans out in the p–q plane, width grows linearly with t
        for (int i = 0; i < TailCount; i++)
        {
            float t    = Math.Clamp((float)i / (TailCount - 1)
                         + (float)(rng.NextDouble() * 2.0 - 1.0) * TJitter, 0f, 1f);
            float dist = tailLength * t;
            float bend = curvature * t * t;

            // Slowly-varying sinusoidal wander of the axis centre.
            float waveP = WaveAmplitude * MathF.Sin(t * WaveFrequencyP * MathF.PI);
            float waveQ = WaveAmplitude * 0.55f * MathF.Cos(t * WaveFrequencyQ * MathF.PI);

            float scatterW = HeadScatter + (TailScatter - HeadScatter) * t;
            float sA = (float)(rng.NextDouble() * 2.0 - 1.0) * scatterW;
            float sB = (float)(rng.NextDouble() * 2.0 - 1.0) * scatterW;

            float x = hx - dx * dist + px * (bend + waveP + sA) + qx * (waveQ + sB);
            float y = hy - dy * dist + py * (bend + waveP + sA) + qy * (waveQ + sB);
            float z = hz - dz * dist + pz * (bend + waveP + sA) + qz * (waveQ + sB);

            bool tooClose = false;
            foreach (var pp in placedPositions)
            {
                float ex = x - pp.X, ey = y - pp.Y, ez = z - pp.Z;
                if (ex * ex + ey * ey + ez * ez < bufferSq) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Size tapers from Large at head to Small at tip.
            AsteroidSize size = AsteroidPaletteHelper.SizeFromT(t);
            var baseKey = AsteroidPaletteHelper.Pick(groups, size, rng);
            if (baseKey == FormKey.Null) continue;

            Place(targetMod, state.Cell, baseKey, x, y, z, rng, state.AsteroidScale);
            placedPositions.Add(new P3Float(x, y, z));
            placedCount++;
        }

        // ── Coma: dense cluster of XLarge asteroids packed at the head ─────────
        for (int i = 0; i < HeadCount; i++)
        {
            float sA = (float)(rng.NextDouble() * 2.0 - 1.0) * HeadScatter;
            float sB = (float)(rng.NextDouble() * 2.0 - 1.0) * HeadScatter;
            float sC = (float)(rng.NextDouble() * 2.0 - 1.0) * HeadScatter;

            float x = hx + sA;
            float y = hy + sB;
            float z = hz + sC;

            bool tooClose = false;
            foreach (var pp in placedPositions)
            {
                float ex = x - pp.X, ey = y - pp.Y, ez = z - pp.Z;
                if (ex * ex + ey * ey + ez * ez < bufferSq) { tooClose = true; break; }
            }
            if (tooClose) continue;

            var baseKey = AsteroidPaletteHelper.Pick(groups, AsteroidSize.XLarge, rng);
            if (baseKey == FormKey.Null) continue;

            Place(targetMod, state.Cell, baseKey, x, y, z, rng, state.AsteroidScale);
            placedPositions.Add(new P3Float(x, y, z));
            placedCount++;
        }

        Console.WriteLine(
            $"[CometTailPass] Placed {placedCount}/{TailCount + HeadCount} asteroids " +
            $"tailLen={tailLength:F0} curve={curvature:F0}.");
    }

    private static void Place(StarfieldMod targetMod, Cell cell, FormKey baseKey,
                               float x, float y, float z, Random rng, float asteroidScale)
    {
        float rx = (float)(rng.NextDouble() * Math.PI * 2.0);
        float ry = (float)(rng.NextDouble() * Math.PI * 2.0);
        float rz = (float)(rng.NextDouble() * Math.PI * 2.0);
        var placed = new PlacedObject(targetMod)
        {
            Position = new P3Float(x, y, z),
            Rotation = new P3Float(rx, ry, rz),
            Scale    = AsteroidPaletteHelper.SizeNoise(rng, asteroidScale),
        };
        placed.XALG = 8uL;
        placed.Base.SetTo(baseKey);
        cell.Temporary.Add(placed);
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
