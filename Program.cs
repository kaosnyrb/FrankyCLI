using FrankyCLI;

Console.WriteLine("FrankyCLI");

if (args.Length == 0)
{
    PrintHelp();
    return 0;
}

string mode = args[0];

switch (mode)
{
    case "gen_retrograde":
        // gen_retrograde [modname] [faction] [stationdesign] [poi|bounty] [quiet] [exportai]
        // Defaults: modname=RG_Generated, faction=random, stationdesign=HabStation, type=bounty
        return gen_retrograde.Generate(BuildArgs("gen_retrograde", args,
            modname:       Get(args, 1, "RG_Generated"),
            faction:       Get(args, 2, ""),
            stationDesign: Get(args, 3, ""),
            questType:     Get(args, 4, ""),
            quiet:         Get(args, 5, ""),
            exportAi:      Get(args, 6, "")));

    case "gen_worldspace":
        // gen_worldspace [modname] [seed] [faction] [design] [quiet]
        // Defaults: modname=RG_Generated, seed=random, faction=Spacer, design=Fort
        return gen_worldspace.Generate(BuildArgs("gen_worldspace", args,
            modname:       Get(args, 1, "RG_Generated"),
            faction:       Get(args, 3, ""),
            stationDesign: Get(args, 4, ""),
            questType:     Get(args, 5, ""),
            quiet:         Get(args, 6, ""),
            seed:          Get(args, 2, "")));

    case "gen_inspect":
        // gen_inspect <recordtype> <editorid_or_formid>
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: gen_inspect <recordtype> <editorid_or_formid>");
            Console.WriteLine();
            Console.WriteLine("Record types: SurfaceBlock, Worldspace, PackIn, Cell, Static, Activator, Npc");
            Console.WriteLine("              worldspace_objects <wsEditorId> — dump all objects in a worldspace");
            Console.WriteLine("              worldspace_smallworld <minDnam>  — list small-world worldspaces");
            Console.WriteLine("              Use 'list' as record type to see all available groups.");
            Console.WriteLine("EditorID: partial match (contains). FormID: prefix with 0x (e.g. 0x00000C36)");
            return 1;
        }
        return gen_inspect.Generate(new[] { "dummy", "gen_inspect", "dummy", args[1], args[2] });

    case "gen_harness":
        // gen_harness [modname] [faction] [size]
        return gen_harness.Generate(new[]
        {
            Get(args, 1, "RG_Generated"),
            "gen_harness",
            "dummy",
            Get(args, 2, "spacer"),
            Get(args, 3, "Small")
        });

    case "gen_report":
        // gen_report [modname]
        return gen_report.Generate(new[]
        {
            Get(args, 1, "RG_Generated"),
            "gen_report", "dummy", "dummy", "dummy"
        });

    case "gen_quest":
        return gen_quest_main.Generate(BuildArgs("gen_quest", args,
            modname:       Get(args, 1, "RG_Generated"),
            faction:       Get(args, 2, ""),
            stationDesign: Get(args, 3, ""),
            questType:     Get(args, 4, ""),
            quiet:         Get(args, 5, ""),
            exportAi:      Get(args, 6, "")));

    case "gen_btd_info":
        // gen_btd_info [btdpath] [--all]
        return gen_btd_info.Generate(new[]
        {
            "dummy", "gen_btd_info", "dummy", "dummy", "dummy",
            Get(args, 1, ""),
            Get(args, 2, "")
        });

    case "gen_btd_test":
        return gen_btd_test.Generate(Pad("gen_btd_test", args));

    case "gen_btd_flatten":
        return gen_btd_flatten.Generate(Pad("gen_btd_flatten", args));

    case "gen_btd_flatcircle":
        return gen_btd_flatcircle.Generate(Pad("gen_btd_flatcircle", args));

    case "gen_coordtest":
        return gen_coordtest.Generate(BuildArgs("gen_coordtest", args,
            modname: Get(args, 1, "RG_CoordTest")));

    case "gen_roompackin":
        return gen_roompackin.Generate(args);

    case "gen_harvester":
        // gen_harvester <baseForm> [radius] [maxVariants] [outputMod]
        return gen_harvester.Generate(args);

    // Legacy ship generators (keep original arg layout)
    case "struct":
    case "flip":
    case "yrotate":
    case "yrotate45":
    case "cellfix":
    case "placer":
    case "pluginmerger":
    case "upgradegenerator":
    case "spaceencounterquest":
    case "branchcreator":
    case "shipicons":
        Console.WriteLine($"Legacy mode '{mode}' — pass all original args including modname:");
        Console.WriteLine($"  FrankyCLI {mode} <modname> <prefix> <item> [modelpath] ...");
        if (args.Length < 3) return 1;
        return RunLegacy(mode, args);

    default:
        Console.WriteLine($"Unknown mode: '{mode}'");
        Console.WriteLine();
        PrintHelp();
        return 1;
}

