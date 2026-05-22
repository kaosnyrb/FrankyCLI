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
            npc.Level = new NpcLevel { Level = 30 };
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
