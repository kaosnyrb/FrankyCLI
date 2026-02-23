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
///   rg_gen_sts_trk_shl_flat_short — 8-tile flat corridor (no stairs)
///   rg_gen_sts_trk_shl_flat_med   — 10-tile flat corridor
///   rg_gen_sts_trk_shl_stair_low  — rising corridor, 2 stairs (+4 Z)
///   rg_gen_sts_trk_shl_stair_med  — rising corridor, 3 stairs (+6 Z, matches rg_sts_trk_shl_001)
///   rg_gen_sts_trk_shl_stair_tall — rising corridor, 4 stairs (+8 Z)
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
