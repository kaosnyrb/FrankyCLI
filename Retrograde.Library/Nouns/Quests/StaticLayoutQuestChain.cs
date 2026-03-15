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

        // Generates 1-2 sentences of prose describing what a recovered data slate reveals
        // about the next step. Does not reference where it was found — that's unknown at generation time.
        private void GenerateDiscoveryBridge(MissionTemplate discoveryTemplate, MissionTemplate toTemplate)
        {
            var bridge = AITools.RunPrompt(
                $"In 1-2 sentences, describe what a recovered data slate reveals that points the player toward " +
                $"the \"{toTemplate.Name}\" stage at {toTemplate.Location}.\n" +
                "Focus only on the contents of the slate and what lead it provides — do not mention where or how the player found it. " +
                "Be concrete — name a data file reference, a contact, a location fragment, or a coded instruction. " +
                "Ground it in the established lore. Output only the 1-2 sentences, no headers or labels."
            );

            if (discoveryTemplate.Addons == null)
                discoveryTemplate.Addons = new List<string>();
            discoveryTemplate.Addons.Add($"<StageBridge>{bridge}</StageBridge>");
        }

        // Generates 1-2 sentences of prose describing what the player finds at fromTemplate
        // that sends them toward toTemplate. Adds to history AND to fromTemplate.Addons.
        private void GenerateStageBridge(MissionTemplate fromTemplate, MissionTemplate toTemplate)
        {
            var bridge = AITools.RunPrompt(
                $"In 1-2 sentences, describe the specific clue or contact the player uncovers at " +
                $"{fromTemplate.Location} (the \"{fromTemplate.Name}\" stage) that points them toward " +
                $"the \"{toTemplate.Name}\" stage at {toTemplate.Location}.\n" +
                $"The source contact or clue is located at {fromTemplate.Location} — use this exactly; do not substitute any other location from the lore context. " +
                "Be concrete — name a data file, a physical trail, or an overheard conversation. " +
                "If a contact provides the lead, describe them by role only (e.g. 'a dock worker', 'a UC officer') — do not invent a name for them. " +
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

            var pinnedInvestigations = InvestigationTemplate != ""
                ? new List<string>() { InvestigationTemplate, DeepTempalte }.Where(s => s != "").ToList()
                : null;

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib,
                pinnedDiscovery:     DiscoveryTemplate     != "" ? DiscoveryTemplate     : null,
                pinnedShowdown:      ShowdownTemplate      != "" ? ShowdownTemplate      : null,
                pinnedInvestigations: pinnedInvestigations);

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
            GenerateStageBridge(DeepInvestigationMissionTemplate, ShowdownMissionTemplate);
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);
            if (!string.IsNullOrEmpty(DeepInvestigationMissionTemplate.outlawQuest.LogMessage))
                AITools.InjectContextIntoHistory($"[Stage '{DeepInvestigationMissionTemplate.Name}' log entry]: {DeepInvestigationMissionTemplate.outlawQuest.LogMessage}");

            if (fork)
            {
                //ForkInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                GenerateStageBridge(ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate);
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
                if (!string.IsNullOrEmpty(ForkInvestigationMissionTemplate.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{ForkInvestigationMissionTemplate.Name}' log entry]: {ForkInvestigationMissionTemplate.outlawQuest.LogMessage}");

                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                GenerateStageBridge(InvestigationMissionTemplate, ForkInvestigationMissionTemplate);
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ForkInvestigationMissionTemplate.outlawQuest);
                if (!string.IsNullOrEmpty(InvestigationMissionTemplate.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{InvestigationMissionTemplate.Name}' log entry]: {InvestigationMissionTemplate.outlawQuest.LogMessage}");
            }
            else
            {
                //InitialInvestigation
                Console.WriteLine("---------------------------------------------------------------------------------");
                GenerateStageBridge(InvestigationMissionTemplate, DeepInvestigationMissionTemplate);
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
                if (!string.IsNullOrEmpty(InvestigationMissionTemplate.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{InvestigationMissionTemplate.Name}' log entry]: {InvestigationMissionTemplate.outlawQuest.LogMessage}");
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            GenerateDiscoveryBridge(DiscoveryMissionTemplate, InvestigationMissionTemplate);

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, InvestigationMissionTemplate.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            outlawNpc.GenerateLegendaryItem();
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();            
            //Generate Voice for the log
            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            SpeechTools.GenerateAllWavs();
            SpeechTools.ConvertAndDeploy();
            return true;
        }
    }
}