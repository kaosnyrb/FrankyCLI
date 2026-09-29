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
    // Make one GenericBaseForm the same MODULE as another, keeping only what makes it a
    // different PART.
    //
    //   copymodule <modname> <src_gbfm> <dst_gbfm> [--dry]
    //
    // WHY THIS EXISTS (2026-09-29). rotate_part.py builds a turned part with `struct`, which
    // writes a STRUCTURAL sheet: mass + variant. It had carried mass and nothing else across,
    // so a turned cargo hold built, rendered, snapped and held nothing (cargolg_06, caught by
    // reading the sheet, restored by hand with setcargo). The same turn of a shield, engine or
    // reactor would drop its stats AND its class/upgrade keywords, silently. Rather than teach
    // the Python a list of fields -- a list is a promise to remember every future field -- this
    // copies EVERYTHING and names the few things the turn owns:
    //
    //   kept from DST   links into THIS plugin (the PackIns: they are the part), matched by the
    //                   link's keyword; the ShipModPosition keyword(s); ShipModuleVariant's value
    //   copied from SRC every other component, property, keyword and link, deep-copied
    //
    // REFUSES, writing nothing: a src link into this plugin whose keyword dst has no link for
    // (e.g. a bay's interior PackIn onto a part that has none), and src == dst.
    // A component TYPE dst carries and src does not is KEPT and printed, never dropped silently.
    // Every property/keyword that changes on dst is printed, so a run is its own diff.
    class gen_copymodule
    {
        // ShipModPosition*, all ten, Starfield.esm 0x27BABC..0x27BAC5 (read back by
        // `gen_inspect Keyword ShipModPosition`; same set gen_setflipset spells).
        static readonly HashSet<uint> PositionIds = new HashSet<uint>(
            Enumerable.Range(0x27BABC, 10).Select(i => (uint)i));
        const uint AV_SHIP_MODULE_VARIANT = 0x27BACE;

        public static int Generate(string[] args)
        {
            // args: [modname, "copymodule", src, dst, (--dry)]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: copymodule <modname> <src_gbfm> <dst_gbfm> [--dry]");
                return 1;
            }
            string modname = args[0], srcId = args[2].Trim(), dstId = args[3].Trim();
            bool dry = args.Skip(4).Any(a => a == "--dry");
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }
            if (string.Equals(srcId, dstId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Error: src and dst are the same record");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);
            }
            var sfKey = new ModKey("Starfield", ModType.Master);

            IGenericBaseFormGetter? Find(string id) => myMod.GenericBaseForms.FirstOrDefault(
                g => string.Equals(g.EditorID, id, StringComparison.OrdinalIgnoreCase));
            var src = Find(srcId);
            var dstOld = Find(dstId);
            if (src == null || dstOld == null)
            {
                Console.WriteLine($"Error: no GenericBaseForm '{(src == null ? srcId : dstId)}' in {modname}");
                return 1;
            }

            var dst = dstOld.DeepCopy();
            var dstLinks = dst.Components.OfType<FormLinkDataComponent>().SelectMany(c => c.Links).ToList();
            var dstPos = dst.Components.OfType<KeywordFormComponent>().SelectMany(c => c.Keywords)
                            .Where(k => k.FormKey.ModKey == sfKey && PositionIds.Contains(k.FormKey.ID)).ToList();
            float? dstVariant = dst.Components.OfType<PropertySheetComponent>().SelectMany(s => s.Properties)
                            .Where(p => p.ActorValue.FormKey == new FormKey(sfKey, AV_SHIP_MODULE_VARIANT))
                            .Select(p => (float?)p.Value).FirstOrDefault();

            var before = Describe(dst);
            var built = new List<AComponent>();
            foreach (var comp in src.Components)
            {
                var copy = (AComponent)((IAComponentGetter)comp).DeepCopy();
                if (copy is FormLinkDataComponent fld)
                {
                    for (int i = 0; i < fld.Links.Count; i++)
                    {
                        var l = fld.Links[i];
                        if (l.LinkedForm.IsNull || l.LinkedForm.FormKey.ModKey != myMod.ModKey) continue;
                        var mine = dstLinks.FirstOrDefault(d => d.Keyword.FormKey == l.Keyword.FormKey);
                        if (mine == null)
                        {
                            Console.WriteLine($"Error: {srcId} links {l.LinkedForm.FormKey} under keyword {l.Keyword.FormKey}, " +
                                              $"and {dstId} has no link for that keyword -- nothing written");
                            return 1;
                        }
                        fld.Links[i] = mine.DeepCopy();
                    }
                }
                else if (copy is KeywordFormComponent kw)
                {
                    var stale = kw.Keywords.Where(k => k.FormKey.ModKey == sfKey && PositionIds.Contains(k.FormKey.ID)).ToList();
                    foreach (var s in stale) kw.Keywords.Remove(s);
                    foreach (var p in dstPos) kw.Keywords.Add(p.FormKey.ToLink<IKeywordGetter>());
                }
                else if (copy is PropertySheetComponent sheet && dstVariant.HasValue)
                {
                    var v = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == new FormKey(sfKey, AV_SHIP_MODULE_VARIANT));
                    if (v != null) v.Value = dstVariant.Value;
                }
                built.Add(copy);
            }
            // A src with no keyword component still owes dst its position.
            if (dstPos.Count > 0 && !built.OfType<KeywordFormComponent>().Any())
                built.Add(new KeywordFormComponent { Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>(
                    dstPos.Select(p => p.FormKey.ToLink<IKeywordGetter>())) });
            foreach (var comp in dst.Components)
                if (!built.Any(b => b.GetType() == comp.GetType()))
                {
                    Console.WriteLine($"  kept dst-only component {comp.GetType().Name} (src has none)");
                    built.Add(comp);
                }

            dst.Components.Clear();
            dst.Components.AddRange(built);
            var after = Describe(dst);

            int changes = 0;
            foreach (var line in before.Except(after)) { Console.WriteLine($"  {dstId}: - {line}"); changes++; }
            foreach (var line in after.Except(before)) { Console.WriteLine($"  {dstId}: + {line}"); changes++; }
            if (changes == 0)
            {
                Console.WriteLine($"  {dstId}: already the same module as {srcId} -- nothing to write");
                return 0;
            }
            if (dry)
            {
                Console.WriteLine($"DRY RUN -- {changes} change(s) shown, nothing written.");
                return 0;
            }

            myMod.GenericBaseForms.Remove(dstOld.FormKey);
            myMod.GenericBaseForms.Add(dst);
            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {dstId} is now {srcId}'s module ({changes} change(s)), FormIDs unchanged.");
            return 0;
        }

        // One line per property, keyword and link, so before/after is a set difference.
        static List<string> Describe(IGenericBaseFormGetter g)
        {
            var lines = new List<string>();
            foreach (var comp in g.Components)
            {
                if (comp is IPropertySheetComponentGetter sheet)
                    foreach (var p in sheet.Properties) lines.Add($"property {p.ActorValue.FormKey} = {p.Value}");
                else if (comp is IKeywordFormComponentGetter kw)
                    foreach (var k in kw.Keywords) lines.Add($"keyword {k.FormKey}");
                else if (comp is IFormLinkDataComponentGetter fld)
                    foreach (var l in fld.Links) lines.Add($"link {l.Keyword.FormKey} -> {l.LinkedForm.FormKey}");
                else lines.Add($"component {comp.GetType().Name}");
            }
            return lines;
        }
    }
}
