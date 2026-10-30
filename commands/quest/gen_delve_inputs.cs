using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// EVERYTHING A DELVE BUILD LOOKS UP IN THE LOAD ORDER FOR BuildBeats, as one record (split step 3's (b),
        /// 2026-10-15): a beat's person, the waves and their package, the speaker, who is at each place, a fight's
        /// forms, a talk exchange's perk. Every one is OWN-OR-MASTER (see OwnOrMaster). BuildBeats reads these and
        /// looks nothing up itself, so its fourteen arguments became eight.
        /// ⚠ place.reach is NOT here: it is LeashPlaces' input, not BuildBeats', and Build resolves it after BuildBeats
        /// runs, so moving it ahead would change what a refused build prints.
        /// </summary>
        private sealed class DelveInputs
        {
            /// <summary>By beat index: the NPC a beat's person is cloned from, and its outfit, company list and voice.</summary>
            public readonly Dictionary<int, (INpcGetter tmpl, FormKey? outfit, FormKey? company, FormKey? voice)> Persons = new();
            /// <summary>The empty RefCollection a hold or a clear copies for its waves; null when no beat holds or clears.</summary>
            public IQuestCollectionAliasGetter? WaveSrc;
            /// <summary>The package a hold's waves run, to come for the player; null when no beat holds or clears.</summary>
            public IPackageGetter? TravelSrc;
            /// <summary>The spoken lines' speaker: the NPC cloned to carry a name, and the voice type; null with no speaker.</summary>
            public (INpcGetter tmpl, FormKey voice)? Speaker;   // ResolveSpeaker's, shared with the space build
            /// <summary>By place index: the creature list of who is there.</summary>
            public readonly Dictionary<int, FormKey> WhoLists = new();
            /// <summary>By beat index: a hold's allies list.</summary>
            public readonly Dictionary<int, FormKey> Allies = new();
            /// <summary>By beat index, for defeat and clear: the target and the lists the fight draws from.</summary>
            public readonly Dictionary<int, FightForms> Fights = new();
            /// <summary>By (beat, exchange): a talk exchange's perk.</summary>
            public readonly Dictionary<(int beat, int ex), FormKey> TalkPerks = new();
            /// <summary>By beat index, for a craft: the bench keyword and each ingredient with its count.</summary>
            public readonly Dictionary<int, (FormKey bench, List<(FormKey part, int count)> parts)> Crafts = new();
            /// <summary>By beat index, for a use with explosion: the Explosion placed at its object.</summary>
            public readonly Dictionary<int, FormKey> Explosions = new();
            /// <summary>The faction table's two parallel lists (gen_delve factions), when a beat reads the site's faction or
            /// place.faction swaps it (plan OT-01).</summary>
            public FormKey? EncKeywords, EncGangs;
            /// <summary>place.faction's keyword, swapped onto the site at the accept.</summary>
            public FormKey? FactionKeep;
            /// <summary>The member gate: the joinable faction, its site keyword, and the keyword a member's site gets instead.</summary>
            public FormKey? GateFaction, GateKeyword, GateInstead;
        }

        /// <summary>A fight beat's forms, resolved own-or-master in ResolveInputs.</summary>
        private sealed class FightForms { public FormKey? Target, Gang, Enemies, Loot, Support, Ship; }

        /// <summary>
        /// Resolves a recipe's DelveInputs. Moved out of Build unchanged: every lookup, refusal and gate is as it was.
        /// The gates read `fail`, which counts Build's earlier failures too, so it comes in and the running total goes
        /// back out. ⚠ They are not uniform, and a pure move keeps them so: persons and fights stop at the first
        /// refusal, talk perks at the end of the beat that holds one, the wave and speaker lookups are skipped once
        /// anything has failed, and the who lists report every refusal whatever came before.
        /// </summary>
        private static int ResolveInputs(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                         Template t, Recipe r, Parts? parts, int fail, out DelveInputs inputs)
        {
            inputs = new DelveInputs();
            // A choose ending's person is resolved here, where the load order is, OWN-OR-MASTER, NEVER "WHATEVER WINS".
            // du_overtime.esp (the CK bridge) carries a copy of every duo_ record under its own ModKey and loads later, so a
            // winning-override lookup by EditorID found duo_GangMembersList_Civ_LIST in the ESP, the write linked to it, and
            // Mutagen ADDED du_overtime.esp TO THE ESM'S MASTERS (2026-10-08, caught before he played).
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                var pp = r.beats[i].person;
                if (pp == null) continue;
                var n = OwnOrMaster<INpcGetter>(myMod, env, pp.template ?? "");
                if (n == null) { Console.WriteLine($"REFUSED: NPC '{pp.template}' is not in {t.mod} or one of its masters."); fail++; continue; }
                FormKey? of = null, co = null;
                if (!string.IsNullOrWhiteSpace(pp.outfit))
                {
                    var o = OwnOrMaster<IOutfitGetter>(myMod, env, pp.outfit!);
                    if (o == null) { Console.WriteLine($"REFUSED: Outfit '{pp.outfit}' is not in {t.mod} or one of its masters."); fail++; continue; }
                    of = o.FormKey;
                }
                if (pp.company != null)
                {
                    var f = OwnOrMaster<IFormListGetter>(myMod, env, pp.company.list ?? "");
                    if (f == null) { Console.WriteLine($"REFUSED: FormList '{pp.company.list}' is not in {t.mod} or one of its masters."); fail++; continue; }
                    co = f.FormKey;
                }
                FormKey? vo = null;
                if (!string.IsNullOrWhiteSpace(pp.voice))
                {
                    var vty = OwnOrMaster<IVoiceTypeGetter>(myMod, env, pp.voice!);
                    if (vty == null) { Console.WriteLine($"REFUSED: VoiceType '{pp.voice}' is not in {t.mod} or one of its masters."); fail++; continue; }
                    vo = vty.FormKey;
                }
                inputs.Persons[i] = (n, of, co, vo);
            }
            // A hold's waves are empty RefCollection aliases filled at runtime. None of our bases carries
            // one, so each is a copy of vanilla's own: UC08_AB_AnomalyBattle's ActiveHostiles, an empty
            // collection its wave scripts fill as hostiles go live (Optional, AllowDisabled, no fill).
            // From the parts file when the build owns its quest; on the old clone path, off Starfield.esm itself, never a
            // winning override.
            if (fail == 0 && r.beats.Any(b => b.type == "hold" || b.type == "clear" || b.support != null))
            {
                var sfm = env.LoadOrder[0].Mod;
                if (parts != null)
                {
                    if (parts.waves?.Kind == "coll") inputs.WaveSrc = (IQuestCollectionAliasGetter)gen_questmodel.WriteAliasModel(parts.waves);
                    else { Console.WriteLine($"REFUSED: {t.parts} carries no wave collection, and a hold or a clear copies one; `gen_delve shell {t.id}` rewrites it."); fail++; }
                }
                else
                {
                    var uc08 = sfm == null || sfm.ModKey.FileName != "Starfield.esm" ? null
                        : sfm.Quests.FirstOrDefault(q => q.FormKey.ID == WaveQuestId);
                    inputs.WaveSrc = WaveCollectionOf(uc08);
                    if (inputs.WaveSrc == null) { Console.WriteLine($"REFUSED: a hold copies its wave collections from UC08_AB_AnomalyBattle's {WaveMemberName} [{WaveQuestId:X6}:Starfield.esm], and it is not there."); fail++; }
                }
                // And the waves come FOR the player (his ask, after the first play: "there's AI packages in
                // the game which control enemies, would be good to have them attack the player"): a copy of
                // the Wanted trait's bounty hunters' Travel package, jog, weapon drawn, to PlayerRef.
                inputs.TravelSrc = sfm?.Packages.FirstOrDefault(p => p.EditorID == "Trait_Wanted_TravelToPlayer");
                if (inputs.TravelSrc == null) { Console.WriteLine("REFUSED: a hold copies its waves' package from Trait_Wanted_TravelToPlayer [0E830B:Starfield.esm], and it is not there."); fail++; }            }
            // The speaker of a Delve's spoken lines: a friendly MAST worker cloned only to carry a NAME and a
            // VOICE TYPE (the voice folder the audio is looked up in). Never placed: the lines are radio.
            if (fail == 0 && r.speaker != null)
            {
                inputs.Speaker = ResolveSpeaker(myMod, env, r, t);
                if (inputs.Speaker == null) fail++;
            }
            // Who is at each place: its creature list, own-or-master, as a person's company list is.
            foreach (var (p, w, _) in WhoOf(r))
            {
                var f = OwnOrMaster<IFormListGetter>(myMod, env, w.list ?? "");
                if (f == null) { Console.WriteLine($"REFUSED: FormList '{w.list}' ({PlaceKey[p]}.who.list) is not in {t.mod} or one of its masters."); fail++; continue; }
                inputs.WhoLists[p] = f.FormKey;
            }
            // A hold's allies list, own-or-master as a person's company list is.
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].allies is not Company al) continue;
                var f = OwnOrMaster<IFormListGetter>(myMod, env, al.list ?? "");
                if (f == null) { Console.WriteLine($"REFUSED: beat {i + 1} allies.list '{al.list}' is not a FormList in {t.mod} or one of its masters."); fail++; continue; }
                inputs.Allies[i] = f.FormKey;
            }
            // A craft's bench and ingredients, own-or-master (ResolveIngredient's rule: never a plugin that is not a master).
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].craft is not Craft c || c.bench == null || !CraftBenches.TryGetValue(c.bench, out var kw)) continue;
                var bench = OwnOrMaster<IKeywordGetter>(myMod, env, kw);
                if (bench == null) { Console.WriteLine($"REFUSED: the bench keyword {kw} is not in {t.mod} or one of its masters."); fail++; continue; }
                var bom = new List<(FormKey, int)>();
                foreach (var (key, count, err) in ReadIngredients(c))
                {
                    var part = err == null ? ResolveIngredient(env, Allowed(env, t), key!, out _) : null;
                    if (part == null) { Console.WriteLine($"REFUSED: beat {i + 1} craft ingredient '{key}' cannot be resolved."); fail++; continue; }
                    bom.Add((part.FormKey, count));
                }
                inputs.Crafts[i] = (bench.FormKey, bom);
            }
            // A fight's forms (defeat / clear), own-or-master as a who list is: the target, and each list it draws from.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                var fb = r.beats[i];
                if (fb.type != "defeat" && fb.type != "clear") continue;
                var ff = new FightForms();
                if (fb.target != null)
                {
                    ff.Target = OwnOrMaster<INpcGetter>(myMod, env, fb.target)?.FormKey ?? OwnOrMaster<ILeveledNpcGetter>(myMod, env, fb.target)?.FormKey;
                    if (ff.Target == null) { Console.WriteLine($"REFUSED: beat {i + 1} target '{fb.target}' is not an Npc or LeveledNpc in {t.mod} or one of its masters."); fail++; continue; }
                }
                foreach (var (what, name, set) in new (string, string?, Action<FormKey>)[] {
                             ("gang", fb.gang?.list, k => ff.Gang = k), ("enemies", fb.enemies?.list, k => ff.Enemies = k), ("loot", fb.loot?.list, k => ff.Loot = k),
                             ("support", fb.support?.list, k => ff.Support = k) })
                {
                    if (name == null) continue;
                    var fl = OwnOrMaster<IFormListGetter>(myMod, env, name);
                    if (fl == null) { Console.WriteLine($"REFUSED: beat {i + 1} {what}.list '{name}' is not a FormList in {t.mod} or one of its masters."); fail++; break; }
                    set(fl.FormKey);
                }
                // A support team's ship: a leveled ship such as LvlShip_UCSecurity_Transport is a GenericBaseForm (read off the
                // record 2026-10-21; his landed bounties bind LvlShip_CrimsonFleet_A_Combat the same way).
                if (fb.support?.ship is string sh)
                {
                    ff.Ship = OwnOrMaster<IGenericBaseFormGetter>(myMod, env, sh)?.FormKey;
                    if (ff.Ship == null) { Console.WriteLine($"REFUSED: beat {i + 1} support.ship '{sh}' is not a GenericBaseForm (a ship) in {t.mod} or one of its masters."); fail++; continue; }
                }
                inputs.Fights[i] = ff;
            }
            // A use beat's explosion, own-or-master as every other form here.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                if (r.beats[i].explosion is not string xn) continue;
                var xp = OwnOrMaster<IExplosionGetter>(myMod, env, xn);
                if (xp == null) { Console.WriteLine($"REFUSED: beat {i + 1} explosion '{xn}' is not an Explosion in {t.mod} or one of its masters."); fail++; continue; }
                inputs.Explosions[i] = xp.FormKey;
            }
            // The site's faction (plan OT-01): a hold or a recover spawns from the site's own faction, read off its one
            // LocEnc keyword through the table gen_delve factions wrote; place.faction swaps that keyword at the accept.
            if (fail == 0 && (r.place.faction != null || r.beats.Any(b => b.type == "hold" || b.type == "recover")))
            {
                var table = LoadFactionTable();
                var ek = table == null ? null : OwnOrMaster<IFormListGetter>(myMod, env, table.keywordList);
                var eg = table == null ? null : OwnOrMaster<IFormListGetter>(myMod, env, table.gangList);
                if (table == null) fail++;
                else if (ek == null || eg == null)
                { Console.WriteLine($"REFUSED: {table.keywordList} or {table.gangList} is not in {t.mod}: run `gen_delve factions` first."); fail++; }
                else if (ek.Items.Count != table.factions.Count || eg.Items.Count != table.factions.Count)
                { Console.WriteLine($"REFUSED: the plugin's faction lists hold {ek.Items.Count}/{eg.Items.Count} entries and factions.json {table.factions.Count}: run `gen_delve factions`."); fail++; }
                else
                {
                    inputs.EncKeywords = ek.FormKey; inputs.EncGangs = eg.FormKey;
                    FormKey? Kw(string row) => OwnOrMaster<IKeywordGetter>(myMod, env,
                        table.factions.First(f => string.Equals(f.name, row, StringComparison.OrdinalIgnoreCase)).keyword)?.FormKey;
                    if (r.place.faction is string pf)
                    {
                        inputs.FactionKeep = Kw(pf);
                        if (inputs.FactionKeep == null) { Console.WriteLine($"REFUSED: place.faction '{pf}' does not resolve to a site keyword."); fail++; }
                    }
                    else if (table.memberGate is { } g)
                    {
                        inputs.GateFaction = OwnOrMaster<IFactionGetter>(myMod, env, g.faction)?.FormKey;
                        inputs.GateKeyword = Kw(g.row); inputs.GateInstead = Kw(g.instead);
                        if (inputs.GateFaction == null || inputs.GateKeyword == null || inputs.GateInstead == null)
                        { Console.WriteLine($"REFUSED: the member gate ({g.faction}, {g.row} -> {g.instead}) does not resolve."); fail++; }
                    }
                }
            }
            // A talk exchange's perk (a [Background] / [Skill] / [Trait] choice), own-or-master as every other form here.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                var tb = r.beats[i];
                if (tb.type != "talk" || tb.exchanges == null) continue;
                for (int x = 0; x < tb.exchanges.Count; x++)
                {
                    if (tb.exchanges[x].perk is not string pn) continue;
                    var pk = OwnOrMaster<IPerkGetter>(myMod, env, pn);
                    if (pk == null) { Console.WriteLine($"REFUSED: beat {i + 1} exchanges[{x}].perk '{pn}' is not a Perk in {t.mod} or one of its masters."); fail++; continue; }
                    inputs.TalkPerks[(i, x)] = pk.FormKey;
                }
            }
            return fail;
        }
    }
}
