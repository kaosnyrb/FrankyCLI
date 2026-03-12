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
                template.Addons = new List<string>();
            template.Addons.Add(
                $"<QuestStageLocation stage=\"{stage}\">{location}</QuestStageLocation>"
            );
        }

        // Generates 1-2 sentences of prose describing what the player finds at fromTemplate
        // that sends them toward toTemplate. Adds to history AND to fromTemplate.Addons.
        private void GenerateStageBridge(MissionTemplate fromTemplate, MissionTemplate toTemplate)
        {
            var bridge = AITools.RunPrompt(
                $"In 1-2 sentences, describe the specific clue or contact the player uncovers at " +
                $"{fromTemplate.Location} (the \"{fromTemplate.Name}\" stage) that points them toward " +
                $"the \"{toTemplate.Name}\" stage at {toTemplate.Location}.\n" +
                "Be concrete — name a data file, an informant, a physical trail, or an overheard conversation. " +
                "Ground it in the established lore. Output only the 1-2 sentences, no headers or labels."
            );

            if (fromTemplate.Addons == null)
                fromTemplate.Addons = new List<string>();
            fromTemplate.Addons.Add($"<StageBridge>{bridge}</StageBridge>");
        }



        public bool GenerateQuest()
        {


            // Story Setup --------------------------------
            Random random = RandomProvider.Random;
            Console.WriteLine("StaticLayoutQuestChain");
            List<ITemplateManager> templates = new List<ITemplateManager>()
                {
                    new AllTemplateManager(new AI_TemplateEngine()),
                    //new FrontierTemplateManager(new AI_TemplateEngine()),
                    //new NoPOITemplateManager(new AI_TemplateEngine()),
                    //new CombatTemplateManager(new AI_TemplateEngine()),
                    //new AllTemplateManager(new RandomTemplateEngine())
                };


            var templateManager = templates[random.Next(templates.Count)];
            if (AITools.AIMODE == false)
            {
                templateManager = new AllTemplateManager(new RandomTemplateEngine());
            }

            Console.WriteLine(templateManager.GetType());

            bool fork = false;
            if (random.Next(100) > 175)
            {
                fork = true;
            }
            MissionTemplate ForkInvestigationMissionTemplate = new MissionTemplate();

            // NPC Target
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            var Lorefile = LorePrompts.GenerateLoreFile(outlawNpc.Traits);

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib);

            //debug mode
            if (ShowdownTemplate != "")
            {
                LorePrompts.PlannedShowdown = ShowdownTemplate;
            }
            if (DiscoveryTemplate != "")
            {
                LorePrompts.PlannedDiscovery = DiscoveryTemplate;

            }
            if (InvestigationTemplate != "")
            {
                LorePrompts.PlannedInvestigations = new List<string>() { InvestigationTemplate, DeepTempalte };
            }

            // Template Choices --------------------------------
            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate(
                string.IsNullOrEmpty(ShowdownTemplate) ? LorePrompts.PlannedShowdown : ShowdownTemplate,
                new List<string>() { "<QuestStage>Showdown</QuestStage>", "<QuestProgress>90%</QuestProgress>" });
            outlawNpc.spacesuit = ShowdownMissionTemplate.parameters.ContainsKey("NeedSpacesuit") && (bool)ShowdownMissionTemplate.parameters["NeedSpacesuit"];
            outlawNpc.GenerateNPC();



            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);
            
            //Quest Steps
            var DeepInvestigationMissionTemplate = templateManager.GetInvestigationMissionTemplate(
                string.IsNullOrEmpty(DeepTempalte) ? (LorePrompts.PlannedInvestigations.Count > 0 ? LorePrompts.PlannedInvestigations[^1] : "") : DeepTempalte,
                new List<string>() { "<QuestStage>DeepInvestigation</QuestStage>", "<QuestProgress>70%</QuestProgress>" });
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
            var InvestigationMissionTemplate = templateManager.GetInvestigationMissionTemplate(
                string.IsNullOrEmpty(InvestigationTemplate) ? "" : InvestigationTemplate,
                new List<string>() { "<QuestStage>InitialInvestigation</QuestStage>", "<QuestProgress>10%</QuestProgress>" });

            var DiscoveryTemplateManager = new AllTemplateManager(new AI_TemplateEngine());
            //var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate("", discoveryAddons); // We currently don't handle the fact that not all template libs have discovery missions.
            var DiscoveryMissionTemplate = DiscoveryTemplateManager.GetDiscoveryMissionTemplate(
                string.IsNullOrEmpty(DiscoveryTemplate) ? LorePrompts.PlannedDiscovery : DiscoveryTemplate,
                new List<string>() { "<QuestStage>Discovery</QuestStage>", "<QuestProgress>0%</QuestProgress>" });

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


            AITools.RunPrompt(
                "Briefly set the scene for the Showdown — the player's final confrontation with " + outlawNpc.name + ".\r\n" +
                "Location: " + ShowdownMissionTemplate.Location + ". Mission type: " + ShowdownMissionTemplate.Name + ".\r\n" +
                "Ground it in the lore. Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
            );
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate,null);

            AITools.InjectContextIntoHistory(
                "Important: from this point on the player does not know where the final showdown takes place. " +
                "Do not state the showdown location explicitly in any investigation or discovery mission. You may plant indirect clues that point toward it."
            );

            Console.WriteLine("---------------------------------------------------------------------------------");
            AITools.RunPrompt(
                "Briefly set the scene for the Deep Investigation — the closest lead before the showdown.\r\n" +
                "The player is closing in on " + outlawNpc.name + " but does not yet know the final location.\r\n" +
                "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
            );
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);

            if (fork)
            {
                //ForkInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                GenerateStageBridge(ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate);
                AITools.RunPrompt(
                    "Briefly set the scene for the Fork Investigation — a side lead that diverges before converging back on the main trail.\r\n" +
                    "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                );
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);

                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                GenerateStageBridge(InvestigationMissionTemplate, ForkInvestigationMissionTemplate);
                AITools.RunPrompt(
                    "Briefly set the scene for the Initial Investigation — a weak early lead, the beginning of the trail.\r\n" +
                    "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                );
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ForkInvestigationMissionTemplate.outlawQuest);
            }
            else
            {
                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                GenerateStageBridge(InvestigationMissionTemplate, DeepInvestigationMissionTemplate);
                AITools.RunPrompt(
                    "Briefly set the scene for the Initial Investigation — a weak early lead, the beginning of the trail.\r\n" +
                    "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                );
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            GenerateStageBridge(DiscoveryMissionTemplate, InvestigationMissionTemplate);
            AITools.RunPrompt(
                "Briefly set the scene for the Discovery — the moment " + outlawNpc.name + "'s existence first surfaces.\r\n" +
                "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
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