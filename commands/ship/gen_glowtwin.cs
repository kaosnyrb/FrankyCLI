using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// Build GLOW TWINS from a plan, in ONE load and ONE write.
    ///
    ///   glowtwin &lt;modname&gt; &lt;plan.json&gt; [--dry]
    ///
    /// WHY IT EXISTS (2026-10-02, his "think everything and keep the name"): a glow pip on every
    /// Stardust part is ~140 PackIns and ~330 modules. By `struct` + `copymodule` that is ~28 s a load
    /// and four to six hours, and `struct` RE-AUTHORS the shell (filter, FNAM, bounds, transforms) where
    /// a twin should COPY it. Here every record is DUPLICATED WHOLE from its source and only the fields
    /// that make it a twin are changed. Nothing is invented.
    ///
    /// A twin differs from its source in exactly these, and nothing else:
    ///   MoveableStatic   EditorID, Model.File (LightLayer and Flags asserted unchanged)
    ///   Cell             EditorID, every placed ref re-keyed, the ref whose Base is the source
    ///                    MoveableStatic rebased onto the twin (every other ref, flares, magnets,
    ///                    fins, markers, kept as it stands)
    ///   PackIn           EditorID, Cell
    ///   GenericBaseForm  EditorID, the SpaceshipLinkedExterior link (source PackIn -> twin PackIn; 0x00662F, read off every dumped GBFM),
    ///                    ShipModuleVariant (the plan's number), and its upgrade chain ONLY when the
    ///                    module plans an "upgrade"
    ///   FormList         the twin GBFM appended, only where the source GBFM is already a member
    ///
    /// PLAN: { "packins": [ {"src": pkin, "mstt": its shell MSTT, "item": "glow_x", "model": "Meshes\\..."} ],
    ///         "modules": [ {"src": gbfm, "dst": gbfm, "packin": twin pkin (planned above or live),
    ///                       "variant": n, "formList": flst or null, "upgrade": kywd (optional)} ] }
    ///
    /// "upgrade" (2026-10-05, his "think there own" for the glow Stokers): a Stoker is a CHAIN OF ONE
    /// (manual 35: the upgrade screen cannot change variant), so its twin may not share the source's
    /// chain. The named keyword must be Type ShipModuleUpgrade and the source must carry EXACTLY ONE
    /// chain, which the twin's replaces. Omitted, the twin keeps the source's chain, as every non-Stoker
    /// twin does.
    ///
    /// ⛔ VALIDATE EVERYTHING, THEN MUTATE: every name resolves, no planned name exists yet, every model
    /// is on disk, every source cell has no PERSISTENT ref and exactly one ref on the planned shell,
    /// every source module links exactly one exterior PackIn (the planned source) and no interior.
    /// Any failure refuses the whole plan and nothing is written. After the write every created GBFM
    /// is read back off disk for STRV, as batch does.
    ///
    /// ⚠ THE CELL BLOCK/SUB-BLOCK RULE IS A SECOND COPY of gen_shipstruct's (the last two digits of the
    /// FormID, ElminsterAU). Named rather than hidden; the shared helper is owed.
    /// </summary>
    internal static class gen_glowtwin
    {
        const uint AV_SHIP_MODULE_VARIANT = 0x27BACE;
        const uint KW_LINKED_EXTERIOR = 0x00662F;

        public static int Generate(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: glowtwin <modname> <plan.json> [--dry]");
                return 1;
            }
            string modname = args[0], planPath = args[2];
            bool dry = args.Skip(3).Any(a => a == "--dry");
            var plan = JsonDocument.Parse(File.ReadAllText(planPath)).RootElement;

            using var session = PluginSession.Open(modname);
            if (session == null) return 1;
            var mod = session.Mod;
            var sf = session.SfKey;
            string dataPath = Path.GetDirectoryName(session.PluginPath)!;
            var errs = new List<string>();

            T? ByEid<T>(IEnumerable<T> g, string id) where T : class, IMajorRecordGetter
            {
                var hits = g.Where(r => string.Equals(r.EditorID, id, StringComparison.OrdinalIgnoreCase)).ToList();
                if (hits.Count > 1) errs.Add($"'{id}' names {hits.Count} records");
                return hits.FirstOrDefault();
            }
            bool Exists(string id) => mod.EnumerateMajorRecords()
                .Any(r => string.Equals(r.EditorID, id, StringComparison.OrdinalIgnoreCase));
            var allCells = mod.Cells.SelectMany(b => b.SubBlocks).SelectMany(s => s.Cells).ToList();

            // ---------------------------------------------------------------- pass 1: validate
            var pkPlans = new List<(IPackInGetter pk, ICellGetter cell, IMoveableStaticGetter ms, string item, string model)>();
            foreach (var p in plan.GetProperty("packins").EnumerateArray())
            {
                string src = p.GetProperty("src").GetString()!, mstt = p.GetProperty("mstt").GetString()!;
                string item = p.GetProperty("item").GetString()!, model = p.GetProperty("model").GetString()!;
                var pk = ByEid(mod.PackIns, src);
                var ms = ByEid(mod.MoveableStatics, mstt);
                if (pk == null) { errs.Add($"no PackIn {src}"); continue; }
                if (ms == null) { errs.Add($"no MoveableStatic {mstt}"); continue; }
                foreach (var n in new[] { $"atsd_ms_{item}", $"atsd_cell_{item}", $"atsd_pkn_{item}" })
                    if (Exists(n)) errs.Add($"{n} already exists -- a re-run must plan only what is missing");
                if (ms.Model?.File == null) errs.Add($"{mstt} has no Model.File");
                if (!File.Exists(Path.Combine(dataPath, model))) errs.Add($"{model} is not on disk under Data");
                var cell = allCells.FirstOrDefault(c => c.FormKey == pk.Cell.FormKey);
                if (cell == null) { errs.Add($"{src}: its cell {pk.Cell.FormKey} is not in {modname}"); continue; }
                if (cell.Persistent.Count > 0) errs.Add($"{src}: cell has {cell.Persistent.Count} PERSISTENT ref(s) (door/docker machinery)");
                int shells = cell.Temporary.OfType<IPlacedObjectGetter>().Count(r => r.Base.FormKey == ms.FormKey);
                if (shells != 1) errs.Add($"{src}: cell places {mstt} {shells} times, not once");
                if (cell.Temporary.Any(r => r is not IPlacedObjectGetter)) errs.Add($"{src}: cell holds a non-object placed ref");
                pkPlans.Add((pk, cell, ms, item, model));
            }
            var plannedPk = pkPlans.Select(x => $"atsd_pkn_{x.item}").ToHashSet(StringComparer.OrdinalIgnoreCase);

            var cache = session.Cache;
            List<FormKey> ChainsOf(IGenericBaseFormGetter g) =>
                g.Components.OfType<IKeywordFormComponentGetter>().SelectMany(c => c.Keywords ?? Enumerable.Empty<IFormLinkGetter<IKeywordGetter>>())
                 .Where(k => cache.TryResolve<IKeywordGetter>(k.FormKey, out var kw) && kw.Type == Keyword.TypeEnum.ShipModuleUpgrade)
                 .Select(k => k.FormKey).ToList();

            var modPlans = new List<(IGenericBaseFormGetter g, string dst, string packin, float variant, IFormListGetter? fl, (FormKey had, FormKey want)? chain)>();
            foreach (var m in plan.GetProperty("modules").EnumerateArray())
            {
                string src = m.GetProperty("src").GetString()!, dst = m.GetProperty("dst").GetString()!;
                string packin = m.GetProperty("packin").GetString()!;
                float variant = (float)m.GetProperty("variant").GetDouble();
                string? flId = m.TryGetProperty("formList", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                string? upId = m.TryGetProperty("upgrade", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                var g = ByEid(mod.GenericBaseForms, src);
                if (g == null) { errs.Add($"no GenericBaseForm {src}"); continue; }
                (FormKey, FormKey)? chain = null;
                if (upId != null)
                {
                    var want = cache.PriorityOrder.WinningOverrides<IKeywordGetter>()
                        .Where(k => string.Equals(k.EditorID, upId, StringComparison.Ordinal)).ToList();
                    var had = ChainsOf(g);
                    if (want.Count != 1) errs.Add($"{dst}: upgrade {upId} names {want.Count} keywords in the load order, not one");
                    else if (want[0].Type != Keyword.TypeEnum.ShipModuleUpgrade) errs.Add($"{dst}: {upId} is Type {want[0].Type}, not ShipModuleUpgrade");
                    else if (had.Count != 1) errs.Add($"{dst}: source {src} carries {had.Count} upgrade chains; replacing one needs exactly one");
                    else if (had[0] == want[0].FormKey) errs.Add($"{dst}: upgrade {upId} is the source's own chain -- omit the field to keep it");
                    else chain = (had[0], want[0].FormKey);
                }
                if (Exists(dst)) errs.Add($"{dst} already exists");
                if (!plannedPk.Contains(packin) && ByEid(mod.PackIns, packin) == null) errs.Add($"{dst}: twin PackIn {packin} neither planned nor live");
                var links = g.Components.OfType<IFormLinkDataComponentGetter>().SelectMany(c => c.Links).ToList();
                // Exactly ONE link into this plugin, and it is the exterior PackIn. Any other (an
                // interior PackIn on a hab, bay or cockpit) refuses: not a shell this command copies.
                var mine = links.Where(l => l.LinkedForm.FormKey.ModKey == mod.ModKey).ToList();
                if (mine.Count != 1 || mine[0].Keyword.FormKey != new FormKey(sf, KW_LINKED_EXTERIOR))
                    errs.Add($"{src} links {mine.Count} record(s) into {modname}; a twin needs exactly one, the exterior PackIn");
                if (!g.Components.OfType<IPropertySheetComponentGetter>().Any()) errs.Add($"{src} has no PropertySheet");
                IFormListGetter? fl = null;
                if (flId != null)
                {
                    fl = ByEid(mod.FormLists, flId);
                    if (fl == null) errs.Add($"no FormList {flId}");
                    else if (!fl.Items.Any(i => i.FormKey == g.FormKey)) errs.Add($"{src} is not a member of {flId}");
                }
                modPlans.Add((g, dst, packin, variant, fl, chain));
            }
            if (errs.Count > 0)
            {
                Console.WriteLine($"REFUSED -- {errs.Count} problem(s), nothing written:");
                foreach (var e in errs.Distinct()) Console.WriteLine("  " + e);
                return 1;
            }

            // ---------------------------------------------------------------- pass 2: apply
            var twinPk = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var (pk, cell, ms, item, model) in pkPlans)
            {
                var nms = ms.Duplicate(mod.GetNextFormKey());
                nms.EditorID = $"atsd_ms_{item}";
                uint? ll = nms.Model!.LightLayer; var fl0 = nms.Model.Flags;
                nms.Model.File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(model);
                if (nms.Model.LightLayer != ll || nms.Model.Flags != fl0) { Console.WriteLine($"REFUSED: {item} model flags moved"); return 1; }
                mod.MoveableStatics.Add(nms);

                var ncell = cell.Duplicate(mod.GetNextFormKey());
                ncell.EditorID = $"atsd_cell_{item}";
                var refs = new ExtendedList<IPlaced>();
                foreach (var r in cell.Temporary.OfType<IPlacedObjectGetter>())
                {
                    var nr = r.Duplicate(mod.GetNextFormKey());
                    if (nr.Base.FormKey == ms.FormKey) nr.Base = nms.ToLink<IPlaceableObjectGetter>();
                    refs.Add(nr);
                }
                ncell.Temporary.Clear();
                ncell.Temporary.AddRange(refs);
                InsertCell(mod, ncell);

                var npk = pk.Duplicate(mod.GetNextFormKey());
                npk.EditorID = $"atsd_pkn_{item}";
                npk.Cell = ncell.ToNullableLink<ICellGetter>();
                mod.PackIns.Add(npk);
                twinPk[npk.EditorID] = npk.FormKey;
                Console.WriteLine($"  + {npk.EditorID} / {nms.EditorID} / {ncell.EditorID}  ({cell.Temporary.Count} refs)  <- {pk.EditorID}  model {model}");
            }

            var created = new List<string>();
            var avVariant = new FormKey(sf, AV_SHIP_MODULE_VARIANT);
            foreach (var (g, dst, packin, variant, fl, chain) in modPlans)
            {
                FormKey pkKey = twinPk.TryGetValue(packin, out var k) ? k : mod.PackIns.First(p => string.Equals(p.EditorID, packin, StringComparison.OrdinalIgnoreCase)).FormKey;
                var ng = g.Duplicate(mod.GetNextFormKey());
                ng.EditorID = dst;
                foreach (var fld in ng.Components.OfType<FormLinkDataComponent>())
                    foreach (var l in fld.Links)
                        if (l.Keyword.FormKey == new FormKey(sf, KW_LINKED_EXTERIOR) && l.LinkedForm.FormKey.ModKey == mod.ModKey)
                            l.LinkedForm = new FormLinkNullable<IStarfieldMajorRecordGetter>(pkKey);
                var sheet = ng.Components.OfType<PropertySheetComponent>().First();
                var v = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == avVariant);
                if (v != null) v.Value = variant;
                else sheet.Properties.Add(new ObjectProperty { ActorValue = avVariant.ToNullableLink<IActorValueInformationGetter>(), Value = variant });
                string chainNote = "";
                if (chain is (FormKey had, FormKey want))
                {
                    var kws = ng.Components.OfType<KeywordFormComponent>().Single().Keywords!;
                    int at = kws.FindIndex(k => k.FormKey == had);
                    kws[at] = want.ToLink<IKeywordGetter>();
                    chainNote = $"  chain {gen_catalogue.Name(had, cache)} -> {gen_catalogue.Name(want, cache)}";
                }
                mod.GenericBaseForms.Add(ng);
                created.Add(dst);
                if (fl != null)
                    mod.FormLists.First(x => x.FormKey == fl.FormKey).Items.Add(ng.FormKey.ToLink<IStarfieldMajorRecordGetter>());
                Console.WriteLine($"  + {dst}  <- {g.EditorID}  variant {variant}  -> {packin}{(fl != null ? "  in " + fl.EditorID : "")}{chainNote}");
            }

            if (dry)
            {
                Console.WriteLine($"--dry: {pkPlans.Count} PackIn twin(s), {created.Count} module twin(s) shown, nothing written.");
                return 0;
            }
            session.Close();
            session.Write();
            Console.WriteLine($"Finished -- {pkPlans.Count} PackIn twin(s), {created.Count} module twin(s) in ONE write.");
            var noStrv = created.Where(id => gen_conform.GbfmHasStrv(session.PluginPath, id) != true).ToList();
            if (noStrv.Count > 0)
            {
                Console.WriteLine($"FAIL -- {noStrv.Count} created GBFM(s) have NO STRV on disk:");
                foreach (var id in noStrv) Console.WriteLine("  " + id);
                return 2;
            }
            Console.WriteLine("STRV present on every created GBFM, read back off disk.");
            return 0;
        }

        // gen_shipstruct's block rule (ElminsterAU): block = last decimal digit of the object id,
        // sub-block = the digit before it. A second copy; the shared helper is owed.
        static void InsertCell(StarfieldMod mod, Cell cell)
        {
            var s = cell.FormKey.ID.ToString();
            int block = int.Parse(s.Substring(s.Length - 1));
            int sub = int.Parse(s.Substring(s.Length - 2, 1));
            var cb = mod.Cells.FirstOrDefault(b => b.BlockNumber == block);
            if (cb == null)
            {
                cb = new CellBlock { BlockNumber = block, GroupType = GroupTypeEnum.InteriorCellBlock, SubBlocks = new ExtendedList<CellSubBlock>() };
                mod.Cells.Add(cb);
            }
            var sb = cb.SubBlocks.FirstOrDefault(x => x.BlockNumber == sub);
            if (sb == null)
            {
                sb = new CellSubBlock { BlockNumber = sub, GroupType = GroupTypeEnum.InteriorCellSubBlock, Cells = new ExtendedList<Cell>() };
                cb.SubBlocks.Add(sb);
            }
            sb.Cells.Add(cell);
        }
    }
}
