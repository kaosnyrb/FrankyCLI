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
        };

        public static int Generate(string[] args)
        {
            // args: [modname, "setflipset", flst_editorid, memberspec]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setflipset <modname> <flst_editorid> <gbfm=Dir>[,<gbfm=Dir>...]");
                Console.WriteLine("Dirs: " + string.Join(" ", PositionKeywords.Keys));
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
                if (halves.Length != 2 || !PositionKeywords.ContainsKey(halves[1].Trim()))
                {
                    Console.WriteLine($"Error: member '{chunk}' is not <gbfm_editorid>=<Dir> (Dirs: {string.Join(" ", PositionKeywords.Keys)})");
                    return 1;
                }
                wanted.Add((halves[0].Trim(), halves[1].Trim()));
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
                    var wantKey = new FormKey(starfieldKey, PositionKeywords[dir]);
                    var gbfm = members[i].DeepCopy();

                    var kwComp = gbfm.Components.OfType<KeywordFormComponent>().FirstOrDefault();
                    if (kwComp == null)
                    {
                        kwComp = new KeywordFormComponent() { Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() };
                        gbfm.Components.Add(kwComp);
                    }

                    var stale = kwComp.Keywords.Where(k => allPositionKeys.Contains(k.FormKey) && k.FormKey != wantKey).ToList();
                    bool has = kwComp.Keywords.Any(k => k.FormKey == wantKey);
                    if (stale.Count == 0 && has)
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
                    if (!has)
                    {
                        kwComp.Keywords.Add(wantKey.ToLink<IKeywordGetter>());
                        Console.WriteLine($"  {editorId}: + ShipModPosition{(dir.Equals("Starboard", StringComparison.OrdinalIgnoreCase) ? "Stbd" : dir)}");
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
