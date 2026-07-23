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
            Console.WriteLine("Record types:");
            Console.WriteLine(FrankyCLI.gen_inspect.SupportedTypes);
            Console.WriteLine();
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

    case "gen_elevenlabs_test":
        // gen_elevenlabs_test [outputPath] [voiceId] [text]
        return gen_elevenlabs_test.Generate(args);

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

    case "gen_shipcompare":
        return gen_shipcompare.Run();

    case "gen_shiptest":
        return gen_shiptest.Run();

    case "gen_shipmodulestats":
        return gen_shipmodulestats.Run();

    case "gen_armorinspect":
        return gen_armorinspect.Run();

    case "gen_coordtest":
        return gen_coordtest.Generate(BuildArgs("gen_coordtest", args,
            modname: Get(args, 1, "RG_CoordTest")));

    case "gen_roompackin":
        return gen_roompackin.Generate(args);

    case "gen_deprefscan":
        // gen_deprefscan [modname]
        return gen_deprefscan.Run(Get(args, 1, "outlaws02"));

    case "gen_fkltest":
        // gen_fkltest — diagnoses FormKeyLookup enumeration regressions after ESM updates
        return gen_fkltest.Run();

    case "gen_hunttest":
        // gen_hunttest [modname] — builds a real ESM with one PredatorHuntTarget per probe planet
        return gen_hunttest.Run(Get(args, 1, "hunttest"));

    case "gen_systemtest":
        // gen_systemtest — exercises planet->system->level lookup against 10 probe planets
        return gen_systemtest.Run();

    case "gen_aspcpatch":
        // gen_aspcpatch — patches du_retrograde: copies ASPC + expands primitive to cell bounds
        return gen_aspcpatch.Run();

    case "gen_dlgtest":
        // gen_dlgtest [modname]  — structural test for NPCDialogueNoun (no AI, no audio)
        return gen_dlgtest.Run(Get(args, 1, "dlgtest"));

    case "gen_promptlab":
        // gen_promptlab <conversationfile> [<N> | --list]
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: gen_promptlab <conversationfile> [<N> | --list]");
            Console.WriteLine("  Replays a [user] block against the preceding conversation history.");
            Console.WriteLine("  <N>      replay the Nth [user] block (1-based); default = last");
            Console.WriteLine("  --list   list all [user] blocks with previews");
            return 1;
        }
        return gen_promptlab.Run(args[1], args.Length > 2 ? args[2] : null);

    // checkpart is read-only and JSON-emitting -- its own case so it prints no legacy usage noise.
    case "checkpart":
        if (args.Length < 3) { Console.WriteLine("Usage: checkpart <modname> <item>"); return 1; }
        return RunLegacy(mode, args);

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
    case "setrecipefilter":
    case "setname":
    case "setsnap":
    case "setcreated":
    case "setrotation":
    case "copyswap":
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
    Console.WriteLine("  gen_elevenlabs_test [outputPath] [voiceId] [text]");
    Console.WriteLine("                     Test the ElevenLabs TTS API. Writes a WAV to outputPath.");
    Console.WriteLine("                     Requires ELEVENLABS_API_KEY env var.");
    Console.WriteLine();
    Console.WriteLine("  gen_roompackin");
Console.WriteLine("                     Generate SCI hallway PackIn variants into generated_templates.esm.");
Console.WriteLine();
Console.WriteLine("  gen_deprefscan  [modname]");
Console.WriteLine("                     Scan a built mod for FormLink references into template mods.");
Console.WriteLine("                     Reports which records hold the dependency and their target FormKeys.");
Console.WriteLine("                     Defaults: modname=outlaws02");
Console.WriteLine();
Console.WriteLine("  gen_aspcpatch");
Console.WriteLine("                     Patch du_retrograde: copies Int_Space_Ship_UC_Small_NoAlarm as");
Console.WriteLine("                     DU_Station_ASPC and expands all placed ASPC primitives to cover");
Console.WriteLine("                     each station cell's full bounding box.");
Console.WriteLine();
Console.WriteLine("  gen_dlgtest     [modname]");
Console.WriteLine("                     Structural test for NPCDialogueNoun — builds a 2-stage dialogue");
Console.WriteLine("                     quest, prints a record-chain diagnostic, and writes the .esm.");
Console.WriteLine("                     No AI or audio. Load output in xEdit to verify. Defaults: modname=dlgtest");
Console.WriteLine();
Console.WriteLine("  gen_promptlab   <conversationfile>");
Console.WriteLine("                     Run the next AI response against a conversation file.");
Console.WriteLine("                     File format: [system] / [user] / [assistant] blocks.");
Console.WriteLine("                     The last block must be [user]. Uses claude-sonnet-4-6.");
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
        "setrecipefilter"    => gen_setrecipefilter.Generate(arr),
        "setname"            => gen_setname.Generate(arr),
        "setsnap"            => gen_setsnap.Generate(arr),
        "setcreated"         => gen_setcreated.Generate(arr),
        "setrotation"        => gen_setrotation.Generate(arr),
        "copyswap"           => gen_copyswap.Generate(arr),
        "checkpart"          => gen_checkpart.Generate(arr),
        _                    => 1
    };
}
