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
            public readonly Dictionary<int, (INpcGetter tmpl, FormKey? outfit, FormKey? company, FormKey? voice, FormKey? face)> Persons = new();
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
            /// <summary>By beat index: a hold's creatures (Npc or LeveledNpc), in the recipe's order, for its own FormList.</summary>
            public readonly Dictionary<int, List<FormKey>> Creatures = new();
            /// <summary>Vanilla's elite components, resolved once when any defeat names an elite.</summary>
            public EliteKit? EliteKit;
            /// <summary>By beat index, for defeat and clear: the target and the lists the fight draws from.</summary>
            public readonly Dictionary<int, FightForms> Fights = new();
            /// <summary>By (beat, exchange): a talk exchange's perk.</summary>
            public readonly Dictionary<(int beat, int ex), FormKey> TalkPerks = new();
            /// <summary>By (beat, exchange): a talk exchange's give, the item, its in-game name (the tag's words) and how many.</summary>
            public readonly Dictionary<(int beat, int ex), (FormKey item, string name, int count)> TalkGives = new();
            /// <summary>By beat index: a deliver's bring, the item, its in-game name (the short box's words) and how many (OT-31).</summary>
            public readonly Dictionary<int, (FormKey item, string name, int count)> Brings = new();
            /// <summary>By beat index: a talk person's stock (person.sells), resolved items and counts.</summary>
            public readonly Dictionary<int, List<(FormKey item, int count)>> VendorStock = new();
            /// <summary>Vanilla's Vendor_OE_Standard_Container [10E232], copied per vendor; null when nobody sells.</summary>
            public IContainerGetter? VendorChestSrc;
            /// <summary>By beat index, for a craft: the bench keyword and each ingredient with its count.</summary>
            public readonly Dictionary<int, (FormKey bench, List<(FormKey part, int count)> parts)> Crafts = new();
            /// <summary>By beat index, for a use with explosion: the Explosion placed at its object.</summary>
            public readonly Dictionary<int, FormKey> Explosions = new();
            /// <summary>By beat index, for a beat with hazard: the Hazard placed at its marker when he comes near (OT-39).</summary>
            public readonly Dictionary<int, FormKey> Hazards = new();
            /// <summary>By beat index, a hazard's look: the FX objects placed with it (OT-39).</summary>
            public readonly Dictionary<int, List<FormKey>> HazardVisuals = new();
            /// <summary>The faction table's two parallel lists (gen_delve factions), when a beat reads the site's faction or
            /// place.faction swaps it (plan OT-01).</summary>
            public FormKey? EncKeywords, EncGangs;
            /// <summary>place.faction's keyword, swapped onto the site at the accept.</summary>
            public FormKey? FactionKeep;
            /// <summary>The member gate: the joinable faction, its site keyword, and the keyword a member's site gets instead.</summary>
            public FormKey? GateFaction, GateKeyword, GateInstead;
        }

        /// <summary>A fight beat's forms, resolved own-or-master in ResolveInputs.</summary>
        private sealed class FightForms { public FormKey? Target, Gang, Enemies, Loot, Support, Ship; public INpcGetter? EliteSrc; }

        /// <summary>Vanilla's elite components, by EditorID off Starfield.esm (manual Inbox 2026-11-02, for 40).</summary>
        private sealed class EliteKit
        {
            public FormKey VeryHard, Boss, Large, Blank;
            public HashSet<FormKey> Challenges = new();
            /// <summary>Small, Normal, Large, Huge, Gigantic: the ladder an elite steps up one rung of.</summary>
            public List<FormKey> Sizes = new();
        }

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
                FormKey? fa = null;
                if (!string.IsNullOrWhiteSpace(pp.face))
                {
                    var fn = OwnOrMaster<INpcGetter>(myMod, env, pp.face!);
                    if (fn == null) { Console.WriteLine($"REFUSED: face '{pp.face}' is not an NPC in {t.mod} or one of its masters."); fail++; continue; }
                    var fr = FaceOf(pp.face!);
                    if (fr == null) { Console.WriteLine($"REFUSED: face '{pp.face}' is not in data/delves/faces.json, so it has no bake of its own to wear."); fail++; continue; }
                    if (pp.voice != null && pp.voice != fr.voice) { Console.WriteLine($"REFUSED: face '{pp.face}' brings the voice type {fr.voice ?? "none"} with it, and this person's voice is {pp.voice}, so her lines would play under nothing. Set voice to {fr.voice ?? "a face that has one"}."); fail++; continue; }
                    fa = fn.FormKey;
                }
                inputs.Persons[i] = (n, of, co, vo, fa);
            }
            // A hold's waves are empty RefCollection aliases filled at runtime. None of our bases carries
            // one, so each is a copy of vanilla's own: UC08_AB_AnomalyBattle's ActiveHostiles, an empty
            // collection its wave scripts fill as hostiles go live (Optional, AllowDisabled, no fill).
            // From the parts file when the build owns its quest; on the old clone path, off Starfield.esm itself, never a
            // winning override.
            if (fail == 0 && r.beats.Any(b => b.type == "hold" || b.type == "clear" || b.type == "ride" || b.support != null))
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
            // A hold's creatures (plan OT-33), own-or-master as every other form here: each an Npc or a LeveledNpc.
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].creatures is not List<string> cs || r.beats[i].type != "hold") continue;
                var keys = new List<FormKey>();
                foreach (var c in cs)
                {
                    var k = OwnOrMaster<INpcGetter>(myMod, env, c)?.FormKey ?? OwnOrMaster<ILeveledNpcGetter>(myMod, env, c)?.FormKey;
                    if (k == null) { Console.WriteLine($"REFUSED: beat {i + 1} creatures '{c}' is not an Npc or LeveledNpc in {t.mod} or one of its masters."); fail++; continue; }
                    keys.Add(k.Value);
                }
                inputs.Creatures[i] = keys;
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
                // In orbit a defeat's target is a SHIP (OT-37): the space build resolves it (delve_space.cs), not this NPC path.
                if (IsSpace(t)) continue;
                var ff = new FightForms();
                if (fb.elite?.from is string ef)
                {
                    ff.EliteSrc = OwnOrMaster<INpcGetter>(myMod, env, ef);
                    if (ff.EliteSrc == null) { Console.WriteLine($"REFUSED: beat {i + 1} elite.from '{ef}' is not an Npc in {t.mod} or one of its masters."); fail++; continue; }
                    if (inputs.EliteKit == null)
                    {
                        FormKey? M(string e) => OwnOrMaster<IAObjectModificationGetter>(myMod, env, e)?.FormKey;
                        var kit = new EliteKit();
                        var need = new Dictionary<string, FormKey?>
                        {
                            ["mod_CCT_Challenge_VeryHard"] = M("mod_CCT_Challenge_VeryHard"), ["mod_CCT_Special_Boss"] = M("mod_CCT_Special_Boss"),
                            ["CCT_Instance_Name_Blank"] = OwnOrMaster<IKeywordGetter>(myMod, env, "CCT_Instance_Name_Blank")?.FormKey,
                        };
                        foreach (var c in new[] { "EasiestLevel1", "VeryEasy", "Easy", "Normal", "Hard", "VeryHard" }) need["mod_CCT_Challenge_" + c] = M("mod_CCT_Challenge_" + c);
                        foreach (var s in new[] { "Small", "Normal", "Large", "Huge", "Gigantic" }) need["mod_CCT_Size_" + s] = M("mod_CCT_Size_" + s);
                        var missing = need.Where(x => x.Value == null).Select(x => x.Key).ToList();
                        if (missing.Count > 0) { Console.WriteLine($"REFUSED: the elite kit is missing {string.Join(", ", missing)} from {t.mod} and its masters."); fail++; continue; }
                        kit.VeryHard = need["mod_CCT_Challenge_VeryHard"]!.Value; kit.Boss = need["mod_CCT_Special_Boss"]!.Value; kit.Blank = need["CCT_Instance_Name_Blank"]!.Value;
                        kit.Challenges = need.Where(x => x.Key.StartsWith("mod_CCT_Challenge_")).Select(x => x.Value!.Value).ToHashSet();
                        kit.Sizes = new[] { "Small", "Normal", "Large", "Huge", "Gigantic" }.Select(s => need["mod_CCT_Size_" + s]!.Value).ToList();
                        kit.Large = kit.Sizes[2];
                        inputs.EliteKit = kit;
                    }
                }
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
            // A beat's hazard zone (OT-39, a probe), the same way.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                if (r.beats[i].hazard is not string hn) continue;
                var hz = OwnOrMaster<IHazardGetter>(myMod, env, hn);
                if (hz == null) { Console.WriteLine($"REFUSED: beat {i + 1} hazard '{hn}' is not a Hazard in {t.mod} or one of its masters."); fail++; continue; }
                inputs.Hazards[i] = hz.FormKey;
                // Its look: each a MoveableStatic (vanilla's FX gas) or a Static.
                var vis = new List<FormKey>();
                foreach (var vn in r.beats[i].visuals ?? new())
                {
                    FormKey? vk = OwnOrMaster<IMoveableStaticGetter>(myMod, env, vn)?.FormKey ?? OwnOrMaster<IStaticGetter>(myMod, env, vn)?.FormKey;
                    if (vk == null) { Console.WriteLine($"REFUSED: beat {i + 1} visual '{vn}' is not a MoveableStatic or a Static in {t.mod} or one of its masters."); fail++; break; }
                    vis.Add(vk.Value);
                }
                if (vis.Count > 0) inputs.HazardVisuals[i] = vis;
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
            // A deliver's bring (OT-31): the same lookup as a give's, kept for its FormKey and name.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                if (r.beats[i].bring is not Bring br) continue;
                var it = OwnOrMaster<IItemGetter>(myMod, env, br.item!);
                string? nm = (it as Mutagen.Bethesda.Plugins.Aspects.ITranslatedNamedGetter)?.Name?.String;
                if (it == null || string.IsNullOrWhiteSpace(nm)) { Console.WriteLine($"REFUSED: beat {i + 1} bring.item '{br.item}' is not a named item in {t.mod} or one of its masters."); fail++; continue; }
                inputs.Brings[i] = (it.FormKey, nm, br.count);
            }
            // A talk person's stock (person.sells, vendors): each item own-or-master, and vanilla's chest base to copy.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                if (r.beats[i].person?.sells is not List<Give> sl) continue;
                var stock = new List<(FormKey, int)>();
                foreach (var s in sl)
                {
                    var it = OwnOrMaster<IItemGetter>(myMod, env, s.item ?? "");
                    if (it == null) { Console.WriteLine($"REFUSED: beat {i + 1} person.sells item '{s.item}' is not an item in {t.mod} or one of its masters."); fail++; break; }
                    stock.Add((it.FormKey, s.count));
                }
                inputs.VendorStock[i] = stock;
                if (inputs.VendorChestSrc == null
                    && !env.LinkCache.TryResolve<IContainerGetter>(TalkBeat.VendorChestSrc, out inputs.VendorChestSrc))
                { Console.WriteLine($"REFUSED: vanilla's vendor chest {TalkBeat.VendorChestSrc} (Vendor_OE_Standard_Container) does not resolve."); fail++; }
            }
            // A talk exchange's perk (a [Background] / [Skill] / [Trait] choice), own-or-master as every other form here.
            for (int i = 0; i < r.beats.Count && fail == 0; i++)
            {
                var tb = r.beats[i];
                if (tb.type != "talk" || tb.exchanges == null) continue;
                for (int x = 0; x < tb.exchanges.Count; x++)
                {
                    if (tb.exchanges[x].give is Give gv)
                    {
                        // The grade has already refused an unresolved or nameless item; this is the same lookup, kept for its FormKey and name.
                        var it = OwnOrMaster<IItemGetter>(myMod, env, gv.item!);
                        string? nm = (it as Mutagen.Bethesda.Plugins.Aspects.ITranslatedNamedGetter)?.Name?.String;
                        if (it == null || string.IsNullOrWhiteSpace(nm)) { Console.WriteLine($"REFUSED: beat {i + 1} exchanges[{x}].give.item '{gv.item}' is not a named item in {t.mod} or one of its masters."); fail++; continue; }
                        inputs.TalkGives[(i, x)] = (it.FormKey, nm, gv.count);
                    }
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
