using Retrograde.Interfaces;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Retrograde.Nouns.Hunt
{
    public class PredatorHuntTarget : IHuntTarget
    {
        // Concrete creature templates in Starfield.esm. These have real
        // model/animation chains. We need ONE to act as DefaultTemplate so
        // the spawned creature actually renders; the planet-correct visual
        // and combat style come from the cloned PCM_'s ObjectTemplates OMOD chain.
        private static readonly string[] ConcreteCreatureTemplates =
        {
            "LC116_EncCataxiRanged00_Template",
            "LC116_EncCataxiMelee00_Template",
            "LC030_EncGryllobaRanged00_Template",
            "LC030_EncGryllobaMelee00_Template",
            "LC030_EncGryllobaQueen00_Template",
            "LC030_EncCotylite00_Template",
            "SEDerelict_EncHexapodAGlider00_Template",
        };

        // System -> recommended level. Sourced from docs/systlistunformmated.txt.
        public static readonly Dictionary<string, int> SystemLevels =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "Al-Battani", 35 },        { "Alchiba", 50 },           { "Algorab", 70 },
                { "Alpha Andraste", 30 },    { "Alpha Centauri", 1 },     { "Alpha Marae", 45 },
                { "Alpha Ternion", 60 },     { "Alpha Tirna", 35 },       { "Altair", 15 },
                { "Andromas", 15 },          { "Aranae", 15 },            { "Archimedes", 75 },
                { "Arcturus", 15 },          { "Bannoc", 50 },            { "Bannoc Secondus", 50 },
                { "Bara", 45 },              { "Bardeen", 70 },           { "Barnard's Star", 1 },
                { "Bel", 55 },               { "Bessel", 10 },            { "Beta Andraste", 20 },
                { "Beta Marae", 45 },        { "Beta Ternion", 40 },      { "Beta Tirna", 35 },
                { "Bohr", 75 },              { "Bolivar", 35 },           { "Bradbury", 20 },
                { "Carinae", 20 },           { "Celebrai", 70 },          { "Charybdis", 65 },
                { "Cheyenne", 1 },           { "Copernicus", 30 },        { "Copernicus Minor", 30 },
                { "Decaran", 60 },           { "Delta Pavonis", 25 },     { "Delta Vulpes", 50 },
                { "Denebola", 30 },          { "Enlil", 65 },             { "Eridani", 20 },
                { "Eta Cassiopeia", 20 },    { "Fermi", 75 },             { "Feynman", 55 },
                { "Foucault", 60 },          { "Freya", 40 },             { "Gamma Vulpes", 50 },
                { "Groombridge", 25 },       { "Guniibuu", 20 },          { "Hawking", 75 },
                { "Heinlein", 45 },          { "Heisenberg", 55 },        { "Huygens", 75 },
                { "Hyla", 40 },              { "Indum", 20 },             { "Ixyll", 40 },
                { "Jaffa", 35 },             { "Kang", 60 },              { "Kapteyn's Star", 10 },
                { "Katydid", 75 },           { "KavnykSHA", 35 },         { "Khayyam", 45 },
                { "Kryx", 20 },              { "Kumasi", 25 },            { "Lantana", 30 },
                { "Leonis", 65 },            { "Leviathan", 55 },         { "Linnaeus", 45 },
                { "Lunara", 25 },            { "Luyten's Star", 5 },      { "Maal", 60 },
                { "Maheo", 1 },              { "Marduk", 70 },            { "Masada", 75 },
                { "McClure", 20 },           { "Moloch", 40 },            { "Muphrid", 15 },
                { "Narion", 1 },             { "Nemeria", 35 },           { "Newton", 55 },
                { "Nikola", 40 },            { "Nirah", 55 },             { "Nirvana", 40 },
                { "Oborum Prime", 20 },      { "Oborum Proxima", 25 },    { "Olympus", 10 },
                { "Ophion", 45 },            { "Piazzi", 10 },            { "Porrima", 30 },
                { "Procyon A", 10 },         { "Procyon B", 5 },          { "Proxima Ternion", 65 },
                { "Pyraas", 70 },            { "Rana", 65 },              { "Rasalhague", 40 },
                { "Rivera", 35 },            { "Rutherford", 45 },        { "Sagan", 15 },
                { "Sakharov", 15 },          { "Schrodinger", 65 },       { "Serpentis", 55 },
                { "Shoza", 35 },             { "Sirius", 5 },             { "Sol", 1 },
                { "Sparta", 60 },            { "Strix", 70 },             { "Syrma", 55 },
                { "Tau Ceti", 10 },          { "The Pup", 10 },           { "Tidacha", 45 },
                { "Toliman", 5 },            { "Ursae Majoris", 30 },     { "Ursae Minoris", 20 },
                { "Valo", 5 },               { "Van Maanen's Star", 10 }, { "Vega", 25 },
                { "Verne", 70 },             { "Volii", 5 },              { "Wolf", 5 },
                { "Xi Ophiuchi", 50 },       { "Zelazny", 60 },           { "Zeta Ophiuchi", 50 },
                { "Zosma", 50 },
            };

        // Looks up the parent system for a planet by walking the vanilla Starfield.esm
        // Planet -> GalaxyData.StarId -> Star.ID chain. Returns null if the planet name
        // does not match any vanilla Planet record or the parent star can't be resolved.
        public static (string system, int level)? GetSystemForPlanet(string planet)
        {
            var sf = RetrogradeContext.Current.StarfieldMod;

            var planetRec = sf.Planets.FirstOrDefault(p =>
                string.Equals(p.Name, planet, StringComparison.OrdinalIgnoreCase));
            if (planetRec?.GalaxyData == null) return null;

            uint starId = planetRec.GalaxyData.StarId;
            var starRec = sf.Stars.FirstOrDefault(s => s.ID == starId);
            if (starRec?.Name == null) return null;

            if (!SystemLevels.TryGetValue(starRec.Name, out int level))
                return (starRec.Name, 1);

            return (starRec.Name, level);
        }

        public static string GetHuntName()
        {
            Random random = RandomProvider.Random;

            var prefixes = new List<string>
            {
                "Old", "Bloody", "Iron", "Black", "Hollow", "Pale",
                "Ash", "Ghost", "Bone", "Marrow", "Scar", "Storm",
                "Blight", "Salt", "Frost", "Ember", "Rust", "Thorn",
                "Cinder", "Sable", "Brine", "Tundra", "Shroud", "Hush"
            };

            var suffixes = new List<string>
            {
                "Fang", "Eater", "Claw", "Stalker", "Shadow", "Hunter",
                "Maw", "Tooth", "Tail", "Wing", "Howl", "Scourge",
                "Reaver", "Crawler", "Striker", "Ripper", "Bane", "Glutton",
                "Devourer", "Hide", "Spine", "Gaze"
            };

            return "The " + prefixes[random.Next(prefixes.Count)] + "-" + suffixes[random.Next(suffixes.Count)];
        }

        public (IFormLink<IStarfieldMajorRecordGetter> targetList, Npc target, string huntName) GetHuntTarget(string planet)
        {
            var ctx = RetrogradeContext.Current;
            var targetMod = ctx.TargetMod;

            // 1. Pick a PCM_*_<planet>_Predator* — this provides the planet-correct
            //    OMOD recipe (visual, combat style, attack type, temperament, etc.)
            var pattern = new Regex(
                $"^PCM_[^_]+_{Regex.Escape(planet)}_Predator\\d+(?:_\\w+)?$",
                RegexOptions.IgnoreCase);

            var pcmCandidates = ctx.StarfieldMod.Npcs
                .Where(n => n.EditorID != null && pattern.IsMatch(n.EditorID))
                .ToList();

            if (pcmCandidates.Count == 0)
                throw new InvalidOperationException(
                    $"PredatorHuntTarget: no PCM_*_{planet}_Predator* NPCs found in Starfield.esm");

            // Filter out aquatic predators — the Skin OMOD in the PCM_'s ObjectTemplates
            // determines the body, and fish/swimmer species shouldn't be hunt targets.
            var aquaticTokens = new[] { "Swimmer", "Sea", "Lionfish", "Sunfish" };
            var aquaticSkinOmods = ctx.StarfieldMod.ObjectModifications
                .Where(o => o.EditorID != null
                    && o.EditorID.StartsWith("mod_CCT_Skin_", StringComparison.OrdinalIgnoreCase)
                    && aquaticTokens.Any(t => o.EditorID.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Select(o => o.FormKey)
                .ToHashSet();

            bool IsAquatic(INpcGetter pcm)
            {
                if (pcm.ObjectTemplates == null || pcm.ObjectTemplates.Count == 0) return false;
                var includes = pcm.ObjectTemplates[0].Includes;
                if (includes == null) return false;
                foreach (var inc in includes)
                    if (aquaticSkinOmods.Contains(inc.Mod.FormKey)) return true;
                return false;
            }

            var landCandidates = pcmCandidates.Where(c => !IsAquatic(c)).ToList();
            if (landCandidates.Count == 0)
                throw new InvalidOperationException(
                    $"PredatorHuntTarget: {planet} has predators but all of them are aquatic");
            pcmCandidates = landCandidates;

            Random random = RandomProvider.Random;
            var pcmGetter = pcmCandidates[random.Next(pcmCandidates.Count)];

            // 2. Pick a concrete _Enc*_Template — provides the spawnable model chain
            var concreteEid = ConcreteCreatureTemplates[random.Next(ConcreteCreatureTemplates.Length)];
            var concreteGetter = ctx.StarfieldMod.Npcs
                .FirstOrDefault(n => n.EditorID == concreteEid)
                ?? throw new InvalidOperationException(
                    $"PredatorHuntTarget: concrete template {concreteEid} not found in Starfield.esm");

            Console.WriteLine(
                $"PredatorHuntTarget: cloning {pcmGetter.EditorID} (OMOD recipe) + retargeting to {concreteEid} (model chain)");

            // 3. Clone the PCM_ NPC — this carries the planet-correct ObjectTemplates
            var sourceNpc = pcmGetter.DeepCopy();
            Npc npc = NPCTools.CloneNPC(targetMod, sourceNpc);

            // 4. Retarget the template chain so the engine has a concrete model to render
            var concreteFk = concreteGetter.FormKey;
            npc.DefaultTemplate = concreteFk.ToNullableLink<INpcGetter>();

            var t = new TemplateActors();
            t.AiDataTemplate           = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.AiPackagesTemplate       = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.AttackDataTemplate       = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.DefPackListTemplate      = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.FactionsTemplate         = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.InventoryTemplate        = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.KeywordsTemplate         = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.ModelOrAnimationTemplate = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.ScriptTemplate           = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.SpellListTemplate        = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            t.StatsTemplate            = concreteFk.ToNullableLink<INpcTemplateTargetGetter>();
            npc.TemplateActors = t;

            // 5. Boss treatment — bump level, force hostile behaviour, blank the CCT name suffix.
            //    PCM_ NPCs default to Unaggressive/Brave/Level 1; we want a real fight.
            //    Level comes from the parent system's recommended level (boss-bumped).
            var sys = GetSystemForPlanet(planet);
            int systemLevel = sys?.level ?? 30;
            int predatorLevel = Math.Max(5, systemLevel + 5);
            Console.WriteLine(
                $"PredatorHuntTarget: planet {planet} -> system {sys?.system ?? "(unknown)"} (lvl {systemLevel}) -> predator lvl {predatorLevel}");
            npc.Level = new NpcLevel { Level = (short)predatorLevel };
            npc.Aggression = Npc.AggressionType.VeryAggressive;
            npc.Confidence = Npc.ConfidenceType.Foolhardy;

            // CCT_Instance_Name_Blank [0x182D74] suppresses the CCT-generated
            // role/species suffix (otherwise the in-game name becomes
            // "Hunting <our name> <species>" via CCT_Instance_Name_Hunting + Skin OMOD).
            var sfKey = ctx.StarfieldModKey;
            npc.Keywords.Add(new FormKey(sfKey, 0x182D74).ToLink<IKeywordGetter>());

            // mod_CCT_Special_Boss [OMOD:0032047B] — tags this NPC as a boss creature
            // for in-game UI / drop tables / health-bar treatment.
            var bossOmodFk = new FormKey(sfKey, 0x32047B);
            if (npc.ObjectTemplates != null && npc.ObjectTemplates.Count > 0)
            {
                var ot = npc.ObjectTemplates[0];
                bool alreadyHas = ot.Includes.Any(inc => inc.Mod.FormKey == bossOmodFk);
                if (!alreadyHas)
                {
                    ot.Includes.Add(new ObjectTemplateInclude
                    {
                        Mod              = bossOmodFk.ToLink<IAObjectModificationGetter>(),
                        AttachPointIndex = 0,
                        DontUseAll       = true,
                        Optional         = true,
                    });
                }
            }

            // 6. Name + EditorID
            string huntName = GetHuntName();
            string nameSlug = huntName.ToLower().Replace(" ", "").Replace("-", "");
            string planetSlug = planet.ToLower().Replace("-", "");

            npc.Name = huntName;
            npc.EditorID = "npc_huntpredator_" + planetSlug + "_" + nameSlug;

            targetMod.Npcs.Add(npc);

            var frmlst = new FormList(targetMod)
            {
                EditorID = "huntpredator_" + planetSlug + "_" + nameSlug,
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            frmlst.Items.Add(npc);
            targetMod.FormLists.Add(frmlst);

            return (targetMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>(), npc, huntName);
        }
    }
}
