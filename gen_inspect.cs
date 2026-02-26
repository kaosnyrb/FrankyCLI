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
                Console.WriteLine("Record types: SurfaceBlock, Worldspace, PackIn, Cell, Static, Activator, Light, Npc");
                Console.WriteLine("              PcmBranchNode, PcmContentNode, Book, Scene");
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
                    allMods.Add(env.LoadOrder[i].Mod);
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
                case "static":
                    foreach (var rec in mod.Statics)
                        if (MatchesSearch(rec.EditorID, rec.FormKey, search))
                        { DumpRecord(rec, "Static"); found++; }
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
                default:
                    Console.WriteLine($"Unknown record type: {recordType}");
                    Console.WriteLine("Supported: SurfaceBlock, Worldspace, PackIn, Cell, Static, Activator, Light, Npc, Location, PcmBranchNode, PcmContentNode, Book, Scene");
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
                ("Cell", mod.Cells.Sum(b => b.SubBlocks.Sum(sb => sb.Cells.Count))),
                ("Light", mod.Lights.Count),
                ("Location", mod.Locations.Count),
                ("Npc", mod.Npcs.Count),
                ("PackIn", mod.PackIns.Count),
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
