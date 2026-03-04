using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Quests;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using System;
using System.Collections.Generic;

namespace Retrograde.Chains
{
    public class StaticLayoutQuestChain : IQuestchain
    {
        public StarfieldMod myMod;

        //Debugging Tools
        public string ShowdownTemplate = "";
        public string DeepTempalte = "";
        public string InvestigationTemplate = "";
        public string DiscoveryTemplate = "";

        public StaticLayoutQuestChain(StarfieldMod myModparam) {
            myMod = myModparam;
        }

        private void AddStageLocation(MissionTemplate template, string stage, string location)
        {
            if (template.Addons == null)
            {
                template.Addons = new List<string>();
            }
            template.Addons.Add(
                $"<QuestStageLocation stage=\"{stage}\">{location}</QuestStageLocation>"
            );
        }



        public bool GenerateQuest()
        {


            // Story Setup --------------------------------
            Random random = RandomProvider.Random;
            Console.WriteLine("StaticLayoutQuestChain");
            List<ITemplateManager> templates = new List<ITemplateManager>()
                {
                    new AllTemplateManager(new AI_TemplateEngine()),
                    new FrontierTemplateManager(new AI_TemplateEngine()),
                    //new NoPOITemplateManager(new AI_TemplateEngine()),
                    new CombatTemplateManager(new AI_TemplateEngine()),
                    new AllTemplateManager(new RandomTemplateEngine())
                };


            var templateManager = templates[random.Next(templates.Count)];
            if (AITools.AIMODE == false)
            {
                templateManager = new AllTemplateManager(new RandomTemplateEngine());
            }

            Console.WriteLine(templateManager.GetType());

            var Lorefile = PromptManager.GenerateLoreFile();


            bool fork = false;            
            if (random.Next(100) > 175)
            {
                fork = true;
            }
            MissionTemplate ForkInvestigationMissionTemplate = new MissionTemplate();

            // NPC Target                
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            PromptManager.LoreContext = AITools.RunPrompt(
                "You are completing a partially written Lore Context File for a Starfield-style outlaw.\r\n" +
                "The Lore Context File is the primary source of truth and MUST be treated as canonical.\r\n" +
                "You will use the outlaw NPC's background ONLY to adapt and enrich this existing lore, not replace it.\r\n\r\n" +

                "Here is the Lore Context File you MUST build from and respect:\r\n\r\n" +
                Lorefile + "\r\n\r\n" +

                "Here is the outlaw NPC this Lore must be aligned with:\r\n" +
                "- Name: " + outlawNpc.name + "\r\n\r\n" +

                "Your task: generate a full lore instance by completing every section that contains instructions.\r\n\r\n" +
                "Rules:\r\n" +
                "- Treat the existing Lore Context File as canon. Do NOT contradict it.\r\n" +
                "- Reuse and expand on existing names, locations, factions, motifs, and events already in the Lore Context File whenever possible.\r\n" +
                "- Only introduce new factions, locations, or concepts when a section explicitly calls for it or when absolutely necessary.\r\n" +
                "- Follow the structure and tags exactly as provided.\r\n" +
                "- For each section that contains instructions (such as <Faction>, <TreasureLegend>, <HistoricalContext>, etc.), replace the instructional text with a fully written lore entry.\r\n" +
                "- Do NOT generate separate entries for each faction; produce a single consolidated lore section per tag, even if multiple factions are mentioned or implied.\r\n" +
                "- Do NOT add new tags or remove existing ones.\r\n" +
                "- Each generated lore section must be no more than 3–6 sentences.\r\n" +
                "- Preserve the order and hierarchy of the Lore Context File.\r\n" +
                "- The Lore Context File is based on the outlaw NPC we just generated: " + outlawNpc.name + ".\r\n" +
                "- Use the character's background to interpret and color the existing Lore Context, but do NOT discard or ignore the original lore.\r\n" +
                "- Update the <Summary> and <StorySummary> to fit the outlaw we've generated, by merging the existing lore with the character background.\r\n" +
                "- When updating <Summary> and <StorySummary>, preserve core themes, key events, and factions from the original Lore Context File.\r\n" +
                "- Expand only sections that contain generation instructions.\r\n" +
                "- Do NOT output explanations. Output ONLY the completed lore instance.\r\n\r\n" +
                "Generate the completed lore instance now."
            );

            // Template Choices --------------------------------
            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate(ShowdownTemplate, new List<string>()
            {
                "<QuestStage>Showdown</QuestStage>",
                "<QuestProgress>90%</QuestProgress>"
            });
            outlawNpc.spacesuit = ShowdownMissionTemplate.parameters.ContainsKey("NeedSpacesuit") && (bool)ShowdownMissionTemplate.parameters["NeedSpacesuit"];
            outlawNpc.GenerateNPC();



            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);
            
            //Quest Steps
            var DeepInvestigationMissionTemplate = templateManager.GetInvestigationMissionTemplate(DeepTempalte, new List<string>()
            {
                "<QuestStage>DeepInvestigation</QuestStage>",
                "<QuestProgress>70%</QuestProgress>"
            });
            if (fork)
            {
                var forktemplates = new Templates_Fork();
                ForkInvestigationMissionTemplate = forktemplates.InvestigationTemplates[random.Next(forktemplates.InvestigationTemplates.Count)];
                ForkInvestigationMissionTemplate.Addons = new List<string>()
                {
                    "<QuestStage>ForkInvestigation</QuestStage>",
                    "<QuestProgress>40%</QuestProgress>"
                };
            }
            var InvestigationMissionTemplate = templateManager.GetInvestigationMissionTemplate(InvestigationTemplate, new List<string>()
            {
                "<QuestStage>InitialInvestigation</QuestStage>",
                "<QuestProgress>10%</QuestProgress>"
            });

