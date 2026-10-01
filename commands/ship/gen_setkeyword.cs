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
    // Add (or remove) keywords on GenericBaseForms -- or PackIns -- that already exist, FormID-stable.
    //
    //   setkeyword <modname> <gbfm|pkin>[,...] <Keyword|0xFORMID>[,...] [--remove]
    //   e.g. setkeyword avontechstardust atsd_gbfm_reactor_01 ShipModuleClassA,ShipDestructionCanModuleVaporizeKeyword
    //        setkeyword avontechstardust atsd_pkn_docker_port SBShip_DockingHatch
    //
    // PACKINS, added 2026-09-16. A docker's PackIn must carry SBShip_DockingHatch (and a
    // top-mounted one SBShip_ModuleTop) or the docker builds, snaps, renders and does not
    // work -- his find in the Creation Kit on the Wharfinger, after two record dumps of
    // mine had printed the keyword list as its type name. Nothing in gen_shipstruct writes a
    // PackIn keyword, so until today the only route was the editor, once per part. A target
    // EditorID resolves as a GenericBaseForm first and a PackIn second; the two groups do not
    // share EditorIDs, and a name in neither refuses. A GBFM keeps its keywords in a
    // KeywordFormComponent; a PackIn carries them as a plain list on the record. Same
    // idempotency, same validate-everything-then-write.
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
                Console.WriteLine("Usage: setkeyword <modname> <gbfm_or_pkin_editorid>[,...] <Keyword|0xFORMID>[,...] [--remove]");
                Console.WriteLine("  Reactor class: ShipModuleClassA / ClassB / ClassC.");
                Console.WriteLine("  Vanilla reactors also carry ShipDestructionCanModuleVaporizeKeyword.");
                Console.WriteLine("  A docker's PackIn wants SBShip_DockingHatch (+ SBShip_ModuleTop when top-mounted).");
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

            int changed = 0;
            using var session = PluginSession.Open(modname);
            if (session == null) return 1;
            var myMod = session.Mod;
            var cache = session.Cache;

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
                    keys.Add((w, new FormKey(session.SfKey, id)));
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

            // Validate every target before touching one: a GBFM first, a PackIn second,
            // neither -> refuse with nothing written.
            var foundGbfm = new List<IGenericBaseFormGetter>();
            var foundPkin = new List<IPackInGetter>();
            foreach (var target in targets)
            {
                var g = myMod.GenericBaseForms.FirstOrDefault(
                    x => string.Equals(x.EditorID, target, StringComparison.OrdinalIgnoreCase));
                if (g != null) { foundGbfm.Add(g); continue; }
                var pk = myMod.PackIns.FirstOrDefault(
                    x => string.Equals(x.EditorID, target, StringComparison.OrdinalIgnoreCase));
                if (pk != null) { foundPkin.Add(pk); continue; }
                Console.WriteLine($"Error: no GenericBaseForm or PackIn '{target}' in {modname}"); return 1;
            }

            foreach (var existing in foundGbfm)
                if (ApplyGbfm(myMod, existing, keys, remove)) changed++;
            foreach (var existing in foundPkin)
                if (ApplyPackIn(myMod, existing, keys, remove)) changed++;
            session.Close();

            if (changed == 0) { Console.WriteLine("Nothing to write."); return 0; }
            session.Write();
            Console.WriteLine($"Finished -- {changed} record(s) patched (GenericBaseForm/PackIn), FormIDs unchanged.");
            return 0;
        }

        /// <summary>
        /// The CORE for a GBFM: add (or remove) resolved keywords on an already-loaded plugin.
        /// True when it changed. Shared by this command and `batch`.
        /// </summary>
        public static bool ApplyGbfm(StarfieldMod myMod, IGenericBaseFormGetter existing,
                                     List<(string name, FormKey key)> keys, bool remove)
        {
            var gbfm = existing.DeepCopy();
            var kwc = gbfm.Components.OfType<KeywordFormComponent>().FirstOrDefault();
            if (kwc == null)
            {
                if (remove)
                {
                    Console.WriteLine($"  {gbfm.EditorID}: no keyword component -- nothing to remove");
                    return false;
                }
                kwc = new KeywordFormComponent();
                gbfm.Components.Add(kwc);
                Console.WriteLine($"  {gbfm.EditorID}: + keyword component (was absent)");
            }
            kwc.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>();
            if (!Edit(gbfm.EditorID ?? "?", kwc.Keywords, keys, remove)) return false;
            myMod.GenericBaseForms.Remove(existing.FormKey);
            myMod.GenericBaseForms.Add(gbfm);
            return true;
        }

        /// <summary>The CORE for a PackIn, same contract as ApplyGbfm.</summary>
        public static bool ApplyPackIn(StarfieldMod myMod, IPackInGetter existing,
                                       List<(string name, FormKey key)> keys, bool remove)
        {
            var pkin = existing.DeepCopy();
            if (pkin.Keywords == null)
            {
                if (remove)
                {
                    Console.WriteLine($"  {pkin.EditorID}: no keyword list -- nothing to remove");
                    return false;
                }
                pkin.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>();
                Console.WriteLine($"  {pkin.EditorID}: + keyword list (was absent)");
            }
            if (!Edit(pkin.EditorID ?? "?", pkin.Keywords, keys, remove)) return false;
            myMod.PackIns.Remove(existing.FormKey);
            myMod.PackIns.Add(pkin);
            return true;
        }

        // One add/remove over a keyword list, whichever record owns the list.
        private static bool Edit(string edid, ExtendedList<IFormLinkGetter<IKeywordGetter>> list,
                                 List<(string name, FormKey key)> keys, bool remove)
        {
            bool touched = false;
            foreach (var (name, key) in keys)
            {
                bool has = list.Any(k => k.FormKey == key);
                if (remove)
                {
                    if (!has) { Console.WriteLine($"  {edid}: {name} not present -- left as is"); continue; }
                    var hit = list.First(k => k.FormKey == key);
                    list.Remove(hit);
                    Console.WriteLine($"  {edid}: - {name}");
                    touched = true;
                }
                else
                {
                    if (has) { Console.WriteLine($"  {edid}: {name} already present -- left as is"); continue; }
                    list.Add(key.ToLink<IKeywordGetter>());
                    Console.WriteLine($"  {edid}: + {name}");
                    touched = true;
                }
            }
            return touched;
        }
    }
}
