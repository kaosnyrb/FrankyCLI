using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.RoomPackinGeneration;
using System;
using System.IO;
using System.Linq;

namespace FrankyCLI;

/// <summary>
/// Generates a set of Science Hallway PackIn variants based on patterns extracted from
/// du_outlaws_template.esm (rg_sts_trk_shl_001–006).
///
/// Usage: gen_roompackin
///
/// Output: generated_templates.esm in the Starfield Data folder.
/// The ESM contains PackIn records with interior cells that reference only Starfield.esm
/// base objects — no dependency on any template mod.
///
/// Generated variants:
///   Straight (S↔N):
///     rg_gen_sts_trk_shl_flat_short  — 8-tile flat corridor (no stairs)
///     rg_gen_sts_trk_shl_flat_med    — 10-tile flat corridor
///     rg_gen_sts_trk_shl_stair_low   — rising corridor, 2 stairs (+4 Z)
///     rg_gen_sts_trk_shl_stair_med   — rising corridor, 3 stairs (+6 Z, matches rg_sts_trk_shl_001)
///     rg_gen_sts_trk_shl_stair_tall  — rising corridor, 4 stairs (+8 Z)
///   SE corners (S↔E):
///     rg_gen_sts_trk_shl_se_min      — minimum bend (0 extra tiles each arm)
///     rg_gen_sts_trk_shl_se_sht      — short legs (1 extra tile each arm, ~ne_10x6y)
///     rg_gen_sts_trk_shl_se_lngy     — long Y arm (2 extra Y tiles, 0 extra X)
///     rg_gen_sts_trk_shl_se_lngx     — long X arm (0 extra Y, 2 extra X tiles)
///   SW corners (S↔W):
///     rg_gen_sts_trk_shl_sw_min      — minimum bend (0 extra tiles each arm)
///     rg_gen_sts_trk_shl_sw_sht      — short legs (1 extra tile each arm)
///     rg_gen_sts_trk_shl_sw_lngy     — long Y arm (2 extra Y tiles, 0 extra X)
///     rg_gen_sts_trk_shl_sw_lngx     — long X arm (0 extra Y, 2 extra X tiles)
///   Rooms (20×20, SciIntRmSm kit):
///     rg_gen_sts_trk_big_s           — S exit only (dead-end/cap room)
///     rg_gen_sts_trk_big_sn          — S+N (through room, N-S axis)
///     rg_gen_sts_trk_big_se          — S+E (corner room)
///     rg_gen_sts_trk_big_sw          — S+W (corner room)
///     rg_gen_sts_trk_big_sne         — S+N+E (T-junction)
///     rg_gen_sts_trk_big_snw         — S+N+W (T-junction)
///     rg_gen_sts_trk_big_sew         — S+E+W (T-junction)
///     rg_gen_sts_trk_big_snew        — S+N+E+W (4-way junction, matches big_001/002)
/// </summary>
public class gen_roompackin
{
    private const string OutputModName = "generated_templates";

