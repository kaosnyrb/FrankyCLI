using Retrograde.AI.Utils;
using Retrograde.Chains;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Retrograde.Grammar
{
    /// <summary>
    /// Makes all structural decisions for a Lorewalker quest chain before any LLM generation fires.
    /// Selects strategy, builds per-stage specs, and assembles enriched Addons for each MissionTemplate.
    /// </summary>
    public class GrammarEngine
    {
        public Strategy Strategy;
        public MoralFraming Framing;
        public Motivation Motivation;
        public CharacterWeb Web;

        private List<StageSpec> _specs;

        public GrammarEngine(Motivation motivation)
        {
            Motivation = motivation;
            Strategy = GrammarData.SelectStrategy(motivation);
            Framing = GrammarData.SelectFraming(Strategy);
        }

        /// <summary>
        /// Build a StageSpec for each slot in the strategy's stage pattern.
        /// Tension and mood are sampled from the arc curve at each stage's normalized position.
        /// </summary>
        public List<StageSpec> BuildStageSpecs()
        {
            _specs = new List<StageSpec>();
            int count = Strategy.Stages.Count;

            for (int i = 0; i < count; i++)
            {
                float normalizedPos = count <= 1 ? 0.5f : (float)i / (count - 1);
                float tension = ArcCurve.SampleTension(Strategy.Arc, normalizedPos);
                string mood = ArcCurve.SampleMood(tension);

                _specs.Add(new StageSpec
                {
                    Role = Strategy.Stages[i],
                    Tension = tension,
                    Mood = mood,
                    RoleDescription = GrammarData.DescribeStageRole(Strategy.Stages[i], Framing)
                });
            }

            return _specs;
        }

        /// <summary>
        /// Assign characters from the web to each stage. Each stage gets only
        /// the characters relevant to that scene.
        /// </summary>
        public void AssignCharactersToStages()
        {
            if (Web == null || _specs == null) return;

            var supporting = Web.Characters.Where(c => c.Role != CharacterRole.Target).ToList();

            for (int i = 0; i < _specs.Count; i++)
            {
                var spec = _specs[i];

                switch (spec.Role)
                {
                    case StageRole.Discovery:
                        // Discovery gets the Fixer (if present) — the one who sends the player on the hunt
                        var fixer = supporting.FirstOrDefault(c => c.Role == CharacterRole.Fixer);
                        if (fixer != null) spec.Characters.Add(fixer);
                        break;

                    case StageRole.Escalation:
                        // Escalation stages get one supporting character that hasn't been used yet
                        var unused = supporting.FirstOrDefault(c =>
                            !_specs.Take(i).Any(s => s.Characters.Contains(c)));
                        if (unused != null)
                            spec.Characters.Add(unused);
                        else if (supporting.Count > 0)
                            spec.Characters.Add(supporting[RandomProvider.Random.Next(supporting.Count)]);
                        break;

                    case StageRole.Setback:
                        // Setback gets an informant or complicator — someone who misleads
                        var misleader = supporting.FirstOrDefault(c =>
                            c.Role == CharacterRole.Complicator || c.Role == CharacterRole.Informant);
                        if (misleader != null) spec.Characters.Add(misleader);
                        break;

                    case StageRole.Revelation:
                        // Revelation gets the character whose secret drives the twist
                        var revealer = supporting.FirstOrDefault(c =>
                            c.Role == CharacterRole.Witness || c.Role == CharacterRole.Informant);
                        if (revealer != null) spec.Characters.Add(revealer);
                        // Framed: the complicator is also relevant
                        if (Framing == MoralFraming.Framed)
                        {
                            var complicator = supporting.FirstOrDefault(c => c.Role == CharacterRole.Complicator);
                            if (complicator != null && !spec.Characters.Contains(complicator))
                                spec.Characters.Add(complicator);
                        }
                        break;

                    case StageRole.Resolution:
                        // Resolution always includes the target
                        spec.Characters.Add(Web.Target);
                        break;
                }

                // The target is always referenced (absent) in non-resolution stages
                if (spec.Role != StageRole.Resolution && Web.Target != null)
                {
                    // Don't add as present — we'll note them as absent in the entity manifest
                }
            }
        }

        /// <summary>
        /// Assemble enriched Addons for a specific stage, incorporating grammar context,
        /// character data, and entity manifest alongside the standard quest markers.
        /// </summary>
        public List<string> AssembleEnrichedAddons(int stageIndex, MissionTemplate template)
        {
            var spec = _specs[stageIndex];
            var addons = new List<string>();

            // Grammar context tags
            addons.Add($"<Motivation>{Motivation}</Motivation>");
            addons.Add($"<Strategy>{Strategy.Name}</Strategy>");
            addons.Add($"<MoralFraming>{DescribeFramingShort(Framing)}</MoralFraming>");
            addons.Add($"<EmotionalArc>{Strategy.Arc}</EmotionalArc>");
            addons.Add($"<Tension>{spec.Tension.ToString("F2", CultureInfo.InvariantCulture)}</Tension>");
            addons.Add($"<Mood>{spec.Mood}</Mood>");
            addons.Add($"<StageRole>{spec.RoleDescription}</StageRole>");

            // Character blocks — only characters assigned to this stage
            foreach (var c in spec.Characters)
            {
                addons.Add(CharacterWeb.FormatCharacterAddon(c));
            }

            // Entity manifest
            addons.Add(BuildEntityManifest(spec, template));

            // Preserve any existing addons the template already has (e.g. QuestStage, QuestProgress)
            if (template.Addons != null)
            {
                foreach (var existing in template.Addons)
                    addons.Add(existing);
            }

            return addons;
        }

        private string BuildEntityManifest(StageSpec spec, MissionTemplate template)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<EntityManifest>");

            // Characters present in this stage
            var presentNames = spec.Characters
                .Select(c => $"{c.Name} ({c.Role}, present)")
                .ToList();

            // Target is always referenced even when absent
            if (Web.Target != null && !spec.Characters.Contains(Web.Target))
                presentNames.Add($"{Web.Target.Name} (target, absent)");

            sb.AppendLine($"  Characters: {string.Join(", ", presentNames)}");

            // Location from the template
            if (!string.IsNullOrEmpty(template.Location))
                sb.AppendLine($"  Locations: {template.Location}");

            sb.AppendLine("  Do NOT reference any character, location, or faction not in this list.");
            sb.Append("</EntityManifest>");

            return sb.ToString();
        }

        private static string DescribeFramingShort(MoralFraming framing)
        {
            return framing switch
            {
                MoralFraming.ClearGuilt => "Clear guilt — the target committed the crime as described.",
                MoralFraming.GuiltyButJustified => "Guilty but justified — the target had a sympathetic reason.",
                MoralFraming.Framed => "Framed — the target did not commit the crime.",
                MoralFraming.SympatheticFugitive => "Sympathetic fugitive — the target is running from something worse.",
                _ => "Standard."
            };
        }

        /// <summary>
        /// Maps stage roles to QuestStage names compatible with the existing prompt system.
        /// </summary>
        public static string StageRoleToQuestStage(StageRole role, bool isDeep)
        {
            return role switch
            {
                StageRole.Discovery => "Discovery",
                StageRole.Resolution => "Showdown",
                _ => isDeep ? "DeepInvestigation" : "InitialInvestigation"
            };
        }
    }
}
