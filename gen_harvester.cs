using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.FurnitureHarvester;
using System;
using System.IO;
using System.Linq;

namespace FrankyCLI;

/// <summary>
/// Scans all interior cells in Starfield.esm for instances of a specified base form
/// (furniture, static, activator, etc.) and emits a PackIn prefab for each found
/// cluster — the anchor at (0,0,0) with no rotation plus every Starfield.esm object
/// within a configurable capture radius.
///
/// Usage:
///   gen_harvester &lt;baseForm&gt; [radius] [maxVariants] [outputMod]
///
///   baseForm     EditorID (partial, case-insensitive) or 0xFormID of the anchor object.
///                Searches Furniture, Statics, and Activators — first match wins.
///   radius       Capture sphere radius in game units.  Default: 150
///   maxVariants  Maximum number of prefabs to generate. Default: 50
///   outputMod    Output .esm name (without extension).  Default: harvested_prefabs
///
/// Output:
///   {DataFolder}/{outputMod}.esm  — PackIn records + interior cells, no template-mod deps.
///
/// Examples:
///   gen_harvester MedBed
///   gen_harvester ChairOffice 120 20
///   gen_harvester 0x00012345 200 10 my_prefabs
/// </summary>
public class gen_harvester
{
    private const string DefaultOutputMod = "harvested_prefabs";
    private const float  DefaultRadius    = 150f;
    private const int    DefaultMax       = 50;

    public static int Generate(string[] args)
    {
        Console.WriteLine("=== gen_harvester: Starfield Furniture Prefab Harvester ===");
        Console.WriteLine();

        if (args.Length < 2)
        {
            PrintUsage();
            return 1;
        }

        string baseFormArg = args[1];
        float  radius      = TryParseFloat(args, 2, DefaultRadius);
        int    maxVariants = TryParseInt  (args, 3, DefaultMax);
        string outputName  = GetArg       (args, 4, DefaultOutputMod);

        Console.WriteLine($"Base form:  {baseFormArg}");
        Console.WriteLine($"Radius:     {radius} units");
        Console.WriteLine($"Max:        {maxVariants} variants");
        Console.WriteLine($"Output:     {outputName}.esm");
        Console.WriteLine();

        using var env = GameEnvironment.Typical
            .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
            .Build();

        gen_quest_main.BuildReadParams(env.LoadOrder);

        var sfModKey  = env.LoadOrder[0].ModKey;
        var starfield = env.LoadOrder[0].Mod;

        if (starfield == null)
        {
            Console.WriteLine("ERROR: Could not load Starfield.esm");
            return 1;
        }

        // ── Resolve the target form ────────────────────────────────────────────
        FormKey targetFormKey;
        string  baseEditorId;

        if (baseFormArg.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!uint.TryParse(baseFormArg[2..],
                    System.Globalization.NumberStyles.HexNumber, null, out uint rawId))
            {
                Console.WriteLine($"ERROR: Invalid FormID '{baseFormArg}' — expected 0xHHHHHH");
                return 1;
            }

            targetFormKey = new FormKey(sfModKey, rawId);

            // Best-effort EditorID lookup for label generation.
            baseEditorId = starfield.EnumerateMajorRecords()
                .FirstOrDefault(r => r.FormKey == targetFormKey)
                ?.EditorID
                ?? $"form_{rawId:X6}";

            Console.WriteLine($"Resolved: [{targetFormKey}] → {baseEditorId}");
        }
        else
        {
            // EditorID partial-match search (case-insensitive).
            // Priority order: Furniture → Statics → Activators → any record.
            IMajorRecordGetter? match =
                (IMajorRecordGetter?)starfield.Furniture
                    .FirstOrDefault(f => f.EditorID?.Contains(
                        baseFormArg, StringComparison.OrdinalIgnoreCase) == true)
                ?? starfield.Statics
                    .FirstOrDefault(s => s.EditorID?.Contains(
                        baseFormArg, StringComparison.OrdinalIgnoreCase) == true)
                ?? starfield.Activators
                    .FirstOrDefault(a => a.EditorID?.Contains(
                        baseFormArg, StringComparison.OrdinalIgnoreCase) == true)
                ?? starfield.EnumerateMajorRecords()
                    .FirstOrDefault(r => r.EditorID?.Contains(
                        baseFormArg, StringComparison.OrdinalIgnoreCase) == true);

            if (match == null)
            {
                Console.WriteLine($"ERROR: No form found with EditorID containing '{baseFormArg}'");
                Console.WriteLine("Tip: use gen_inspect to look up the FormID first.");
                return 1;
            }

            targetFormKey = match.FormKey;
            baseEditorId  = match.EditorID ?? baseFormArg;
            Console.WriteLine($"Resolved: {match.EditorID} [{targetFormKey}]");
        }

