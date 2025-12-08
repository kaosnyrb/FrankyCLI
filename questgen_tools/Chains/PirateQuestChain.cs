using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Utils;
using GameFinder.Common;
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
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class PirateQuestChain : IQuestchain
    {
        public StarfieldMod myMod;

        public PirateQuestChain(StarfieldMod myModparam) {
            myMod = myModparam;
        }


        public bool GenerateQuest()
        {
            // Story Setup --------------------------------
            Random random = RandomUtils.random;
            Console.WriteLine("PirateQuestChain");
            List<ITemplateManager> templates = new List<ITemplateManager>()
            {
                new AICardTemplateManager(),
                //new RandomTemplateManager()
            };
            var templateManager = templates[random.Next(templates.Count)];
            var Lorefile = File.ReadAllText("C:\\Git\\FrankyCLI\\questgen_quests\\Lorefiles\\PirateTreasure.md");

            // AI Seeding
            string MissionSetupPrompt = "";
            MissionSetupPrompt += "You will be generating the story from the final encounter backwards, try and link things together in way that makes sense.\r\n\r\n";
            MissionSetupPrompt += "First we'll generate a showdown mission, then a number of investigation missions. As we generate the missions you should reveal less and less about the target (We're generating from the end first).\r\n\r\n";
            MissionSetupPrompt += "We're doing this so information about the target is slowly revealed over the course of the missions.\r\n\r\n";
            MissionSetupPrompt += "Use the information generated in the last step to inform the current step.\r\n\r\n";
            MissionSetupPrompt += "You will receive new <Lore> entries as things are created. Use these to flesh out the story. Incorporate at least one relevant lore detail (faction, tech, or city) to ground the scene.\r\n\r\n";
            MissionSetupPrompt += Lorefile;
            Console.WriteLine("Seeding AI With Lorefile...");
            AITools.RunPrompt(MissionSetupPrompt);

            // Template Choices --------------------------------
            Console.WriteLine("Choosing Templates...");
            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate("");
            bool fork = false;
            if (random.Next(100) > 50)
            {
                fork = true;
            }
            MissionTemplate ForkInvestigationMissionTemplate = new MissionTemplate();

            // NPC Target                
            OutlawNpc outlawNpc = new OutlawNpc(myMod, ShowdownMissionTemplate.needSpacesuit);
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);

            //Quest Steps
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Feeding the stages into the AI...");
            AITools.RunPrompt(
                "<Summary> The next section contains all the locations and types of missions that will be happening. " +
                "Use this to tie things together around the treasure hunt and the rivalry between the Crimson Fleet pirate and the outlaw target."
            );

            var DeepInvestigationMissionTemplate = templateManager.GetInvestigationMissionTemplate("");
            if (fork)
            {
                var forktemplates = new Templates_Fork();
                ForkInvestigationMissionTemplate = forktemplates.InvestigationTemplates[random.Next(forktemplates.InvestigationTemplates.Count)];
            }
            var InvestigationMissionTemplate = templateManager.GetInvestigationMissionTemplate("");
            var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate();

            AITools.RunPrompt("<Showdown Summary>" + ShowdownMissionTemplate.Description + " Location: " + ShowdownMissionTemplate.Location);
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
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate, null);

            Console.WriteLine("---------------------------------------------------------------------------------");
            AITools.RunPrompt("<DeepInvestigation>");
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);

            AITools.RunPrompt(
                "When generating from this point on, the true location of the <Showdown>" +
                "are not yet known to the characters. Do not reveal them, but you can hint at clues, partial maps, encrypted data, " +
                "or rumors that all point toward the Showdown."
            );

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

            return true;
        }

    }
}