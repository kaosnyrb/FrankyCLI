using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// Investigates Starfield forms by dumping their properties.
    /// Usage: gen_inspect &lt;recordtype&gt; &lt;editorid_or_formid&gt;
    /// Example: gen_inspect SurfaceBlock OverlayBlockstbblock001
    /// Example: gen_inspect Worldspace 0x00000C36
    /// </summary>
    public class gen_inspect
    {
        /// <summary>
        /// The supported record types, in ONE place. Program.cs prints this for its usage text
        /// and the unknown-type branch below prints it too. Previously each site kept its own
        /// copy and all of them had drifted — none listed MoveableStatic, Planet, Star, Race or
        /// Biome, which have been supported for some time.
        /// </summary>
        public const string SupportedTypes =
            "  SurfaceBlock, Worldspace, WorldspaceStructure, PackIn, Cell, Static, MoveableStatic\n" +
            "  Activator, Light, Npc, Location, Location_Full, Keyword, Book, Scene\n" +
            "  PcmBranchNode, PcmContentNode, Planet, Star, Race, Biome (biom)\n" +
            "  Quest, Quest_VMAD, DialogBranch, DialogTopic, AudioLog (full dialog chain dump)\n" +
            "  Message (mesg), Faction, Global, FormList, LeveledSpaceCell (lvsc)\n" +
            "  QuestAlias (qalias) - alias fills: which one is set and what it points at\n" +
            "  QuestAll (qall)    - the WHOLE quest record + a report of what it did NOT render\n" +
            "  Armor (armo), ObjectModification (omod), ObjectEffect (ench), Perk, Spell (spel)\n" +
            "  MagicEffect (mgef), DamageType (dmgt), LegendaryItem (lgdi), Outfit (otft)\n" +
            "  ActorValueInformation (avif)\n" +
            "  Ship-module chain: SnapTemplate (sntp), GenericBaseForm (gbfm),\n" +
            "                     ConstructibleObject (cobj), LayeredMaterialSwap (lmsw)\n" +
            "  PlacedObject (refr), refr_xflg, placed\n" +
            "  worldspace_objects <wsEditorId>, worldspace_smallworld <minDnam>\n" +
            "  'list' - enumerate all record groups with counts";

        public static int Generate(string[] args)
        {
            // Arity is guaranteed by the caller: Program.cs guards args.Length < 3 and then
            // always invokes with exactly 5. A second guard here was unreachable (rule 4).
            string recordType = args[3];
            string search = args[4];

            Console.WriteLine($"=== Form Inspector ===");
            Console.WriteLine($"Record type: {recordType}");
            Console.WriteLine($"Search: {search}");
            Console.WriteLine();

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var starfield = env.LoadOrder[0].Mod;

            if (starfield == null)
            {
                Console.WriteLine("ERROR: Could not load Starfield.esm");
                return 1;
            }

            // Collect all loaded mods
            var allMods = new List<IStarfieldModGetter>();
            for (int i = 0; i < env.LoadOrder.Count; i++)
            {
                if (env.LoadOrder[i].Mod != null)
                    allMods.Add(env.LoadOrder[i].Mod!);
            }

            if (recordType.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                ListRecordGroups(starfield);
                return 0;
            }

            if (recordType.Equals("worldspace_smallworld", StringComparison.OrdinalIgnoreCase))
            {
                int minDnam = int.TryParse(search, out int m) ? m : 4;
                Console.WriteLine($"SmallWorld worldspaces with DNAM >= {minDnam}:");
                Console.WriteLine();
                int found2 = ListSmallWorldWorldspaces(allMods, minDnam);
                Console.WriteLine();
                Console.WriteLine($"Total: {found2}");
                return 0;
            }

            if (recordType.Equals("worldspace_objects", StringComparison.OrdinalIgnoreCase))
            {
                int found3 = 0;
                foreach (var mod in allMods)
                    found3 += DumpWorldspaceObjects(mod, search);
                Console.WriteLine();
                Console.WriteLine($"Total placed objects: {found3}");
                return 0;
            }

            int found = 0;
            foreach (var mod in allMods)
            {
                found += InspectRecordType(mod, recordType, search, allMods, env.LinkCache);
            }

            if (found == 0)
            {
                Console.WriteLine($"No {recordType} records found matching '{search}'");
                Console.WriteLine("Try using 'list' as record type to see available groups.");
            }

            Console.WriteLine();
            Console.WriteLine($"Total records found: {found}");
            return 0;
        }

        private static int InspectRecordType(IStarfieldModGetter mod, string recordType, string search, List<IStarfieldModGetter>? allMods = null, ILinkCache? cache = null)
        {
            int found = 0;
            switch (recordType.ToLowerInvariant())
            {
                case "surfaceblock":
                    foreach (var rec in mod.SurfaceBlocks)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpSurfaceBlock(rec); found++; }
                    break;
                case "worldspace":
                    foreach (var rec in mod.Worldspaces)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Worldspace"); found++; }
                    break;
                case "packin":
                    foreach (var rec in mod.PackIns)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "PackIn"); found++; }
                    break;
                case "cell":
                    foreach (var block in mod.Cells)
                        foreach (var subBlock in block.SubBlocks)
                            foreach (var cell in subBlock.Cells)
                                if (MatchesSearch(cell.EditorID, cell.FormKey, search))
                                { DumpCell(cell, cache); found++; }
                    // Also search worldspace subcells and TopCells
                    foreach (var ws in mod.Worldspaces)
                    {
                        if (ws.TopCell != null && MatchesSearch(ws.TopCell.EditorID, ws.TopCell.FormKey, search))
                        { Console.Write($"  [Worldspace TopCell: {ws.EditorID}] "); DumpCell(ws.TopCell, cache); found++; }
                        foreach (var wsBlock in ws.SubCells)
                            foreach (var wsSubBlock in wsBlock.Items)
                                foreach (var cell in wsSubBlock.Items)
                                    if (MatchesSearch(cell.EditorID, cell.FormKey, search))
                                    { Console.Write($"  [Worldspace: {ws.EditorID} grid ({wsSubBlock.BlockNumberX},{wsSubBlock.BlockNumberY})] "); DumpCell(cell, cache); found++; }
                    }
                    break;
                case "refr_xflg":
                {
                    // Scan a cell (search = EditorID or FormKey) and list all placed objects
                    // that have a non-null XFLG sub-record. Used to identify what XFLG bytes
                    // correspond to specific CK flags.
                    void ScanCell(ICellGetter cell)
                    {
                        if (!MatchesSearch(cell.EditorID, cell.FormKey, search)) return;
                        Console.WriteLine($"--- Cell {cell.FormKey} {cell.EditorID} ---");
                        int count = 0;
                        foreach (var entry in cell.Persistent.Concat(cell.Temporary))
                        {
                            if (entry is IPlacedObjectGetter po && po.XFLG.HasValue)
                            {
                                Console.WriteLine($"  REFR:{po.FormKey}  Base={po.Base.FormKey}  " +
                                                  $"XFLG={BitConverter.ToString(po.XFLG.Value.ToArray())}  " +
                                                  $"HdrFlags=0x{po.MajorRecordFlagsRaw:X8}");
                                count++;
                            }
                        }
                        Console.WriteLine($"  ({count} with XFLG)");
                        found++;
                    }
                    foreach (var block in mod.Cells)
                        foreach (var subBlock in block.SubBlocks)
                            foreach (var cell in subBlock.Cells)
                                ScanCell(cell);
                    foreach (var ws in mod.Worldspaces)
                    {
                        if (ws.TopCell != null) ScanCell(ws.TopCell);
                        foreach (var wsBlock in ws.SubCells)
                            foreach (var wsSubBlock in wsBlock.Items)
                                foreach (var cell in wsSubBlock.Items)
                                    ScanCell(cell);
                    }
                    break;
                }
                case "static":
                    foreach (var rec in mod.Statics)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Static"); found++; }
                    break;
                case "moveablestatic":
                case "moveablestatics":
                    foreach (var rec in mod.MoveableStatics)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        {
                            DumpRecord(rec, "MoveableStatic");
                            if (rec.Keywords != null && rec.Keywords.Count > 0)
                            {
                                Console.WriteLine($"  Keywords [{rec.Keywords.Count}]:");
                                foreach (var kw in rec.Keywords)
                                {
                                    string? eid = null;
                                    if (allMods != null)
                                        foreach (var m in allMods)
                                        {
                                            var r = m.EnumerateMajorRecords().FirstOrDefault(x => x.FormKey == kw.FormKey);
                                            if (r != null) { eid = r.EditorID; break; }
                                        }
                                    Console.WriteLine($"    {eid ?? "<unresolved>"} [{kw.FormKey}]");
                                }
                            }
                            found++;
                        }
                    break;
                // --- Ship-module chain (docs/formlib/ship_module.md) -------------------
                // MSTT -> SNTP -> CELL -> PKIN -> GBFM -> COBJ. MoveableStatic, Cell and
                // PackIn were already reachable; these four close the chain so a part can be
                // verified end to end without opening xEdit.
                case "snaptemplate":
                case "sntp":
                    foreach (var rec in mod.SnapTemplates)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpSnapTemplate(rec, allMods); found++; }
                    break;
                case "genericbaseform":
                case "gbfm":
                    foreach (var rec in mod.GenericBaseForms)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpGenericBaseForm(rec, allMods); found++; }
                    break;
                case "constructibleobject":
                case "cobj":
                    foreach (var rec in mod.ConstructibleObjects)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpConstructibleObject(rec, allMods); found++; }
                    break;
                case "layeredmaterialswap":
                case "lmsw":
                    // Reflection only, deliberately. The generators here never author one (they
                    // link the three vanilla paint layers by FormID), so we have no source of
                    // truth for the layout — and a CK-authored swap keeps its payload in REFL,
                    // which reflection reports as opaque binary. So this resolves EditorID and
                    // FormKey (enough to verify what a MoveableStatic's MaterialSwaps point at)
                    // and does NOT show the material mapping. Don't read a dump here as proof of
                    // which textures a swap binds — that still needs xEdit.
                    foreach (var rec in mod.LayeredMaterialSwaps)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        {
                            Console.WriteLine("--- LayeredMaterialSwap ---");
                            Console.WriteLine($"  EditorID: {rec.EditorID}");
                            Console.WriteLine($"  FormKey: {rec.FormKey}");
                            // REFL (source->target mapping) is opaque; but the KeywordFormComponent
                            // carries the recolour-CHANNEL keyword the ship-builder repaint UI keys on.
                            var kwc = rec.Components?.OfType<IKeywordFormComponentGetter>().FirstOrDefault();
                            if (kwc?.Keywords != null && kwc.Keywords.Count > 0)
                            {
                                Console.WriteLine($"  Keywords [{kwc.Keywords.Count}]:");
                                foreach (var kw in kwc.Keywords)
                                {
                                    string? eid = null;
                                    if (allMods != null)
                                        foreach (var m in allMods)
                                        {
                                            var r = m.EnumerateMajorRecords().FirstOrDefault(x => x.FormKey == kw.FormKey);
                                            if (r != null) { eid = r.EditorID; break; }
                                        }
                                    Console.WriteLine($"    {eid ?? "<unresolved>"} [{kw.FormKey}]");
                                }
                            }
                            else Console.WriteLine("  (no KeywordFormComponent keywords)");
                            found++;
                        }
                    break;
                case "activator":
                    foreach (var rec in mod.Activators)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Activator"); found++; }
                    break;
                case "light":
                    foreach (var rec in mod.Lights)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpLight(rec); found++; }
                    break;
                case "npc":
                    foreach (var rec in mod.Npcs)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Npc"); DumpNpcExtras(rec, allMods); found++; }
                    break;
                case "location":
                    foreach (var rec in mod.Locations)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Location"); found++; }
                    break;
                case "location_full":
                    foreach (var rec in mod.Locations)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpLocationFull(rec); found++; }
                    break;
                case "book":
                    foreach (var rec in mod.Books)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpBook(rec, allMods ?? new List<IStarfieldModGetter> { mod }); found++; }
                    break;
                case "scene":
                    // Scenes are SCEN sub-records inside Quest records
                    foreach (var quest in mod.Quests)
                    {
                        if (quest.Scenes == null) continue;
                        foreach (var scene in quest.Scenes)
                        {
                            if (MatchesSearch(scene.EditorID, scene.FormKey, search))
                            {
                                Console.WriteLine($"[Quest: {quest.FormKey} {quest.EditorID}]");
                                DumpScene(scene);
                                found++;
                            }
                        }
                    }
                    break;
                case "dialogtopic":
                    // DialogTopics are sub-records of Quests (or top-level DIAL group)
                    foreach (var quest in mod.Quests)
                    {
                        foreach (var topic in quest.DialogTopics)
                        {
                            if (MatchesSearch(topic.EditorID, topic.FormKey, search))
                            {
                                DumpDialogTopic(topic);
                                found++;
                            }
                        }
                    }
                    break;
                case "quest":
                    foreach (var rec in mod.Quests)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpQuest(rec); found++; }
                    break;
                case "quest_vmad":
                case "questvmad":
                    foreach (var rec in mod.Quests)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpQuestVMAD(rec); found++; }
                    break;
                case "dialogbranch":
                    foreach (var quest in mod.Quests)
                    {
                        foreach (var branch in quest.DialogBranches)
                        {
                            if (MatchesSearch(branch.EditorID, branch.FormKey, search))
                            {
                                Console.WriteLine($"[Quest: {quest.FormKey} {quest.EditorID}]");
                                DumpDialogBranch(branch);
                                found++;
                            }
                        }
                    }
                    break;
                case "audiolog":
                    // Full dump: Quest + all its DialogBranches + Topics + Responses
                    foreach (var quest in mod.Quests)
                    {
                        if (!MatchesSearch(quest.EditorID, quest.FormKey, search)) continue;
                        DumpQuestFull(quest);
                        found++;
                    }
                    break;
                case "placedobject":
                case "refr":
                {
                    // Search all cells for a specific placed object FormKey and dump its flags.
                    void SearchCellRefr(ICellGetter cell)
                    {
                        foreach (var entry in cell.Persistent.Concat(cell.Temporary))
                        {
                            if (entry is IPlacedObjectGetter po && MatchesSearch(po.EditorID, po.FormKey, search))
                            {
                                Console.WriteLine($"--- PlacedObject (REFR) ---");
                                Console.WriteLine($"  FormKey:              {po.FormKey}");
                                Console.WriteLine($"  EditorID:             {po.EditorID ?? "(none)"}");
                                Console.WriteLine($"  MajorRecordFlagsRaw:  {po.MajorRecordFlagsRaw} (0x{po.MajorRecordFlagsRaw:X8})");
                                Console.WriteLine($"  StarfieldFlags:       {po.StarfieldMajorRecordFlags}");
                                Console.WriteLine($"  XFLG:                 {(po.XFLG.HasValue ? BitConverter.ToString(po.XFLG.Value.ToArray()) : "(null)")}");
                                Console.WriteLine($"  XNSE:                 {(po.XNSE.HasValue ? BitConverter.ToString(po.XNSE.Value.ToArray()) : "(null)")}");
                                Console.WriteLine($"  XALG:                 {(po.XALG.HasValue ? $"0x{po.XALG.Value:X16}" : "(null)")}");
                                Console.WriteLine($"  Base:                 {po.Base.FormKey}");
                                Console.WriteLine($"  Position:             {po.Position}");
                                Console.WriteLine($"  Rotation:             {po.Rotation}");
                                Console.WriteLine($"  Scale:                {po.Scale}");
                                Console.WriteLine($"  Cell:                 {cell.FormKey} {cell.EditorID}");
                                // Linked references were absent from this renderer entirely, which is
                                // worse than it sounds: a REFR that HAS them rendered identically to one
                                // that does not, so the view could not distinguish wired from unwired.
                                // (2026-08-24: 0F3287 carried two and this printed none.)
                                DumpLinkedRefs(po, cache, "  ");
                                found++;
                            }
                        }
                    }
                    foreach (var block in mod.Cells)
                        foreach (var subBlock in block.SubBlocks)
                            foreach (var cell in subBlock.Cells)
                                SearchCellRefr(cell);
                    foreach (var ws in mod.Worldspaces)
                    {
                        if (ws.TopCell != null) SearchCellRefr(ws.TopCell);
                        foreach (var wsBlock in ws.SubCells)
                            foreach (var wsSubBlock in wsBlock.Items)
                                foreach (var cell in wsSubBlock.Items)
                                    SearchCellRefr(cell);
                    }
                    break;
                }
                case "worldspace_structure":
                case "worldspacestructure":
                {
                    // Dump structural summary of a worldspace override — useful for comparing
                    // a CK-generated template mod vs a Mutagen-generated mod to find differences.
                    foreach (var ws in mod.Worldspaces)
                    {
                        if (!MatchesSearch(ws.EditorID, ws.FormKey, search)) continue;
                        Console.WriteLine($"--- Worldspace structure [{mod.ModKey}] ---");
                        Console.WriteLine($"  FormKey:        {ws.FormKey}");
                        Console.WriteLine($"  EditorID:       {ws.EditorID}");
                        Console.WriteLine($"  OffsetData:     {(ws.OffsetData.HasValue ? $"{ws.OffsetData.Value.Length} bytes" : "(null)")}");
                        Console.WriteLine($"  Flags:          {ws.Flags}");
                        Console.WriteLine($"  SubCells:       {ws.SubCells.Count} block(s)");
                        int totalCells = 0;
                        foreach (var b in ws.SubCells)
                            foreach (var sb in b.Items)
                                totalCells += sb.Items.Count;
                        Console.WriteLine($"    → {totalCells} total exterior cell(s)");
                        if (ws.TopCell != null)
                        {
                            Console.WriteLine($"  TopCell:        {ws.TopCell.FormKey}");
                            Console.WriteLine($"    Persistent:   {ws.TopCell.Persistent.Count}");
                            Console.WriteLine($"    Temporary:    {ws.TopCell.Temporary.Count}");
                        }
                        else
                            Console.WriteLine($"  TopCell:        (null)");
                        Console.WriteLine();
                        found++;
                    }
                    break;
                }
                case "placed":
                    // Search all cells in all worldspaces for placed objects whose Base OR own FormKey matches
                    foreach (var ws in mod.Worldspaces)
                    {
                        if (ws.TopCell != null)
                            foreach (var entry in ws.TopCell.Persistent)
                                if (entry is IPlacedObjectGetter po && (MatchesSearch(po.Base.FormKey.ToString(), po.Base.FormKey, search) || MatchesSearch(po.EditorID, po.FormKey, search)))
                                { Console.WriteLine($"[WS:{ws.EditorID} TopCell Persistent] {po.FormKey} Base={po.Base.FormKey} Pos={po.Position} Rot={po.Rotation}"); found++; }
                        foreach (var wsBlock in ws.SubCells)
                            foreach (var wsSubBlock in wsBlock.Items)
                                foreach (var cell in wsSubBlock.Items)
                                {
                                    foreach (var entry in cell.Persistent)
                                        if (entry is IPlacedObjectGetter po && (MatchesSearch(po.Base.FormKey.ToString(), po.Base.FormKey, search) || MatchesSearch(po.EditorID, po.FormKey, search)))
                                        { Console.WriteLine($"[WS:{ws.EditorID} Persistent ({wsSubBlock.BlockNumberX},{wsSubBlock.BlockNumberY})] {po.FormKey} Base={po.Base.FormKey} Pos={po.Position} Rot={po.Rotation}"); found++; }
                                    foreach (var entry in cell.Temporary)
                                        if (entry is IPlacedObjectGetter po && (MatchesSearch(po.Base.FormKey.ToString(), po.Base.FormKey, search) || MatchesSearch(po.EditorID, po.FormKey, search)))
                                        { Console.WriteLine($"[WS:{ws.EditorID} Temporary ({wsSubBlock.BlockNumberX},{wsSubBlock.BlockNumberY})] {po.FormKey} Base={po.Base.FormKey} Pos={po.Position} Rot={po.Rotation}"); found++; }
                                }
                    }
                    break;
                case "pcmbranchnode":
                    foreach (var rec in mod.PlanetContentManagerBranchNodes)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpPcmBranchNode(rec); found++; }
                    break;
                case "pcmcontentnode":
                    foreach (var rec in mod.PlanetContentManagerContentNodes)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpPcmContentNode(rec); found++; }
                    break;
                case "keyword":
                    found += SearchWithRecovery(mod.Keywords, search, "Keyword");
                    break;
                case "message":
                case "mesg":
                    foreach (var rec in mod.Messages)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpMessage(rec); found++; }
                    break;
                case "faction":
                    foreach (var rec in mod.Factions)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Faction"); found++; }
                    break;
                case "global":
                    foreach (var rec in mod.Globals)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { Console.WriteLine($"--- Global ---"); Console.WriteLine($"  FormKey:  {rec.FormKey}"); Console.WriteLine($"  EditorID: {rec.EditorID}"); Console.WriteLine($"  Data:     {rec.Data}"); Console.WriteLine(); found++; }
                    break;
                case "formlist":
                    foreach (var rec in mod.FormLists)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpFormList(rec, allMods); found++; }
                    break;
                case "questall":
                case "qall":
                    foreach (var rec in mod.Quests)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpQuestEverything(rec, allMods); found++; }
                    break;
                case "questalias":
                case "qalias":
                    foreach (var rec in mod.Quests)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpQuestAliases(rec, allMods); found++; }
                    break;
                case "leveledspacecell":
                case "lvsc":
                    foreach (var rec in mod.LeveledSpaceCells)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpLeveledSpaceCell(rec, allMods); found++; }
                    break;
                case "armor":
                case "armo":
                    found += SearchWithRecovery(mod.Armors, search, "Armor");
                    break;
                case "objectmodification":
                case "omod":
                    found += SearchWithRecovery(mod.ObjectModifications, search, "ObjectModification");
                    break;
                case "objecteffect":
                case "ench":
                    found += SearchWithRecovery(mod.ObjectEffects, search, "ObjectEffect");
                    break;
                case "perk":
                    found += SearchWithRecovery(mod.Perks, search, "Perk");
                    break;
                case "magiceffect":
                case "mgef":
                    found += SearchWithRecovery(mod.MagicEffects, search, "MagicEffect");
                    break;
                case "damagetype":
                case "dmgt":
                    found += SearchWithRecovery(mod.DamageTypes, search, "DamageType");
                    break;
                case "legendaryitem":
                case "lgdi":
                    found += SearchWithRecovery(mod.LegendaryItems, search, "LegendaryItem");
                    break;
                case "outfit":
                case "otft":
                    found += SearchWithRecovery(mod.Outfits, search, "Outfit");
                    break;
                case "actorvalueinformation":
                case "avif":
                    found += SearchWithRecovery(mod.ActorValueInformation, search, "ActorValueInformation");
                    break;
                case "spell":
                case "spel":
                    found += SearchWithRecovery(mod.Spells, search, "Spell");
                    break;
                case "race":
                    found += SearchWithRecovery(mod.Races, search, "Race");
                    break;
                case "planet":
                    foreach (var rec in mod.Planets)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        {
                            DumpRecord(rec, "Planet");
                            var kwComp = rec.Components?.OfType<IKeywordFormComponentGetter>().FirstOrDefault();
                            if (kwComp?.Keywords != null && kwComp.Keywords.Count > 0)
                            {
                                Console.WriteLine($"  Keywords [{kwComp.Keywords.Count}]:");
                                foreach (var kw in kwComp.Keywords)
                                {
                                    string? eid = null;
                                    if (allMods != null)
                                        foreach (var m in allMods)
                                        {
                                            var r = m.EnumerateMajorRecords().FirstOrDefault(x => x.FormKey == kw.FormKey);
                                            if (r != null) { eid = r.EditorID; break; }
                                        }
                                    Console.WriteLine($"    {eid ?? "<unresolved>"} [{kw.FormKey}]");
                                }
                            }
                            Console.WriteLine();
                            found++;
                        }
                    break;
                case "star":
                    found += SearchWithRecovery(mod.Stars, search, "Star");
                    break;
                case "biome":
                case "biom":
                    found += SearchWithRecovery(mod.Biomes, search, "Biome");
                    break;
                default:
                    Console.WriteLine($"Unknown record type: {recordType}");
                    Console.WriteLine("Supported:");
                    Console.WriteLine(SupportedTypes);
                    break;
            }
            return found;
        }

        private static bool MatchesSearch(string? editorId, FormKey formKey, string search)
        {
            // FormID search (hex)
            if (search.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(search.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out uint id))
                    return formKey.ID == id;
            }

            // EditorID search (partial match)
            if (editorId != null && editorId.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            // Exact FormKey string match
            if (formKey.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// Iterates a record group with try/catch per record.
        /// Some record types (Armor, Keyword) crash Mutagen's binary parser on certain records
        /// (e.g. BGSAdaptiveTriggerData_Component). This skips broken records and continues.
        /// </summary>
        private static int SearchWithRecovery<T>(IEnumerable<T> records, string search, string typeName)
            where T : Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter
        {
            int found = 0;
            var enumerator = records.GetEnumerator();
            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext()) break;
                    var rec = enumerator.Current;
                    if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                    {
                        try
                        {
                            DumpRecord(rec, typeName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"--- {typeName} ---");
                            Console.WriteLine($"  FormKey:  {rec.FormKey}");
                            Console.WriteLine($"  EditorID: {rec.EditorID}");
                            Console.WriteLine($"  ERROR dumping properties: {ex.Message}");
                            Console.WriteLine();
                        }
                        found++;
                    }
                }
                catch (Exception)
                {
                    // Mutagen parsing error on this record — skip and continue
                    continue;
                }
            }
            return found;
        }

        private static void DumpSurfaceBlock(ISurfaceBlockGetter block)
        {
            Console.WriteLine($"--- SurfaceBlock ---");
            Console.WriteLine($"  FormKey:  {block.FormKey}");
            Console.WriteLine($"  EditorID: {block.EditorID}");
            Console.WriteLine($"  ANAM:     {block.ANAM}");
            Console.WriteLine();

            // Dump all public properties via reflection
            DumpPropertiesReflection(block, "  ", maxDepth: 2);
        }

        private static void DumpMessage(IMessageGetter msg)
        {
            Console.WriteLine($"--- Message (MESG) ---");
            Console.WriteLine($"  FormKey:     {msg.FormKey}");
            Console.WriteLine($"  EditorID:    {msg.EditorID}");
            Console.WriteLine($"  Name:        {msg.Name}");
            Console.WriteLine($"  Description: {msg.Description}");
            Console.WriteLine($"  Flags:       {msg.Flags}");
            Console.WriteLine($"  DisplayTime: {msg.DisplayTime}");
            Console.WriteLine($"  BNAM:        {(msg.BNAM.HasValue ? msg.BNAM.Value.ToString() : "(null)")}");
            Console.WriteLine($"  OwnerQuest:  {(msg.OwnerQuest.IsNull ? "(null)" : msg.OwnerQuest.FormKey.ToString())}");
            if (msg.MenuButtons != null && msg.MenuButtons.Count > 0)
            {
                Console.WriteLine($"  MenuButtons ({msg.MenuButtons.Count}):");
                for (int i = 0; i < msg.MenuButtons.Count; i++)
                    Console.WriteLine($"    [{i}] {msg.MenuButtons[i].Text}  (conditions: {msg.MenuButtons[i].Conditions?.Count ?? 0})");
            }
            Console.WriteLine();
        }

        /// Resolve a FormKey to its EditorID for display. Returns "" when the cache cannot
        /// name it -- an unnamed record is normal (most PlacedObjects have no EditorID), so
        /// this must never present a miss as an error.
        private static string NameOf(FormKey key, ILinkCache? cache)
        {
            if (key.IsNull || cache == null) return "";
            // The typed resolve, NOT TryResolveIdentifier -- Mutagen marks the identifier
            // overload obsolete ("not as optimized ... use as a last resort") and this runs
            // once per cell entry and once per linked ref, which on a 32-object hab interior
            // is dozens of lookups per dump.
            return cache.TryResolve<IStarfieldMajorRecordGetter>(key, out var rec)
                   && !string.IsNullOrEmpty(rec!.EditorID)
                ? rec.EditorID! : "";
        }

        /// The linked-reference block, in ONE place. Both cell-entry lists and the refr
        /// renderer call it, so a REFR's links can never again be visible from one view and
        /// invisible from another.
        private static void DumpLinkedRefs(IPlacedObjectGetter po, ILinkCache? cache, string indent)
        {
            if (po.LinkedReferences == null || po.LinkedReferences.Count == 0) return;
            Console.WriteLine($"{indent}    LinkedReferences: [{po.LinkedReferences.Count}]");
            foreach (var lr in po.LinkedReferences)
            {
                var kw = lr.KeywordOrReference.FormKey;
                var rf = lr.Reference.FormKey;
                string kwName = NameOf(kw, cache), rfName = NameOf(rf, cache);
                Console.WriteLine($"{indent}      {(kwName.Length > 0 ? kwName : "(unnamed)")} [{kw}]"
                                  + $" -> {rf}{(rfName.Length > 0 ? " " + rfName : "")}");
            }
        }

        /// ONE renderer for a cell entry, called by BOTH the persistent and temporary lists.
        /// ⛔ THEY WERE TWO HAND-WRITTEN LOOPS AND THEY HAD DIVERGED: the persistent one
        /// rendered MapMarker, TeleportDestination and LinkedReferences; the temporary one
        /// rendered a single summary line. So a temporary entry's links were INVISIBLE, and
        /// an absence in the view was indistinguishable from an absence in the plugin --
        /// found 2026-08-24 when a screenshot proved a temporary REFR carried two links this
        /// dump showed none of. Extracted rather than copied: a rule open-coded in N places
        /// is N bugs, and fixing the first makes the rest invisible.
        private static void DumpCellEntry(IPlacedGetter entry, ILinkCache? cache)
        {
            if (entry is IPlacedObjectGetter po)
            {
                string baseName = NameOf(po.Base.FormKey, cache);
                Console.WriteLine($"    PlacedObject {po.FormKey} EditorID={po.EditorID}"
                                  + $" Base={po.Base.FormKey}{(baseName.Length > 0 ? " " + baseName : "")}"
                                  + $" Pos={po.Position} Rot={po.Rotation}");
                if (po.MapMarker != null)
                {
                    var mm = po.MapMarker;
                    Console.WriteLine($"      MapMarker:");
                    Console.WriteLine($"        Flags:   {mm.Flags}");
                    Console.WriteLine($"        Name:    {mm.Name}");
                    Console.WriteLine($"        Type:    {mm.Type}");
                    Console.WriteLine($"        Unknown: {mm.Unknown}");
                    if (mm.UNAM != null) Console.WriteLine($"        UNAM:    {mm.UNAM}");
                    if (mm.VNAM != null) Console.WriteLine($"        VNAM:    {mm.VNAM}");
                    if (mm.VISI != null) Console.WriteLine($"        VISI:    {mm.VISI}");
                }
                if (po.TeleportDestination != null)
                {
                    var td = po.TeleportDestination;
                    Console.WriteLine($"      TeleportDestination:");
                    Console.WriteLine($"        Door:              {td.Door.FormKey}");
                    Console.WriteLine($"        TransitionInterior:{td.TransitionInterior.FormKey}");
                    Console.WriteLine($"        Position:          {td.Position}");
                    Console.WriteLine($"        Rotation:          {td.Rotation}");
                    Console.WriteLine($"        Flags:             {td.Flags}");
                }
                DumpLinkedRefs(po, cache, "  ");
            }
            else if (entry is IPlacedNpcGetter npc)
                Console.WriteLine($"    PlacedNpc {npc.FormKey} EditorID={npc.EditorID} Base={npc.Base.FormKey} Pos={npc.Position} Rot={npc.Rotation}");
            else
                Console.WriteLine($"    {entry.GetType().Name} {entry.FormKey}");
        }

        private static void DumpCell(ICellGetter cell, ILinkCache? cache = null)
        {
            Console.WriteLine($"--- Cell ---");
            Console.WriteLine($"  FormKey:  {cell.FormKey}");
            Console.WriteLine($"  EditorID: {cell.EditorID}");
            Console.WriteLine($"  Flags:    {cell.Flags}");
            Console.WriteLine($"  Persistent count:  {cell.Persistent.Count}");
            Console.WriteLine($"  Temporary count:   {cell.Temporary.Count}");
            Console.WriteLine();

            if (cell.Persistent.Count > 0)
            {
                Console.WriteLine("  Persistent entries:");
                foreach (var entry in cell.Persistent.Take(200))
                    DumpCellEntry(entry, cache);
                if (cell.Persistent.Count > 200)
                    Console.WriteLine($"    ... and {cell.Persistent.Count - 200} more");
            }

            if (cell.Temporary.Count > 0)
            {
                Console.WriteLine("  Temporary entries:");
                foreach (var entry in cell.Temporary)
                    DumpCellEntry(entry, cache);
            }
            Console.WriteLine();
        }

        private static void DumpLocationFull(ILocationGetter loc)
        {
            Console.WriteLine($"--- Location (Full) ---");
            Console.WriteLine($"  FormKey:  {loc.FormKey}");
            Console.WriteLine($"  EditorID: {loc.EditorID}");

            if (loc.MasterSpecialReferences != null)
            {
                Console.WriteLine($"  MasterSpecialReferences [{loc.MasterSpecialReferences.Count}]:");
                foreach (var r in loc.MasterSpecialReferences)
                    Console.WriteLine($"    Marker={r.Marker.FormKey} LocRefType={r.LocationRefType.FormKey} Location={r.Location.FormKey} Grid={r.Grid}");
            }
            if (loc.AddedSpecialReferences != null)
            {
                Console.WriteLine($"  AddedSpecialReferences [{loc.AddedSpecialReferences.Count}]:");
                foreach (var r in loc.AddedSpecialReferences)
                    Console.WriteLine($"    Marker={r.Marker.FormKey} LocRefType={r.LocationRefType.FormKey} Location={r.Location.FormKey} Grid={r.Grid}");
            }
            if (loc.MasterPersistLocationReferences != null)
            {
                Console.WriteLine($"  MasterPersistLocationReferences [{loc.MasterPersistLocationReferences.Count}]:");
                foreach (var r in loc.MasterPersistLocationReferences)
                    Console.WriteLine($"    Actor={r.Actor.FormKey} Location={r.Location.FormKey} Grid={r.Grid}");
            }
            Console.WriteLine();
        }

        private static void DumpPcmBranchNode(IPlanetContentManagerBranchNodeGetter node)
        {
            Console.WriteLine($"--- PcmBranchNode ---");
            Console.WriteLine($"  FormKey:  {node.FormKey}");
            Console.WriteLine($"  EditorID: {node.EditorID}");
            Console.WriteLine($"  NodeType: {node.NodeType}");
            Console.WriteLine($"  Parent:   {node.ParentNode.FormKey}");
            Console.WriteLine($"  Nodes [{node.Nodes.Count}]:");
            foreach (var n in node.Nodes)
                Console.WriteLine($"    {n.FormKey}");
            Console.WriteLine($"  Components [{node.Components.Count}]:");
            foreach (var c in node.Components)
            {
                Console.WriteLine($"    {c.GetType().Name}");
                if (c is IPlanetContentManagerContentPropertiesComponentGetter p)
                {
                    if (p.ZNAM.HasValue) Console.WriteLine($"      ZNAM: {p.ZNAM}");
                    if (p.YNAM.HasValue) Console.WriteLine($"      YNAM: {p.YNAM}");
                    if (p.XNAM.HasValue) Console.WriteLine($"      XNAM: {p.XNAM}");
                    if (p.WNAM.HasValue) Console.WriteLine($"      WNAM: {p.WNAM}");
                    if (p.VNAM.HasValue) Console.WriteLine($"      VNAM: {p.VNAM}");
                    if (p.UNAM.HasValue) Console.WriteLine($"      UNAM: {p.UNAM}");
                    if (p.NAM1.HasValue) Console.WriteLine($"      NAM1: {p.NAM1}");
                    if (!p.Global.IsNull)  Console.WriteLine($"      Global: {p.Global.FormKey}");
                    if (p.NAM3.HasValue) Console.WriteLine($"      NAM3: {p.NAM3}");
                    if (p.NAM4.HasValue) Console.WriteLine($"      NAM4: {BitConverter.ToString(p.NAM4.Value.ToArray())}");
                    if (p.NAM5.HasValue) Console.WriteLine($"      NAM5: {p.NAM5}");
                    if (p.NAM6.HasValue) Console.WriteLine($"      NAM6: {p.NAM6}");
                    if (p.NAM7.HasValue) Console.WriteLine($"      NAM7: {p.NAM7}");
                    if (p.NAM8.HasValue) Console.WriteLine($"      NAM8: {p.NAM8}");
                    if (p.NAM9.HasValue) Console.WriteLine($"      NAM9: {p.NAM9}");
                }
            }
            if (node.Conditions != null && node.Conditions.Count > 0)
            {
                Console.WriteLine($"  Conditions [{node.Conditions.Count}]:");
                foreach (var cond in node.Conditions)
                    Console.WriteLine($"    {cond}");
            }
            Console.WriteLine();
        }

        private static void DumpPcmContentNode(IPlanetContentManagerContentNodeGetter node)
        {
            Console.WriteLine($"--- PcmContentNode ---");
            Console.WriteLine($"  FormKey:  {node.FormKey}");
            Console.WriteLine($"  EditorID: {node.EditorID}");
            Console.WriteLine($"  Content:  {node.Content.FormKey}");
            Console.WriteLine($"  Parent:   {node.ParentNode.FormKey}");
            Console.WriteLine($"  Components [{node.Components.Count}]:");
            foreach (var c in node.Components)
                Console.WriteLine($"    {c.GetType().Name}");
            Console.WriteLine();
        }

        private static void DumpBook(IBookGetter book, List<IStarfieldModGetter> allMods)
        {
            Console.WriteLine($"--- Book ---");
            Console.WriteLine($"  FormKey:             {book.FormKey}");
            Console.WriteLine($"  EditorID:            {book.EditorID}");
            Console.WriteLine($"  Name:                {book.Name}");
            Console.WriteLine($"  Text:                {(book.Text?.String?.Length > 120 ? book.Text.String.Substring(0, 120) + "..." : book.Text?.String)}");
            Console.WriteLine($"  Description:         {book.Description}");
            Console.WriteLine($"  DataSlateType:       {book.DataSlateType}");
            Console.WriteLine($"  DataSlateHeaderLeft: {book.DataSlateHeaderLeft}");
            Console.WriteLine($"  DataSlateHeaderRight:{book.DataSlateHeaderRight}");
            Console.WriteLine($"  Flags:               {book.Flags}");
            Console.WriteLine($"  Value:               {book.Value}");
            Console.WriteLine($"  Weight:              {book.Weight}");
            Console.WriteLine($"  TextOffsetX:         {book.TextOffsetX}");
            Console.WriteLine($"  TextOffsetY:         {book.TextOffsetY}");
            Console.WriteLine($"  InventoryArt:        {(book.InventoryArt.IsNull ? "null" : book.InventoryArt.FormKey.ToString())}");
            Console.WriteLine($"  Scene:               {(book.Scene.IsNull ? "null" : book.Scene.FormKey.ToString())}");
            Console.WriteLine($"  FeaturedItemMessage: {(book.FeaturedItemMessage.IsNull ? "null" : book.FeaturedItemMessage.FormKey.ToString())}");
            if (book.Keywords != null && book.Keywords.Count > 0)
            {
                Console.WriteLine($"  Keywords [{book.Keywords.Count}]:");
                foreach (var kw in book.Keywords)
                    Console.WriteLine($"    {kw.FormKey}");
            }
            if (book.PickupSound != null)
                Console.WriteLine($"  PickupSound:         {book.PickupSound}");
            if (book.DropdownSound != null)
                Console.WriteLine($"  DropdownSound:       {book.DropdownSound}");
            if (book.Teaches != null)
                Console.WriteLine($"  Teaches:             {book.Teaches}");
            if (book.Model != null)
                Console.WriteLine($"  Model:               {book.Model.File}");
            if (book.VirtualMachineAdapter != null)
            {
                Console.WriteLine($"  Scripts [{book.VirtualMachineAdapter.Scripts.Count}]:");
                foreach (var s in book.VirtualMachineAdapter.Scripts)
                    Console.WriteLine($"    {s.Name}");
            }
            // If Scene is set, look it up inside Quest sub-records (SCEN are embedded in QUST)
            if (!book.Scene.IsNull)
            {
                Console.WriteLine();
                Console.WriteLine($"  === Linked Scene: {book.Scene.FormKey} ===");
                ISceneGetter? linkedScene = null;
                IQuestGetter? ownerQuest = null;
                foreach (var mod in allMods)
                {
                    foreach (var quest in mod.Quests)
                    {
                        if (quest.Scenes == null) continue;
                        foreach (var s in quest.Scenes)
                            if (s.FormKey == book.Scene.FormKey)
                            { linkedScene = s; ownerQuest = quest; break; }
                        if (linkedScene != null) break;
                    }
                    if (linkedScene != null) break;
                }
                if (linkedScene != null)
                {
                    Console.WriteLine($"  Owner Quest: {ownerQuest!.FormKey} ({ownerQuest.EditorID})");
                    DumpScene(linkedScene);
                }
                else
                    Console.WriteLine($"  (Scene {book.Scene.FormKey} not found in any Quest's Scenes list)");
            }
            Console.WriteLine();
        }

        private static void DumpScene(ISceneGetter scene)
        {
            Console.WriteLine($"--- Scene ---");
            Console.WriteLine($"  FormKey:  {scene.FormKey}");
            Console.WriteLine($"  EditorID: {scene.EditorID}");
            Console.WriteLine($"  Quest:    {(scene.Quest.IsNull ? "null" : scene.Quest.FormKey.ToString())}");
            Console.WriteLine($"  Flags:    {scene.Flags}");
            Console.WriteLine($"  VNAM:     {(scene.VNAM.HasValue ? BitConverter.ToString(scene.VNAM.Value.ToArray()) : "null")}");
            Console.WriteLine($"  Notes:    {scene.Notes}");
            if (scene.Actors != null && scene.Actors.Count > 0)
            {
                Console.WriteLine($"  Actors [{scene.Actors.Count}]:");
                foreach (var a in scene.Actors)
                    Console.WriteLine($"    ID={a.ID} BehaviorFlags={a.BehaviorFlags} Flags={a.Flags}");
            }
            if (scene.Actions != null && scene.Actions.Count > 0)
            {
                Console.WriteLine($"  Actions [{scene.Actions.Count}]:");
                foreach (var a in scene.Actions)
                {
                    Console.WriteLine($"    [{a.Index}] {a.GetType().Name} Name={a.Name} AliasID={a.AliasID} StartPhase={a.StartPhase} EndPhase={a.EndPhase} Flags={a.Flags}");
                    if (a is IDialogueSceneActionGetter da)
                    {
                        Console.WriteLine($"      Topic:           {(da.Topic.IsNull ? "null" : da.Topic.FormKey.ToString())}");
                        Console.WriteLine($"      DialogueSubtype: {(da.DialogueSubtype.IsNull ? "null" : da.DialogueSubtype.FormKey.ToString())}");
                        if (da.WED0 != null) Console.WriteLine($"      WED0 (sound):    {da.WED0}");
                    }
                    else if (a is IRadioSceneActionGetter ra)
                    {
                        Console.WriteLine($"      Topic:           {(ra.Topic.IsNull ? "null" : ra.Topic.FormKey.ToString())}");
                        Console.WriteLine($"      DialogueSubtype: {(ra.DialogueSubtype.IsNull ? "null" : ra.DialogueSubtype.FormKey.ToString())}");
                        if (ra.WED0 != null) Console.WriteLine($"      WED0 (sound):    {ra.WED0}");
                        if (ra.WED1 != null) Console.WriteLine($"      WED1 (sound):    {ra.WED1}");
                    }
                }
            }
            if (scene.Phases != null && scene.Phases.Count > 0)
            {
                Console.WriteLine($"  Phases [{scene.Phases.Count}]:");
                foreach (var p in scene.Phases)
                    Console.WriteLine($"    Name={p.Name} Flags={p.Flags} StartConds={p.StartConditions.Count} CompletionConds={p.CompletionConditions.Count}");
            }
            Console.WriteLine();
        }

        private static void DumpQuest(IQuestGetter quest)
        {
            Console.WriteLine($"--- Quest ---");
            Console.WriteLine($"  FormKey:      {quest.FormKey}");
            Console.WriteLine($"  EditorID:     {quest.EditorID}");
            Console.WriteLine($"  Name:         {quest.Name}");
            Console.WriteLine($"  Priority:     {quest.Data?.Priority}");
            Console.WriteLine($"  Type:         {quest.Data?.Type}");
            Console.WriteLine($"  Flags:        {quest.Data?.Flags}");
            Console.WriteLine($"  DialogBranches[{quest.DialogBranches.Count}]:");
            foreach (var b in quest.DialogBranches)
                Console.WriteLine($"    {b.FormKey} {b.EditorID} Category={b.Category} Flags={b.Flags} StartingTopic={b.StartingTopic.FormKey}");
            Console.WriteLine($"  DialogTopics  [{quest.DialogTopics.Count}]:");
            foreach (var t in quest.DialogTopics)
                Console.WriteLine($"    {t.FormKey} {t.EditorID} Branch={t.Branch.FormKey} Category={t.Category} Subtype={t.Subtype}");
            Console.WriteLine();
        }

        private static void DumpQuestVMAD(IQuestGetter quest)
        {
            Console.WriteLine($"--- Quest VMAD ---");
            Console.WriteLine($"  FormKey:  {quest.FormKey}");
            Console.WriteLine($"  EditorID: {quest.EditorID}");
            Console.WriteLine($"  Name:     {quest.Name}");
            Console.WriteLine($"  Flags:    0x{(uint)(quest.Data?.Flags ?? 0):X8}");
            Console.WriteLine();

            var vma = quest.VirtualMachineAdapter;
            if (vma == null)
            {
                Console.WriteLine("  VirtualMachineAdapter: NULL");
                Console.WriteLine();
                return;
            }

            Console.WriteLine($"  VirtualMachineAdapter:");
            Console.WriteLine($"    Version:              {vma.Version}");
            Console.WriteLine($"    ObjectFormat:         {vma.ObjectFormat}");
            Console.WriteLine($"    ExtraBindDataVersion: {vma.ExtraBindDataVersion}");
            Console.WriteLine();

            // Fragment script (auto-generated __QF_ script)
            if (vma.Script != null)
            {
                Console.WriteLine($"    Fragment Script (vma.Script):");
                Console.WriteLine($"      Name:  {vma.Script.Name}");
                Console.WriteLine($"      Flags: 0x{(ushort)vma.Script.Flags:X4}");
                if (vma.Script.Properties.Count > 0)
                {
                    Console.WriteLine($"      Properties [{vma.Script.Properties.Count}]:");
                    foreach (var prop in vma.Script.Properties)
                        DumpScriptProperty(prop, "        ");
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"    Fragment Script (vma.Script): null");
                Console.WriteLine();
            }

            // Stage/objective fragments
            Console.WriteLine($"    Fragments [{vma.Fragments?.Count ?? 0}]:");
            if (vma.Fragments != null)
                foreach (var frag in vma.Fragments)
                    Console.WriteLine($"      Stage={frag.Stage} StageIndex={frag.StageIndex} Unknown={frag.Unknown} ScriptName={frag.ScriptName} FragmentName={frag.FragmentName}");
            Console.WriteLine();

            // Quest-level scripts
            Console.WriteLine($"    Scripts [{vma.Scripts.Count}]:");
            for (int si = 0; si < vma.Scripts.Count; si++)
            {
                var script = vma.Scripts[si];
                Console.WriteLine($"      [{si}] Name={script.Name}  Flags=0x{(ushort)script.Flags:X4}");
                Console.WriteLine($"           Properties [{script.Properties.Count}]:");
                foreach (var prop in script.Properties)
                    DumpScriptProperty(prop, "             ");
            }
            Console.WriteLine();

            // VMA-side alias bindings (QuestFragmentAlias)
            Console.WriteLine($"    VMA.Aliases [{vma.Aliases?.Count ?? 0}]:");
            if (vma.Aliases != null)
            {
                for (int ai = 0; ai < vma.Aliases.Count; ai++)
                {
                    var fa = vma.Aliases[ai];
                    Console.WriteLine($"      [{ai}] Version={fa.Version}  ObjectFormat={fa.ObjectFormat}");
                    // Every scalar on the linking property, not just the three that used to print.
                    // The alias INDEX lives on this property, and without it the dump cannot say
                    // WHICH alias a script is attached to -- the same swallowed-field failure that
                    // left the quest fragment reader unable to show per-fragment ScriptName.
                    var linkBits = new List<string>();
                    foreach (var pi in fa.Property.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (pi.GetIndexParameters().Length > 0) continue;
                        object? v; try { v = pi.GetValue(fa.Property); } catch { continue; }
                        if (v == null || v is System.Collections.ICollection) continue;
                        linkBits.Add($"{pi.Name}={v}");
                    }
                    Console.WriteLine($"           Property: {string.Join("  ", linkBits)}");
                    Console.WriteLine($"           Scripts [{fa.Scripts.Count}]:");
                    foreach (var s in fa.Scripts)
                    {
                        Console.WriteLine($"             Script: Name={s.Name}  Flags=0x{(ushort)s.Flags:X4}");
                        foreach (var prop in s.Properties)
                            DumpScriptProperty(prop, "               ");
                    }
                }
            }
            Console.WriteLine();

            // Quest.Aliases (gameplay side)
            Console.WriteLine($"  Quest.Aliases [{quest.Aliases?.Count ?? 0}]:");
            if (quest.Aliases != null)
            {
                foreach (var alias in quest.Aliases)
                {
                    if (alias is IQuestReferenceAliasGetter refAlias)
                    {
                        Console.WriteLine($"    [RefAlias] ID={refAlias.ID}  Name={refAlias.Name}  Flags=0x{(uint)refAlias.Flags:X8}");
                        Console.WriteLine($"      UniqueActor:    {(refAlias.UniqueActor.IsNull    ? "null" : refAlias.UniqueActor.FormKey.ToString())}");
                        Console.WriteLine($"      ForcedRef:      {(refAlias.ForcedReference.IsNull ? "null" : refAlias.ForcedReference.FormKey.ToString())}");
                        Console.WriteLine($"      UniqueBase:     {(refAlias.UniqueBaseForm.IsNull   ? "null" : refAlias.UniqueBaseForm.FormKey.ToString())}");
                        if (refAlias.CreateReferenceToObject != null)
                            Console.WriteLine($"      CreateRefTo:    {refAlias.CreateReferenceToObject.Object.FormKey}");
                        if (refAlias.Conditions != null && refAlias.Conditions.Count > 0)
                        {
                            Console.WriteLine($"      Conditions [{refAlias.Conditions.Count}]:");
                            foreach (var cond in refAlias.Conditions)
                                DumpConditionBrief(cond, "        ");
                        }
                    }
                    else if (alias is IQuestLocationAliasGetter locAlias)
                    {
                        // Was an inline copy printing raw FormKeys where the sibling copy resolved
                        // names -- two dumpers for one type, already disagreeing. One helper now
                        // (2026-08-07); it resolves names and reports any property it did not render.
                        //
                        // null mod list: DumpQuestVMAD does not take one, and ResolveName's declared
                        // fallback is the bare FormKey -- which is EXACTLY what this site printed
                        // before, so behaviour here is preserved rather than quietly degraded.
                        // Threading allMods down to the VMAD dumper would upgrade this site to
                        // resolved names; that is a signature change through its callers and is a
                        // separate, larger edit than the one asked for.
                        DumpLocAlias(locAlias, null, "    ");
                    }
                    else if (alias is IQuestCollectionAliasGetter colAlias)
                    {
                        // ID/Name are not on the getter interface — try reflection
                        var t = alias.GetType();
                        var idProp   = t.GetProperty("ID",   System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var nameProp = t.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var idVal   = idProp   != null ? idProp.GetValue(alias)   : "?";
                        var nameVal = nameProp != null ? nameProp.GetValue(alias) : "?";
                        Console.WriteLine($"    [ColAlias]  ID={idVal}  Name={nameVal}  Collection=[{colAlias.Collection.Count}]");
                        foreach (var ca in colAlias.Collection)
                        {
                            Console.WriteLine($"      CollectionEntry ID={ca.ID}  MaxFill={ca.MaxInitialFillCount}  ALAM={ca.ALAM}");
                            if (ca.ReferenceAlias != null)
                            {
                                var ra = ca.ReferenceAlias;
                                var raIdProp   = ra.GetType().GetProperty("ID",   System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                var raNamProp  = ra.GetType().GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                var raFlagProp = ra.GetType().GetProperty("Flags",System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                Console.WriteLine($"        RefAlias ID={raIdProp?.GetValue(ra)}  Name={raNamProp?.GetValue(ra)}  Flags={raFlagProp?.GetValue(ra)}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    [Unknown alias type={alias.GetType().Name}]");
                    }
                }
            }
            Console.WriteLine();

            // Objectives
            Console.WriteLine($"  Objectives [{quest.Objectives.Count}]:");
            foreach (var obj in quest.Objectives)
                Console.WriteLine($"    [{obj.Index}] Flags={obj.Flags}  Text={obj.DisplayText}");
            Console.WriteLine();

            // Stages
            Console.WriteLine($"  Stages [{quest.Stages.Count}]:");
            foreach (var stage in quest.Stages)
            {
                Console.Write($"    Index={stage.Index}  Flags={stage.Flags}");
                if (stage.LogEntries.Count > 0)
                    Console.Write($"  LogEntries=[{string.Join(", ", stage.LogEntries.Select(e => $"\"{e.Entry}\""))}]");
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        private static void DumpScriptProperty(IScriptPropertyGetter prop, string indent)
        {
            switch (prop)
            {
                case IScriptObjectPropertyGetter obj:
                    Console.WriteLine($"{indent}[Obj]    Name={prop.Name}  Flags=0x{(ushort)prop.Flags:X4}  Object={obj.Object.FormKey}");
                    break;
                case IScriptIntPropertyGetter i:
                    Console.WriteLine($"{indent}[Int]    Name={prop.Name}  Flags=0x{(ushort)prop.Flags:X4}  Value={i.Data}");
                    break;
                case IScriptBoolPropertyGetter b:
                    Console.WriteLine($"{indent}[Bool]   Name={prop.Name}  Flags=0x{(ushort)prop.Flags:X4}  Value={b.Data}");
                    break;
                case IScriptFloatPropertyGetter f:
                    Console.WriteLine($"{indent}[Float]  Name={prop.Name}  Flags=0x{(ushort)prop.Flags:X4}  Value={f.Data}");
                    break;
                case IScriptStringPropertyGetter s:
                    Console.WriteLine($"{indent}[String] Name={prop.Name}  Flags=0x{(ushort)prop.Flags:X4}  Value={s.Data}");
                    break;
                default:
                    Console.WriteLine($"{indent}[{prop.GetType().Name}] Name={prop.Name}  Flags=0x{(ushort)prop.Flags:X4}");
                    break;
            }
        }

        private static void DumpConditionBrief(IConditionGetter cond, string indent)
            => DumpConditionBrief(cond, indent, null);

        /// <summary>
        /// ⛔ THIS USED TO RENDER THE OPERATOR AND NOT THE PARAMETERS, and that is how a
        /// _lvl10 ship recipe printed as `GetLevel >= 1.00` with nothing to say which level it
        /// meant (2026-08-17, the reactor gating spike). Only TWO condition kinds -- GetGlobal
        /// and GetStage -- were special-cased; every other function's parameters were simply
        /// absent from the output, and an absent field prints as nothing, which reads as "this
        /// condition has no parameters" rather than "this tool only knows two".
        ///
        /// Same shape as the SnapTemplate node table that printed "?" for 53 of 59 node kinds,
        /// and the same fix: enumerate generically and fall back honestly. Parameters are read
        /// by REFLECTION over the ConditionData's First/Second/Third parameter properties, so a
        /// condition function this file has never heard of still renders its arguments.
        /// </summary>
        private static void DumpConditionBrief(IConditionGetter cond, string indent,
                                               List<IStarfieldModGetter>? allMods)
        {
            string op  = cond.CompareOperator.ToString();
            string val = cond is IConditionFloatGetter cf ? cf.ComparisonValue.ToString("F2") : "?";
            string fn  = cond.Data?.GetType().Name ?? "?";

            var parts = new List<string>();
            if (cond.Data != null)
            {
                foreach (var prop in cond.Data.GetType().GetProperties())
                {
                    if (!prop.Name.EndsWith("Parameter", StringComparison.Ordinal)) continue;
                    object? raw;
                    try { raw = prop.GetValue(cond.Data); } catch { continue; }
                    if (raw == null) continue;
                    parts.Add($"{prop.Name.Replace("Parameter", "")}={DescribeParam(raw, allMods)}");
                }
                // RunOnType / Reference say WHOSE level or keyword is being tested, which is
                // half the meaning of the condition and was also absent.
                foreach (var name in new[] { "RunOnType", "Reference" })
                {
                    var prop = cond.Data.GetType().GetProperty(name);
                    if (prop == null) continue;
                    object? raw;
                    try { raw = prop.GetValue(cond.Data); } catch { continue; }
                    if (raw == null) continue;
                    var s = DescribeParam(raw, allMods);
                    if (!string.IsNullOrEmpty(s) && s != "Subject" && s != "Null")
                        parts.Add($"{name}={s}");
                }
            }
            string extra = parts.Count > 0 ? " " + string.Join(" ", parts) : "";
            Console.WriteLine($"{indent}{fn}{extra} {op} {val}  flags=0x{(byte)cond.Flags:X2}");
        }

        /// Render one condition parameter: resolve a FormLink to its EditorID where we can,
        /// because a bare FormKey cannot be eyeballed for "is this the right perk".
        private static string DescribeParam(object raw, List<IStarfieldModGetter>? allMods)
        {
            var t = raw.GetType();
            var linkProp = t.GetProperty("Link");
            if (linkProp != null)
            {
                var link = linkProp.GetValue(raw);
                var fkProp = link?.GetType().GetProperty("FormKey");
                if (fkProp?.GetValue(link) is FormKey fk)
                {
                    if (fk.IsNull) return "Null";
                    var eid = ResolveEditorIdOnly(fk, allMods);
                    return string.IsNullOrEmpty(eid) || eid == "?" ? fk.ToString() : $"{eid} [{fk}]";
                }
            }
            if (raw is FormKey k)
            {
                if (k.IsNull) return "Null";
                var eid = ResolveEditorIdOnly(k, allMods);
                return string.IsNullOrEmpty(eid) || eid == "?" ? k.ToString() : $"{eid} [{k}]";
            }
            return raw.ToString() ?? "?";
        }

        private static void DumpDialogBranch(IDialogBranchGetter branch)
        {
            Console.WriteLine($"--- DialogBranch ---");
            Console.WriteLine($"  FormKey:       {branch.FormKey}");
            Console.WriteLine($"  EditorID:      {branch.EditorID}");
            Console.WriteLine($"  Quest:         {branch.Quest.FormKey}");
            Console.WriteLine($"  Category:      {branch.Category}");
            Console.WriteLine($"  Flags:         {branch.Flags}");
            Console.WriteLine($"  StartingTopic: {(branch.StartingTopic.IsNull ? "null" : branch.StartingTopic.FormKey.ToString())}");
            Console.WriteLine();
        }

        private static void DumpQuestFull(IQuestGetter quest)
        {
            Console.WriteLine($"=== Quest (FULL) ===");
            Console.WriteLine($"  FormKey:  {quest.FormKey}");
            Console.WriteLine($"  EditorID: {quest.EditorID}");
            Console.WriteLine($"  Name:     {quest.Name}");
            Console.WriteLine($"  Type:     {quest.Data?.Type}");
            Console.WriteLine($"  Flags:    {quest.Data?.Flags}  (raw: 0x{(uint)(quest.Data?.Flags ?? 0):X8})");
            Console.WriteLine();

            Console.WriteLine($"  Stages [{quest.Stages.Count}]:");
            foreach (var stage in quest.Stages)
                Console.WriteLine($"    Index={stage.Index}  Flags={stage.Flags}");
            Console.WriteLine();

            Console.WriteLine($"  Aliases [{quest.Aliases!.Count}]:");
            foreach (var alias in quest.Aliases)
            {
                if (alias is IQuestReferenceAliasGetter refAlias)
                {
                    Console.WriteLine($"    [RefAlias] ID={refAlias.ID} Name={refAlias.Name}");
                    Console.WriteLine($"      Flags:          {refAlias.Flags}");
                    Console.WriteLine($"      UniqueActor:    {(refAlias.UniqueActor.IsNull   ? "null" : refAlias.UniqueActor.FormKey.ToString())}");
                    Console.WriteLine($"      ForcedRef:      {(refAlias.ForcedReference.IsNull ? "null" : refAlias.ForcedReference.FormKey.ToString())}");
                    Console.WriteLine($"      UniqueBase:     {(refAlias.UniqueBaseForm.IsNull  ? "null" : refAlias.UniqueBaseForm.FormKey.ToString())}");
                }
                else
                {
                    Console.WriteLine($"    [Alias type={alias.GetType().Name}] {alias}");
                }
            }
            Console.WriteLine();

            Console.WriteLine($"  DialogBranches [{quest.DialogBranches.Count}]:");
            foreach (var branch in quest.DialogBranches)
            {
                Console.WriteLine($"    [{branch.FormKey}] EditorID={branch.EditorID}");
                Console.WriteLine($"      Category:      {branch.Category}");
                Console.WriteLine($"      Flags:         {branch.Flags}");
                Console.WriteLine($"      StartingTopic: {(branch.StartingTopic.IsNull ? "null" : branch.StartingTopic.FormKey.ToString())}");
            }
            Console.WriteLine();

            Console.WriteLine($"  Scenes [{quest.Scenes?.Count ?? 0}]:");
            if (quest.Scenes != null)
            {
                foreach (var scene in quest.Scenes)
                {
                    Console.WriteLine($"    [{scene.FormKey}] EditorID={scene.EditorID}");
                    Console.WriteLine($"      Quest:    {(scene.Quest.IsNull ? "null" : scene.Quest.FormKey.ToString())}");
                    Console.WriteLine($"      Flags:    0x{(uint)scene.Flags.GetValueOrDefault():X8} ({scene.Flags})");
                    Console.WriteLine($"      VNAM:     {(scene.VNAM.HasValue ? BitConverter.ToString(scene.VNAM.Value.ToArray()) : "null")}");
                    if (scene.Conditions != null && scene.Conditions.Count > 0)
                    {
                        Console.WriteLine($"      Conditions [{scene.Conditions.Count}]:");
                        foreach (var cond in scene.Conditions)
                        {
                            string op  = cond.CompareOperator.ToString();
                            string val = cond is IConditionFloatGetter cf ? cf.ComparisonValue.ToString("F0") : "?";
                            string fn  = cond.Data?.GetType().Name ?? "?";
                            string p1  = "";
                            if (cond.Data is IGetStageConditionDataGetter gs)
                                p1 = $" quest={gs.FirstParameter.Link.FormKey} stage={gs.SecondParameter}";
                            else if (cond.Data is IGetStageDoneConditionDataGetter gsd)
                                p1 = $" quest={gsd.FirstParameter.Link.FormKey} stage={gsd.SecondParameter}";
                            Console.WriteLine($"        {fn}{p1} {op} {val}  flags=0x{(byte)cond.Flags:X2}");
                        }
                    }
                    else
                        Console.WriteLine($"      Conditions: none");
                    if (scene.Actors != null && scene.Actors.Count > 0)
                    {
                        Console.WriteLine($"      Actors [{scene.Actors.Count}]:");
                        foreach (var a in scene.Actors)
                            Console.WriteLine($"        ID={a.ID} BehaviorFlags={a.BehaviorFlags} Flags={a.Flags}");
                    }
                    if (scene.Phases != null && scene.Phases.Count > 0)
                    {
                        Console.WriteLine($"      Phases [{scene.Phases.Count}]:");
                        foreach (var p in scene.Phases)
                        {
                            Console.WriteLine($"        Name={p.Name} EditorWidth={p.EditorWidth} Flags={p.Flags}");
                            if (p.StartConditions.Count > 0)
                            {
                                Console.WriteLine($"          StartConditions [{p.StartConditions.Count}]:");
                                foreach (var cond in p.StartConditions)
                                {
                                    string op  = cond.CompareOperator.ToString();
                                    string val = cond is IConditionFloatGetter cf2 ? cf2.ComparisonValue.ToString("F0") : "?";
                                    string fn  = cond.Data?.GetType().Name ?? "?";
                                    string p1  = "";
                                    if (cond.Data is IGetStageConditionDataGetter gs2)
                                        p1 = $" quest={gs2.FirstParameter.Link.FormKey} stage={gs2.SecondParameter}";
                                    else if (cond.Data is IGetStageDoneConditionDataGetter gsd2)
                                        p1 = $" quest={gsd2.FirstParameter.Link.FormKey} stage={gsd2.SecondParameter}";
                                    Console.WriteLine($"            {fn}{p1} {op} {val}");
                                }
                            }
                            if (p.CompletionConditions.Count > 0)
                            {
                                Console.WriteLine($"          CompletionConditions [{p.CompletionConditions.Count}]:");
                                foreach (var cond in p.CompletionConditions)
                                {
                                    string op  = cond.CompareOperator.ToString();
                                    string val = cond is IConditionFloatGetter cf3 ? cf3.ComparisonValue.ToString("F0") : "?";
                                    string fn  = cond.Data?.GetType().Name ?? "?";
                                    Console.WriteLine($"            {fn} {op} {val}");
                                }
                            }
                        }
                    }
                    if (scene.Actions != null && scene.Actions.Count > 0)
                    {
                        Console.WriteLine($"      Actions [{scene.Actions.Count}]:");
                        foreach (var a in scene.Actions)
                        {
                            Console.WriteLine($"        [{a.Index}] {a.GetType().Name} Name={a.Name} AliasID={a.AliasID} StartPhase={a.StartPhase} EndPhase={a.EndPhase} Flags={a.Flags}");
                            if (a is IDialogueSceneActionGetter da)
                            {
                                Console.WriteLine($"          Topic:           {(da.Topic.IsNull ? "null" : da.Topic.FormKey.ToString())}");
                                Console.WriteLine($"          DialogueSubtype: {(da.DialogueSubtype.IsNull ? "null" : da.DialogueSubtype.FormKey.ToString())}");
                            }
                            else if (a is IRadioSceneActionGetter ra2)
                            {
                                Console.WriteLine($"          Topic:           {(ra2.Topic.IsNull ? "null" : ra2.Topic.FormKey.ToString())}");
                            }
                            else if (a is IPlayerDialogueSceneActionGetter pda)
                            {
                                Console.WriteLine($"          DialogueList [{pda.DialogueList.Count}]:");
                                foreach (var item in pda.DialogueList)
                                {
                                    string pc = item.PlayerChoice.IsNull ? "null" : item.PlayerChoice.FormKey.ToString();
                                    string ss = item.StartScene.IsNull  ? "null" : item.StartScene.FormKey.ToString();
                                    string nr = item.NpcResponse.IsNull  ? "null" : item.NpcResponse.FormKey.ToString();
                                    Console.WriteLine($"            PlayerChoice={pc}  StartScene={ss}  NpcResponse={nr}  PhaseIndex={item.PhaseIndex}  PAPN={item.PAPN}");
                                }
                            }
                        }
                    }
                    Console.WriteLine();
                }
            }
            Console.WriteLine();

            Console.WriteLine($"  DialogTopics [{quest.DialogTopics.Count}]:");
            foreach (var topic in quest.DialogTopics)
            {
                Console.WriteLine($"    [{topic.FormKey}] EditorID={topic.EditorID}");
                Console.WriteLine($"      Name:     {topic.Name}");
                Console.WriteLine($"      Branch:   {(topic.Branch.IsNull ? "null" : topic.Branch.FormKey.ToString())}");
                Console.WriteLine($"      Category: {topic.Category}");
                Console.WriteLine($"      Subtype:  {topic.Subtype}");
                Console.WriteLine($"      Responses [{topic.Responses?.Count ?? 0}]:");
                if (topic.Responses != null)
                {
                    foreach (var resp in topic.Responses)
                    {
                        Console.WriteLine($"        [INFO {resp.FormKey}] EditorID={resp.EditorID}");
                        Console.WriteLine($"          MajorFlags:       {resp.MajorFlags}");
                        Console.WriteLine($"          Speaker:          {(!resp.Speaker.IsNull ? resp.Speaker.FormKey.ToString() : "null")}");
                        Console.WriteLine($"          Prompt:           {resp.Prompt}");
                        Console.WriteLine($"          StartScene:       {(!resp.StartScene.IsNull ? resp.StartScene.FormKey.ToString() : "null")}");
                        Console.WriteLine($"          SubtitlePriority: {resp.SubtitlePriority}");
                        Console.WriteLine($"          TPIC:             {(resp.TPIC.HasValue ? BitConverter.ToString(resp.TPIC.Value.ToArray()) : "null")}");
                        if (resp.SetParentQuestStage != null)
                            Console.WriteLine($"          SetParentQuestStage: OnBegin={resp.SetParentQuestStage.OnBegin} OnEnd={resp.SetParentQuestStage.OnEnd}");
                        if (resp.Conditions != null && resp.Conditions.Count > 0)
                        {
                            Console.WriteLine($"          Conditions [{resp.Conditions.Count}]:");
                            foreach (var cond in resp.Conditions)
                            {
                                string op  = cond.CompareOperator.ToString();
                                string val = cond is IConditionFloatGetter cf ? cf.ComparisonValue.ToString("F0") : "?";
                                string fn  = cond.Data?.GetType().Name ?? "?";
                                string p1  = "";
                                if (cond.Data is IGetStageConditionDataGetter gs)
                                    p1 = $" quest={gs.FirstParameter.Link.FormKey} stage={gs.SecondParameter}";
                                else if (cond.Data is IGetStageDoneConditionDataGetter gsd)
                                    p1 = $" quest={gsd.FirstParameter.Link.FormKey} stage={gsd.SecondParameter}";
                                else if (cond.Data is IGetIsAliasRefConditionDataGetter gia)
                                    p1 = $" alias={gia.FirstParameter}";
                                Console.WriteLine($"            {fn}{p1} {op} {val}");
                            }
                        }
                        Console.WriteLine($"          ResponseLines [{resp.Responses.Count}]:");
                        for (int i = 0; i < resp.Responses.Count; i++)
                        {
                            var r = resp.Responses[i];
                            Console.WriteLine($"            [Line {i}] WEMFile=0x{r.WEMFile:X8} Emotion={r.Emotion.FormKey} EmotionOut={r.EmotionOut}");
                            Console.WriteLine($"              ResponseText: {r.ResponseText}");
                            Console.WriteLine($"              TextHash:     {(r.TextHash.HasValue ? BitConverter.ToString(r.TextHash.Value.ToArray()) : "null")}");
                            if (r.TROTs != null && r.TROTs.Count > 0)
                                foreach (var trot in r.TROTs)
                                    Console.WriteLine($"              TROT: VoiceType={trot.VoiceType.FormKey} EmotionOut={trot.EmotionOut}");
                        }
                    }
                }
            }
            Console.WriteLine();
        }

        private static void DumpDialogTopic(IDialogTopicGetter topic)
        {
            Console.WriteLine($"--- DialogTopic ---");
            Console.WriteLine($"  FormKey:  {topic.FormKey}");
            Console.WriteLine($"  EditorID: {topic.EditorID}");
            Console.WriteLine($"  Name:     {topic.Name}");
            Console.WriteLine($"  Quest:    {topic.Quest.FormKey}");
            Console.WriteLine($"  Branch:   {(topic.Branch.IsNull ? "null" : topic.Branch.FormKey.ToString())}");
            if (topic.Responses != null && topic.Responses.Count > 0)
            {
                Console.WriteLine($"  Responses [{topic.Responses.Count}]:");
                foreach (var resp in topic.Responses)
                {
                    Console.WriteLine($"    --- DialogResponses {resp.FormKey} ({resp.EditorID}) ---");
                    Console.WriteLine($"      MajorFlags: {resp.MajorFlags}");
                    if (resp.Speaker != null && !resp.Speaker.IsNull)
                        Console.WriteLine($"      Speaker:    {resp.Speaker.FormKey}");
                    foreach (var r in resp.Responses)
                    {
                        Console.WriteLine($"      Response:");
                        Console.WriteLine($"        ResponseText: {r.ResponseText}");
                        Console.WriteLine($"        WEMFile:      {r.WEMFile} (0x{r.WEMFile:X8})");
                        Console.WriteLine($"        Emotion:      {r.Emotion.FormKey}");
                        Console.WriteLine($"        ScriptNotes:  {r.ScriptNotes}");
                        if (r.RVSH != null) Console.WriteLine($"        RVSH:         {r.RVSH}");
                    }
                }
            }
            Console.WriteLine();
        }

        private static int DumpWorldspaceObjects(IStarfieldModGetter mod, string wsEditorId)
        {
            int found = 0;
            foreach (var ws in mod.Worldspaces)
            {
                if (ws.EditorID == null || !ws.EditorID.Contains(wsEditorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                Console.WriteLine($"=== Worldspace: {ws.EditorID} ({ws.FormKey}) ===");

                if (ws.TopCell != null)
                {
                    int n = ws.TopCell.Persistent.Count + ws.TopCell.Temporary.Count;
                    if (n > 0)
                    {
                        Console.WriteLine($"  [TopCell]");
                        foreach (var entry in ws.TopCell.Persistent.Concat(ws.TopCell.Temporary))
                        {
                            if (entry is IPlacedObjectGetter po)
                                Console.WriteLine($"    PlacedObject {po.FormKey} Base={po.Base.FormKey} EdID={po.EditorID} Pos={po.Position} Rot={po.Rotation}");
                            else if (entry is IPlacedNpcGetter npc)
                                Console.WriteLine($"    PlacedNpc    {npc.FormKey} Base={npc.Base.FormKey} EdID={npc.EditorID} Pos={npc.Position}");
                        }
                    }
                }

                foreach (var wsBlock in ws.SubCells)
                {
                    foreach (var wsSubBlock in wsBlock.Items)
                    {
                        foreach (var cell in wsSubBlock.Items)
                        {
                            int n2 = cell.Persistent.Count + cell.Temporary.Count;
                            if (n2 == 0) continue;
                            Console.WriteLine($"  [SubCell grid=({wsSubBlock.BlockNumberX},{wsSubBlock.BlockNumberY}) cell={cell.FormKey}] persistent={cell.Persistent.Count} temporary={cell.Temporary.Count}");
                            foreach (var entry in cell.Persistent.Concat(cell.Temporary))
                            {
                                if (entry is IPlacedObjectGetter po)
                                {
                                    Console.WriteLine($"    PlacedObject {po.FormKey} Base={po.Base.FormKey} EdID={po.EditorID} Pos={po.Position} Rot={po.Rotation}");
                                    found++;
                                }
                                else if (entry is IPlacedNpcGetter npc)
                                {
                                    Console.WriteLine($"    PlacedNpc    {npc.FormKey} Base={npc.Base.FormKey} EdID={npc.EditorID} Pos={npc.Position}");
                                    found++;
                                }
                            }
                        }
                    }
                }
            }
            return found;
        }

        private static int ListSmallWorldWorldspaces(List<IStarfieldModGetter> allMods, int minDnam)
        {
            // Build a SurfaceBlock lookup by FormKey across all mods
            var sbLookup = new Dictionary<FormKey, ISurfaceBlockGetter>();
            foreach (var mod in allMods)
                foreach (var sb in mod.SurfaceBlocks)
                    if (!sbLookup.ContainsKey(sb.FormKey))
                        sbLookup[sb.FormKey] = sb;

            int found = 0;
            foreach (var mod in allMods)
            {
                foreach (var ws in mod.Worldspaces)
                {
                    try
                    {
                        if (ws.Flags?.HasFlag(Worldspace.Flag.SmallWorld) != true) continue;
                        if (string.IsNullOrEmpty(ws.EditorID)) continue;

                        var overlayComp = ws.Components?.OfType<IWorldSpaceOverlayComponentGetter>().FirstOrDefault();
                        if (overlayComp == null) continue;

                        if (overlayComp.SurfaceBlock?.FormKey is FormKey sbKey && !sbKey.IsNull &&
                            sbLookup.TryGetValue(sbKey, out var sb))
                        {
                            int dnam = (int)(sb.DNAM?.First ?? 0);
                            if (dnam >= minDnam)
                            {
                                Console.WriteLine($"  \"{ws.EditorID}\",  // DNAM={dnam}x{dnam} SB={sb.EditorID} ANAM={sb.ANAM}");
                                found++;
                            }
                        }
                    }
                    catch { }
                }
            }
            return found;
        }

        private static void DumpNpcExtras(INpcGetter npc, List<IStarfieldModGetter>? allMods)
        {
            // Resolve a FormKey to its EditorID by scanning loaded mods.
            string Resolve(FormKey fk)
            {
                if (fk.IsNull) return "Null";
                if (allMods != null)
                {
                    foreach (var m in allMods)
                    {
                        var r = m.EnumerateMajorRecords().FirstOrDefault(x => x.FormKey == fk);
                        if (r != null) return $"{r.EditorID ?? "<no-eid>"} [{fk}]";
                    }
                }
                return fk.ToString();
            }

            Console.WriteLine("--- Npc Extras ---");

            // Keywords
            if (npc.Keywords != null && npc.Keywords.Count > 0)
            {
                Console.WriteLine($"  Keywords [{npc.Keywords.Count}]:");
                foreach (var kw in npc.Keywords)
                    Console.WriteLine($"    {Resolve(kw.FormKey)}");
            }

            // ObjectTemplates — carries OMOD chains (CCT_Skin variants etc.)
            if (npc.ObjectTemplates != null && npc.ObjectTemplates.Count > 0)
            {
                Console.WriteLine($"  ObjectTemplates [{npc.ObjectTemplates.Count}]:");
                for (int i = 0; i < npc.ObjectTemplates.Count; i++)
                {
                    var ot = npc.ObjectTemplates[i];
                    Console.WriteLine($"    [{i}] Default={ot.Default}  LevelMin={ot.LevelMin}  LevelMax={ot.LevelMax}");

                    if (ot.Keywords != null && ot.Keywords.Count > 0)
                    {
                        Console.WriteLine($"        Keywords [{ot.Keywords.Count}]:");
                        foreach (var kw in ot.Keywords)
                            Console.WriteLine($"          {Resolve(kw.FormKey)}");
                    }

                    if (ot.Includes != null && ot.Includes.Count > 0)
                    {
                        Console.WriteLine($"        Includes [{ot.Includes.Count}]:");
                        foreach (var inc in ot.Includes)
                        {
                            Console.WriteLine($"          OMOD: {Resolve(inc.Mod.FormKey)}");
                            DumpPropertiesReflection(inc, "            ", maxDepth: 1);
                        }
                    }

                    if (ot.Properties != null && ot.Properties.Count > 0)
                    {
                        Console.WriteLine($"        Properties [{ot.Properties.Count}]:");
                        for (int p = 0; p < ot.Properties.Count; p++)
                            Console.WriteLine($"          [{p}] {ot.Properties[p].GetType().Name}");
                    }
                }
            }

            // Properties — AV bindings, can encode species variant
            if (npc.Properties != null && npc.Properties.Count > 0)
            {
                Console.WriteLine($"  Properties [{npc.Properties.Count}]:");
                for (int i = 0; i < npc.Properties.Count; i++)
                {
                    var p = npc.Properties[i];
                    Console.Write($"    [{i}] ");
                    DumpPropertiesReflection(p, "      ", maxDepth: 2);
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Snap-node directions (Starfield.esm). From docs/formlib/ship_module.md — the whole
        /// point of dumping a SnapTemplate is checking a flipped variant's nodes got remapped,
        /// and a bare FormKey can't be eyeballed for that.
        /// </summary>
        /// Widened from `private` to `internal` 2026-08-17 so gen_checkpart can emit node
        /// directions WITHOUT a second copy of this table. A face-name mapping open-coded in
        /// two places is two places to get a flip wrong, and this is the exact table the
        /// Fore/Aft defect turned on.
        internal static readonly Dictionary<uint, string> SnapNodeDirections = new()
        {
            [0x0004AB6F] = "Fore",
            [0x0004AB70] = "Aft",
            [0x0004AB73] = "Port",
            [0x0004AB74] = "Starboard",
            [0x0004AB77] = "Top",
            [0x0004AB78] = "Bottom",
        };

        /// <summary>
        /// FormKey -> EditorID index over the whole load order, built once on first use.
        ///
        /// This MUST be an index, not a per-lookup scan. Resolving by walking
        /// EnumerateMajorRecords() for each FormKey is O(load order) per lookup: fine for the
        /// handful in one record, quadratic the moment you dump a group. Dumping the 397 ship
        /// modules in AvontechShipyards resolves ~12 FormKeys each, which is ~9,500 full scans
        /// of Starfield.esm — it does not finish. Worse, Starfield.esm is allMods[0], so every
        /// mod-local link (each part's own PackIn) pays the biggest scan before finding its
        /// target, and memoising results alone would not have saved it.
        /// </summary>
        private static Dictionary<FormKey, string>? _editorIdIndex;

        private static Dictionary<FormKey, string> EditorIdIndex(List<IStarfieldModGetter> allMods)
        {
            if (_editorIdIndex != null) return _editorIdIndex;
            var index = new Dictionary<FormKey, string>();
            foreach (var m in allMods)
                foreach (var r in m.EnumerateMajorRecords())
                    index[r.FormKey] = r.EditorID ?? "<no-eid>";   // later mods win, matching override order
            _editorIdIndex = index;
            return index;
        }

        /// <summary>Resolve a FormKey to "EditorID [FormKey]", or the bare FormKey if unknown.</summary>
        private static string ResolveName(FormKey fk, List<IStarfieldModGetter>? allMods)
        {
            if (fk.IsNull) return "(null)";
            if (allMods != null && EditorIdIndex(allMods).TryGetValue(fk, out var eid))
                return $"{eid} [{fk}]";
            return fk.ToString();
        }

        /// <summary>EditorID alone (no FormKey suffix) for a compact column; "?" if unresolvable.</summary>
        private static string ResolveEditorIdOnly(FormKey fk, List<IStarfieldModGetter>? allMods)
        {
            if (fk.IsNull) return "(null)";
            if (allMods != null && EditorIdIndex(allMods).TryGetValue(fk, out var eid))
                return eid.StartsWith("SnapNode_", StringComparison.OrdinalIgnoreCase)
                    ? eid.Substring("SnapNode_".Length)   // the prefix is on every one of them
                    : eid;
            return "?";
        }

        // A FormList's whole content is its Items array, and DumpRecord printed it as
        // "<enumerable BinaryOverlayListByLocationArray`1>" -- i.e. the one fact the record
        // carries was the one fact you could not read. That is not a cosmetic gap: a ship
        // part's flip SET is a FormList, so "which parts does the builder cycle between"
        // was unanswerable from this tool, and the answer had to be guessed from counting
        // GBFMs. Resolve each item to its EditorID, same as every other FormKey here.
        private static void DumpFormList(IFormListGetter flst, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"--- FormList (FLST) ---");
            Console.WriteLine($"  FormKey:  {flst.FormKey}");
            Console.WriteLine($"  EditorID: {flst.EditorID}");
            Console.WriteLine($"  Items [{flst.Items.Count}]:");
            foreach (var item in flst.Items)
                Console.WriteLine($"    {ResolveEditorIdOnly(item.FormKey, allMods)}  [{item.FormKey}]");
            Console.WriteLine();
        }

        // The whole quest record in one place, with a coverage report at the end.
        //
        // Four commands each showed a slice -- `quest` the header, `quest_vmad` the scripts and
        // stages, `audiolog` a partial alias dump, `qalias` the fills -- so reading a mission meant
        // knowing which of them held the field you wanted, and NOT knowing meant working off
        // whatever the one you picked happened to print.
        //
        // The coverage report is the point, not a flourish. Every previous gap here was silent: a
        // fill swallowed by an else-branch, a struct property printed as a type name, a condition
        // value printed as "?". A reader that cannot say what it left out is a reader you can only
        // trust by having read its source. So this one enumerates IQuestGetter's own properties and
        // names any it did not render -- if a field exists and is not shown above, it is listed
        // below by name, and the omission is visible instead of inferred.
        private static readonly HashSet<string> QuestPropsRendered = new()
        {
            "FormKey", "EditorID", "Name", "Data", "Stages", "Objectives", "Aliases",
            "VirtualMachineAdapter", "DialogBranches", "DialogTopics", "Scenes",
            // the mission-board card + classification, added once the coverage report named them
            "MissionBoardDescription", "MissionBoardInfoPanels", "MissionTypeKeyword",
            "QuestType", "QuestFaction", "QuestGroup", "Location", "SourceQuest",
            "QuestTimeLimit", "Event", "Keywords", "TextDisplayGlobals", "UnusedConditions",
            "Timestamp", "Unknown",
            // structural / not content
            "FormVersion", "Version2", "VersionControl", "IsCompressed", "IsDeleted",
            "MajorFlags", "MajorRecordFlagsRaw", "StarfieldMajorRecordFlags",
        };

        private static void DumpQuestEverything(IQuestGetter q, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"=== QUEST (ALL) ===");
            Console.WriteLine($"  FormKey:  {q.FormKey}");
            Console.WriteLine($"  EditorID: {q.EditorID}");
            Console.WriteLine($"  Name:     {q.Name}");
            Console.WriteLine($"  Priority: {q.Data?.Priority}   Type: {q.Data?.Type}");
            Console.WriteLine($"  Flags:    {q.Data?.Flags}  (0x{(uint)(q.Data?.Flags ?? 0):X8})");
            Console.WriteLine();

            // ---- The mission-board card ------------------------------------------------------
            // This section exists because the coverage report below named it on its first run:
            // MissionBoardDescription is the text a player reads on the board, and no reader in
            // this tool had ever shown one. It is printed FIRST and IN FULL -- it is the surface
            // most of the authored work in du_overtime lives on.
            Console.WriteLine("  Mission board card:");
            Console.WriteLine($"    MissionTypeKeyword: {(q.MissionTypeKeyword.IsNull ? "null" : ResolveName(q.MissionTypeKeyword.FormKey, allMods))}");
            Console.WriteLine($"    QuestType:          {(q.QuestType.IsNull ? "null" : ResolveName(q.QuestType.FormKey, allMods))}");
            Console.WriteLine($"    Description:        {(q.MissionBoardDescription?.String is { Length: > 0 } d ? $"\"{d}\"" : "(none)")}");
            Console.WriteLine($"    InfoPanels [{q.MissionBoardInfoPanels?.Count ?? 0}]:");
            if (q.MissionBoardInfoPanels != null)
                foreach (var panel in q.MissionBoardInfoPanels)
                {
                    // Was six identical type names -- i.e. a populated panel read exactly like an
                    // empty one. Describe each from its own scalars rather than guess at property
                    // names a second time.
                    var bits = new List<string>();
                    foreach (var pi in panel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (pi.GetIndexParameters().Length > 0) continue;
                        object? pv;
                        try { pv = pi.GetValue(panel); } catch { continue; }
                        if (pv == null) continue;
                        if (pv is System.Collections.ICollection pc && pc.Count == 0) continue;
                        var s = pv.ToString() ?? "";
                        if (s.StartsWith("Mutagen.")) continue;   // nested overlay, no scalar value
                        bits.Add($"{pi.Name}={s}");
                    }
                    Console.WriteLine($"      {(bits.Count > 0 ? string.Join("  ", bits) : "(no scalar fields)")}");
                }
            Console.WriteLine();

            // ---- Classification / linkage ----------------------------------------------------
            Console.WriteLine("  Linkage:");
            Console.WriteLine($"    QuestFaction:  {(q.QuestFaction.IsNull  ? "null" : ResolveName(q.QuestFaction.FormKey,  allMods))}");
            Console.WriteLine($"    QuestGroup:    {(q.QuestGroup.IsNull    ? "null" : ResolveName(q.QuestGroup.FormKey,    allMods))}");
            Console.WriteLine($"    Location:      {(q.Location.IsNull      ? "null" : ResolveName(q.Location.FormKey,      allMods))}");
            Console.WriteLine($"    SourceQuest:   {(q.SourceQuest.IsNull   ? "null" : ResolveName(q.SourceQuest.FormKey,   allMods))}");
            Console.WriteLine($"    Event:         {q.Event}");
            Console.WriteLine($"    Keywords [{q.Keywords?.Count ?? 0}]:");
            if (q.Keywords != null)
                foreach (var kw in q.Keywords)
                    Console.WriteLine($"      {ResolveName(kw.FormKey, allMods)}");
            Console.WriteLine();

            // ---- Stages, with their log text -------------------------------------------------
            Console.WriteLine($"  Stages [{q.Stages?.Count ?? 0}]:");
            if (q.Stages != null)
                foreach (var s in q.Stages)
                {
                    Console.WriteLine($"    Index={s.Index}  Flags={s.Flags}  logEntries={s.LogEntries?.Count ?? 0}");
                    if (s.LogEntries != null)
                        foreach (var e in s.LogEntries)
                        {
                            if (e.Entry != null && e.Entry.String?.Length > 0)
                                Console.WriteLine($"      text: \"{e.Entry}\"");
                            if (e.Conditions != null && e.Conditions.Count > 0)
                                foreach (var c in e.Conditions)
                                    DumpConditionBrief(c, "      cond: ");
                        }
                }
            Console.WriteLine();

            // ---- Objectives ------------------------------------------------------------------
            Console.WriteLine($"  Objectives [{q.Objectives?.Count ?? 0}]:");
            if (q.Objectives != null)
                foreach (var o in q.Objectives)
                {
                    Console.WriteLine($"    [{o.Index}] Flags={o.Flags}  \"{o.DisplayText}\"");
                    if (o.Targets != null)
                        foreach (var t in o.Targets)
                        {
                            // The getter has no `Alias` property (the compiler said so on the first
                            // cut). Rather than guess a second name, describe the target from its
                            // own scalars -- self-describing beats a plausible guess, and this is
                            // the field that says WHICH alias an objective points at.
                            var bits = new List<string>();
                            foreach (var pi in t.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (pi.GetIndexParameters().Length > 0) continue;
                                object? pv;
                                try { pv = pi.GetValue(t); } catch { continue; }
                                if (pv == null) continue;
                                if (pv is System.Collections.ICollection pc && pc.Count == 0) continue;
                                bits.Add($"{pi.Name}={pv}");
                            }
                            Console.WriteLine($"      target: {string.Join("  ", bits)}");
                        }
                }
            Console.WriteLine();

            // ---- Aliases, with fills (shares DumpRefAlias with the qalias reader) -------------
            Console.WriteLine($"  Aliases [{q.Aliases?.Count ?? 0}]:");
            if (q.Aliases != null)
                foreach (var alias in q.Aliases)
                {
                    switch (alias)
                    {
                        case IQuestReferenceAliasGetter ra:
                            DumpRefAlias(ra, allMods, "    ");
                            break;
                        case IQuestCollectionAliasGetter coll:
                            Console.WriteLine($"    [CollectionAlias] members={coll.Collection?.Count ?? 0}");
                            if (coll.Collection != null)
                                foreach (var m in coll.Collection)
                                    if (m.ReferenceAlias != null)
                                        DumpRefAlias(m.ReferenceAlias, allMods, "        ");
                            break;
                        case IQuestLocationAliasGetter la:
                            // Was an inline copy that printed SpecificLocation and conditions only --
                            // no ID, no Name, no Flags, and silently nothing at all for an ALPS-filled
                            // alias, which is the interesting kind. One helper now (2026-08-07).
                            DumpLocAlias(la, allMods, "    ");
                            break;
                        default:
                            Console.WriteLine($"    [UNHANDLED ALIAS TYPE: {alias.GetType().Name}] -- extend DumpQuestEverything.");
                            break;
                    }
                }
            Console.WriteLine();

            // ---- VMAD: the script layer ------------------------------------------------------
            var vma = q.VirtualMachineAdapter;
            Console.WriteLine("  VirtualMachineAdapter:");
            if (vma == null)
            {
                Console.WriteLine("    NULL");
            }
            else
            {
                if (vma.Script != null)
                    Console.WriteLine($"    FragmentScript: {vma.Script.Name}");
                Console.WriteLine($"    Fragments [{vma.Fragments?.Count ?? 0}]:");
                if (vma.Fragments != null)
                    foreach (var f in vma.Fragments)
                        Console.WriteLine($"      Stage={f.Stage} idx={f.StageIndex} -> {f.FragmentName}");

                Console.WriteLine($"    Scripts [{vma.Scripts?.Count ?? 0}]:");
                if (vma.Scripts != null)
                    foreach (var sc in vma.Scripts)
                    {
                        Console.WriteLine($"      {sc.Name}  ({sc.Properties?.Count ?? 0} properties)");
                        if (sc.Properties != null)
                            foreach (var p in sc.Properties)
                                DumpScriptPropertyResolved(p, "        ", allMods);
                    }

                Console.WriteLine($"    Alias scripts [{vma.Aliases?.Count ?? 0}]:");
                if (vma.Aliases != null)
                    foreach (var va in vma.Aliases)
                        if (va.Scripts != null)
                            foreach (var sc in va.Scripts)
                            {
                                Console.WriteLine($"      {sc.Name}");
                                if (sc.Properties != null)
                                    foreach (var p in sc.Properties)
                                        DumpScriptPropertyResolved(p, "        ", allMods);
                            }
            }
            Console.WriteLine();

            // ---- Coverage: what this reader did NOT show -------------------------------------
            var missed = new List<string>();
            foreach (var prop in typeof(IQuestGetter).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (QuestPropsRendered.Contains(prop.Name)) continue;
                object? v;
                try { v = prop.GetValue(q); } catch { continue; }
                if (v == null) continue;
                if (v is System.Collections.ICollection c && c.Count == 0) continue;
                string shown = v is System.Collections.ICollection cc ? $"[{cc.Count} items]" : v.ToString() ?? "";
                if (shown.Length > 90) shown = shown.Substring(0, 90) + "…";
                missed.Add($"    {prop.Name} = {shown}");
            }
            if (missed.Count == 0)
            {
                Console.WriteLine("  Coverage: every non-empty property on this record is rendered above.");
            }
            else
            {
                Console.WriteLine($"  ⚠ NOT RENDERED ABOVE [{missed.Count}] -- present on the record, not decoded by this reader:");
                foreach (var m in missed) Console.WriteLine(m);
            }
            Console.WriteLine();
        }

        /// <summary>
        /// DumpScriptProperty with two fixes: FormKeys resolve to EditorIDs (the helper existed and
        /// this path never used it, so every Object= was a number you had to look up separately),
        /// and a struct/list property reports its shape instead of only its type name.
        /// </summary>
        private static void DumpScriptPropertyResolved(IScriptPropertyGetter prop, string indent,
                                                       List<IStarfieldModGetter>? allMods)
        {
            switch (prop)
            {
                case IScriptObjectPropertyGetter o:
                    Console.WriteLine($"{indent}[Obj]    {prop.Name} = {ResolveName(o.Object.FormKey, allMods)}");
                    break;
                case IScriptIntPropertyGetter i:
                    Console.WriteLine($"{indent}[Int]    {prop.Name} = {i.Data}");
                    break;
                case IScriptBoolPropertyGetter b:
                    Console.WriteLine($"{indent}[Bool]   {prop.Name} = {b.Data}");
                    break;
                case IScriptFloatPropertyGetter f:
                    Console.WriteLine($"{indent}[Float]  {prop.Name} = {f.Data}");
                    break;
                case IScriptStringPropertyGetter s:
                    Console.WriteLine($"{indent}[String] {prop.Name} = \"{s.Data}\"");
                    break;
                default:
                    // Was printed as a bare type name -- so a populated ChangeLocationStages read
                    // identically to an empty one. Say how many entries it has, at least.
                    int count = -1;
                    foreach (var pi in prop.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (pi.GetIndexParameters().Length > 0) continue;
                        try
                        {
                            if (pi.GetValue(prop) is System.Collections.ICollection col) { count = col.Count; break; }
                        }
                        catch { }
                    }
                    Console.WriteLine($"{indent}[{prop.GetType().Name.Replace("BinaryOverlay", "")}] {prop.Name}"
                                      + (count >= 0 ? $" = [{count} entries]" : " = (not decoded)"));
                    break;
            }
        }

        // Quest aliases, and specifically the FILL -- which of the mutually-exclusive fill
        // properties a reference alias actually uses, and what it points at.
        //
        // DumpQuestFull (reachable only via `gen_inspect audiolog`) already prints aliases, but
        // handles ONE alias type and three of its fills; every other alias type falls into an
        // else-branch that prints a type name and an opaque ToString(). That is why the
        // levelled-space-cell binding was unreadable: it lives on a QuestCollectionAlias, whose
        // Collection[n].ReferenceAlias.CreateReferenceToObject.Object carries the LVSC -- a
        // different type, silently swallowed by the fallback.
        //
        // So this reader does two things that one does not: it walks collection aliases into their
        // member reference aliases, and it NAMES any alias type it cannot handle instead of
        // printing something that looks like output. An unhandled case that prints nothing useful
        // reads as "this record has nothing in it", which is the failure being fixed here.
        private static void DumpQuestAliases(IQuestGetter quest, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"--- Quest Aliases ---");
            Console.WriteLine($"  FormKey:  {quest.FormKey}");
            Console.WriteLine($"  EditorID: {quest.EditorID}");
            Console.WriteLine($"  Name:     {quest.Name}");

            if (quest.Aliases == null || quest.Aliases.Count == 0)
            {
                Console.WriteLine("  Aliases: none");
                Console.WriteLine();
                return;
            }

            Console.WriteLine($"  Aliases [{quest.Aliases.Count}]:");
            foreach (var alias in quest.Aliases)
            {
                switch (alias)
                {
                    case IQuestReferenceAliasGetter refAlias:
                        DumpRefAlias(refAlias, allMods, "    ");
                        break;

                    case IQuestCollectionAliasGetter coll:
                        // No ID/Name on this getter -- the compiler said so, and rather than guess a
                        // second property name the identity is left to the member aliases, which
                        // carry their own. The one thing his own writer proves exists is Collection
                        // (QuestNoun.SetQuestLevelledSpaceCellAlias walks
                        // Collection[0].ReferenceAlias.CreateReferenceToObject.Object).
                        Console.WriteLine($"    [CollectionAlias] members={coll.Collection?.Count ?? 0}");
                        if (coll.Collection != null)
                        {
                            int i = 0;
                            foreach (var member in coll.Collection)
                            {
                                Console.WriteLine($"      member[{i++}]:");
                                if (member.ReferenceAlias != null)
                                    DumpRefAlias(member.ReferenceAlias, allMods, "        ");
                                else
                                    Console.WriteLine("        (no ReferenceAlias)");
                            }
                        }
                        break;

                    // WHERE the mission happens, as against what it spawns. Added 2026-08-07 on his
                    // "oh get location aliases working now": this was the last alias type falling
                    // into the default branch, and on a space mission it is two of eleven aliases.
                    case IQuestLocationAliasGetter locAlias:
                        DumpLocAlias(locAlias, allMods, "    ");
                        break;

                    default:
                        // Loud, not decorative: name the type so the next gap is visible.
                        Console.WriteLine($"    [UNHANDLED ALIAS TYPE: {alias.GetType().Name}] " +
                                          $"-- this reader does not decode it; extend DumpQuestAliases.");
                        break;
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// A reference alias's fill properties are mutually exclusive -- exactly one is meant to be
        /// set. Print WHICH one, because that is the fact that classifies the alias: create-obj
        /// means we control what spawns, from-event means the story manager supplies it.
        /// </summary>
        /// <summary>
        /// A condition, in full: which function, against what, compared how, and how it chains.
        ///
        /// DumpConditionBrief printed the function name, the operator, and "?" for the comparison
        /// value on anything that was not a float condition -- and hand-decoded exactly two of the
        /// engine's several hundred condition-data types, so every other one's PARAMETERS were
        /// invisible. On an alias, the parameters ARE the content: "has keyword X" is only useful
        /// if you can see which keyword X is.
        ///
        /// So the parameters come from reflection over the Data object rather than a case list that
        /// would be permanently incomplete, with FormKeys resolved to EditorIDs. A condition type
        /// nobody has hand-written support for still prints its parameters.
        /// </summary>
        private static void DumpConditionFull(IConditionGetter cond, string indent,
                                              List<IStarfieldModGetter>? allMods)
        {
            string fn = cond.Data?.GetType().Name.Replace("ConditionDataBinaryOverlay", "")
                                                  .Replace("ConditionData", "") ?? "?";

            // The comparison value lives on the concrete condition type, not the interface.
            string val = cond switch
            {
                IConditionFloatGetter cf  => cf.ComparisonValue.ToString("0.##"),
                IConditionGlobalGetter cg => $"global:{ResolveEditorIdOnly(cg.ComparisonValue.FormKey, allMods)}",
                _                         => "(value not on this condition type)",
            };

            Console.WriteLine($"{indent}{fn} {cond.CompareOperator} {val}"
                              + $"   flags={cond.Flags}");

            if (cond.Data == null) return;

            // Parameters, by reflection -- a case list over condition functions is a blocklist
            // wearing a switch, and the engine has hundreds.
            foreach (var pi in cond.Data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0) continue;
                if (pi.Name is "RunOnType" or "Reference" or "Unknown3" or "UseAliases") continue;
                object? v;
                try { v = pi.GetValue(cond.Data); } catch { continue; }
                if (v == null) continue;

                string shown;
                // FormLink-shaped parameters carry the fact worth reading; resolve them.
                var linkProp = v.GetType().GetProperty("Link", BindingFlags.Public | BindingFlags.Instance);
                var fkProp   = (linkProp != null ? linkProp.PropertyType : v.GetType())
                                   .GetProperty("FormKey", BindingFlags.Public | BindingFlags.Instance);
                try
                {
                    var target = linkProp != null ? linkProp.GetValue(v) : v;
                    if (target != null && fkProp != null && fkProp.GetValue(target) is FormKey fk && !fk.IsNull)
                        shown = ResolveName(fk, allMods);
                    else
                        shown = v.ToString() ?? "";
                }
                catch { shown = v.ToString() ?? ""; }

                // Never drop a parameter silently. The first cut `continue`d on anything that
                // resolved to a Mutagen type name -- and the casualty was FirstParameter on
                // HasRefType, i.e. WHICH ref type a marker alias filters by, which is the single
                // fact the condition exists to carry. An omission that looks like an absent field
                // is the exact failure this reader keeps being rebuilt to stop.
                if (shown is "" or "Null") continue;
                if (shown.StartsWith("Mutagen."))
                {
                    // Dig one level for a FormKey the generic path missed, then fall back to naming
                    // the type rather than pretending the parameter is not there.
                    string deeper = shown.Substring(shown.LastIndexOf('.') + 1);
                    foreach (var inner in v.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (inner.GetIndexParameters().Length > 0) continue;
                        try
                        {
                            var iv = inner.GetValue(v);
                            if (iv is FormKey ifk && !ifk.IsNull) { deeper = ResolveName(ifk, allMods); break; }
                            var ifkProp = iv?.GetType().GetProperty("FormKey", BindingFlags.Public | BindingFlags.Instance);
                            if (ifkProp?.GetValue(iv) is FormKey ifk2 && !ifk2.IsNull)
                            { deeper = ResolveName(ifk2, allMods); break; }
                        }
                        catch { }
                    }
                    shown = deeper;
                }
                Console.WriteLine($"{indent}  {pi.Name}: {shown}");
            }
        }

        /// <summary>Properties DumpRefAlias renders itself; everything else gets named as undecoded.</summary>
        private static readonly HashSet<string> RefAliasPropsRendered = new()
        {
            "ID", "Name", "Flags",
            "CreateReferenceToObject", "FindMatchingRefFromEvent", "ForcedReference",
            "UniqueActor", "UniqueBaseForm", "Location", "External",
            "ReferenceCollectionAliasID",
        };

        private static void DumpRefAlias(IQuestReferenceAliasGetter a, List<IStarfieldModGetter>? allMods, string pad)
        {
            Console.WriteLine($"{pad}[RefAlias] ID={a.ID} Name={a.Name}");
            Console.WriteLine($"{pad}  Flags: {a.Flags}");

            int fills = 0;

            if (a.CreateReferenceToObject != null)
            {
                var c = a.CreateReferenceToObject;
                Console.WriteLine($"{pad}  FILL create-obj (ALCO/ALCA/ALCL):");
                Console.WriteLine($"{pad}    Object:  {ResolveName(c.Object.FormKey, allMods)}");
                Console.WriteLine($"{pad}    AliasID: {c.AliasID}   (the alias to create AT)");
                Console.WriteLine($"{pad}    Create:  {c.Create}   Level: {c.Level}");
                fills++;
            }
            if (a.FindMatchingRefFromEvent != null)
            {
                Console.WriteLine($"{pad}  FILL from-event (ALFE/ALFD): {a.FindMatchingRefFromEvent}");
                fills++;
            }
            if (!a.ForcedReference.IsNull)
            {
                Console.WriteLine($"{pad}  FILL forced-ref (ALFR): {ResolveName(a.ForcedReference.FormKey, allMods)}");
                fills++;
            }
            if (!a.UniqueActor.IsNull)
            {
                Console.WriteLine($"{pad}  FILL unique-actor: {ResolveName(a.UniqueActor.FormKey, allMods)}");
                fills++;
            }
            if (!a.UniqueBaseForm.IsNull)
            {
                Console.WriteLine($"{pad}  FILL unique-base: {ResolveName(a.UniqueBaseForm.FormKey, allMods)}");
                fills++;
            }
            if (a.Location != null)
            {
                Console.WriteLine($"{pad}  FILL location (ALLA): {a.Location}");
                fills++;
            }
            if (a.External != null)
            {
                Console.WriteLine($"{pad}  FILL external (ALEQ/ALEA): {a.External}");
                fills++;
            }
            // The one the alias-level coverage report surfaced, and it is how a mission's objective
            // finds its position: a marker alias is filled FROM ANOTHER ALIAS'S COLLECTION. On
            // duo_MB01a, SpawnMarker01 and PatrolMarker01 both carry ReferenceCollectionAliasID=10,
            // which is SpaceCellRefs -- the collection created from the levelled space cell. So the
            // cell is spawned, its markers populate these aliases, and PrimaryRef then creates the
            // activator AT SpawnMarker01. Undecoded, this read as "no fill set", which was a claim
            // of absence about the single most load-bearing link in the chain.
            if (a.ReferenceCollectionAliasID != null)
            {
                Console.WriteLine($"{pad}  FILL from-collection: ReferenceCollectionAliasID={a.ReferenceCollectionAliasID}" +
                                  $"  (filled from that alias's collection -- e.g. markers inside a spawned space cell)");
                fills++;
            }

            // The fills are meant to be mutually exclusive, so both zero and >1 are worth seeing
            // rather than inferring from an absence of lines.
            if (fills > 1)
                Console.WriteLine($"{pad}  ** {fills} fills set -- these are meant to be mutually exclusive **");

            // Coverage, at the ALIAS level. The first cut printed "FILL: none set" whenever none of
            // the seven fills above were populated -- which is an assertion of absence this code
            // cannot actually make. SpawnMarker01 reports no fill and is demonstrably resolved (a
            // PrimaryRef create-obj targets it), so "none set" was false and read as a finding.
            //
            // The record-level coverage report could not catch this: it enumerates IQuestGetter,
            // and an undecoded property on an ALIAS is invisible to it. So the same principle is
            // applied one level down -- name any non-empty property this dumper did not render,
            // and never claim emptiness that has not been established.
            var undecoded = new List<string>();
            foreach (var pi in typeof(IQuestReferenceAliasGetter).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0) continue;
                if (RefAliasPropsRendered.Contains(pi.Name)) continue;
                object? v;
                try { v = pi.GetValue(a); } catch { continue; }
                if (v == null) continue;
                if (v is System.Collections.ICollection col && col.Count == 0) continue;
                var s = v.ToString() ?? "";
                if (s is "Null" or "") continue;
                if (s.Length > 70) s = s.Substring(0, 70) + "…";
                undecoded.Add($"{pi.Name}={s}");
            }

            if (fills == 0 && undecoded.Count == 0)
                Console.WriteLine($"{pad}  FILL: no fill property set, and nothing else on the record either");
            else if (fills == 0)
                Console.WriteLine($"{pad}  FILL: none of the decoded fills -- see undecoded below");

            // Conditions are how the engine CHOOSES among candidates -- on a marker alias filled
            // from a collection, they are the filter that decides WHICH marker in the spawned cell
            // this alias resolves to. That makes them the difference between a composer being able
            // to aim the objective at a particular site and only being able to offer a pool.
            if (a.Conditions != null && a.Conditions.Count > 0)
            {
                Console.WriteLine($"{pad}  Conditions [{a.Conditions.Count}]:");
                foreach (var c in a.Conditions)
                    DumpConditionFull(c, pad + "    ", allMods);
            }

            if (undecoded.Count > 0)
                Console.WriteLine($"{pad}  ⚠ not decoded here: {string.Join("  ", undecoded)}");
        }

        // Properties DumpLocAlias renders explicitly. Same contract as RefAliasPropsRendered:
        // anything NOT in this set and non-empty gets named in the "not decoded here" line, so a
        // field this dumper has never heard of surfaces instead of vanishing.
        private static readonly HashSet<string> LocAliasPropsRendered = new()
        {
            "ID", "Name", "Flags", "SpecificLocation", "ALPS", "Conditions",
            "LocationTypeKeyword", "SystemLocationAliasID", "ALFG",
        };

        // A quest's LOCATION aliases -- the answer to "where does this mission happen", as opposed
        // to the reference aliases' "what does it spawn". They resolve one of two ways: a
        // SpecificLocation (pinned at authoring time) or, far more interestingly, an ALPS block
        // carrying a PCM type keyword, which is the request into the Planet Content Manager tree --
        // the open cross-mod registry that makes a board mission's destination pool grow as the
        // installed ecosystem grows.
        //
        // WHY THIS EXISTS (2026-08-07, his "oh get location aliases working now"): this type was
        // decoded in TWO other paths in this same file and NOT in DumpQuestAliases, so `qalias` --
        // the command whose entire job is "which fill is set and what does it point at" -- printed
        // "[UNHANDLED ALIAS TYPE: QuestLocationAliasBinaryOverlay]" for exactly the aliases that
        // answer the WHERE question. Two of duo_MB15a_qst's eleven aliases read as opaque.
        //
        // It is written as ONE helper called from all three sites rather than a third inline copy:
        // the two existing copies had already drifted apart (one printed raw FormKeys, the other
        // resolved names and dropped ID/Name/Flags entirely), which is the standing tell that a
        // rule open-coded in N places is N bugs -- and fixing the first makes the rest invisible.
        private static void DumpLocAlias(IQuestLocationAliasGetter a, List<IStarfieldModGetter>? allMods, string pad)
        {
            Console.WriteLine($"{pad}[LocAlias] ID={a.ID} Name={a.Name}");
            Console.WriteLine($"{pad}  Flags: {a.Flags} (0x{(uint)a.Flags:X8})");

            int fills = 0;
            if (!a.SpecificLocation.IsNull)
            {
                Console.WriteLine($"{pad}  FILL specific-location (ALFL): {ResolveName(a.SpecificLocation.FormKey, allMods)}");
                fills++;
            }
            if (a.ALPS != null)
            {
                // The PCM request. Naming it as such matters: a reader who sees only "ALPS" has no
                // way to know this is the hook into the cross-mod location registry.
                Console.WriteLine($"{pad}  FILL pcm-request (ALPS) -- resolves through the Planet Content Manager tree:");
                Console.WriteLine($"{pad}    PcmTypeKeyword: " +
                                  (a.ALPS.PcmTypeKeyword.IsNull ? "null" : ResolveName(a.ALPS.PcmTypeKeyword.FormKey, allMods)));
                foreach (var pi in a.ALPS.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (pi.GetIndexParameters().Length > 0 || pi.Name == "PcmTypeKeyword") continue;
                    object? v; try { v = pi.GetValue(a.ALPS); } catch { continue; }
                    if (v == null) continue;
                    var vs = v.ToString() ?? "";
                    if (vs is "Null" or "" or "0") continue;
                    if (vs.Length > 60) vs = vs.Substring(0, 60) + "…";
                    Console.WriteLine($"{pad}    {pi.Name}: {vs}");
                }
                fills++;
            }

            // ---- the three that the FIRST cut of this dumper missed, and they are the ones doing
            // the work. Rendering SpecificLocation and ALPS only (which is all either inline copy
            // ever did) reported "no fill set" on every shipped board mission -- because a board
            // mission does not PIN its destination, it DESCRIBES it and lets the story manager
            // pick. The reflection reporter below is what surfaced them; this block is it closing.
            if (!a.LocationTypeKeyword.IsNull)
            {
                // The actual filter: the location the board rolls must carry this keyword. Together
                // with Conditions this IS the fill for a radiant destination.
                Console.WriteLine($"{pad}  FILL by-type (ALLT): LocationTypeKeyword = " +
                                  $"{ResolveName(a.LocationTypeKeyword.FormKey, allMods)}");
                Console.WriteLine($"{pad}    (radiant: the board picks any location matching this keyword + the conditions below)");
                fills++;
            }
            if (a.SystemLocationAliasID != null)
            {
                // Which alias supplies the STAR SYSTEM this location is drawn within. Negative
                // values are sentinels rather than alias indices -- called out as unknown rather
                // than glossed, because guessing a sentinel's meaning is how a wrong law gets banked.
                var sid = a.SystemLocationAliasID;
                Console.WriteLine($"{pad}  SystemLocationAliasID: {sid}" +
                                  (sid < 0 ? "   (negative = sentinel, meaning NOT established -- do not infer one)"
                                           : "   (the alias supplying the star system to search within)"));
            }
            if (a.ALFG != null && a.ALFG != 0)
            {
                // ALFG is a 4-byte FLOAT and the getter surfaces it as an integer, so the raw view
                // of a tuned value reads as garbage: 1103383190 is 24.54, sitting inside the 8-32
                // band his own eye settled across four in-game trials. Printed raw it looks exactly
                // like an uninitialised field, which is the worst possible display for a number
                // somebody deliberately chose.
                var bits = unchecked((uint)a.ALFG.Value);
                var alt = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
                Console.WriteLine($"{pad}  ALFG orbit altitude: {alt:0.###}   (raw int {a.ALFG}; " +
                                  $"settled band 8-32, and past ~32-256 the engine DISCARDS it and reverts to default)");
            }
            else if (a.ALFG != null)
            {
                // NOT "zero means ground". The first cut of this line said exactly that and printed
                // it on PlayerStarSystemLocation -- a star-system alias on a SPACE mission -- so the
                // tool asserted "ground" about a mission that is not. The banked rule (ground 122 /
                // space 103, no exceptions) was measured on TargetPlanetLocation specifically, and
                // carrying it to every location alias is the prescription escaping the bound of its
                // evidence. State the value; scope the inference.
                Console.WriteLine($"{pad}  ALFG orbit altitude: 0" +
                                  (a.Name != null && a.Name.Contains("TargetPlanet", StringComparison.OrdinalIgnoreCase)
                                      ? "   (on a TargetPlanetLocation, zero = a GROUND mission -- measured, no exceptions either way)"
                                      : "   (unset; the ground/space reading is only established for TargetPlanetLocation)"));
            }

            // Same honesty rule as DumpRefAlias: never assert emptiness that has not been
            // established, and never let "no lines printed" read as "no fill set".
            var undecoded = new List<string>();
            foreach (var pi in typeof(IQuestLocationAliasGetter).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0) continue;
                if (LocAliasPropsRendered.Contains(pi.Name)) continue;
                object? v;
                try { v = pi.GetValue(a); } catch { continue; }
                if (v == null) continue;
                if (v is System.Collections.ICollection col && col.Count == 0) continue;
                var s = v.ToString() ?? "";
                if (s is "Null" or "") continue;
                if (s.Length > 70) s = s.Substring(0, 70) + "…";
                undecoded.Add($"{pi.Name}={s}");
            }

            if (fills == 0 && undecoded.Count == 0)
                Console.WriteLine($"{pad}  FILL: no fill property set, and nothing else on the record either");
            else if (fills == 0)
                Console.WriteLine($"{pad}  FILL: none of the decoded fills -- see undecoded below");
            if (fills > 1)
                Console.WriteLine($"{pad}  ** {fills} fills set -- these are meant to be mutually exclusive **");

            if (a.Conditions != null && a.Conditions.Count > 0)
            {
                Console.WriteLine($"{pad}  Conditions [{a.Conditions.Count}]:");
                foreach (var c in a.Conditions)
                    DumpConditionFull(c, pad + "    ", allMods);
            }

            if (undecoded.Count > 0)
                Console.WriteLine($"{pad}  ⚠ not decoded here: {string.Join("  ", undecoded)}");
        }

        // Same shape, and the same reason, as DumpFormList above: a LeveledSpaceCell IS its
        // entry list -- the pool a quest's levelled-space-cell alias draws its arrival cell
        // from -- so the reflection dumper would have printed the one fact the record carries
        // as "<enumerable ...>". Resolve each entry's Reference to its EditorID, and print the
        // level/count columns beside it so a pool's shape is readable at a glance.
        private static void DumpLeveledSpaceCell(ILeveledSpaceCellGetter lvsc, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"--- LeveledSpaceCell (LVSC) ---");
            Console.WriteLine($"  FormKey:    {lvsc.FormKey}");
            Console.WriteLine($"  EditorID:   {lvsc.EditorID}");
            Console.WriteLine($"  ChanceNone: {lvsc.ChanceNone}");
            Console.WriteLine($"  Entries [{lvsc.Entries?.Count ?? 0}]:");
            if (lvsc.Entries != null)
                foreach (var e in lvsc.Entries)
                    Console.WriteLine($"    Lvl={e.Level,-4} Count={e.Count,-4} " +
                                      $"{ResolveEditorIdOnly(e.Reference.FormKey, allMods)}  [{e.Reference.FormKey}]");
            Console.WriteLine();
        }

        private static void DumpSnapTemplate(ISnapTemplateGetter snap, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"--- SnapTemplate (SNTP) ---");
            Console.WriteLine($"  FormKey:    {snap.FormKey}");
            Console.WriteLine($"  EditorID:   {snap.EditorID}");
            Console.WriteLine($"  NextNodeID: {snap.NextNodeID}");
            Console.WriteLine($"  Nodes [{snap.Nodes.Count}]:");
            foreach (var node in snap.Nodes)
            {
                // The six-name table below covers the structural faces; everything else used to
                // print "?", which reads as "this node has no name" rather than "this tool only
                // knows six". There are 59 distinct node forms in the load order -- equipment /
                // weapon mounts (SnapNode_SHIP_Equipment_*) among them -- so the "?" was hiding
                // most of the vocabulary, and a survey built on this output undercounted them to
                // six. Fall back to the EditorID index that already exists for every other
                // FormKey in this file.
                var id = node.Node.FormKey.ID;
                string dir = SnapNodeDirections.TryGetValue(id, out var d)
                    ? d
                    : ResolveEditorIdOnly(node.Node.FormKey, allMods);
                Console.WriteLine($"    {dir,-9} NodeID={node.NodeID}  Node={node.Node.FormKey}");
                Console.WriteLine($"              Rotation={node.Rotation}  Offset={node.Offset}");
            }
            Console.WriteLine();
        }

        private static void DumpGenericBaseForm(IGenericBaseFormGetter gbfm, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"--- GenericBaseForm (GBFM) ---");
            Console.WriteLine($"  FormKey:  {gbfm.FormKey}");
            Console.WriteLine($"  EditorID: {gbfm.EditorID}");
            Console.WriteLine($"  Template: {ResolveName(gbfm.Template.FormKey, allMods)}");
            Console.WriteLine($"  Components [{gbfm.Components?.Count ?? 0}]:");
            if (gbfm.Components != null)
            {
                foreach (var c in gbfm.Components)
                {
                    switch (c)
                    {
                        case IPropertySheetComponentGetter ps:
                            Console.WriteLine($"    PropertySheet [{ps.Properties?.Count ?? 0}]:");
                            if (ps.Properties != null)
                                foreach (var p in ps.Properties)
                                    Console.WriteLine($"      {ResolveName(p.ActorValue.FormKey, allMods)} = {p.Value}");
                            break;
                        case IFormLinkDataComponentGetter fl:
                            Console.WriteLine($"    FormLinkData [{fl.Links?.Count ?? 0}]:");
                            if (fl.Links != null)
                                foreach (var l in fl.Links)
                                    Console.WriteLine($"      {ResolveName(l.Keyword.FormKey, allMods)} -> {ResolveName(l.LinkedForm.FormKey, allMods)}");
                            break;
                        case IKeywordFormComponentGetter kw:
                            Console.WriteLine($"    Keywords [{kw.Keywords?.Count ?? 0}]:");
                            if (kw.Keywords != null)
                                foreach (var k in kw.Keywords)
                                    Console.WriteLine($"      {ResolveName(k.FormKey, allMods)}");
                            break;
                        case IFullNameComponentGetter fn:
                            Console.WriteLine($"    FullName: {fn.Name}");
                            break;
                        default:
                            // Vanilla modules carry six more component types (AttachParentArray,
                            // DestructibleObject, ObjectWindowFilter, StoredTraversals, ...) that
                            // we don't author. Name them and reflect rather than guess a layout.
                            Console.WriteLine($"    {c.GetType().Name}:");
                            DumpPropertiesReflection(c, "      ", maxDepth: 2);
                            break;
                    }
                }
            }
            Console.WriteLine();
        }

        private static void DumpConstructibleObject(IConstructibleObjectGetter co, List<IStarfieldModGetter>? allMods)
        {
            Console.WriteLine($"--- ConstructibleObject (COBJ) ---");
            Console.WriteLine($"  FormKey:          {co.FormKey}");
            Console.WriteLine($"  EditorID:         {co.EditorID}");
            Console.WriteLine($"  Description:      {co.Description}");
            Console.WriteLine($"  CreatedObject:    {ResolveName(co.CreatedObject.FormKey, allMods)}");
            Console.WriteLine($"  WorkbenchKeyword: {ResolveName(co.WorkbenchKeyword.FormKey, allMods)}");
            Console.WriteLine($"  AmountProduced:   {co.AmountProduced}");
            Console.WriteLine($"  MenuSortOrder:    {co.MenuSortOrder}");
            Console.WriteLine($"  LearnMethod:      {co.LearnMethod}");
            Console.WriteLine($"  Value:            {co.Value}");
            Console.WriteLine($"  Tier:             {co.Tier}");
            // ⛔ RQPK WAS RENDERED NOWHERE AT ALL, and that is how a whole gating mechanism
            // stayed invisible on 2026-08-17. A COBJ's skill requirement is NOT a condition and
            // is NOT on the perk record -- it is `Required Perks` on the recipe itself, two
            // fields below the Conditions this dumper did print. HE found it in xEdit; nothing
            // here could have. Third instance in one day of "an absent field prints as nothing,
            // and nothing is invisible in a dump".
            if (co.RequiredPerks != null && co.RequiredPerks.Count > 0)
            {
                Console.WriteLine($"  RequiredPerks [{co.RequiredPerks.Count}]:");
                foreach (var rp in co.RequiredPerks)
                {
                    var eid = ResolveEditorIdOnly(rp.Perk.FormKey, allMods);
                    var curve = rp.CurveTable.IsNull ? "" : $"  curve={ResolveEditorIdOnly(rp.CurveTable.FormKey, allMods)}";
                    Console.WriteLine($"    {eid} [{rp.Perk.FormKey}]  rank {rp.Rank}{curve}");
                }
            }
            if (co.Conditions != null && co.Conditions.Count > 0)
            {
                Console.WriteLine($"  Conditions [{co.Conditions.Count}]:");
                foreach (var cond in co.Conditions)
                    DumpConditionBrief(cond, "    ", allMods);
            }
            Console.WriteLine();
        }

        private static void DumpLight(ILightGetter light)
        {
            Console.WriteLine($"--- Light ---");
            Console.WriteLine($"  FormKey:  {light.FormKey}");
            Console.WriteLine($"  EditorID: {light.EditorID}");
            Console.WriteLine($"  Radius:   {light.Radius}");
            Console.WriteLine($"  Color:    {light.Color}");
            Console.WriteLine($"  Flags:    {light.Flags}");
            Console.WriteLine($"  FOV:      {light.FOV}");
            Console.WriteLine($"  NearClip: {light.NearClip}");
            Console.WriteLine($"  FalloffExponent: {light.FalloffExponent}");
            if (!string.IsNullOrEmpty(light.Model?.File))
                Console.WriteLine($"  Model:    {light.Model.File}");
            Console.WriteLine();
        }

        private static void DumpRecord(object record, string typeName)
        {
            Console.WriteLine($"--- {typeName} ---");
            DumpPropertiesReflection(record, "  ", maxDepth: 2);
            Console.WriteLine();
        }

        private static void DumpPropertiesReflection(object obj, string indent, int maxDepth, int currentDepth = 0)
        {
            if (obj == null || currentDepth >= maxDepth) return;

            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => p.Name);

            foreach (var prop in properties)
            {
                try
                {
                    // Skip indexer properties
                    if (prop.GetIndexParameters().Length > 0) continue;
                    // Skip properties that commonly throw or are too noisy
                    if (prop.Name == "Registration" || prop.Name == "StaticRegistration") continue;

                    var value = prop.GetValue(obj);
                    if (value == null)
                    {
                        // Skip null values to reduce noise
                        continue;
                    }

                    var valueType = value.GetType();

                    // Handle collections
                    if (value is System.Collections.ICollection collection)
                    {
                        if (collection.Count == 0) continue;
                        Console.WriteLine($"{indent}{prop.Name}: [{collection.Count} items]");
                        int i = 0;
                        foreach (var item in collection)
                        {
                            if (i >= 10) { Console.WriteLine($"{indent}  ... and {collection.Count - 10} more"); break; }
                            Console.WriteLine($"{indent}  [{i}] {item}");
                            i++;
                        }
                    }
                    else if (value is System.Collections.IEnumerable enumerable && valueType != typeof(string) && !valueType.IsPrimitive)
                    {
                        // Skip complex enumerables to avoid infinite loops
                        Console.WriteLine($"{indent}{prop.Name}: <enumerable {valueType.Name}>");
                    }
                    // Handle simple/value types
                    else if (valueType.IsPrimitive || value is string || value is FormKey || value is Enum
                        || value is Noggog.P3Float || value is Noggog.P2Float)
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {value}");
                    }
                    // Handle FormLink types
                    else if (valueType.Name.Contains("FormLink"))
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {value}");
                    }
                    // Handle MemorySlice/ReadOnlyMemorySlice (binary data)
                    else if (valueType.Name.Contains("MemorySlice"))
                    {
                        Console.WriteLine($"{indent}{prop.Name}: <binary data>");
                    }
                    // Recurse into complex objects (one level)
                    else if (currentDepth < maxDepth - 1)
                    {
                        Console.WriteLine($"{indent}{prop.Name}: ({valueType.Name})");
                        DumpPropertiesReflection(value, indent + "  ", maxDepth, currentDepth + 1);
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {value}");
                    }
                }
                catch
                {
                    // Silently skip properties that throw
                }
            }
        }

        private static void ListRecordGroups(IStarfieldModGetter mod)
        {
            Console.WriteLine("Available record groups in Starfield.esm:");
            Console.WriteLine();

            var groups = new (string Name, int Count)[]
            {
                ("Activator", mod.Activators.Count),
                ("ActorValueInformation", mod.ActorValueInformation.Count),
                ("Armor", mod.Armors.Count),
                ("Cell", mod.Cells.Sum(b => b.SubBlocks.Sum(sb => sb.Cells.Count))),
                ("ConstructibleObject", mod.ConstructibleObjects.Count),
                ("DamageType", mod.DamageTypes.Count),
                ("FormList", mod.FormLists.Count),
                ("GenericBaseForm", mod.GenericBaseForms.Count),
                ("LegendaryItem", mod.LegendaryItems.Count),
                ("Light", mod.Lights.Count),
                ("Location", mod.Locations.Count),
                ("MagicEffect", mod.MagicEffects.Count),
                ("Npc", mod.Npcs.Count),
                ("ObjectEffect", mod.ObjectEffects.Count),
                ("ObjectModification", mod.ObjectModifications.Count),
                ("Outfit", mod.Outfits.Count),
                ("PackIn", mod.PackIns.Count),
                ("Perk", mod.Perks.Count),
                ("SnapTemplate", mod.SnapTemplates.Count),
                ("Spell", mod.Spells.Count),
                ("Static", mod.Statics.Count),
                ("MoveableStatic", mod.MoveableStatics.Count),
                ("SurfaceBlock", mod.SurfaceBlocks.Count),
                ("Worldspace", mod.Worldspaces.Count),
            };

            foreach (var (name, count) in groups.OrderBy(g => g.Name))
            {
                Console.WriteLine($"  {name,-20} {count,8:N0} records");
            }
        }
    }
}
