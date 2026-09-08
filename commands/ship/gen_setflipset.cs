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
    // Group an existing family of oriented GenericBaseForms into a flip SET, in place,
    // FormID-stable: author (or update) a FormList over the members and stamp each member
    // with its ShipModPosition keyword -- the two facts the ship builder's flip key reads.
    //
    //   setflipset <modname> <flst_editorid> <gbfm=Dir>[,<gbfm=Dir>...]
    //   e.g. setflipset avontechstardust atsd_flst_fin01
    //          atsd_gbfm_fin01=Top,atsd_gbfm_fin01_port=Port,atsd_gbfm_fin01_sbd=Stbd,atsd_gbfm_fin01_bot=Bottom
    //
    // Dirs: Fore Aft Port Stbd|Starboard Top Bottom.
    //
    // The case it was written for: the fin family moved off gen_shipflips (placement-rotation)
    // onto per-orientation baked meshes, each authored as its own gen_shipstruct chain -- so the
    // FormList and position keywords that gen_shipflips would have written never existed. This
    // supplies exactly those two facts and nothing else; finish with
    //   setcreated <modname> <base_cobj> <flst_editorid>
    // and remove the per-orientation COBJs (removerecord) so the set has exactly ONE recipe.
    //
    // Idempotent: a member already carrying the right position keyword is left as is; a member
    // carrying a DIFFERENT position keyword has it replaced (said out loud). An existing FormList
    // with this EditorID has its Items replaced in the given order.
    class gen_setflipset
    {
        static readonly Dictionary<string, uint> PositionKeywords = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "Fore",      0x0027BABD },
            { "Aft",       0x0027BABC },
            { "Bottom",    0x0027BABE },
            { "Top",       0x0027BABF },
            { "Stbd",      0x0027BAC2 },
            { "Starboard", 0x0027BAC2 },
            { "Port",      0x0027BAC5 },
            // The four CORNERS. The vocabulary is closed at TEN and corners exist only for
            // Port/Stbd x Top/Bottom (there is no TopAft). Added 2026-09-08: without them this
            // command could not spell -- and so could not WRITE -- a set it had itself helped
            // build. The Fettler Pylon put its four rev members on corners on 09-08, which made
            // that set unextendable: every new member forced either a demotion of the four or a
            // hand restoration afterwards. Verified against Starfield.esm, all ten read back by
            // `gen_inspect Keyword ShipModPosition`.
            { "PortTop",    0x0027BAC4 },
            { "PortBottom", 0x0027BAC3 },
            { "StbdTop",    0x0027BAC1 },
            { "StbdBottom", 0x0027BAC0 },
        };

        /// <summary>
        /// The Dir word for a member that carries NO position keyword -- an UNHANDED shape.
        /// Vanilla ships five unhanded landers, and Stardust's Drayman is a symmetric full-width
        /// cargo hold (bounds -4.0011/+4.0008 in X against the handed Porters' -4.0/+3.69), so it
        /// mounts centrally and has no side to be on.
        ///
        /// LEGAL ONLY FOR A SET IN WHICH NO MEMBER IS HANDED -- enforced below, see the sibling
        /// rule. Position and variant are enumerated as the axes IN PLAY across the set: with no
        /// member claiming a side there is no position axis at all and variant alone enumerates it
        /// (proven at the glass 2026-09-07 on atsd_flst_inlinestruct_01 -- six unhanded members,
        /// all six placeable). Mix the two and the keyless member holds no coordinate on an axis
        /// the builder IS using, and its slot aliases to its neighbour.
        ///
        /// The honest cost of that rule, recorded rather than hidden: this word was added so a
        /// symmetric part need not be stamped with "a side it does not have -- a false fact in the
        /// record to satisfy a tool's arity". Inside a handed set that false fact is now the only
        /// thing that works, and the Drayman carries a Port keyword it does not deserve. The engine
        /// gives a set one position axis or none; it has no way to say "this member is exempt".
        /// A false keyword that renders correctly beats a true absence that renders as its
        /// neighbour -- but it IS a false keyword, and that is the trade, not a tidy win.
        /// </summary>
        const string NoPosition = "None";

        public static int Generate(string[] args)
        {
            // args: [modname, "setflipset", flst_editorid, memberspec]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setflipset <modname> <flst_editorid> <gbfm=Dir>[,<gbfm=Dir>...]");
                Console.WriteLine("Dirs: " + string.Join(" ", PositionKeywords.Keys) + " " + NoPosition);
                Console.WriteLine($"  {NoPosition} = carries no ShipModPosition keyword (an unhanded shape).");
                Console.WriteLine($"  ALL members must be {NoPosition}, or none: a keyless member of a set whose");
                Console.WriteLine("  siblings are handed renders as its NEIGHBOUR, not as itself.");
                return 1;
            }
            string modname = args[0];
            string flstName = args[2];
            string memberSpec = args[3];

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var wanted = new List<(string editorId, string dir)>();
            foreach (var chunk in memberSpec.Split(','))
            {
                var halves = chunk.Split('=');
                bool known = halves.Length == 2
                             && (PositionKeywords.ContainsKey(halves[1].Trim())
                                 || string.Equals(halves[1].Trim(), NoPosition, StringComparison.OrdinalIgnoreCase));
                if (!known)
                {
                    Console.WriteLine($"Error: member '{chunk}' is not <gbfm_editorid>=<Dir> (Dirs: {string.Join(" ", PositionKeywords.Keys)} {NoPosition})");
                    return 1;
                }
                wanted.Add((halves[0].Trim(), halves[1].Trim()));
            }

            // ---- THE SIBLING RULE (2026-09-07) -------------------------------------------------
            // A member may carry NO position keyword only when NO member of the set carries one.
            //
            // Why: the ship builder enumerates a set on the axes that are IN PLAY across it. If no
            // member claims a side there is no position axis, variant alone enumerates, and an
            // all-unhanded set is correct -- proven at the glass on atsd_flst_inlinestruct_01, six
            // unhanded members, all six placed. If SOME member claims a side then the axis exists,
            // and the keyless member has no coordinate on it: its slot ALIASES TO ITS NEIGHBOUR.
            // It does not error and does not blank -- it draws the previous member again, so the
            // fault reports as "there are two of these" and points at the wrong record. That is
            // what the Porter set did (the Drayman keyless at variant 3; slot 3 drew as slot 2)
            // and it cost two wrong diagnoses because the records were exactly as authored.
            //
            // Checked over `wanted` and not over the plugin, deliberately: this command REPLACES
            // the FormList's items with exactly these members, so `wanted` IS the set after the
            // write. There is no current state to read and therefore none to read stale.
            //
            // Sited here, before the environment is opened, for two reasons: validate everything
            // then mutate, and so the refusal can be bitten without touching a plugin at all.
            var unhandedMembers = wanted.Where(w => string.Equals(w.dir, NoPosition, StringComparison.OrdinalIgnoreCase)).ToList();
            var handedMembers = wanted.Where(w => !string.Equals(w.dir, NoPosition, StringComparison.OrdinalIgnoreCase)).ToList();
            if (unhandedMembers.Count > 0 && handedMembers.Count > 0)
            {
                Console.WriteLine("Error: MIXED SET -- nothing written.");
                Console.WriteLine($"  asked for {NoPosition}: {string.Join(", ", unhandedMembers.Select(u => u.editorId))}");
                Console.WriteLine($"  but {handedMembers.Count} sibling(s) carry a side: {string.Join(", ", handedMembers.Select(h => h.editorId + "=" + h.dir))}");
                Console.WriteLine("  A keyless member of a set that HAS a position axis does not render as itself:");
                Console.WriteLine("  its slot aliases to its neighbour, so the builder draws the previous member again.");
                Console.WriteLine($"  Give every member a side, or give none of them one. {NoPosition} is for a wholly");
                Console.WriteLine("  unhanded set, never for one member of a handed one -- even a symmetric part.");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;

            // env holds the plugin open, so it is scoped to close before the write (same reason as
            // gen_setrecipefilter -- a same-path WriteToBinary inside the using throws and leaves
            // the old bytes looking like a persisted no-op).
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

                var starfieldKey = env.LoadOrder[0].ModKey;
                var allPositionKeys = PositionKeywords.Values.Distinct()
                    .Select(id => new FormKey(starfieldKey, id)).ToHashSet();

                // Resolve EVERY member before touching anything -- a typo must never write half a set.
                var members = new List<GenericBaseForm>();
                foreach (var (editorId, dir) in wanted)
                {
                    var existing = myMod.GenericBaseForms.FirstOrDefault(
                        g => string.Equals(g.EditorID, editorId, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        Console.WriteLine($"Error: no GenericBaseForm '{editorId}' in {modname} -- nothing written");
                        return 1;
                    }
                    members.Add(existing);
                }

                for (int i = 0; i < members.Count; i++)
                {
                    var (editorId, dir) = wanted[i];
                    // An unhanded member wants NO position keyword, so there is no key to want.
                    // Left null, the stale filter below then treats EVERY position keyword as
                    // stale -- which is exactly the intent: strip the side, add nothing.
                    bool unhanded = string.Equals(dir, NoPosition, StringComparison.OrdinalIgnoreCase);
                    FormKey? wantKey = unhanded ? null : new FormKey(starfieldKey, PositionKeywords[dir]);
                    var gbfm = members[i].DeepCopy();

                    var kwComp = gbfm.Components.OfType<KeywordFormComponent>().FirstOrDefault();
                    if (kwComp == null)
                    {
                        kwComp = new KeywordFormComponent() { Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() };
                        gbfm.Components.Add(kwComp);
                    }

                    var stale = kwComp.Keywords.Where(k => allPositionKeys.Contains(k.FormKey) && k.FormKey != wantKey).ToList();
                    bool has = wantKey != null && kwComp.Keywords.Any(k => k.FormKey == wantKey.Value);
                    if (stale.Count == 0 && (has || unhanded))
                    {
                        Console.WriteLine($"  {editorId}: already {dir} -- left as is");
                        members[i] = members[i]; // unchanged record stays in the mod as read
                        continue;
                    }
                    foreach (var s in stale)
                    {
                        kwComp.Keywords.Remove(s);
                        Console.WriteLine($"  {editorId}: removed stale position keyword {s.FormKey}");
                    }
                    if (!has && !unhanded)
                    {
                        kwComp.Keywords.Add(wantKey!.Value.ToLink<IKeywordGetter>());
                        Console.WriteLine($"  {editorId}: + ShipModPosition{(dir.Equals("Starboard", StringComparison.OrdinalIgnoreCase) ? "Stbd" : dir)}");
                    }
                    else if (unhanded)
                    {
                        Console.WriteLine($"  {editorId}: unhanded -- no position keyword (carries none by design)");
                    }
                    myMod.GenericBaseForms.Remove(members[i].FormKey);
                    myMod.GenericBaseForms.Add(gbfm);
                    members[i] = gbfm;
                }

                var existingFlst = myMod.FormLists.FirstOrDefault(
                    f => string.Equals(f.EditorID, flstName, StringComparison.OrdinalIgnoreCase));
                FormList flst;
                if (existingFlst != null)
                {
                    flst = existingFlst.DeepCopy();
                    flst.Items.Clear();
                    myMod.FormLists.Remove(existingFlst.FormKey);
                    Console.WriteLine($"  {flstName}: exists -- items replaced");
                }
                else
                {
                    flst = new FormList(myMod) { EditorID = flstName };
                    Console.WriteLine($"Building Record : {flstName}");
                }
                foreach (var m in members)
                    flst.Items.Add(m.ToLink<IStarfieldMajorRecordGetter>());
                myMod.FormLists.Add(flst);
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {flstName} holds {wanted.Count} member(s), FormIDs unchanged.");
            Console.WriteLine($"Next: setcreated {modname} <base_cobj> {flstName}   (one recipe per set)");
            return 0;
        }
    }
}
