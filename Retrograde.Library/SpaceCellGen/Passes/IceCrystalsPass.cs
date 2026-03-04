using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Scatters snowflake-like ice crystal formations throughout the space cell.
///
/// Each crystal consists of 20 large shards arranged at the vertices of a regular
/// dodecahedron — all equidistant from the crystal centre, giving perfect icosahedral
/// symmetry. Each shard is rotated so its local +Z axis points radially outward from
/// the crystal centre, like spines on a sea urchin.
///
/// Each crystal receives a random global orientation so no two look identical from
/// any viewing angle.
/// </summary>
public class IceCrystalsPass : ISpaceCellPass
{
    private const int MinCrystals = 8;
    private const int MaxCrystals = 16;

    private readonly float _yawOffsetRad;

    /// <param name="yawOffsetDegrees">Extra Z-axis yaw applied to every shard (degrees).</param>
    public IceCrystalsPass(float yawOffsetDegrees = 0f)
    {
        _yawOffsetRad = yawOffsetDegrees * MathF.PI / 180f;
    }

    // Radius of each crystal sphere, in world units, relative to AsteroidScale.
    // At IceShard AsteroidScale=20 this gives 600 units — close but visually distinct.
    private const float CrystalRadiusPerScale = 30f;

    // Crystal centres are placed within this fraction of the scaled cell radius.
    private const float PlacementFraction = 1f;

    // 20 unit-sphere directions at the vertices of a regular dodecahedron.
    // Vertices group as: 8 cube corners (±n,±n,±n) + 4 in each coordinate plane.
    // All vertices are at distance √3 from the origin, so dividing by √3 normalises them.
    private static readonly (float X, float Y, float Z)[] DodecahedronDirs = BuildDirs();

    public void RunPass(SpaceCellState state)
    {
        if (state.AsteroidPalette == null || state.AsteroidPalette.Count == 0)
        {
            Console.WriteLine("[IceCrystalsPass] No asteroid palette — skipping.");
            return;
        }

        var rng       = RandomProvider.Random;
        var targetMod = RetrogradeContext.Current.TargetMod;
        var groups    = AsteroidPaletteHelper.GroupBySize(
            state.AsteroidPalette, RetrogradeContext.Current.StarfieldMod);

        int   crystalCount     = rng.Next(MinCrystals, MaxCrystals + 1);
        float placementRadius  = state.VanillaRadius * state.Scale * PlacementFraction;
        float crystalRadius    = CrystalRadiusPerScale * state.AsteroidScale;
        int   totalPlaced      = 0;

        for (int c = 0; c < crystalCount; c++)
        {
            // ── Random crystal centre ────────────────────────────────────────────
            float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
            float phi   = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
            float dist  = (float)(rng.NextDouble() * placementRadius);
            float cx    = dist * MathF.Sin(phi) * MathF.Cos(theta);
            float cy    = dist * MathF.Sin(phi) * MathF.Sin(theta);
            float cz    = dist * MathF.Cos(phi);

            // ── Random global orientation for this crystal ───────────────────────
            float grx = (float)(rng.NextDouble() * Math.PI * 2.0);
            float gry = (float)(rng.NextDouble() * Math.PI * 2.0);
            float grz = (float)(rng.NextDouble() * Math.PI * 2.0);

            foreach (var dir in DodecahedronDirs)
            {
                // Apply the crystal's global rotation so each crystal is unique.
                (float dx, float dy, float dz) = RotateVec(dir.X, dir.Y, dir.Z, grx, gry, grz);

                float px = cx;
                float py = cy;
                float pz = cz;

                // Align model +Z with the outward direction (dx, dy, dz).
                // Bethesda intrinsic XYZ: RotX=0 (no roll), RotY=polar, RotZ=azimuth.
                float rotY = MathF.Acos(Math.Clamp(dz, -1f, 1f));    // polar tilt from +Z
                float rotZ = MathF.Atan2(dy, dx);                      // azimuth around Z

                var key = AsteroidPaletteHelper.Pick(groups, AsteroidSize.XXLarge, rng);
                if (key == FormKey.Null) continue;

                var placed = new PlacedObject(targetMod)
                {
                    Position = new P3Float(px, py, pz),
                    Rotation = new P3Float(0f, rotY, rotZ + _yawOffsetRad),
                    Scale    = AsteroidPaletteHelper.SizeNoise(rng, state.AsteroidScale) * 5f,
                };
                placed.XALG = 8uL;
                placed.Base.SetTo(key);

                state.Cell.Temporary.Add(placed);
                totalPlaced++;
            }
        }

        Console.WriteLine(
            $"[IceCrystalsPass] {crystalCount} crystals × 20 shards = {totalPlaced} placed, " +
            $"crystalRadius={crystalRadius:F0}, placementRadius={placementRadius:F0}");
    }

    // Rotates vector (vx, vy, vz) by extrinsic ZYX = intrinsic XYZ Euler angles.
    private static (float, float, float) RotateVec(
        float vx, float vy, float vz, float rx, float ry, float rz)
    {
        // Rx
        float y1 = vy * MathF.Cos(rx) - vz * MathF.Sin(rx);
        float z1 = vy * MathF.Sin(rx) + vz * MathF.Cos(rx);
        // Ry
        float x2 =  vx * MathF.Cos(ry) + z1 * MathF.Sin(ry);
        float z2 = -vx * MathF.Sin(ry) + z1 * MathF.Cos(ry);
        // Rz
        float x3 = x2 * MathF.Cos(rz) - y1 * MathF.Sin(rz);
        float y3 = x2 * MathF.Sin(rz) + y1 * MathF.Cos(rz);
        return (x3, y3, z2);
    }

    // Builds the 20 normalised dodecahedron vertex directions.
    // All raw vertices lie at distance √3 from the origin; dividing by √3 normalises them.
    //   n = 1/√3          (cube-corner component)
    //   a = φ/√3          (long component, φ = golden ratio)
    //   b = 1/(φ·√3)      (short component)
    private static (float, float, float)[] BuildDirs()
    {
        float sqrt3 = MathF.Sqrt(3f);
        float phi   = (1f + MathF.Sqrt(5f)) / 2f;
        float n     = 1f / sqrt3;
        float a     = phi / sqrt3;
        float b     = 1f / (phi * sqrt3);

        return new (float, float, float)[]
        {
            // 8 cube-corner vertices (±n, ±n, ±n)
            ( n,  n,  n), ( n,  n, -n), ( n, -n,  n), ( n, -n, -n),
            (-n,  n,  n), (-n,  n, -n), (-n, -n,  n), (-n, -n, -n),
            // 4 vertices in the YZ plane  (0, ±a, ±b)
            (0f,  a,  b), (0f,  a, -b), (0f, -a,  b), (0f, -a, -b),
            // 4 vertices in the XZ plane  (±b, 0, ±a)
            ( b, 0f,  a), (-b, 0f,  a), ( b, 0f, -a), (-b, 0f, -a),
            // 4 vertices in the XY plane  (±a, ±b, 0)
            ( a,  b, 0f), ( a, -b, 0f), (-a,  b, 0f), (-a, -b, 0f),
        };
    }
}
