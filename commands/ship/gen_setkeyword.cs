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
    // Add (or remove) keywords on GenericBaseForms that already exist, FormID-stable.
    //
    //   setkeyword <modname> <gbfm>[,<gbfm>...] <Keyword|0xFORMID>[,...] [--remove]
    //   e.g. setkeyword avontechstardust atsd_gbfm_reactor_01 ShipModuleClassA,ShipDestructionCanModuleVaporizeKeyword
    //
    // WHY THIS ONE *IS* GENERAL, WHERE THE PROPERTY SETTERS ARE NAMED -- and it is his own
    // argument applied the other way round. He ruled named commands for properties because
    // "there's only like 4 of these left in the whole game": a CLOSED set, nearly exhausted,
    // where each command can carry its own measured vanilla reference. KEYWORDS ARE THE
    // OPPOSITE -- class, manufacturer, position, destruction, upgrade chains, and whatever
    // Bethesda adds next. There is no exhausting that, so `setkeywordclassa`,
    // `setkeywordvaporize`, `setkeywordupgrade`... is the shape the third-copy rule actually
    // warns about.
    //
    // WHAT IT REPLACES. Keyword authoring existed in exactly one place -- gen_setflipset --
    // and was hardcoded to the SIX position keywords. So a reactor could not be given its
    // CLASS, which is the thing the ship builder reads to decide what the module even is.
    // Found 2026-08-17 when he looked at the Linesman in game: "reactor is missing some
    // states. Probably keywords." Diffed against SMA_Reactor_AmunDunn_340T_Stellarator:
    // vanilla carries FIVE keywords, ours carried one.
    //
    // ⚠ IT RESOLVES BY EditorID ACROSS THE WHOLE LOAD ORDER, then refuses on a miss. A
    // keyword invented by typo would otherwise be a FormKey pointing at nothing, which the
    // record model accepts happily and the builder ignores silently -- the exact class of
    // failure that makes a part look built and behave wrong.
    //
    // Idempotent: a keyword already present is reported and left. --remove is the inverse and
    // is equally idempotent. Validates EVERY target and EVERY keyword before mutating
    // anything, for the reason conform was fixed for: a lookup that fails halfway leaves a
    // partly-patched plugin behind a success line.
    class gen_setkeyword
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setkeyword", gbfm_editorids, keywords, (--remove)?]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setkeyword <modname> <gbfm_editorid>[,...] <Keyword|0xFORMID>[,...] [--remove]");
                Console.WriteLine("  Reactor class: ShipModuleClassA / ClassB / ClassC.");
                Console.WriteLine("  Vanilla reactors also carry ShipDestructionCanModuleVaporizeKeyword.");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            var wanted = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            bool remove = args.Skip(4).Any(a => string.Equals(a, "--remove", StringComparison.OrdinalIgnoreCase));

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

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

                var cache = env.LinkCache;

                // Resolve every keyword FIRST. A typo must refuse, never become a dangling
                // FormKey the builder ignores in silence.
                var keys = new List<(string name, FormKey key)>();
                foreach (var w in wanted)
                {
                    if (w.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!uint.TryParse(w.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var id))
                        {
                            Console.WriteLine($"Error: '{w}' is not a valid FormID"); return 1;
                        }
                        keys.Add((w, new FormKey(env.LoadOrder[0].ModKey, id)));
                        continue;
                    }
                    var kw = cache.PriorityOrder.WinningOverrides<IKeywordGetter>()
                                  .FirstOrDefault(k => string.Equals(k.EditorID, w, StringComparison.OrdinalIgnoreCase));
                    if (kw == null)
                    {
                        Console.WriteLine($"Error: no Keyword '{w}' anywhere in the load order -- refusing "
                                          + "rather than writing a FormKey that points at nothing");
                        return 1;
                    }
                    keys.Add((kw.EditorID ?? w, kw.FormKey));
                }

                var found = new List<IGenericBaseFormGetter>();
                foreach (var target in targets)
                {
                    var existing = myMod.GenericBaseForms.FirstOrDefault(
                        g => string.Equals(g.EditorID, target, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        Console.WriteLine($"Error: no GenericBaseForm '{target}' in {modname}"); return 1;
                    }
                    found.Add(existing);
                }

                foreach (var existing in found)
                {
                    var gbfm = ((IGenericBaseFormGetter)existing).DeepCopy();
                    var kwc = gbfm.Components.OfType<KeywordFormComponent>().FirstOrDefault();
                    if (kwc == null)
                    {
                        if (remove)
                        {
                            Console.WriteLine($"  {gbfm.EditorID}: no keyword component -- nothing to remove");
                            continue;
                        }
                        kwc = new KeywordFormComponent();
                        gbfm.Components.Add(kwc);
                        Console.WriteLine($"  {gbfm.EditorID}: + keyword component (was absent)");
                    }
                    kwc.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>();

                    bool touched = false;
                    foreach (var (name, key) in keys)
                    {
                        bool has = kwc.Keywords.Any(k => k.FormKey == key);
                        if (remove)
                        {
                            if (!has) { Console.WriteLine($"  {gbfm.EditorID}: {name} not present -- left as is"); continue; }
                            var hit = kwc.Keywords.First(k => k.FormKey == key);
                            kwc.Keywords.Remove(hit);
                            Console.WriteLine($"  {gbfm.EditorID}: - {name}");
                            touched = true;
                        }
                        else
                        {
                            if (has) { Console.WriteLine($"  {gbfm.EditorID}: {name} already present -- left as is"); continue; }
                            kwc.Keywords.Add(key.ToLink<IKeywordGetter>());
                            Console.WriteLine($"  {gbfm.EditorID}: + {name}");
                            touched = true;
                        }
                    }
                    if (!touched) continue;

                    myMod.GenericBaseForms.Remove(existing.FormKey);
                    myMod.GenericBaseForms.Add(gbfm);
                    changed++;
                }
            }

            if (changed == 0) { Console.WriteLine("Nothing to write."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} GenericBaseForm(s) patched, FormIDs unchanged.");
            return 0;
        }
    }
}
