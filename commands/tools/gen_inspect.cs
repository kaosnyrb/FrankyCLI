using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Retrograde.Utils;

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
        /// The record types with a BESPOKE renderer, in ONE place. Program.cs prints this for its
        /// usage text and the unknown-type branch below prints it too. Previously each site kept
        /// its own copy and all of them had drifted — none listed MoveableStatic, Planet, Star,
        /// Race or Biome, which have been supported for some time.
        ///
        /// ⛔ AND IT IS NO LONGER THE LIST OF WHAT WORKS, WHICH IS THE POINT (2026-09-15, his
        /// *"we have a list of all record types now can we not fill it all out?"*). Every one of
        /// the mod's 177 record groups is now inspectable through the generic fallback, so this
        /// names the ones that get a HAND-WRITTEN view — where a relation is resolved rather than
        /// printed as a link. **Treating this as the list of what the tool can reach is what made
        /// a Door unreadable for the life of the tool.** `gen_inspect list x` is the real list and
        /// it is derived, not typed.
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
            "  'list' - EVERY record group the mod exposes, with counts, derived from the type\n" +
            "           system rather than typed here. 177 of them, and ANY of those names works:\n" +
            "           the ones above get a bespoke view, the rest get a full property dump.\n" +
            "  'selftest' - positive + negative controls for the Mutagen field-width tell\n" +
            "               (no plugin loaded, exits 1 on failure, so it can gate)";

        public static int Generate(string[] args)
        {
            // Arity is guaranteed by the caller: Program.cs guards args.Length < 3 and then
            // always invokes with exactly 5. A second guard here was unreachable (rule 4).
            string recordType = args[3];
            string search = args[4];

            // Before the environment, because it needs no plugin and booting the whole load order
            // to check arithmetic would make nobody run it.
            if (string.Equals(recordType, "selftest", StringComparison.OrdinalIgnoreCase))
                return SelfTest();

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
            bool unknownType = false;
            foreach (var mod in allMods)
            {
                found += InspectRecordType(mod, recordType, search, out bool unknown, allMods, env.LinkCache);
                // Break rather than repeat the refusal once per loaded plugin: an
                // unknown record type is a property of the ARGUMENT, not of the mod.
                if (unknown) { unknownType = true; break; }
            }

            // ⛔ THESE TWO OUTCOMES USED TO PRINT THE SAME SENTENCE AND IT COST REAL CONFUSION.
            // Asking for a Door printed "Unknown record type" and then, because nothing was found,
            // ALSO printed "No Door records found matching '0x16C025'". The second line reads as
            // SEARCHED AND ABSENT, so a reader skimming the tail concludes the record does not
            // exist when the truth is the tool could not look. *I could not look* and *I looked and
            // there was nothing* are different facts and they now have different words.
            if (unknownType)
            {
                Console.WriteLine($"'{recordType}' is not a record type this tool can reach.");
                Console.WriteLine("  NOTHING WAS SEARCHED -- this is not a statement about whether such a record exists.");
                Console.WriteLine($"  `gen_inspect list x` prints every group the mod exposes (177 of them), and any");
                Console.WriteLine("  of those names works. Bespoke renderers:");
                Console.WriteLine(SupportedTypes);
            }
            else if (found == 0)
            {
                Console.WriteLine($"No {recordType} records found matching '{search}' -- the group WAS searched and holds no match.");
            }

            Console.WriteLine();
            Console.WriteLine($"Total records found: {found}");
            return 0;
        }

        private static int InspectRecordType(IStarfieldModGetter mod, string recordType, string search, out bool unknownType, List<IStarfieldModGetter>? allMods = null, ILinkCache? cache = null)
        {
            int found = 0;
            unknownType = false;
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
                        { DumpRecord(rec, "Worldspace", cache); found++; }
                    break;
                case "packin":
                    foreach (var rec in mod.PackIns)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "PackIn", cache); found++; }
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
                        { DumpRecord(rec, "Static", cache); found++; }
                    break;
                case "moveablestatic":
                case "moveablestatics":
                    foreach (var rec in mod.MoveableStatics)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        {
                            DumpRecord(rec, "MoveableStatic", cache);
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
                        { DumpRecord(rec, "Activator", cache); found++; }
                    break;
                case "light":
                    foreach (var rec in mod.Lights)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpLight(rec); found++; }
                    break;
                case "npc":
                    foreach (var rec in mod.Npcs)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Npc", cache); DumpNpcExtras(rec, allMods); found++; }
                    break;
                case "location":
                    foreach (var rec in mod.Locations)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Location", cache); found++; }
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
                                DumpPlacedExtras(po, cache, "  ");
                                // Everything this renderer does NOT decode, named rather than
                                // silently dropped -- see DumpCoverage's head for why it took six
                                // weeks for the quest dumper's own check to reach a second record.
                                DumpCoverage<IPlacedObjectGetter>(po, RefrPropsRendered, "  ");
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
                        { DumpRecord(rec, "Faction", cache); found++; }
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
                            DumpRecord(rec, "Planet", cache);
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
                    // Not a bespoke renderer -- try the mod's own record groups. This is what
                    // makes every one of Mutagen's 177 groups inspectable instead of the ~40
                    // somebody remembered to hand-write a case for.
                    //
                    // ⚠ IT IS A FALLBACK, NOT A CATCH-ALL. The group set is CLOSED and derived
                    // from the type system, so a typo still refuses: `Dor` matches no group and
                    // comes back as an unknown type, which is the property that keeps this from
                    // turning a mistyped record type into a silent empty result.
                    int generic = DumpGenericGroup(mod, recordType, search, allMods, cache);
                    if (generic < 0) unknownType = true; else found += generic;
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
        /// XLOC (lock data) and XLKT (linked-ref transient) on a placed reference, plus the two
        /// nullable links beside them. Called from the SAME two places as DumpLinkedRefs, for the
        /// reason written on that method: a field visible from one view and invisible from another
        /// is how an absence in the DUMP becomes indistinguishable from an absence in the PLUGIN.
        ///
        /// ⛔ WHY IT EXISTS (2026-09-15). A bay door that renders and does nothing was chased for
        /// two days, and the one thing nobody could compare was its lock data, because this dumper
        /// never printed it. The answer arrived as a SCREENSHOT of xEdit. A record field the office
        /// cannot read is a field the office cannot reason about, and it will reliably be the field
        /// somebody wants at the worst moment.
        ///
        /// ⚠ THE NAMES ARE MUTAGEN'S, NOT xEDIT'S, and they do not match: XLOC is `Lock` and
        /// XLKT is `IsLinkedRefTransient`. Both spellings are printed so a reader holding an xEdit
        /// window can join the two views.
        ///
        /// ⭐ AND XLKT IS AN EMPTY SUBRECORD -- `wbEmpty(XLKT, 'Transient')` in xEdit's SF1
        /// definitions, sitting INSIDE the Linked References group beside the XLKR array. It has no
        /// payload: PRESENCE is the whole meaning. That is also why it can only ever appear on a ref
        /// that has linked references, which is a property of the FORMAT rather than a coincidence
        /// in the data.
        private static void DumpPlacedExtras(IPlacedObjectGetter po, ILinkCache? cache, string indent)
        {
            var lk = po.Lock;
            if (lk != null)
            {
                string keyName = NameOf(lk.Key.FormKey, cache);
                // ⛔ MUTAGEN READS LEVEL AND FLAGS FOUR BYTES WIDE AND THE FORMAT IS ONE BYTE PLUS
                // THREE UNUSED. Settled at the SOURCE rather than by experiment: xEdit is open, and
                // Core/wbDefinitionsSF1.pas on branch dev-4.1.5 (matching the 4.1.5p build he runs)
                // declares XLOC as
                //     Level itU8 + wbUnused(3) | Key FormID | Flags itU8 + wbUnused(3) | Unknown itU32
                // So a raw 0x006100FE is Level 0xFE with 00 61 00 of padding swallowed, and
                // 0x40D44E01 is Flags 0x01 with the same. Take the LOW BYTE.
                //
                // ⚠ The enum below is xEdit's table, carried WITH its citation rather than absorbed:
                // the raw byte is printed beside the label so a drifted table cannot hide the value.
                // And Mutagen's `Unused` is NOT padding -- it is xEdit's trailing `Unknown` U32, so
                // the two tools disagree about which field is the throwaway one.
                int lvl = (int)lk.Level & 0xFF, flg = (int)lk.Flags & 0xFF;
                string lvlName = lvl switch
                {
                    0 => "None", 1 => "Novice 1", 25 => "Novice 25", 50 => "Advanced",
                    75 => "Expert", 100 => "Master", 251 => "Barred", 252 => "Chained",
                    253 => "Requires Terminal", 254 => "Inaccessible", 255 => "Requires Key",
                    _ => "(not in xEdit's table)",
                };
                var flagNames = new List<string>();
                if ((flg & 0x01) != 0) flagNames.Add("Unknown 0");
                if ((flg & 0x04) != 0) flagNames.Add("Leveled Lock");
                Console.WriteLine($"{indent}    XLOC (Lock): Level={lvl} ({lvlName}) "
                                  + $"Flags=0x{flg:X2} ({(flagNames.Count > 0 ? string.Join("+", flagNames) : "none")}) "
                                  + $"Unknown(U32)={lk.Unused}");
                Console.WriteLine($"{indent}      raw: Level=0x{(int)lk.Level:X8} Flags=0x{(int)lk.Flags:X8} "
                                  + "(Mutagen reads both 4 bytes wide; SF1 defines 1 + 3 unused)");
                Console.WriteLine($"{indent}      Key: {(lk.Key.FormKey.IsNull ? "NULL" : lk.Key.FormKey.ToString())}"
                                  + (keyName.Length > 0 ? " " + keyName : ""));
            }
            // A bool, so it is printed ALWAYS rather than only when true: "false" and "the dumper
            // does not know about this field" are the same blank otherwise, which is the defect
            // this whole method was written to close.
            Console.WriteLine($"{indent}    XLKT (IsLinkedRefTransient): {po.IsLinkedRefTransient}");
            if (!po.XLTW.IsNull)
                Console.WriteLine($"{indent}    XLTW: {po.XLTW.FormKey}"
                                  + (NameOf(po.XLTW.FormKey, cache) is { Length: > 0 } n1 ? " " + n1 : ""));
            if (!po.XLIB.IsNull)
                Console.WriteLine($"{indent}    XLIB: {po.XLIB.FormKey}"
                                  + (NameOf(po.XLIB.FormKey, cache) is { Length: > 0 } n2 ? " " + n2 : ""));
        }

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
                DumpPlacedExtras(po, cache, "  ");
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
        /// What the REFR renderer below actually prints. Everything else on IPlacedObjectGetter is
        /// named by the coverage report instead of vanishing.
        ///
        /// ⛔ THE MEASUREMENT THAT MADE THIS NECESSARY (2026-09-15). This reader printed TWELVE
        /// fields. xEdit's REFR definition in Core/wbDefinitionsSF1.pas declares roughly FIFTY
        /// subrecords, and this repo's own docs/formlib/placed_object.md lists ~70 fields that
        /// CellTools.CloneCellById copies. So the CLONER knew the record and the READER did not,
        /// which is the worst possible split: work that moves a field correctly, beside a view that
        /// cannot show you it moved.
        ///
        /// ⚠ THE LIST IS OF NAMES THE RENDERER HANDLES, NOT OF FIELDS THAT MATTER. Adding a name
        /// here SILENCES it, so a name goes in only when a line above genuinely prints it. That is
        /// the one way this check can be made to lie, and it is easier to do by accident than on
        /// purpose -- the qall list one screen down carries the same hazard.
        private static readonly HashSet<string> RefrPropsRendered = new()
        {
            // printed by the REFR renderer
            "FormKey", "EditorID", "Base", "Position", "Rotation", "Scale",
            "MajorRecordFlagsRaw", "StarfieldMajorRecordFlags", "XFLG", "XNSE", "XALG",
            // printed by DumpLinkedRefs / DumpPlacedExtras
            "LinkedReferences", "Lock", "IsLinkedRefTransient", "XLTW", "XLIB",
            // structural / not content, same exclusions the quest set makes
            "FormVersion", "Version2", "VersionControl", "IsCompressed", "IsDeleted",
            "MajorFlags",
        };

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
                    // stageFlags, not Flags: the bare label read as "this stage has no flags at
                    // all" and cost a wrong claim about how duo_artifact_local_qst04a ends
                    // (2026-09-23). QuestStage.Flags is only RunOnStart/RunOnStop/
                    // KeepInstanceDataFromHereOn and CANNOT express completion -- the quest ends on
                    // QuestLogEntry.Flags = CompleteQuest, one level down, which this never printed.
                    // So every quest in every mod read Flags=0 and the field that decides the end of
                    // a quest was invisible. A reader that names a property and hides a DIFFERENT
                    // one of the same name is worse than one that omits both.
                    Console.WriteLine($"    Index={s.Index}  stageFlags={s.Flags}  logEntries={s.LogEntries?.Count ?? 0}");
                    if (s.LogEntries != null)
                        foreach (var e in s.LogEntries)
                        {
                            // Printed for EVERY entry, including a flagless one, and printed BEFORE
                            // the text: an entry carrying only a flag and no journal line rendered
                            // nothing whatsoever before this, so the log entry that ends the quest
                            // was not merely mislabelled, it was absent from the dump.
                            Console.WriteLine($"      entryFlags: {(e.Flags.HasValue ? e.Flags.Value.ToString() : "(none)")}");
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
            DumpCoverage<IQuestGetter>(q, QuestPropsRendered, "  ");
        }

        /// Name every non-empty property the renderer above did NOT show.
        ///
        /// ⛔ EXTRACTED 2026-09-15 FROM THE QUEST DUMPER, WHERE IT WAS THE ONLY ONE. His instruction
        /// when it was first built was *"I don't want you working off incomplete data"*, and it
        /// worked -- its first run named 15 undecoded properties including MissionBoardDescription,
        /// the card text 146 missions are authored with and which no reader had ever shown. Then it
        /// stayed welded to ONE record type for six weeks, so every other renderer in this file kept
        /// the exact defect it was built to cure: a hand-picked list of fields, and no way to tell a
        /// field that is ABSENT from a field the reader was never taught.
        ///
        /// ⭐ A COMPLETENESS CHECK PROTECTS EXACTLY THE LEVEL IT ENUMERATES, and the level you forget
        /// is the one you needed. That law is already written on this file twice (record vs alias);
        /// this is the third payout and the cure is generic rather than another copy.
        ///
        /// ⚠ IT GRADES MUTAGEN'S SURFACE, NOT THE FORMAT'S. A property Mutagen does not expose at all
        /// cannot appear here, so a clean coverage line means "this reader showed everything the
        /// library offered", never "this reader showed everything the record holds". Those are
        /// different claims and only xEdit's definitions answer the second.
        /// ⛔ AND ITS FIRST RUN ON A REFR PROVED THE FILTER WAS THE WHOLE JOB. It named 19
        /// properties, of which 17 read `Null` or `False`: Mutagen's nullable FormLink is a STRUCT
        /// and is never C# null, so `v == null` -- which is all the quest version ever tested -- does
        /// not catch an unset link, and `false` is not null either. Nineteen rows of nothing is a
        /// WOLF-CRIER, and a gate that cries wolf has stopped being a gate, because the reader is the
        /// component that fails. The real undecoded field would land in that block and nobody would
        /// read it.
        ///
        /// ⭐ SO IT IS TWO TIERS RATHER THAN A TIGHTER FILTER, and that distinction is the point: a
        /// filter DELETES the empty ones and then "absent from the record" and "suppressed by my
        /// predicate" become the same blank again, which is the exact defect this whole mechanism
        /// exists to close. They are counted and named on one line instead, values omitted. Nothing
        /// is hidden; only the loud half is loud.
        private static void DumpCoverage<T>(T rec, HashSet<string> rendered, string indent)
        {
            var carrying = new List<string>();
            var empty = new List<string>();
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (rendered.Contains(prop.Name)) continue;
                object? v;
                try { v = prop.GetValue(rec); } catch { continue; }
                if (v == null) { empty.Add(prop.Name); continue; }
                if (IsEmptyValue(v)) { empty.Add(prop.Name); continue; }
                string shown = v is System.Collections.ICollection cc ? $"[{cc.Count} items]" : v.ToString() ?? "";
                if (shown.Length > 90) shown = shown.Substring(0, 90) + "…";
                carrying.Add($"{indent}  {prop.Name} = {shown}{WidthTell(v)}");
            }
            if (carrying.Count == 0)
            {
                // The claim is scoped to what this pass actually walked, and the scope is printed
                // WITH it rather than left to the reader. The old wording was "every property
                // Mutagen exposes that CARRIES a value is rendered above", which was false on the
                // very record it was printed under: a property listed in `rendered` was treated as
                // covered even when its renderer had interpolated the object bare and emitted a
                // CLASS NAME, and this pass walks T only -- it can see nothing nested inside an
                // alias, a fill or a condition. A completeness check protects exactly the level it
                // enumerates, and its PASS LINE is where the scope goes missing.
                Console.WriteLine($"{indent}Coverage: every TOP-LEVEL property of {typeof(T).Name} that carries a value was CLAIMED by a renderer above.");
                Console.WriteLine($"{indent}  SCOPE, stated rather than assumed: this walks {typeof(T).Name} itself and nothing nested in it. An undecoded field on an alias, a fill or a condition is invisible here and is reported by that level's own coverage line. And CLAIMED means a renderer named the property, not that it printed the contents.");
            }
            else
            {
                Console.WriteLine($"{indent}⚠ NOT RENDERED ABOVE [{carrying.Count}] -- carries a value, not decoded by this reader:");
                foreach (var m in carrying) Console.WriteLine(m);
            }
            if (empty.Count > 0)
                Console.WriteLine($"{indent}  (also undecoded but EMPTY on this record [{empty.Count}]: {string.Join(", ", empty)})");
            Console.WriteLine();
        }

        /// "Empty" = the record does not carry this field. Deliberately narrow: a zero NUMBER is not
        /// empty, because 0 is a legitimate authored value (a rotation, an offset, a count) and
        /// treating it as absence is how a real value gets suppressed. Only the shapes that genuinely
        /// mean "unset" are counted: an unset FormLink, a false bool, an empty collection or string.
        private static bool IsEmptyValue(object v)
        {
            if (v is IFormLinkGetter fl && fl.IsNull) return true;
            if (v is bool b) return !b;
            if (v is string s) return s.Length == 0;
            if (v is System.Collections.ICollection c) return c.Count == 0;
            if (v is System.Collections.IEnumerable e && v is not string)
            {
                foreach (var _ in e) return false;
                return true;
            }
            return false;
        }

        /// ⭐ THE REUSABLE HALF OF THE XLOC BUG, TURNED INTO A PROMPT.
        ///
        /// Chasing a bay door on 2026-09-15, Mutagen read XLOC's Level as 6357246 and Flags as
        /// 1087655425 where xEdit's pane read `Inaccessible` and `Unknown 0`. Core/wbDefinitionsSF1.pas
        /// settles it: XLOC is `Level itU8 + wbUnused(3)`, so Mutagen reads a ONE-byte field FOUR
        /// bytes wide and swallows the padding. 0x006100FE is Level 0xFE = 254 = Inaccessible.
        ///
        /// The general form is worth more than that one fix: A MUTAGEN FIELD ON THIS GAME THAT COMES
        /// BACK AS A LARGE NONSENSE INTEGER IS A CANDIDATE FOR THE SAME WIDTH BUG, not a quirk of how
        /// Mutagen exposes it. That reading cost two hours of treating a decoded value as unreadable.
        ///
        /// ⚠ IT IS A PROMPT TO LOOK, NEVER A VERDICT, and it is deliberately quiet: it fires only on
        /// an integer that does not fit in two bytes, where the low byte is a plausible small value
        /// and the upper bytes are not zero. A band that convicts nothing is decoration; one that
        /// convicts everything is a wolf-crier. This one says where to grep and stops.
        /// Dump any record from any group Mutagen exposes, by walking its properties.
        ///
        /// ⭐ WHY A FALLBACK AND NOT 137 MORE HAND-WRITTEN CASES (2026-09-15, his *"we have a list
        /// of all record types now can we not fill it all out?"*). `gen_inspect list` reports 177
        /// groups; the switch above hand-renders about 40. Writing the other 137 by hand would be
        /// the same defect at greater length, and every one would rot independently. The generic
        /// walk costs one method and covers all of them.
        ///
        /// ⚠ A BESPOKE RENDERER IS STILL BETTER WHERE ONE EXISTS and this does not replace any: a
        /// quest's alias fills, a cell's contents and a REFR's linked references are RELATIONS, and
        /// a property walk prints the link and not what it resolves to. So the rule is bespoke
        /// where we have it, generic everywhere else, and the usage text says which is which.
        ///
        /// Returns the number of records dumped, or -1 when the name matches no group at all --
        /// which the caller turns into a refusal that says NOTHING WAS SEARCHED.
        private static int DumpGenericGroup(IStarfieldModGetter mod, string recordType, string search,
                                            List<IStarfieldModGetter>? allMods, ILinkCache? cache)
        {
            RecordGroups.Group? match = null;
            foreach (var g in RecordGroups.Enumerate(mod))
            {
                if (string.Equals(g.Name, recordType, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(g.PropertyName, recordType, StringComparison.OrdinalIgnoreCase))
                { match = g; break; }
            }
            if (match == null) return -1;

            int found = 0;
            // The SAFE enumerator, not a bare foreach: the [Bethesda] manual's standing caution is
            // that some record shapes read flaky in Mutagen, and a dumper that dies on one bad
            // record is useless on exactly the corpus that has one.
            foreach (var rec in RecordGroups.Safe(match.Value.Records, match.Value.Name,
                                                  mod.ModKey.FileName, "gen_inspect"))
            {
                if (!MatchesSearch(rec.EditorID, rec.FormKey, search)) continue;
                Console.WriteLine($"--- {match.Value.Name} ({rec.FormKey}) ---");
                Console.WriteLine($"  EditorID: {rec.EditorID ?? "(none)"}");
                DumpAllProperties(rec, match.Value.ElementType, cache, "  ");
                Console.WriteLine();
                found++;
            }
            return found;
        }

        /// Every property that carries a value, then the empty ones named on one line.
        ///
        /// Same two-tier shape as DumpCoverage and for the same reason: filtering the empties away
        /// would make "the record does not carry this" and "my predicate hid it" the same silence.
        /// Here it matters more, not less -- this is the ONLY view of these record types.
        private static void DumpAllProperties(object rec, Type declared, ILinkCache? cache, string indent)
        {
            var carrying = new List<string>();
            var empty = new List<string>();
            var props = declared.IsInterface
                ? declared.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                : rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (prop.Name is "FormKey" or "EditorID") continue;   // printed by the caller
                object? v;
                try { v = prop.GetValue(rec); } catch { continue; }
                if (v == null || IsEmptyValue(v)) { empty.Add(prop.Name); continue; }

                string shown = Render(v, cache);
                if (shown.Length > 200) shown = shown.Substring(0, 200) + "…";
                carrying.Add($"{indent}  {prop.Name} = {shown}{WidthTell(v)}");
            }
            foreach (var c in carrying) Console.WriteLine(c);
            if (empty.Count > 0)
                Console.WriteLine($"{indent}  (empty on this record [{empty.Count}]: {string.Join(", ", empty)})");
        }

        /// Render one property value for the generic dump.
        ///
        /// ⛔ ITS FIRST RUN PRINTED `Model = Mutagen.Bethesda.Starfield.ModelBinaryOverlay`, WHICH
        /// IS THE TYPE NAME STANDING WHERE THE NIF PATH SHOULD BE. On a door the model path is the
        /// single most useful field on the record, and the dump was hiding it behind the default
        /// `object.ToString()`. **A value that renders as its own type name is indistinguishable
        /// from a value the reader cannot show, which is the defect this whole pass exists to
        /// close, reappearing one layer in.**
        ///
        /// ⭐ THE CURE IS A RULE, NOT A LIST OF SPECIAL CASES: when `ToString()` returns the type's
        /// own name, that IS the default implementation, so expand one level of the value's own
        /// properties instead. That fixes Model, ObjectBounds, the sound references and every
        /// future overlay type at once, where a hardcoded list would have fixed the four I happened
        /// to be looking at and gone stale on the fifth.
        ///
        /// ⚠ ONE LEVEL ONLY, deliberately. Deeper is a record browser, not a dump, and an
        /// unbounded walk over a graph with FormLinks in it does not terminate usefully.
        private static string Render(object v, ILinkCache? cache, bool expand = true)
        {
            // A FormLink prints as an id, and an id is not a name -- resolve it, the same courtesy
            // the bespoke renderers pay, because an unresolved link is the commonest reason
            // somebody has to go and run a second command.
            if (v is IFormLinkGetter fl)
            {
                if (fl.IsNull) return "NULL";
                string n = NameOf(fl.FormKey, cache);
                return fl.FormKey + (n.Length > 0 ? $"  {n}" : "");
            }
            if (v is string s) return s;

            if (v is System.Collections.IEnumerable en and not string)
            {
                var parts = new List<string>();
                int n = 0;
                foreach (var item in en)
                {
                    n++;
                    if (n <= 6 && item != null) parts.Add(Render(item, cache, expand: false));
                }
                if (n == 0) return "[]";
                string head = string.Join(", ", parts);
                return n > 6 ? $"[{n} items] {head}, …" : $"[{n}] {head}";
            }

            var t = v.GetType();
            string str = v.ToString() ?? "";
            if (str != t.FullName && str != t.Name) return str;   // a real ToString, use it
            if (!expand) return t.Name;

            // Default ToString: expand one level rather than print the type name.
            var bits = new List<string>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object? pv;
                try { pv = p.GetValue(v); } catch { continue; }
                if (pv == null || IsEmptyValue(pv)) continue;
                bits.Add($"{p.Name}={Render(pv, cache, expand: false)}");
            }
            return bits.Count == 0 ? $"({t.Name}, nothing set)" : "{ " + string.Join(", ", bits) + " }";
        }

        /// ⛔ THE POSITIVE CONTROL, AND IT EXISTS BECAUSE THE SWEEP CAME BACK ZERO.
        ///
        /// WidthTell was run across all 22 placed refs in avontechstardust and fired on NONE of
        /// them. That is either "no REFR here carries an undecoded oversized integer" or "the tell
        /// is broken", and those two are byte-identical from the outside: A SEARCH RETURNING ZERO IS
        /// A NEEDLE FAILURE UNTIL A POSITIVE CONTROL SAYS OTHERWISE. So the values below are the two
        /// REAL readings from the 2026-09-15 bay door, the ones xEdit rendered as `Inaccessible` and
        /// `Unknown 0`, and they are asserted rather than eyeballed.
        ///
        /// It also carries NEGATIVE controls, because a tell that fires on everything is the same
        /// useless as one that fires on nothing.
        ///
        ///   dotnet run -- gen_inspect selftest x
        ///
        /// Exit 1 on any failure, so it can gate rather than inform.
        private static int SelfTest()
        {
            int fail = 0;
            void Check(string label, bool ok, string detail)
            {
                Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + "   " + detail);
                if (!ok) fail++;
            }

            // POSITIVE: the two raw values Mutagen actually returned for the bay door's XLOC.
            string lvl = WidthTell(6357246u);                 // 0x006100FE -> Level 0xFE = 254
            Check("XLOC Level 0x006100FE tells", lvl.Contains("0xFE") && lvl.Contains("(254)"),
                  lvl.Length > 0 ? "fired" : "SILENT -- the tell did not fire on a known case");
            string flg = WidthTell(1087655425u);              // 0x40D44E01 -> Flags 0x01 = 1
            Check("XLOC Flags 0x40D44E01 tells", flg.Contains("0x01") && flg.Contains("(1)"),
                  flg.Length > 0 ? "fired" : "SILENT -- the tell did not fire on a known case");

            // NEGATIVE: values that must stay quiet, or the block becomes noise nobody reads.
            Check("a 2-byte value stays quiet", WidthTell(65535u).Length == 0, "0x0000FFFF");
            Check("a large value with a ZERO low byte stays quiet", WidthTell(0x40D44E00u).Length == 0,
                  "0x40D44E00 -- nothing to recover, so nothing to say");
            Check("XALG's documented 8uL stays quiet", WidthTell(8uL).Length == 0, "8");
            Check("a non-integer stays quiet", WidthTell("some string").Length == 0, "string");
            Check("a negative int stays quiet", WidthTell(-5).Length == 0, "-5");

            Console.WriteLine();
            Console.WriteLine(fail == 0
                ? "gen_inspect selftest: all checks passed"
                : $"gen_inspect selftest: {fail} FAILED");
            return fail == 0 ? 0 : 1;
        }

        private static string WidthTell(object v)
        {
            ulong raw;
            switch (v)
            {
                case uint u: raw = u; break;
                case int i when i > 0: raw = (ulong)i; break;
                case ulong ul: raw = ul; break;
                case long l when l > 0: raw = (ulong)l; break;
                default: return "";
            }
            if (raw <= 0xFFFF) return "";                 // fits the widths that are genuinely 2 bytes
            ulong low = raw & 0xFF;
            if (low == 0) return "";                      // a low byte of 0 tells us nothing
            return $"   ⚠ >2 bytes: if SF1 declares this itU8 + wbUnused(3), the value is the LOW BYTE"
                   + $" 0x{low:X2} ({low}). raw 0x{raw:X8}. grep the signature in"
                   + " C:\\Git\\TES5Edit\\Core\\wbDefinitionsSF1.pas before trusting either reading.";
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

        // ---------------------------------------------------------------------------------
        // DescribeSub -- render a nested Mutagen sub-object by REFLECTION, never by ToString().
        //
        // WHY (2026-09-23, his "decode the three fills"): three fill renderers interpolated the
        // object bare -- `$"FILL location (ALLA): {a.Location}"` -- so the output was
        // `Mutagen.Bethesda.Starfield.LocationAliasReferenceBinaryOverlay`, the CLASS NAME. Those
        // properties are in RefAliasPropsRendered, so the alias-level coverage pass treated them as
        // RENDERED and never named them. The property was marked covered while its contents were
        // invisible, which is worse than omitting it: the reader sees a line and believes he has
        // looked. Found while asking whether a ref alias can fill from a POI's marker -- the
        // mechanism that answers it lives inside the ALLA block and could not be read at all.
        //
        // Reflection rather than typed accessors is deliberate. The typed route needs a member name
        // guessed against a fan-authored library and fails at COMPILE time on a guess, or worse
        // renders a subset that then looks complete. This walks whatever is actually there, so a
        // field nobody has heard of surfaces instead of vanishing -- the same contract the alias and
        // record coverage passes already keep.
        private static string DescribeSub(object? o, List<IStarfieldModGetter>? allMods, int depth = 0)
        {
            if (o == null) return "(null)";
            var parts = new List<string>();
            foreach (var pi in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0) continue;
                object? v;
                try { v = pi.GetValue(o); } catch { continue; }
                var s = DescribeValue(v, allMods, depth);
                if (s == null) continue;
                parts.Add($"{pi.Name}={s}");
            }
            // An empty render is a CLAIM and it gets said out loud rather than printed as blank:
            // "nothing readable here" and "I did not look" must not be the same output.
            return parts.Count == 0 ? "(no readable properties on " + o.GetType().Name + ")" : string.Join("  ", parts);
        }

        // One value, resolved as far as it can honestly be taken. Returns null for things that
        // carry nothing, so the caller drops the key entirely rather than printing `X=`.
        private static string? DescribeValue(object? v, List<IStarfieldModGetter>? allMods, int depth)
        {
            if (v == null) return null;
            var t = v.GetType();

            // A FormLink/FormKey is the whole point: resolve it to an EditorID where we can.
            var fkProp = t.GetProperty("FormKey");
            if (fkProp != null)
            {
                try
                {
                    var fk = fkProp.GetValue(v);
                    if (fk is FormKey key)
                    {
                        if (key.IsNull) return null;
                        return ResolveName(key, allMods);
                    }
                }
                catch { /* fall through to the generic paths */ }
            }
            if (v is FormKey bare) return bare.IsNull ? null : ResolveName(bare, allMods);

            if (v is string str) return str.Length == 0 ? null : "\"" + str + "\"";
            if (t.IsPrimitive || t.IsEnum) return v.ToString();

            // IEnumerable, NOT the non-generic ICollection. Mutagen's BinaryOverlayList implements
            // the GENERIC IReadOnlyList<T> and does not implement System.Collections.ICollection,
            // so an ICollection test falls straight through to the object path and renders the
            // list's Count property as if it were the value. Caught by RUNNING this on the record
            // it was written for: the ALPS conditions came back `{Count=6}`, which is the same
            // class of half-answer this helper exists to kill, produced by the helper itself.
            if (v is System.Collections.IEnumerable seq && v is not string)
            {
                var items = new List<string>();
                foreach (var item in seq)
                {
                    if (depth >= 2) { items.Add("…"); break; }
                    var s = DescribeValue(item, allMods, depth + 1) ?? DescribeSub(item, allMods, depth + 1);
                    items.Add(s);
                }
                if (items.Count == 0) return null;
                return "[" + string.Join(" | ", items) + "]";
            }

            // A nested Mutagen record shape: recurse once, then stop and SAY that we stopped.
            if (t.Namespace != null && t.Namespace.StartsWith("Mutagen"))
            {
                if (depth >= 2) return $"({t.Name}, not expanded at this depth)";
                return "{" + DescribeSub(v, allMods, depth + 1) + "}";
            }

            var plain = v.ToString();
            if (string.IsNullOrEmpty(plain)) return null;
            // The defect this whole helper exists for: never let a bare type name pass as a value.
            if (plain == t.FullName || plain == t.Name) return $"({t.Name}, no readable value)";
            return plain;
        }

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
                Console.WriteLine($"{pad}  FILL from-event (ALFE/ALFD): {DescribeSub(a.FindMatchingRefFromEvent, allMods)}");
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
                Console.WriteLine($"{pad}  FILL location (ALLA): {DescribeSub(a.Location, allMods)}");
                fills++;
            }
            if (a.External != null)
            {
                Console.WriteLine($"{pad}  FILL external (ALEQ/ALEA): {DescribeSub(a.External, allMods)}");
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
                    // Was `v.ToString()` truncated to 60 chars, which on the ALPS Conditions list
                    // printed `Mutagen...BinaryOverlayList+Bi…` -- a CLASS NAME, cut in half, under
                    // a heading promising the PCM request. Same defect as the three fills above and
                    // the same cure: walk the value, never name its type.
                    var vs = DescribeValue(v, allMods, 0);
                    if (vs == null || vs is "Null" or "" or "0") continue;
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

        private static void DumpRecord(object record, string typeName, ILinkCache? cache = null)
        {
            Console.WriteLine($"--- {typeName} ---");
            DumpPropertiesReflection(record, "  ", maxDepth: 2, currentDepth: 0, cache: cache);
            Console.WriteLine();
        }

        private static void DumpPropertiesReflection(object obj, string indent, int maxDepth, int currentDepth = 0,
                                                     ILinkCache? cache = null)
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
                        // ⛔ THIS PRINTED "<enumerable BinaryOverlayListByStartIndex`1>" AND DROPPED THE
                        // LIST, and on 2026-09-16 that hid the REQUIRED keyword on a PackIn
                        // (SBShip_DockingHatch) through two full dumps of the working reference and
                        // the broken part. A value rendering as its own TYPE NAME is indistinguishable
                        // from one the reader cannot show -- the same defect this file's generic
                        // dumper was cured of that morning, in the OTHER of its two generic dumpers.
                        // Mutagen's keyword/overlay lists are IEnumerable and NOT ICollection, so they
                        // fell here rather than into the counted branch above.
                        //
                        // Bounded, not unbounded: the original comment's infinite-loop worry is real
                        // for a self-referencing enumerable, so it takes at most 10 like the
                        // ICollection branch, counts no further, and renders each item through Render
                        // so a FormLink resolves to an EditorID instead of a bare FormID.
                        var shown = new List<string>();
                        int seen = 0;
                        try
                        {
                            foreach (var item in enumerable)
                            {
                                seen++;
                                if (seen <= 10 && item != null) shown.Add(Render(item, cache, expand: false));
                                if (seen > 10) break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Say WHICH property could not be walked. A swallowed exception here is
                            // the same blindness one layer down.
                            Console.WriteLine($"{indent}{prop.Name}: <could not enumerate {valueType.Name}: {ex.GetType().Name}>");
                            continue;
                        }
                        if (seen == 0) continue;                       // empty, same as an empty ICollection
                        Console.WriteLine($"{indent}{prop.Name}: [{(seen > 10 ? "10+" : seen.ToString())} items]");
                        foreach (var s in shown) Console.WriteLine($"{indent}  {s}");
                        if (seen > 10) Console.WriteLine($"{indent}  ... and more");
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
                        DumpPropertiesReflection(value, indent + "  ", maxDepth, currentDepth + 1, cache);
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

        /// Every record group the mod actually exposes, DERIVED rather than listed.
        ///
        /// ⛔ THIS WAS A HAND-WRITTEN TABLE OF 24 ENTRIES AND IT WAS THE THIRD SUCH LIST IN THIS
        /// FILE. The other two are `SupportedTypes` (the usage text) and the `switch` that decides
        /// what actually works, and all three had drifted into different subsets of each other:
        /// `list` advertised 24 groups, the switch handled about 40 types, and a Door was in
        /// neither. **A rule open-coded in three places is three bugs, and fixing the one you are
        /// looking at makes the others invisible.**
        ///
        /// ⭐ His correction, and it moved the seam rather than lengthening the list: *"we have a
        /// list of all record types now can we not fill it all out?"* Mutagen already declares every
        /// group in the type system, so any copy of that list in our source can only go stale.
        private static void ListRecordGroups(IStarfieldModGetter mod)
        {
            Console.WriteLine("Record groups in Starfield.esm (derived from the mod, not a list):");
            Console.WriteLine();

            int groups = 0, nonEmpty = 0;
            foreach (var g in RecordGroups.Enumerate(mod))
            {
                groups++;
                // Count via the group's own Count where it has one; a group whose Count throws is
                // reported as unreadable rather than as zero, because "no records" and "I could not
                // ask" are the same blank otherwise and that blank is this file's oldest defect.
                string count;
                try
                {
                    var cp = g.Records.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                    int n = cp == null ? -1 : (int)cp.GetValue(g.Records)!;
                    count = n < 0 ? "(no Count)" : $"{n:N0} records";
                    if (n > 0) nonEmpty++;
                }
                catch (Exception ex) { count = $"(Count threw: {ex.GetType().Name})"; }
                Console.WriteLine($"  {g.Name,-28} {count,-22} [{g.PropertyName}]");
            }
            Console.WriteLine();
            Console.WriteLine($"  {groups} group(s), {nonEmpty} non-empty in Starfield.esm.");
            Console.WriteLine("  Any of these names works as a record type; the ones with a bespoke");
            Console.WriteLine("  renderer are listed by the usage text, the rest fall back to a full");
            Console.WriteLine("  property dump.");
        }
    }
}
