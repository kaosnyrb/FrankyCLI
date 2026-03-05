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

            var Lorefile = LorePrompts.GenerateLoreFile(outlawNpc.Goal, outlawNpc.Flaw, outlawNpc.Occupation, outlawNpc.Crime);

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib);

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
                "Briefly set the scene for the Showdown — the player's final confrontation with " + outlawNpc.name + ".\r\n" +
                "Use the location and mission type from the summary above. Ground it in the lore.\r\n" +
                "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
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
                AITools.RunPrompt(
                    "Briefly set the scene for the Fork Investigation — a side lead that diverges before converging back on the main trail.\r\n" +
                    "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                );
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);

                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
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
                AITools.RunPrompt(
                    "Briefly set the scene for the Initial Investigation — a weak early lead, the beginning of the trail.\r\n" +
                    "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                );
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
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