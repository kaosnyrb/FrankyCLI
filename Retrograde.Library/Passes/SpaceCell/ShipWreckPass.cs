using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Randomly places a shipwreck cluster near the origin (player spawn point).
///
/// The wreck consists of:
///   - 1-3 engines (SMOD_Eng_*)           — placed around the cluster centre
///   - 1-3 cargo modules (SMOD_Cargo_*)   — placed around the cluster centre
///
/// All parts are drawn from vanilla Starfield.esm MoveableStatic ship module records,
/// so they tumble and float just like asteroids.
/// </summary>
public class ShipWreckPass : ISpaceCellPass
{
    // Probability this pass fires for any given space cell.
    // Set to 0 to disable, 1 to always fire.
    private const float WreckChance = 0.4f;

    // Maximum distance the cluster centre can be offset from the origin (player spawn).
    private const float ClusterRadius = 300f;

    // Maximum distance each engine/cargo part can be offset from the cockpit.
    private const float PartScatter = 200f;

    // ── Engine palette (SMOD_Eng_* MoveableStatics, all Starfield.esm) ───────────
    private static readonly uint[] EngineIds =
    {
        0x030214u, // SMOD_Eng_Reladyne_WhiteDwarf-2000_Port
        0x030215u, // SMOD_Eng_Reladyne_WhiteDwarf-2000_Stbd
        0x059B30u, // SMOD_Eng_Amun_Amun-11
        0x17AD04u, // SMOD_Eng_Reladyne_WhiteDwarf-3000
        0x17AD05u, // SMOD_Eng_Reladyne_WhiteDwarf-1000_Port
        0x17AD06u, // SMOD_Eng_Reladyne_SuperNova-2000
        0x17AD07u, // SMOD_Eng_Amun_Dunn-41
        0x17AD08u, // SMOD_Eng_Reladyne_WhiteDwarf-1000_Stbd
        0x1A1D8Du, // SMOD_Eng_Slayton_SAL-6110
        0x1A9B02u, // SMOD_Eng_Amun_Amun-1
        0x224826u, // SMOD_Eng_Reladyne_Nova-1000_Stb
        0x224828u, // SMOD_Eng_Reladyne_Nova-1000_Port
        0x236258u, // SMOD_Eng_Amun_AmunDunn-X-100
        0x2392D4u, // SMOD_Eng_Panoptes_DT10_Stb
        0x2392D6u, // SMOD_Eng_Panoptes_DT10_Port
    };

    // ── Cargo palette (SMOD_Cargo_* MoveableStatics, all Starfield.esm) ──────────
    private static readonly uint[] CargoIds =
    {
        0x030207u, // SMOD_Cargo_Sextant_100CM
        0x059B2Au, // SMOD_Cargo_Protectorate_Caravel-V101_Port
        0x165553u, // SMOD_Cargo_Dogstar_StorMax-40_Stbd
        0x167B0Eu, // SMOD_Cargo_Protectorate_Caravel-V101_Stbd
        0x17AB20u, // SMOD_Cargo_Sextant_10T-Hauler_Fore
        0x17AD0Au, // SMOD_Cargo_Sextant_200CM_Ballast_Stbd
        0x17AD0Bu, // SMOD_Cargo_Sextant_20-THauler
        0x17AD0Cu, // SMOD_Cargo_Sextant_10T-Hauler_Aft
        0x17AD0Du, // SMOD_Cargo_Protectorate_Caravel-V102_Stbd
        0x17AD0Eu, // SMOD_Cargo_Protectorate_Caravel-V102_Port
        0x17AD0Fu, // SMOD_Cargo_Protectorate_Caravel-V103_Port
        0x17AD10u, // SMOD_Cargo_Protectorate_Caravel-V103_Stbd
        0x17AD11u, // SMOD_Cargo_Protectorate_Caravel-V104_Port
    };

    public void RunPass(SpaceCellState state)
    {
        var rng = RandomProvider.Random;

        if ((float)rng.NextDouble() >= WreckChance)
        {
            Console.WriteLine("[ShipWreckPass] Skipped (chance roll).");
            return;
        }

        var targetMod = RetrogradeContext.Current.TargetMod;
        var sfKey     = RetrogradeContext.Current.StarfieldModKey;

        // Offset the cluster centre from the origin by up to ClusterRadius in a random direction.
        float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
        float phi   = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
        float r     = (float)(rng.NextDouble() * ClusterRadius);
        float cx = r * MathF.Sin(phi) * MathF.Cos(theta);
        float cy = r * MathF.Sin(phi) * MathF.Sin(theta);
        float cz = r * MathF.Cos(phi);

        // ── Pick one variant from each palette (consistent "manufacturer" feel) ───
        var engineKey = new FormKey(sfKey, EngineIds[rng.Next(EngineIds.Length)]);
        var cargoKey  = new FormKey(sfKey, CargoIds[rng.Next(CargoIds.Length)]);

        // ── Place 1-3 engines scattered around the cluster centre ─────────────────
        int engineCount = rng.Next(1, 4);
        for (int i = 0; i < engineCount; i++)
        {
            (float sx, float sy, float sz) = RandomOffset(rng, PartScatter);
            Place(targetMod, state.Cell, engineKey, cx + sx, cy + sy, cz + sz, rng);
        }

        // ── Place 1-3 cargo modules scattered around the cluster centre ──────────
        int cargoCount = rng.Next(1, 4);
        for (int i = 0; i < cargoCount; i++)
        {
            (float sx, float sy, float sz) = RandomOffset(rng, PartScatter);
            Place(targetMod, state.Cell, cargoKey, cx + sx, cy + sy, cz + sz, rng);
        }

        Console.WriteLine(
            $"[ShipWreckPass] Placed wreck near ({cx:F0},{cy:F0},{cz:F0}): " +
            $"1 cockpit, {engineCount} engines, {cargoCount} cargo.");
    }

    private static void Place(StarfieldMod targetMod, Cell cell, FormKey baseKey,
                               float x, float y, float z, Random rng)
    {
        float rx    = (float)(rng.NextDouble() * Math.PI * 2.0);
        float ry    = (float)(rng.NextDouble() * Math.PI * 2.0);
        float rz    = (float)(rng.NextDouble() * Math.PI * 2.0);
        var placed = new PlacedObject(targetMod)
        {
            Position = new P3Float(x, y, z),
            Rotation = new P3Float(rx, ry, rz),
            Scale    = 2.0f,
        };
        // Ship parts should always be fixed — disable physics simulation.
        placed.XALG = 8uL;
        placed.Base.SetTo(baseKey);

        cell.Temporary.Add(placed);
    }

    private static (float, float, float) RandomOffset(Random rng, float maxRadius)
    {
        float t = (float)(rng.NextDouble() * Math.PI * 2.0);
        float p = (float)(Math.Acos(2.0 * rng.NextDouble() - 1.0));
        float r = (float)(rng.NextDouble() * maxRadius);
        return (
            r * MathF.Sin(p) * MathF.Cos(t),
            r * MathF.Sin(p) * MathF.Sin(t),
            r * MathF.Cos(p)
        );
    }
}
