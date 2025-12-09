using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class LoopingLayoutQuestChain : IQuestchain
    {
        public StarfieldMod myMod;

        public LoopingLayoutQuestChain(StarfieldMod myModparam)
        {
            myMod = myModparam;
        }

        private void AddStageLocation(MissionTemplate template, string stage, string location)
        {
            if (template.Addons == null)
                template.Addons = new List<string>();

            template.Addons.Add(
                $"<QuestStageLocation stage=\"{stage}\">{location}</QuestStageLocation>"
            );
        }

        public bool GenerateQuest()
        {
            // Story Setup --------------------------------
            Random random = RandomUtils.random;
            Console.WriteLine("LoopingLayoutQuestChain");

            List<ITemplateManager> templates = new List<ITemplateManager>()
            {
                //new AllTemplateManager(new AI_TemplateEngine()),
                //new FrontierTemplateManager(new AI_TemplateEngine()),
                new NoPOITemplateManager(new AI_TemplateEngine()),
                //new RandomTemplateManager()
            };
            var templateManager = templates[random.Next(templates.Count)];

            //            var Lorefile = File.ReadAllText(".\\questgen_quests\\Lorefiles\\LostMarine.md");
            var Lorefile = PromptManager.LoadRandomLoreFile();

            // AI Seeding (instructions only)
            string MissionSetupPrompt =
                "Acknowledge the following instructions, but do not generate any story content yet:\r\n\r\n" +

                "- You will eventually generate the story from the final encounter backwards.\r\n" +
                "- Ensure later that all stages link together logically.\r\n" +
                "- A showdown mission will be generated first, followed by investigation missions, then a discovery mission.\r\n" +
                "- Each earlier mission should reveal progressively less about the target (because we generate from the end first).\r\n" +
                "- This structure ensures information about the target is uncovered gradually.\r\n" +
                "- Use information from each previously generated step to inform the next.\r\n" +
                "- New <Lore> entries will appear during generation; these must be used to enrich the story.\r\n" +
                "- At least one relevant lore detail (faction, tech, location, rumor, etc.) should ground each scene.\r\n\r\n" +

                "Respond only with: \"Instructions acknowledged.\"";

            AITools.RunPrompt(MissionSetupPrompt);

            // NPC Target (base setup) --------------------------------
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            // Build LoreContext from Lorefile and NPC
            PromptManager.LoreContext = AITools.RunPrompt(
                "You are given a Lore Context File that contains prompts inside structured tags.\r\n" +
                "Your task is to generate a full lore instance by completing every section that contains instructions.\r\n\r\nRules:\r\n" +
                "- Follow the structure and tags exactly as provided.\r\n" +
                "- For each section that contains instructions (such as <Faction>, <TreasureLegend>, <HistoricalContext>, etc.), replace the instructional text with a fully written lore entry.\r\n" +
                "- Do NOT generate separate entries for each faction; produce a single consolidated lore section per tag, even if multiple factions are mentioned or implied.\r\n" +
                "- Do NOT add new tags or remove existing ones.\r\n" +
                "- Each generated lore section must be no more than 3–6 sentences.\r\n" +
                "- Preserve the order and hierarchy of the Lore Context file.\r\n" +
                "- The Lore Context file is based on the Outlaw NPC we just generated: " + outlawNpc.name + ".\r\n" +
                "- Use the character's background when generating the Lore Context: " + outlawNpc.background + ".\r\n" +
                "- Update the <Summary> and <StorySummary> to fit the Outlaw we've generated. Keep its theme and story, merging it with the character background.\r\n" +
                "- Expand only sections that contain generation instructions.\r\n" +
                "- Do NOT output explanations. Output ONLY the completed lore instance.\r\n\r\n" +
                "Here is the Lore Context File:\r\n\r\n" +
                Lorefile +
                "\r\n\r\nGenerate the completed lore instance now."
            );

            // Template Choices --------------------------------

            // Showdown (final encounter, high completion)
            var showdownAddons = new List<string>()
            {
                "<QuestStage>Showdown</QuestStage>",
                "<QuestProgress>90%</QuestProgress>"
            };

            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate("", showdownAddons);

            outlawNpc.spacesuit = ShowdownMissionTemplate.needSpacesuit;
            outlawNpc.GenerateNPC();

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);

            // Decide how many investigation missions we want between discovery and showdown
            int count = 2 + random.Next(4); // 2–5 investigations

            // We will collect investigation templates first, then run them from the end backwards
            var investigationStages = new List<(string Stage, MissionTemplate Template)>();

            // Generate investigation templates (from near-showdown backwards)
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");

                // Map position in chain to an investigation stage and quest completion
                // i = 0  -> closest to showdown (deep)
                // i > 0  -> progressively earlier (initial-style)
                string stageName = (i == 0) ? "DeepInvestigation" : "InitialInvestigation";

                double t = (count == 1) ? 0.0 : (double)i / (count - 1);
                int progressValue = 70 - (int)((70 - 20) * t); // roughly 70% down to ~20%
                if (progressValue < 10) progressValue = 10;

                var investigationAddons = new List<string>()
                {
                    $"<QuestStage>{stageName}</QuestStage>",
                    $"<QuestProgress>{progressValue}%</QuestProgress>"
                };

                // Base investigation template from the manager
                var template = templateManager.GetInvestigationMissionTemplate("", investigationAddons);

                // Optional fork substitution (kept from original logic)
                // Don't run on the first and last.
                if (i != 0 && i != count - 1)
                {
                    // Run on odd indices; avoids fork at very start or multiple in a row
                    if (i % 2 != 0 && random.Next(100) > 0)
                    {
                        var forktemplates = new Templates_Fork();
                        var forkTemplate = forktemplates.InvestigationTemplates[random.Next(forktemplates.InvestigationTemplates.Count)];

                        forkTemplate.Addons = new List<string>()
                        {
                            "<QuestStage>ForkInvestigation</QuestStage>",
                            $"<QuestProgress>{progressValue}%</QuestProgress>"
                        };

                        template = forkTemplate;
                        stageName = "ForkInvestigation";
                    }
                }

                Console.WriteLine("Investigation Template: " + template.Name);
                investigationStages.Add((stageName, template));
            }

            // Finally build the discovery step (earliest, lowest completion)
            Console.WriteLine("---------------------------------------------------------------------------------");

            var discoveryAddons = new List<string>()
            {
                "<QuestStage>Discovery</QuestStage>",
                "<QuestProgress>0%</QuestProgress>"
            };

            var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate("", discoveryAddons);

            // Now we build a story-ordered list for stage location history:
            // Discovery -> earliest investigation -> ... -> closest investigation -> Showdown
            var storyStages = new List<(string Stage, MissionTemplate Template)>();

            storyStages.Add(("Discovery", DiscoveryMissionTemplate));

            // investigations were added from near-showdown backwards;
            // reverse to put earliest first in story order
            for (int i = investigationStages.Count - 1; i >= 0; i--)
            {
                storyStages.Add(investigationStages[i]);
            }

            storyStages.Add(("Showdown", ShowdownMissionTemplate));

            // Add QuestStageLocation history to every stage based on where the player has already been
            for (int i = 0; i < storyStages.Count; i++)
            {
                var current = storyStages[i];

                // All previous stages in story order are "history" for this stage
                for (int j = 0; j < i; j++)
                {
                    var previous = storyStages[j];
                    AddStageLocation(current.Template, previous.Stage, previous.Template.Location);
                }
            }

            // At this point:
            // - Showdown, all investigations, and Discovery have <QuestStage> / <QuestProgress> in Addons
            // - Each has <QuestStageLocation> history for earlier story steps

            // Now we actually generate the quests from the end backwards

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var showdownQuest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate, null);

            var lastOutlawQuest = ShowdownMissionTemplate.outlawQuest;

            // Generate investigation quests in the order they were originally chosen:
            // closest to showdown first, then progressively earlier
            for (int i = 0; i < investigationStages.Count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var (stageName, template) = investigationStages[i];

                Console.WriteLine(stageName + ": " + template.Name);
                Quest formmission = template.outlawQuest.Setup(myMod, outlawNpc, template, lastOutlawQuest);
                lastOutlawQuest = template.outlawQuest;

                // After the first investigation, remind the AI not to spoil the showdown location
                if (i == 0)
                {
                    AITools.RunPrompt(
                        "Important note: From now on, the player does not know where the final showdown takes place. " +
                        "Do not state the showdown location explicitly, but you may hint at clues that point toward it."
                    );
                }
            }

            // Finally build the discovery step, linked to the earliest investigation
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Discovery: " + DiscoveryMissionTemplate.Name);

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(
                myMod,
                outlawNpc,
                DiscoveryMissionTemplate,
                lastOutlawQuest
            );

            //We have now generated all the stages. Do any final linking steps
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            return true;
        }

    }
}