            var DiscoveryTemplateManager = new AllTemplateManager(new AI_TemplateEngine());
            //var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate("", discoveryAddons); // We currently don't handle the fact that not all template libs have discovery missions.
            var DiscoveryMissionTemplate = DiscoveryTemplateManager.GetDiscoveryMissionTemplate("", new List<string>()
            {
                "<QuestStage>Discovery</QuestStage>",
                "<QuestProgress>0%</QuestProgress>"
            });

            //Now we go forwards to let the templates know where we've been.
            AddStageLocation(ShowdownMissionTemplate, "InitialInvestigation", InvestigationMissionTemplate.Location);
            AddStageLocation(ShowdownMissionTemplate, "DeepInvestigation", DeepInvestigationMissionTemplate.Location);

            AddStageLocation(DeepInvestigationMissionTemplate, "InitialInvestigation", InvestigationMissionTemplate.Location);

            if (fork)
            {
                AddStageLocation(ShowdownMissionTemplate, "ForkInvestigation", ForkInvestigationMissionTemplate.Location);
                AddStageLocation(DeepInvestigationMissionTemplate, "ForkInvestigation", ForkInvestigationMissionTemplate.Location);

                AddStageLocation(ForkInvestigationMissionTemplate, "InitialInvestigation", InvestigationMissionTemplate.Location);
            }


            var stageSummary = new System.Text.StringBuilder();
            stageSummary.AppendLine("<Showdown Summary>" + ShowdownMissionTemplate.Description + " Location: " + ShowdownMissionTemplate.Location);
            stageSummary.AppendLine("<DeepInvestigation Summary>" + DeepInvestigationMissionTemplate.Description + " Location: " + DeepInvestigationMissionTemplate.Location);
            if (fork)
            {
                stageSummary.AppendLine("<ForkInvestigation Summary>" + ForkInvestigationMissionTemplate.Description + " Location: " + ForkInvestigationMissionTemplate.Location);
            }
            stageSummary.AppendLine("<InitialInvestigation Summary>" + InvestigationMissionTemplate.Description + " Location: " + InvestigationMissionTemplate.Location);
            stageSummary.AppendLine("<Discovery Summary>" + DiscoveryMissionTemplate.Description + " Location: " + DiscoveryMissionTemplate.Location);
            stageSummary.AppendLine();
            stageSummary.AppendLine("Summary complete. The stages will be generated next, starting from the final encounter and working backwards.");
            stageSummary.AppendLine("Each stage should feel like a natural step in the player's journey toward that final confrontation.");
            stageSummary.AppendLine("Do not generate any content yet — this is context only.");
            AITools.RunPrompt(stageSummary.ToString());

            AITools.RunPrompt(
                "Generate the Showdown mission now. This is the player's final confrontation with " + outlawNpc.name + ".\r\n" +
                "Draw on the lore and use the location and mission type from the summary above.\r\n" +
                "This is the narrative climax — make it feel earned."
            );
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate,null);

            AITools.RunPrompt(
                "Important: from this point on the player does not know where the final showdown takes place. " +
                "Do not state the showdown location explicitly in any investigation or discovery mission. You may plant indirect clues that point toward it."
            );

            Console.WriteLine("---------------------------------------------------------------------------------");
            AITools.RunPrompt(
                "Generate the Deep Investigation mission now. This is the closest lead before the showdown.\r\n" +
                "The player is closing in on " + outlawNpc.name + " but does not yet know the final location.\r\n" +
                "Use lore details to ground the scene and plant a clear but indirect clue toward the showdown."
            );
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);

            if (fork)
            {
                //ForkInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                AITools.RunPrompt(
                    "Generate the Fork Investigation mission now. This is a side lead — a divergent path the player follows before it converges back toward the main trail.\r\n" +
                    "Use lore details and keep the narrative consistent with what has been established."
                );
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);

                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                AITools.RunPrompt(
                    "Generate the Initial Investigation mission now. The player has only a weak lead at this stage — they know very little about " + outlawNpc.name + ".\r\n" +
                    "Use lore details to ground the scene. This should feel like the beginning of a trail, not the end of one."
                );
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ForkInvestigationMissionTemplate.outlawQuest);
            }
            else
            {
                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                AITools.RunPrompt(
                    "Generate the Initial Investigation mission now. The player has only a weak lead at this stage — they know very little about " + outlawNpc.name + ".\r\n" +
                    "Use lore details to ground the scene. This should feel like the beginning of a trail, not the end of one."
                );
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            AITools.RunPrompt(
                "Generate the Discovery mission now. This is the player's very first encounter with this quest — the moment " + outlawNpc.name + "'s existence becomes known.\r\n" +
                "The player knows nothing yet. Use lore details to establish atmosphere and intrigue.\r\n" +
                "Plant the seeds that will eventually lead toward the final confrontation without revealing anything explicit."
            );

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, InvestigationMissionTemplate.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            outlawNpc.GenerateLegendaryItem();
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();            
            //Generate Voice for the log
            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            SpeechTools.ConvertAndDeploy();
            return true;
        }
    }
}