        Console.WriteLine();

        // ── Sanitise label for EditorID use ───────────────────────────────────
        string safeLabel = new string(
            baseEditorId.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        if (safeLabel.Length > 24)
            safeLabel = safeLabel[..24];

        // ── Create output mod ──────────────────────────────────────────────────
        var outputModKey = new ModKey(outputName, ModType.Master);
        var outputMod    = new StarfieldMod(outputModKey, StarfieldRelease.Starfield);

        // ── Run harvester ──────────────────────────────────────────────────────
        Console.WriteLine("Scanning interior cells...");
        Console.WriteLine();

        var harvester = new FurnitureHarvester(outputMod, sfModKey);
        int count     = harvester.Harvest(starfield, targetFormKey, safeLabel, radius, maxVariants);

        Console.WriteLine();
        Console.WriteLine($"Variants harvested: {count}");

        if (count == 0)
        {
            Console.WriteLine("Nothing to write — no qualifying clusters found.");
            Console.WriteLine("Try: increasing radius, checking the EditorID, or using gen_inspect to verify placement cells.");
            return 0;
        }

        // ── Write output ───────────────────────────────────────────────────────
        string outputPath = Path.Combine(env.DataFolderPath, outputName + ".esm");
        outputMod.WriteToBinary(outputPath, gen_quest_main.BuildWriteParams());

        Console.WriteLine($"Written: {outputPath}");
        Console.WriteLine($"PackIns: {outputMod.PackIns.Count}");
        Console.WriteLine($"Cells:   {outputMod.Cells.Sum(b => b.SubBlocks.Sum(sb => sb.Cells.Count))}");

        return 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: gen_harvester <baseForm> [radius] [maxVariants] [outputMod]");
        Console.WriteLine();
        Console.WriteLine("  baseForm     EditorID (partial, case-insensitive) or 0xFormID");
        Console.WriteLine("               Searches: Furniture → Statics → Activators → all records");
        Console.WriteLine("  radius       Capture radius in game units (default: 150)");
        Console.WriteLine("  maxVariants  Max prefabs to generate (default: 50)");
        Console.WriteLine("  outputMod    Output ESM name, no extension (default: harvested_prefabs)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  gen_harvester MedBed");
        Console.WriteLine("  gen_harvester ChairOffice 120 20");
        Console.WriteLine("  gen_harvester 0x00012345 200 10 my_prefabs");
        Console.WriteLine();
        Console.WriteLine("Tip: use gen_inspect to find EditorIDs or FormIDs before running.");
    }

    private static string GetArg(string[] args, int i, string def)
        => i < args.Length && !string.IsNullOrWhiteSpace(args[i]) ? args[i] : def;

    private static float TryParseFloat(string[] args, int i, float def)
        => i < args.Length && float.TryParse(args[i], out float v) ? v : def;

    private static int TryParseInt(string[] args, int i, int def)
        => i < args.Length && int.TryParse(args[i], out int v) ? v : def;
}
