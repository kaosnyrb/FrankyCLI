using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    // Author (or refresh) a FormList of every LIGHT base form placed inside a vanilla ship
    // module interior -- the set a boarding script needs in order to turn a ship dark with
    // FindAllReferencesOfType + Disable, without editing a single cell.
    //
    //   shiplightlist <modname> <flst_editorid> [--dry]
    //   e.g. shiplightlist du_overtime duo_flst_shipinteriorlights --dry
    //
    // WHY THE LIST IS DERIVED AND NOT HARDCODED. The set is a fact about the shipped corpus,
    // so scanning re-derives it after a game update instead of rotting in a literal. Same
    // reason the mission board pins at generation time rather than conditioning at runtime:
    // author the rules, stamp the records.
    //
    // WHY NOT A NAME FILTER -- this is the trap the command exists to avoid. The obvious
    // build is "every Light whose EditorID starts LGT_ShipInterior", 173 records, done. That
    // gets 65 of the 100 forms actually placed and silently misses 35 (2,789 placements),
    // including LGT_ShipKit_Omni_NS_Cool_001_75k at 1,368 placements -- the second
    // most-placed light in ship interiors -- plus six carrying no LGT_ prefix at all
    // (ShipLightCoolFill, ShipLightFill, ShipLightCool, ShipLightOrange, ShipRunningLight,
    // PrelightTestSuperBright) and several from the GENERIC interior palette reused in habs.
    // A name filter leaves the brightest lights burning and looks like it worked. The
    // placements are the fact; the naming convention is a promise nobody kept.
    //
    // THE CELL NEEDLE IS "ShipPI", NOT "PackInShipPI". A PackIn's storage cell takes its
    // EditorID from the PackIn with the underscores stripped, so the DLC's own ship habs read
    // PackInSFBGS050ShipPI... -- which "PackInShipPI" does not match. Scanning on the narrower
    // needle missed 93 cells and one whole light form (ShipLightFill, 44 placements). The
    // module classes this covers, measured rather than assumed: Hab, Cockpit, Bay, Docker,
    // Struct, LandDeck, CrossBrace and the two Starborn interiors.
    //
    // VANILLA ONLY, for two independent reasons and the second one is structural.
    // (1) His ruling: a third party's ship can only reach these missions by injecting into the
    //     levelled ship lists the quests call, and nobody does that.
    // (2) MASTER DEPENDENCY, which is the one that would actually bite. du_overtime.esm carries
    //     exactly ONE master, Starfield.esm. Putting a light from any other plugin into a
    //     FormList here adds that plugin as a master -- so pulling in the two Shattered Space
    //     (sfbgs00d.esm) lights would break the mod outright for every player without the DLC.
    //     At ~217k downloads that is not a trade, it is a defect.
    // Measured today: three non-vanilla forms excluded (asc_taiyo x1, sfbgs00d x2, 26 placements
    // between them). Cells are still scanned across the WHOLE load order -- it is the LIGHT that
    // must be vanilla, not the module that happens to place it.
    //
    // COMPARE ON THE FORMKEY, NEVER ON ITS SPELLING. The scaffolding pass that found this set
    // first was a throwaway script keying on the rendered "ID:ModKey" string, and it read 85
    // forms where the truth is 100. Cause: 5,732 of these refs spell their master
    // "starfield.esm" in lowercase, and Mutagen's ModKey compares case-insensitively while a
    // string dictionary does not. The undercount looked entirely plausible and would have left
    // fifteen light forms burning.
    class gen_shiplightlist
    {
        // The needle that selects a ship-module interior storage cell. See the header.
        const string CellNeedle = "ShipPI";

        // A scan that finds nothing must not quietly author an empty FormList -- an empty list
        // makes the boarding script a no-op that presents exactly like a working one. The floor
        // is well under the 85 measured on 2026-08-07 and well over anything a broken scan
        // would return.
        const int MinimumPlausibleForms = 40;

        public static int Generate(string[] args)
        {
            // args: [modname, "shiplightlist", flst_editorid, ("--dry")?]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: shiplightlist <modname> <flst_editorid> [--dry]");
                return 1;
            }
            string modname = args[0];
            string flstName = args[2];
            bool dry = args.Skip(3).Any(a => string.Equals(a, "--dry", StringComparison.OrdinalIgnoreCase));

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            var ordered = new List<(FormKey key, string editorId, int placements)>();

            // env holds the plugin open, so it is scoped to close before the write -- same
            // reason as gen_setflipset: a same-path WriteToBinary inside the using throws and
            // leaves the old bytes looking like a persisted no-op.
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;

                var allMods = new List<IStarfieldModGetter>();
                for (int i = 0; i < env.LoadOrder.Count; i++)
                    if (env.LoadOrder[i].Mod != null)
                        allMods.Add(env.LoadOrder[i].Mod!);

                var starfieldKey = env.LoadOrder[0].ModKey;

                // 1. Index every Light record in the load order, so a placed ref can be
                //    classified by its base. Only vanilla ones are eligible (see header), but
                //    the index carries all of them so the excluded ones can be NAMED rather
                //    than silently dropped.
                var lightNames = new Dictionary<FormKey, string>();
                foreach (var mod in allMods)
                    foreach (var light in mod.Lights)
                        lightNames[light.FormKey] = light.EditorID ?? light.FormKey.ToString();
                Console.WriteLine($"Light records indexed: {lightNames.Count}");

                // 2. Walk every ship-module interior storage cell and count placed light refs
                //    by base form.
                var counts = new Dictionary<FormKey, int>();
                int cellsScanned = 0, refsScanned = 0;

                void ScanCell(ICellGetter cell)
                {
                    if (cell.EditorID == null) return;
                    if (cell.EditorID.IndexOf(CellNeedle, StringComparison.OrdinalIgnoreCase) < 0) return;
                    cellsScanned++;
                    foreach (var entry in cell.Persistent.Concat(cell.Temporary))
                    {
                        if (entry is not IPlacedObjectGetter po) continue;
                        refsScanned++;
                        var baseKey = po.Base.FormKey;
                        if (!lightNames.ContainsKey(baseKey)) continue;
                        counts.TryGetValue(baseKey, out int n);
                        counts[baseKey] = n + 1;
                    }
                }

                foreach (var mod in allMods)
                {
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
                }

                Console.WriteLine($"Ship-module interior cells scanned: {cellsScanned:N0}  (placed refs {refsScanned:N0})");

                // 3. Split vanilla from the rest, and SAY which were dropped -- an exclusion
                //    that prints is a fact; an exclusion that doesn't is a promise to remember.
                var excluded = counts.Where(kv => kv.Key.ModKey != starfieldKey)
                                     .OrderByDescending(kv => kv.Value).ToList();
                foreach (var kv in excluded)
                    Console.WriteLine($"  excluded (non-vanilla): {kv.Value,6:N0}x  {kv.Key}  {lightNames[kv.Key]}");

                // Deterministic order: busiest first, FormID as the tie-break, so two runs over
                // an unchanged corpus produce a byte-identical list.
                ordered = counts.Where(kv => kv.Key.ModKey == starfieldKey)
                                .OrderByDescending(kv => kv.Value)
                                .ThenBy(kv => kv.Key.ID)
                                .Select(kv => (kv.Key, lightNames[kv.Key], kv.Value))
                                .ToList();

                Console.WriteLine();
                Console.WriteLine($"Vanilla ship-interior light forms: {ordered.Count}  ({ordered.Sum(o => o.placements):N0} placements)");
                foreach (var (key, editorId, placements) in ordered)
                    Console.WriteLine($"  {placements,6:N0}x  {key.ID:X6}  {editorId}");
                Console.WriteLine();

                if (ordered.Count < MinimumPlausibleForms)
                {
                    Console.WriteLine($"REFUSED: found {ordered.Count} form(s), below the plausibility floor of {MinimumPlausibleForms}.");
                    Console.WriteLine("Nothing written. The scan is wrong, not the corpus -- check the cell needle before lowering this.");
                    return 1;
                }

                if (dry)
                {
                    Console.WriteLine($"--dry: nothing written. Would author '{flstName}' in {modname}.esm with {ordered.Count} member(s).");
                    return 0;
                }

                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                var existingFlst = myMod.FormLists.FirstOrDefault(
                    f => string.Equals(f.EditorID, flstName, StringComparison.OrdinalIgnoreCase));
                FormList flst;
                if (existingFlst != null)
                {
                    flst = existingFlst.DeepCopy();
                    flst.Items.Clear();
                    myMod.FormLists.Remove(existingFlst.FormKey);
                    Console.WriteLine($"  {flstName}: exists -- items replaced (FormID {existingFlst.FormKey.ID:X6} kept)");
                }
                else
                {
                    flst = new FormList(myMod) { EditorID = flstName };
                    Console.WriteLine($"Building Record : {flstName}");
                }
                foreach (var (key, _, _) in ordered)
                    flst.Items.Add(key.ToLink<IStarfieldMajorRecordGetter>());
                myMod.FormLists.Add(flst);
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {flstName} holds {ordered.Count} light form(s).");
            Console.WriteLine($"Next: hand it to the boarding script as a FormList property and loop");
            Console.WriteLine($"      FindAllReferencesOfType({flstName}, radius) -> Disable() on each.");
            return 0;
        }
    }
}