static void PrintHelp()
{
    Console.WriteLine("Usage: FrankyCLI <mode> [args...]");
    Console.WriteLine();
    Console.WriteLine("Main generators:");
    Console.WriteLine("  gen_retrograde  [modname] [faction] [stationdesign] [poi|bounty] [quiet] [exportai]");
    Console.WriteLine("                     Generate a bounty/POI quest with a randomised space station.");
    Console.WriteLine("                     Defaults: modname=RG_Generated, faction=random, design=HabStation, type=bounty");
    Console.WriteLine();
    Console.WriteLine("  gen_worldspace  [modname] [seed] [faction] [design] [quiet]");
    Console.WriteLine("                     Generate a worldspace (planet fort/dungeon).");
    Console.WriteLine("                     Defaults: modname=RG_Generated, seed=random, faction=Spacer, design=Fort");
    Console.WriteLine();
    Console.WriteLine("  gen_inspect     <recordtype> <editorid_or_formid>");
    Console.WriteLine("                     Dump properties of a Starfield form.");
    Console.WriteLine("                     Record types: SurfaceBlock, Worldspace, PackIn, Cell, Static, Activator, Npc, list");
    Console.WriteLine();
    Console.WriteLine("  gen_quest       [modname] [faction] [stationdesign] [poi|bounty] [quiet]");
    Console.WriteLine("                     Generate a quest (alternate generator).");
    Console.WriteLine();
    Console.WriteLine("  gen_harness     [modname] [faction] [size]");
    Console.WriteLine("                     Run the generation harness (multi-run test).");
    Console.WriteLine("                     Defaults: modname=RG_Generated, faction=spacer, size=Small");
    Console.WriteLine();
    Console.WriteLine("  gen_report      [modname]");
    Console.WriteLine("                     Generate a room-usage report for a mod.");
    Console.WriteLine("                     Defaults: modname=RG_Generated");
    Console.WriteLine();
    Console.WriteLine("  gen_coordtest   [modname]");
    Console.WriteLine("                     Place coordinate test markers in a worldspace.");
    Console.WriteLine("                     Defaults: modname=RG_CoordTest");
    Console.WriteLine();
    Console.WriteLine("  gen_roompackin");
    Console.WriteLine("                     Generate SCI hallway PackIn variants into generated_templates.esm.");
    Console.WriteLine();
    Console.WriteLine("  gen_harvester   <baseForm> [radius] [maxVariants] [outputMod]");
    Console.WriteLine("                     Scan Starfield interior cells for instances of a base form");
    Console.WriteLine("                     (furniture, static, etc.) and emit PackIn prefabs of each");
    Console.WriteLine("                     found cluster — anchor at (0,0,0) + nearby Starfield objects.");
    Console.WriteLine("                     baseForm: EditorID partial match or 0xFormID");
    Console.WriteLine("                     Defaults: radius=150, maxVariants=50, outputMod=harvested_prefabs");
Console.WriteLine();
Console.WriteLine("BTD terrain tools:");
    Console.WriteLine("  gen_btd_info    [btdpath] [--all]   Dump BTD file structure.");
    Console.WriteLine("  gen_btd_test                        Run BTD reader/writer tests.");
    Console.WriteLine("  gen_btd_flatten                     Add cosine hill to BTD centre cell.");
    Console.WriteLine("  gen_btd_flatcircle                  Flatten a circular area in a BTD.");
}

// Returns args[index] if it exists and is non-empty, otherwise defaultValue.
static string Get(string[] args, int index, string defaultValue)
    => index < args.Length && !string.IsNullOrEmpty(args[index]) ? args[index] : defaultValue;

// Builds the legacy flat args array [modname, mode, "RG", "001", "001", ...extra].
static string[] BuildArgs(string mode, string[] userArgs, string modname = "RG_Generated",
    string faction = "", string stationDesign = "", string questType = "",
    string quiet = "", string exportAi = "", string seed = "")
{
    if (mode == "gen_worldspace")
    {
        return new[]
        {
            modname, mode, "RG", "001", "001",
            seed, faction, stationDesign, quiet
        };
    }
    return new[]
    {
        modname, mode, "RG", "001", "001",
        faction, stationDesign, questType, quiet, exportAi
    };
}

// Pads args for generators that just need [modname, mode, ...] with dummies.
static string[] Pad(string mode, string[] userArgs)
{
    var list = new System.Collections.Generic.List<string>
    {
        Get(userArgs, 1, "dummy"), // modname
        mode,                      // mode
        "dummy",                   // prefix
        "dummy",                   // item
        "dummy"                    // form
    };
    // append any extra user args starting from index 2
    for (int i = 2; i < userArgs.Length; i++) list.Add(userArgs[i]);
    return list.ToArray();
}

// Route legacy ship/tool modes - user provides modname as first arg after mode.
static int RunLegacy(string mode, string[] args)
{
    // Reconstruct as [modname, mode, prefix, item, modelpath, ...]
    var fullArgs = new System.Collections.Generic.List<string>();
    fullArgs.Add(args[1]); // modname
    fullArgs.Add(mode);
    for (int i = 2; i < args.Length; i++) fullArgs.Add(args[i]);
    var arr = fullArgs.ToArray();

    if (arr[0] == "Starfield")
    {
        Console.WriteLine("No way am I allowing you to edit Starfield.esm");
        return 1;
    }

    return mode switch
    {
        "struct"             => gen_shipstruct.Generate(arr),
        "flip"               => gen_shipflips.Generate(arr),
        "yrotate"            => gen_shipyrotates.Generate(arr),
        "yrotate45"          => shipyfortyfiverotates.Generate(arr),
        "cellfix"            => gen_cellfixer.Generate(arr),
        "placer"             => gen_placer.Generate(arr),
        "pluginmerger"       => gen_pluginmerger.Generate(arr),
        "upgradegenerator"   => gen_upgradegenerator.Generate(arr),
        "spaceencounterquest"=> gen_spaceencounterquest.Generate(arr),
        "branchcreator"      => gen_branchcreator.Generate(arr),
        "shipicons"          => gen_msicon.Generate(arr),
        _                    => 1
    };
}
