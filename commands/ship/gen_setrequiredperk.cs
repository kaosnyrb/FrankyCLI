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
    // Author the RQPK "Required Perks" list on a ConstructibleObject, FormID-stable.
    //
    //   setrequiredperk <modname> <cobj>[,<cobj>...] <Perk|0xFORMID> <rank>
    //   setrequiredperk <modname> <cobj>[,<cobj>...] --clear
    //
    // ⛔ WHY THIS IS NOT A CONDITION, AND WHY THAT COST HALF A MORNING. A ship recipe's SKILL
    // requirement is its own subrecord -- `RQPK - Required Perks` -- sitting on the COBJ
    // alongside the conditions, NOT among them. Reading the Conditions and finding no perk is
    // therefore true and worthless: `co_SMA_Reactor_Xiang_Tokamak_X-200_lvl24` carries exactly
    // two conditions (a vendor keyword and GetLevel >= 19) AND a Required Perk of
    // Skill_StarshipDesign rank 1, and the ship builder shows the second.
    //
    // I concluded from the absence in Conditions that vanilla reactors were not perk-gated.
    // They are. HE found it in xEdit; no tool here rendered RQPK at all, so it was invisible
    // rather than contradicted. gen_inspect prints it now. *An absent field prints as nothing,
    // and nothing is invisible in a dump* -- third instance in one day.
    //
    // THE SHAPE, read off the record rather than guessed:
    //   ConstructibleRequiredPerk { Perk: IFormLink<IPerk>, Rank: UInt32, CurveTable: IFormLink }
    // Rank is a real field, so a multi-rank skill is expressed HERE and not as a comparison
    // value on a condition -- which is what I would have written, wrongly, an hour earlier.
    //
    // ⭐ AND IT IS THE RIGHT MECHANISM FOR A MOD, not merely the vanilla one: it lives on OUR
    // recipe. The alternative routes both bind someone else's records -- overriding
    // Skill_StarshipDesign (a vanilla record every other mod may also touch) or borrowing one
    // of the 12 ShipUpgrade_Reactor_* keywords, which ties a Stardust part to Xiang's or
    // AmunDunn's progression.
    //
    // Idempotent: re-running with the same perk REPLACES that perk's entry (so a rank retune
    // is one command and cannot stack two requirements for one skill); a different perk is
    // added alongside. --clear empties the list.
    class gen_setrequiredperk
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setrequiredperk", cobj_editorids, perk, rank]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setrequiredperk <modname> <cobj>[,...] <Perk|0xFORMID> <rank>");
                Console.WriteLine("       setrequiredperk <modname> <cobj>[,...] --clear");
                Console.WriteLine();
                Console.WriteLine("Vanilla example: co_SMA_Reactor_Xiang_Tokamak_X-200_lvl24 requires");
                Console.WriteLine("Skill_StarshipDesign rank 1, ALONGSIDE its two conditions.");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            string perkArg = args[3];
            bool clear = string.Equals(perkArg, "--clear", StringComparison.OrdinalIgnoreCase);
            uint rank = 1;

            if (!clear)
            {
                if (args.Length < 5) { Console.WriteLine("Error: a rank is required (vanilla uses 1..4)"); return 1; }
                if (!uint.TryParse(args[4], out rank)) { Console.WriteLine($"Error: '{args[4]}' is not a valid rank"); return 1; }
                if (rank < 1) { Console.WriteLine("Error: rank 0 requires nothing -- use --clear if that is the intent"); return 1; }
            }
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
                { Console.WriteLine($"Error: {modname}.esm is not in the load order"); return 1; }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);
                var cache = env.LinkCache;

                // Resolve the perk FIRST and refuse on a miss -- an unresolvable FormKey is a
                // requirement the game cannot test, i.e. a gate that silently is not there.
                FormKey perkKey = FormKey.Null;
                string perkName = perkArg;
                if (!clear)
                {
                    if (perkArg.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!uint.TryParse(perkArg.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var id))
                        { Console.WriteLine($"Error: '{perkArg}' is not a valid FormID"); return 1; }
                        perkKey = new FormKey(env.LoadOrder[0].ModKey, id);
                    }
                    else
                    {
                        var pk = cache.PriorityOrder.WinningOverrides<IPerkGetter>()
                                      .FirstOrDefault(k => string.Equals(k.EditorID, perkArg, StringComparison.OrdinalIgnoreCase));
                        if (pk == null) { Console.WriteLine($"Error: no Perk '{perkArg}' in the load order -- refusing"); return 1; }
                        perkKey = pk.FormKey; perkName = pk.EditorID ?? perkArg;
                    }
                }

                var found = new List<IConstructibleObjectGetter>();
                foreach (var target in targets)
                {
                    var existing = myMod.ConstructibleObjects.FirstOrDefault(
                        c => string.Equals(c.EditorID, target, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    { Console.WriteLine($"Error: no ConstructibleObject '{target}' in {modname}"); return 1; }
                    found.Add(existing);
                }

                foreach (var existing in found)
                {
                    var cobj = ((IConstructibleObjectGetter)existing).DeepCopy();
                    if (cobj.RequiredPerks == null)
                    {
                        Console.WriteLine($"Error: {cobj.EditorID} has no RequiredPerks list -- refusing to invent one");
                        return 1;
                    }

                    if (clear)
                    {
                        if (cobj.RequiredPerks.Count == 0)
                        { Console.WriteLine($"  {cobj.EditorID}: no required perks -- left as is"); continue; }
                        Console.WriteLine($"  {cobj.EditorID}: - {cobj.RequiredPerks.Count} required perk(s)");
                        cobj.RequiredPerks.Clear();
                    }
                    else
                    {
                        var already = cobj.RequiredPerks.FirstOrDefault(r => r.Perk.FormKey == perkKey);
                        if (already != null && already.Rank == rank)
                        { Console.WriteLine($"  {cobj.EditorID}: {perkName} rank {rank} already required -- left as is"); continue; }
                        int removed = 0;
                        for (int i = cobj.RequiredPerks.Count - 1; i >= 0; i--)
                            if (cobj.RequiredPerks[i].Perk.FormKey == perkKey) { cobj.RequiredPerks.RemoveAt(i); removed++; }

                        var entry = new ConstructibleRequiredPerk() { Rank = rank };
                        entry.Perk.SetTo(perkKey);
                        cobj.RequiredPerks.Add(entry);
                        Console.WriteLine($"  {cobj.EditorID}: {(removed > 0 ? "~" : "+")} {perkName} rank {rank}"
                                          + (removed > 0 ? "  (replaced)" : ""));
                    }

                    myMod.ConstructibleObjects.Remove(existing.FormKey);
                    myMod.ConstructibleObjects.Add(cobj);
                    changed++;
                }
            }

            if (changed == 0) { Console.WriteLine("Nothing to write."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} ConstructibleObject(s) patched, FormIDs unchanged.");
            return 0;
        }
    }
}
