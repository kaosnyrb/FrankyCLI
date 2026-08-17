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
    // Author the gate conditions on a ConstructibleObject that already exists, FormID-stable.
    //
    //   setcondition <modname> <cobj>[,<cobj>...] getlevel <op> <value>
    //   setcondition <modname> <cobj>[,<cobj>...] haskeyword <Keyword|0xFORMID>
    //   setcondition <modname> <cobj>[,<cobj>...] hasperk <Perk|0xFORMID>
    //   setcondition <modname> <cobj>[,<cobj>...] --clear
    //     <op> = ge | gt | eq | le | lt | ne     (default ge)
    //
    // WHY THIS EXISTS. Nothing in this tool could author a COBJ condition, so every Stardust
    // part is buildable from level 1 with no requirement of any kind -- verified on our own
    // atsd_co_reactor_01, which carries NO conditions at all. gen_shipstruct sets
    // LearnMethod = DefaultOrConditions and then writes nothing for those conditions to be.
    // His ask 2026-08-17: "level/perk gated versions".
    //
    // ⭐ WHAT VANILLA ACTUALLY DOES, measured across 52 reactor recipes rather than assumed --
    // and it is NOT what the ask's wording implies:
    //   * the gate is TWO conditions: a HasKeyword and a GetLevel.
    //   * the keyword is a VENDOR keyword (VendorSM_BasicParts_<Maker>_Reactor, four of them),
    //     NOT a perk. No vanilla reactor recipe carries a perk condition at all.
    //   * ⛔ the `_lvlNN` suffix in the EditorID IS NOT THE GATE. Measured: it equals the
    //     GetLevel threshold in 0 of 49 tagged recipes. `..._lvl10` gates at level 1;
    //     `..._lvl66` gates at 52. The tag is a levelled-list tier, and reading it as the gate
    //     is exactly the mistake this command exists to let us avoid making by hand.
    //
    // hasperk is offered because HE asked for perk gating and the engine supports it -- but it
    // is NOT the vanilla idiom for this record class, and that is said here rather than
    // discovered later from a part that behaves unlike every neighbour.
    //
    // Conditions are ADDED, replacing any existing condition of the same function so the
    // command is idempotent and re-runnable; --clear removes them all. Every target and every
    // referenced form is validated BEFORE anything mutates, for the reason conform was fixed
    // for: a lookup that fails halfway leaves a partly-patched plugin behind a success line.
    class gen_setcondition
    {
        // PlayerRef, Starfield.esm 0x000014 -- read off every vanilla reactor recipe's
        // GetLevel condition, not guessed.
        private static readonly FormKey PLAYER_REF =
            new FormKey(ModKey.FromNameAndExtension("Starfield.esm"), 0x000014);

        private static readonly Dictionary<string, CompareOperator> OPS = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ge"] = CompareOperator.GreaterThanOrEqualTo,
            ["gt"] = CompareOperator.GreaterThan,
            ["eq"] = CompareOperator.EqualTo,
            ["le"] = CompareOperator.LessThanOrEqualTo,
            ["lt"] = CompareOperator.LessThan,
            ["ne"] = CompareOperator.NotEqualTo,
        };

        public static int Generate(string[] args)
        {
            // args: [modname, "setcondition", cobj_editorids, function, ...]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setcondition <modname> <cobj>[,...] getlevel <ge|gt|eq|le|lt|ne> <value>");
                Console.WriteLine("       setcondition <modname> <cobj>[,...] haskeyword <Keyword|0xFORMID>");
                Console.WriteLine("       setcondition <modname> <cobj>[,...] hasperk <Perk|0xFORMID>");
                Console.WriteLine("       setcondition <modname> <cobj>[,...] --clear");
                Console.WriteLine();
                Console.WriteLine("Vanilla reactors use haskeyword(a VENDOR keyword) + getlevel ge N, N in 1..60.");
                Console.WriteLine("The _lvlNN suffix on a vanilla EditorID is NOT the gate -- 0 of 49 match.");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            string fn = args[3].ToLowerInvariant();

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            bool clear = fn == "--clear";
            CompareOperator op = CompareOperator.GreaterThanOrEqualTo;
            float value = 1f;
            string formArg = "";

            if (!clear)
            {
                switch (fn)
                {
                    case "getlevel":
                        if (args.Length < 6) { Console.WriteLine("Error: getlevel needs <op> <value>"); return 1; }
                        if (!OPS.TryGetValue(args[4], out op))
                        { Console.WriteLine($"Error: '{args[4]}' is not an operator ({string.Join("|", OPS.Keys)})"); return 1; }
                        if (!float.TryParse(args[5], out value))
                        { Console.WriteLine($"Error: '{args[5]}' is not a valid level"); return 1; }
                        if (value < 1) { Console.WriteLine($"Error: a level gate below 1 gates nothing (got {value})"); return 1; }
                        break;
                    case "haskeyword":
                    case "hasperk":
                        if (args.Length < 5) { Console.WriteLine($"Error: {fn} needs a form"); return 1; }
                        formArg = args[4];
                        op = CompareOperator.EqualTo; value = 1f;
                        break;
                    default:
                        Console.WriteLine($"Error: unknown condition function '{fn}'. Known: getlevel, haskeyword, hasperk, --clear");
                        return 1;
                }
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

                // Resolve the referenced form FIRST. A typo must refuse, never become a
                // FormKey pointing at nothing -- which the record model accepts and the game
                // silently ignores, i.e. a gate that is not there.
                FormKey formKey = FormKey.Null;
                if (formArg.Length > 0)
                {
                    if (formArg.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!uint.TryParse(formArg.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var id))
                        { Console.WriteLine($"Error: '{formArg}' is not a valid FormID"); return 1; }
                        formKey = new FormKey(env.LoadOrder[0].ModKey, id);
                    }
                    else if (fn == "haskeyword")
                    {
                        var kw = cache.PriorityOrder.WinningOverrides<IKeywordGetter>()
                                      .FirstOrDefault(k => string.Equals(k.EditorID, formArg, StringComparison.OrdinalIgnoreCase));
                        if (kw == null) { Console.WriteLine($"Error: no Keyword '{formArg}' in the load order -- refusing"); return 1; }
                        formKey = kw.FormKey;
                    }
                    else
                    {
                        var pk = cache.PriorityOrder.WinningOverrides<IPerkGetter>()
                                      .FirstOrDefault(k => string.Equals(k.EditorID, formArg, StringComparison.OrdinalIgnoreCase));
                        if (pk == null) { Console.WriteLine($"Error: no Perk '{formArg}' in the load order -- refusing"); return 1; }
                        formKey = pk.FormKey;
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
                    // Conditions is init-only on the record, so it cannot be replaced here --
                    // only mutated in place. Mutagen constructs the list, so a null would mean
                    // a record shape this command does not understand: refuse rather than
                    // silently skip, because a skipped gate looks exactly like a set one.
                    if (cobj.Conditions == null)
                    {
                        Console.WriteLine($"Error: {cobj.EditorID} has no Conditions list -- refusing to invent one");
                        return 1;
                    }

                    if (clear)
                    {
                        if (cobj.Conditions.Count == 0)
                        { Console.WriteLine($"  {cobj.EditorID}: no conditions -- left as is"); continue; }
                        Console.WriteLine($"  {cobj.EditorID}: - {cobj.Conditions.Count} condition(s)");
                        cobj.Conditions.Clear();
                    }
                    else
                    {
                        ConditionData data = fn switch
                        {
                            "getlevel"   => new GetLevelConditionData(),
                            "haskeyword" => Kw(formKey),
                            _            => Perk(formKey),
                        };
                        // Replace any existing condition of the SAME function, so a re-run
                        // retunes the gate rather than stacking a second one beside it.
                        var wantType = data.GetType();
                        int removed = 0;
                        for (int i = cobj.Conditions.Count - 1; i >= 0; i--)
                            if (cobj.Conditions[i].Data?.GetType() == wantType) { cobj.Conditions.RemoveAt(i); removed++; }

                        // ⛔ RUN IT ON THE PLAYER, EXPLICITLY. Every vanilla reactor recipe's
                        // GetLevel carries `RunOnType=Reference Reference=000014` (PlayerRef);
                        // a condition left on the default subject reads Reference=Null and is
                        // asking about nobody. Caught 2026-08-17 by reading the gate back
                        // through the parameter rendering added to gen_inspect the same hour --
                        // it would otherwise have shipped as a gate that looked set and tested
                        // the wrong actor.
                        if (fn == "getlevel")
                        {
                            data.RunOnType = Condition.RunOnType.Reference;
                            data.Reference.SetTo(PLAYER_REF);
                            data.Reference.SetTo(PLAYER_REF);
                        }
                        cobj.Conditions.Add(new ConditionFloat()
                        {
                            CompareOperator = op,
                            ComparisonValue = value,
                            Data = data,
                        });
                        string what = fn == "getlevel" ? $"{fn} {op} {value}" : $"{fn} {formArg}";
                        Console.WriteLine($"  {cobj.EditorID}: {(removed > 0 ? "~" : "+")} {what}"
                                          + (removed > 0 ? $"  (replaced {removed})" : ""));
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

        private static ConditionData Kw(FormKey k)
        {
            var d = new HasKeywordConditionData();
            d.FirstParameter.Link.SetTo(k);
            return d;
        }

        private static ConditionData Perk(FormKey k)
        {
            var d = new HasPerkConditionData();
            d.FirstParameter.Link.SetTo(k);
            return d;
        }
    }
}
