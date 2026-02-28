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
                    new RandomTemplateManager()
                };


            var templateManager = templates[random.Next(templates.Count)];
            if (AITools.AIMODE == false)
            {
                templateManager = new RandomTemplateManager();
            }

            Console.WriteLine(templateManager.GetType());

            var Lorefile = PromptManager.GenerateLoreFile();

            //AI Seeding
            string MissionSetupPrompt = "";
            MissionSetupPrompt +=
                "Acknowledge the following instructions, but do not generate any story content yet:\r\n\r\n" +

                "- You will eventually generate the story from the final encounter backwards.\r\n" +
                "- Ensure later that all stages link together logically.\r\n" +
                "- A showdown mission will be generated first, followed by investigation missions.\r\n" +
                "- Each earlier mission should reveal progressively less about the target.\r\n" +
                "- This structure ensures information about the target is uncovered gradually.\r\n" +
                "- Use information from each previously generated step to inform the next.\r\n" +
                "- New <Lore> entries will appear during generation; these must be used to enrich the story.\r\n" +
                "- At least one relevant lore detail (faction, tech, location, rumor, etc.) should ground each scene.\r\n\r\n" +

                "Respond only with: \"Instructions acknowledged.\"";
            
            AITools.RunPrompt(MissionSetupPrompt);

            bool fork = false;            
            if (random.Next(100) > 175)
            {
                fork = true;
            }
            MissionTemplate ForkInvestigationMissionTemplate = new MissionTemplate();

            // NPC Target                
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            PromptManager.LoreContext = AITools.RunPrompt("You are given a Lore Context File that contains prompts inside structured tags.\r\n" +
                "Your task is to generate a full lore instance by completing every section that contains instructions.\r\n\r\nRules:\r\n" +
                "- Follow the structure and tags exactly as provided.\r\n" +
                "- For each section that contains instructions (such as <Faction>, <TreasureLegend>, <HistoricalContext>, etc.), replace the instructional text with a fully written lore entry.\r\n" +
                "- Do NOT generate separate entries for each faction; produce a single consolidated lore section per tag, even if multiple factions are mentioned or implied.\r\n" +
                "- Do NOT add new tags or remove existing ones.\r\n" +
                "- Each generated lore section must be no more than 3–6 sentences." +
                "- Preserve the order and hierarchy of the Lore Context file.\r\n" +
                "- The Lore Context file is based on the Outlaw NPC we just generated: " + outlawNpc.name + ".\r\n" +
                "- Update the <Summary> and <StorySummary> to fit the Outlaw we've generated. Keep it's theme and story, merging it with the character backgound.\r\n" +
                "- Expand only sections that contain generation instructions.\r\n" +
                "- Do NOT output explanations. Output ONLY the completed lore instance.\r\n\r\n" +
                "Here is the Lore Context File:\r\n\r\n" +
                Lorefile +
                "\r\n\r\n Generate the completed lore instance now.");

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
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Feeding the stages into the AI...");
            AITools.RunPrompt("<Summary> The next section contains all the locations and types of missions that will be happening. Use this to tie things together.");
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


            AITools.RunPrompt("<Showdown Summary>" + ShowdownMissionTemplate.Description  +  " Location: " + ShowdownMissionTemplate.Location);
            AITools.RunPrompt("<DeepInvestigation Summary>" + DeepInvestigationMissionTemplate.Description + " Location: " + DeepInvestigationMissionTemplate.Location);
            if (fork)
            {
                AITools.RunPrompt("<ForkInvestigation Summary>" + ForkInvestigationMissionTemplate.Description + " Location: " + ForkInvestigationMissionTemplate.Location);
            }
            AITools.RunPrompt("<InitialInvestigation Summary>" + InvestigationMissionTemplate.Description + " Location: " + InvestigationMissionTemplate.Location);
            AITools.RunPrompt("<Discovery Summary>" + DiscoveryMissionTemplate.Description + " Location: " + DiscoveryMissionTemplate.Location);

            AITools.RunPrompt("</Summary>That was the summary, we are now generating the stages.");

            AITools.RunPrompt("<Showdown>");
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate,null);
            Console.WriteLine("---------------------------------------------------------------------------------");
            AITools.RunPrompt("<DeepInvestigation>");
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);
            AITools.RunPrompt("When generating from this point on the player doesn't know where the <Showdown> will take place. Don't reveal it but you can hint at clues.");

            if (fork)
            {
                //ForkInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                AITools.RunPrompt("<ForkInvestigation>");
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);

                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                AITools.RunPrompt("<InitialInvestigation>");
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ForkInvestigationMissionTemplate.outlawQuest);
            }
            else
            {
                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                AITools.RunPrompt("<InitialInvestigation>");
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            AITools.RunPrompt("<Discovery>");

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, InvestigationMissionTemplate.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();            
            //Generate Voice for the log
            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            
            return true;
        }
    }
}