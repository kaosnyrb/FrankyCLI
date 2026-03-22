using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// Investigates Starfield forms by dumping their properties.
    /// Usage: gen_inspect &lt;modname&gt; gen_inspect &lt;prefix&gt; &lt;recordtype&gt; &lt;editorid_or_formid&gt;
    /// Example: dummy gen_inspect dummy SurfaceBlock OverlayBlockstbblock001
    /// Example: dummy gen_inspect dummy Worldspace 0x00000C36
    /// </summary>
    public class gen_inspect
    {
        public static int Generate(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: <modname> gen_inspect <dummy> <recordtype> <editorid_or_formid>");
                Console.WriteLine();
                Console.WriteLine("Record types: SurfaceBlock, Worldspace, PackIn, Cell, Static, Activator, Light, Npc, Keyword");
                Console.WriteLine("              PcmBranchNode, PcmContentNode, Book, Scene");
                Console.WriteLine("              Quest, Quest_VMAD (full VirtualMachineAdapter + alias dump)");
                Console.WriteLine("              Message (alias: mesg), Faction, Global, FormList");
                Console.WriteLine("              Armor (armo), ObjectModification (omod), ObjectEffect (ench)");
                Console.WriteLine("              Perk, MagicEffect (mgef), DamageType (dmgt), Spell (spel)");
                Console.WriteLine("              LegendaryItem (lgdi), Outfit (otft), ActorValueInformation (avif)");
                Console.WriteLine("              Use 'list' as record type to see all available groups.");
                Console.WriteLine();
                Console.WriteLine("EditorID search: partial match (contains)");
                Console.WriteLine("FormID search:   prefix with 0x (e.g. 0x00000C36)");
                return 1;
            }

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
                found += InspectRecordType(mod, recordType, search, allMods);
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

        private static int InspectRecordType(IStarfieldModGetter mod, string recordType, string search, List<IStarfieldModGetter>? allMods = null)
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
                                { DumpCell(cell); found++; }
                    // Also search worldspace subcells and TopCells
                    foreach (var ws in mod.Worldspaces)
                    {
                        if (ws.TopCell != null && MatchesSearch(ws.TopCell.EditorID, ws.TopCell.FormKey, search))
                        { Console.Write($"  [Worldspace TopCell: {ws.EditorID}] "); DumpCell(ws.TopCell); found++; }
                        foreach (var wsBlock in ws.SubCells)
                            foreach (var wsSubBlock in wsBlock.Items)
                                foreach (var cell in wsSubBlock.Items)
                                    if (MatchesSearch(cell.EditorID, cell.FormKey, search))
                                    { Console.Write($"  [Worldspace: {ws.EditorID} grid ({wsSubBlock.BlockNumberX},{wsSubBlock.BlockNumberY})] "); DumpCell(cell); found++; }
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
                        { DumpRecord(rec, "MoveableStatic"); found++; }
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
                        { DumpRecord(rec, "Npc"); found++; }
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
                    foreach (var rec in mod.Keywords)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Keyword"); found++; }
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
                        { DumpRecord(rec, "FormList"); found++; }
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
                default:
                    Console.WriteLine($"Unknown record type: {recordType}");
                    Console.WriteLine("Supported: SurfaceBlock, Worldspace, WorldspaceStructure, PackIn, Cell, Static, Activator, Light, Npc, Location, Keyword, PcmBranchNode, PcmContentNode, Book, Scene");
                    Console.WriteLine("           Quest, DialogBranch, DialogTopic, AudioLog (full dialog chain dump)");
                    Console.WriteLine("           Message (alias: mesg), Faction, Global, FormList");
                    Console.WriteLine("           Armor (alias: armo), ObjectModification (alias: omod), ObjectEffect (alias: ench)");
                    Console.WriteLine("           Perk, MagicEffect (alias: mgef), DamageType (alias: dmgt), Spell (alias: spel)");
                    Console.WriteLine("           LegendaryItem (alias: lgdi), Outfit (alias: otft), ActorValueInformation (alias: avif)");
                    Console.WriteLine("           PlacedObject (alias: refr) — search for placed objects by EditorID or FormID (0x...)");
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

        private static void DumpCell(ICellGetter cell)
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
                {
                    if (entry is IPlacedObjectGetter po)
                    {
                        Console.WriteLine($"    PlacedObject {po.FormKey} EditorID={po.EditorID} Base={po.Base.FormKey} Pos={po.Position} Rot={po.Rotation}");
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
                        if (po.LinkedReferences != null && po.LinkedReferences.Count > 0)
                        {
                            Console.WriteLine($"      LinkedReferences: [{po.LinkedReferences.Count}]");
                            foreach (var lr in po.LinkedReferences)
                                Console.WriteLine($"        KeywordOrRef={lr.KeywordOrReference.FormKey} Ref={lr.Reference.FormKey}");
                        }
                    }
                    else if (entry is IPlacedNpcGetter npc)
                        Console.WriteLine($"    PlacedNpc {npc.FormKey} EditorID={npc.EditorID} Base={npc.Base.FormKey} Pos={npc.Position} Rot={npc.Rotation}");
                    else
                        Console.WriteLine($"    {entry.GetType().Name} {entry.FormKey}");
                }
                if (cell.Persistent.Count > 200)
                    Console.WriteLine($"    ... and {cell.Persistent.Count - 200} more");
            }

            if (cell.Temporary.Count > 0)
            {
                Console.WriteLine("  Temporary entries:");
                foreach (var entry in cell.Temporary)
                {
                    if (entry is IPlacedObjectGetter po)
                        Console.WriteLine($"    PlacedObject {po.FormKey} EditorID={po.EditorID} Base={po.Base.FormKey} Pos={po.Position} Rot={po.Rotation}");
                    else if (entry is IPlacedNpcGetter npc)
                        Console.WriteLine($"    PlacedNpc {npc.FormKey} EditorID={npc.EditorID} Base={npc.Base.FormKey} Pos={npc.Position} Rot={npc.Rotation}");
                    else
                        Console.WriteLine($"    {entry.GetType().Name} {entry.FormKey}");
                }
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
                    Console.WriteLine($"           Property.Name={fa.Property.Name}  Property.Flags=0x{(ushort)fa.Property.Flags:X4}  Property.Object={fa.Property.Object.FormKey}");
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
                        Console.WriteLine($"    [LocAlias]  ID={locAlias.ID}  Name={locAlias.Name}  Flags=0x{(uint)locAlias.Flags:X8}");
                        Console.WriteLine($"      SpecificLocation: {(locAlias.SpecificLocation.IsNull ? "null" : locAlias.SpecificLocation.FormKey.ToString())}");
                        if (locAlias.ALPS != null)
                            Console.WriteLine($"      ALPS.PcmTypeKeyword: {(locAlias.ALPS.PcmTypeKeyword.IsNull ? "null" : locAlias.ALPS.PcmTypeKeyword.FormKey.ToString())}");
                        if (locAlias.Conditions != null && locAlias.Conditions.Count > 0)
                        {
                            Console.WriteLine($"      Conditions [{locAlias.Conditions.Count}]:");
                            foreach (var cond in locAlias.Conditions)
                                DumpConditionBrief(cond, "        ");
                        }
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
        {
            string op  = cond.CompareOperator.ToString();
            string val = cond is IConditionFloatGetter cf ? cf.ComparisonValue.ToString("F2") : "?";
            string fn  = cond.Data?.GetType().Name ?? "?";
            string extra = "";
            if (cond.Data is IGetGlobalValueConditionDataGetter ggv)
                extra = $" global={ggv.FirstParameter.Link.FormKey}";
            else if (cond.Data is IGetStageConditionDataGetter gs)
                extra = $" quest={gs.FirstParameter.Link.FormKey} stage={gs.SecondParameter}";
            Console.WriteLine($"{indent}{fn}{extra} {op} {val}  flags=0x{(byte)cond.Flags:X2}");
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
                        if (!ws.Flags.HasFlag(Worldspace.Flag.SmallWorld)) continue;
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
                ("DamageType", mod.DamageTypes.Count),
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
                ("Spell", mod.Spells.Count),
                ("Static", mod.Statics.Count),
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
