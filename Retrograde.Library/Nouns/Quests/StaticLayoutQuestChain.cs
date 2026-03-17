using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Quests;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using Retrograde.Writing;
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

        public bool GenerateQuest()
        {


            // Story Setup --------------------------------
            Random random = RandomProvider.Random;
            Console.WriteLine("StaticLayoutQuestChain");
            List<ITemplateManager> templates = new List<ITemplateManager>()
                {
                    new AllTemplateManager(new RandomTemplateEngine())
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

            var pinnedInvestigations = InvestigationTemplate != ""
                ? new List<string>() { InvestigationTemplate, DeepTempalte }.Where(s => s != "").ToList()
                : null;

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib,
                pinnedDiscovery:     DiscoveryTemplate     != "" ? DiscoveryTemplate     : null,
                pinnedShowdown:      ShowdownTemplate      != "" ? ShowdownTemplate      : null,
                pinnedInvestigations: pinnedInvestigations, selectArc : false);

            // Template Choices --------------------------------
            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate(
                string.IsNullOrEmpty(ShowdownTemplate) ? LorePrompts.PlannedShowdown : ShowdownTemplate,
                new List<string>() { "<QuestStage>Showdown</QuestStage>", "<QuestProgress>90%</QuestProgress>" });
            outlawNpc.spacesuit = ShowdownMissionTemplate.parameters.ContainsKey("NeedSpacesuit") && (bool)ShowdownMissionTemplate.parameters["NeedSpacesuit"];
            outlawNpc.GenerateNPC();
            AITools.InjectContextIntoHistory(
                $"The outlaw target's name is '{outlawNpc.name}'. " +
                "Use this name in log entries, clues, and any in-world references to the target."
            );

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


            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate,null);
            if (!string.IsNullOrEmpty(ShowdownMissionTemplate.outlawQuest.LogMessage))
                AITools.InjectContextIntoHistory($"[Stage '{ShowdownMissionTemplate.Name}' log entry]: {ShowdownMissionTemplate.outlawQuest.LogMessage}");

            AITools.InjectContextIntoHistory(
                "Important: from this point on the player does not know where the final showdown takes place. " +
                "Do not state the showdown location explicitly in any investigation or discovery mission. You may plant indirect clues that point toward it."
            );

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);
            if (!string.IsNullOrEmpty(DeepInvestigationMissionTemplate.outlawQuest.LogMessage))
                AITools.InjectContextIntoHistory($"[Stage '{DeepInvestigationMissionTemplate.Name}' log entry]: {DeepInvestigationMissionTemplate.outlawQuest.LogMessage}");

            if (fork)
            {
                //ForkInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
                if (!string.IsNullOrEmpty(ForkInvestigationMissionTemplate.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{ForkInvestigationMissionTemplate.Name}' log entry]: {ForkInvestigationMissionTemplate.outlawQuest.LogMessage}");

                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ForkInvestigationMissionTemplate.outlawQuest);
                if (!string.IsNullOrEmpty(InvestigationMissionTemplate.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{InvestigationMissionTemplate.Name}' log entry]: {InvestigationMissionTemplate.outlawQuest.LogMessage}");
            }
            else
            {
                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
                if (!string.IsNullOrEmpty(InvestigationMissionTemplate.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{InvestigationMissionTemplate.Name}' log entry]: {InvestigationMissionTemplate.outlawQuest.LogMessage}");
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, InvestigationMissionTemplate.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            outlawNpc.GenerateLegendaryItem();
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            // ── Writing polish pass ───────────────────────────────────────────
            // Runs BEFORE audio staging so improved text drives WAV generation.
            // Narrative order: Discovery → Initial Investigation → (Fork) → Deep Investigation → Showdown
            var polishables = new List<IPolishable>();
            foreach (var p in DiscoveryMissionTemplate.outlawQuest.GetPolishables())
                polishables.Add(new StageAnnotatedPolishable(p, "Act 1: Discovery"));
            foreach (var p in InvestigationMissionTemplate.outlawQuest.GetPolishables())
                polishables.Add(new StageAnnotatedPolishable(p, "Act 2: Initial Investigation"));
            if (fork)
                foreach (var p in ForkInvestigationMissionTemplate.outlawQuest.GetPolishables())
                    polishables.Add(new StageAnnotatedPolishable(p, "Act 2: Fork Investigation"));
            foreach (var p in DeepInvestigationMissionTemplate.outlawQuest.GetPolishables())
                polishables.Add(new StageAnnotatedPolishable(p, "Act 2: Deep Investigation"));
            foreach (var p in ShowdownMissionTemplate.outlawQuest.GetPolishables())
                polishables.Add(new StageAnnotatedPolishable(p, "Act 3: Showdown (Climax)"));
            foreach (var p in outlawNpc.GetPolishables())
                polishables.Add(new StageAnnotatedPolishable(p, "Found Document (Outlaw Log)"));
            WritingPolishPass.Run(polishables);

            // ── Stage audio (uses current text, post-polish) ─────────────────
            ShowdownMissionTemplate.outlawQuest.StageAudio();
            DeepInvestigationMissionTemplate.outlawQuest.StageAudio();
            if (fork)
                ForkInvestigationMissionTemplate.outlawQuest.StageAudio();
            InvestigationMissionTemplate.outlawQuest.StageAudio();
            DiscoveryMissionTemplate.outlawQuest.StageAudio();

            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            SpeechTools.GenerateAllWavs();
            SpeechTools.ConvertAndDeploy();
            return true;
        }
    }
}