    public static int Generate(string[] args)
    {
        Console.WriteLine("=== gen_roompackin: SCI Hallway PackIn Generator ===");
        Console.WriteLine();

        using var env = GameEnvironment.Typical
            .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
            .Build();

        // Populate MasterFlagsCache so BuildWriteParams() works correctly
        gen_quest_main.BuildReadParams(env.LoadOrder);

        var sfModKey = env.LoadOrder[0].ModKey; // Starfield.esm
        Console.WriteLine($"Starfield.esm: {sfModKey}");
        Console.WriteLine($"Data folder:   {env.DataFolderPath}");
        Console.WriteLine();

        // Create the output mod
        var outputModKey = new ModKey(OutputModName, ModType.Master);
        var outputMod    = new StarfieldMod(outputModKey, StarfieldRelease.Starfield);

        // Run the generator
        var gen = new SciHallwayGenerator(outputMod, sfModKey);

        // 8-tile flat corridor — same N and S at Z=0
        gen.Generate("rg_gen_sts_trk_shl_flat_short", flatTilesStart: 3, stairCount: 0, flatTilesEnd: 3);

        // 10-tile flat corridor — longer straight run
        gen.Generate("rg_gen_sts_trk_shl_flat_med", flatTilesStart: 4, stairCount: 0, flatTilesEnd: 4);

        // Rising corridor — 2 stairs, +4 units rise
        gen.Generate("rg_gen_sts_trk_shl_stair_low", flatTilesStart: 2, stairCount: 2, flatTilesEnd: 2);

        // Rising corridor — 3 stairs, +6 units rise  (mirrors rg_sts_trk_shl_001 layout)
        gen.Generate("rg_gen_sts_trk_shl_stair_med", flatTilesStart: 2, stairCount: 3, flatTilesEnd: 2);

        // Rising corridor — 4 stairs, +8 units rise
        gen.Generate("rg_gen_sts_trk_shl_stair_tall", flatTilesStart: 3, stairCount: 4, flatTilesEnd: 3);

        // SE corners — S entry, E exit
        gen.GenerateCorner("rg_gen_sts_trk_shl_se_min",  exitEast: true,  yStraight: 0, xStraight: 0);
        gen.GenerateCorner("rg_gen_sts_trk_shl_se_sht",  exitEast: true,  yStraight: 1, xStraight: 1);
        gen.GenerateCorner("rg_gen_sts_trk_shl_se_lngy", exitEast: true,  yStraight: 2, xStraight: 0);
        gen.GenerateCorner("rg_gen_sts_trk_shl_se_lngx", exitEast: true,  yStraight: 0, xStraight: 2);

        // SW corners — S entry, W exit
        gen.GenerateCorner("rg_gen_sts_trk_shl_sw_min",  exitEast: false, yStraight: 0, xStraight: 0);
        gen.GenerateCorner("rg_gen_sts_trk_shl_sw_sht",  exitEast: false, yStraight: 1, xStraight: 1);
        gen.GenerateCorner("rg_gen_sts_trk_shl_sw_lngy", exitEast: false, yStraight: 2, xStraight: 0);
        gen.GenerateCorner("rg_gen_sts_trk_shl_sw_lngx", exitEast: false, yStraight: 0, xStraight: 2);

        // ── Science Rooms (SciIntRmSm kit) ───────────────────────────────────
        // Reverse-engineered from rg_sts_trk_big_001–006 (du_outlaws_template.esm).
        // All tiles are Starfield.esm Statics placed directly in the cell — no nested PackIns.
        // Connectors centred on each face. S connector uses ConnRotNorth (Z=0, inward).
        Console.WriteLine();
        Console.WriteLine("--- Science Rooms ---");

        var room = new SciRoomGenerator(outputMod, sfModKey);

        // 1-exit rooms: dead-end cap (S only)
        room.Generate("rg_gen_sts_trk_big_s",
            exitSouth: true, exitNorth: false, exitEast: false, exitWest: false);

        // 2-exit rooms: through / corner
        room.Generate("rg_gen_sts_trk_big_sn",
            exitSouth: true, exitNorth: true,  exitEast: false, exitWest: false);
        room.Generate("rg_gen_sts_trk_big_se",
            exitSouth: true, exitNorth: false, exitEast: true,  exitWest: false);
        room.Generate("rg_gen_sts_trk_big_sw",
            exitSouth: true, exitNorth: false, exitEast: false, exitWest: true);

        // 3-exit rooms: T-junctions
        room.Generate("rg_gen_sts_trk_big_sne",
            exitSouth: true, exitNorth: true,  exitEast: true,  exitWest: false);
        room.Generate("rg_gen_sts_trk_big_snw",
            exitSouth: true, exitNorth: true,  exitEast: false, exitWest: true);
        room.Generate("rg_gen_sts_trk_big_sew",
            exitSouth: true, exitNorth: false, exitEast: true,  exitWest: true);

        // 4-exit room: full junction (matches big_001 / big_002 layout)
        room.Generate("rg_gen_sts_trk_big_snew",
            exitSouth: true, exitNorth: true,  exitEast: true,  exitWest: true);

        // Write to Starfield Data folder
        string outputPath = Path.Combine(env.DataFolderPath, OutputModName + ".esm");
        outputMod.WriteToBinary(outputPath, gen_quest_main.BuildWriteParams());

        Console.WriteLine();
        Console.WriteLine($"Written: {outputPath}");
        Console.WriteLine($"PackIns created: {outputMod.PackIns.Count}");
        Console.WriteLine($"Cells created:   {outputMod.Cells.Sum(b => b.SubBlocks.Sum(sb => sb.Cells.Count))}");

        return 0;
    }
}
