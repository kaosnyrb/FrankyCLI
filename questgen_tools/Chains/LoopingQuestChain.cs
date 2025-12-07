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
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class LoopingLayoutQuestChain : IQuestchain
    {
        public StarfieldMod myMod;

        public LoopingLayoutQuestChain(StarfieldMod myModparam) {
            myMod = myModparam;
        }

        public bool GenerateQuest()
        {
            // Story Setup --------------------------------
            Random random = RandomUtils.random;
            Console.WriteLine("LoopingLayoutQuestChain");
            List<ITemplateManager> templates = new List<ITemplateManager>()
                {
                    new AICardTemplateManager(),
                    new RandomTemplateManager()
                };
            var templateManager = templates[random.Next(templates.Count)];
            //AI Seeding
            string MissionSetupPrompt = "";
            MissionSetupPrompt += "You will be generating the story from the final encounter backwards, try and link things together in way that makes sense.\r\n\r\n";
            MissionSetupPrompt += "First we'll generate a showdown mission, then loop through a number of investigation missions. As we generate the missions you should reveal less and less about the target (We're generating from the end first).\r\n\r\n";
            MissionSetupPrompt += "We're doing this so information about the target is slowly revealed over the course of the missions.\r\n\r\n";
            MissionSetupPrompt += "The theme for this mission is " + PromptFlavourTools.GetQuestTheme() + " when generating for the quest from now on try and keep in this theme.\r\n\r\n";
            MissionSetupPrompt += "Use the information generated in the last step to inform the current step.\r\n\r\n";
            MissionSetupPrompt += "You will recheive new <Lore> entries as things are created, use these to flesh out the story. Incorporate at least one relevant lore detail (faction, tech, or city) to ground the scene.\r\n\r\n";
            AITools.RunPrompt(MissionSetupPrompt);

            // Template Choices --------------------------------
            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate("");

            // NPC Target                
            OutlawNpc outlawNpc = new OutlawNpc(myMod, ShowdownMissionTemplate.needSpacesuit);
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);

            //Quest Steps
            var quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate, null);
            var lastoutlaw = ShowdownMissionTemplate.outlawQuest;
            int count = 2 + random.Next(4);

            for (int i = 0; i < count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var template = templateManager.GetInvestigationMissionTemplate("");
                //Don't run on the first and last.
                if (i != 0 && i != count - 1)
                {
                    if (i % 2 != 0 && random.Next(100) > 0)
                    {
                        //Run on Odd as things get wierd if we get a choice at the start or in a row.
                        var forktemplates = new Templates_Fork();
                        template = forktemplates.InvestigationTemplates[random.Next(forktemplates.InvestigationTemplates.Count)];
                    }
                }
                Console.WriteLine("Investigation Template: " + template.Name);
                Quest formmission = template.outlawQuest.Setup(myMod, outlawNpc, template, lastoutlaw);
                lastoutlaw = template.outlawQuest;
                if (i == 0)
                {
                    AITools.RunPrompt("Important note: From now on don't mention where the final showdown takes place.");
                }
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate();
            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, lastoutlaw);

            //We have now generated all the stages. Do any final linking steps
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            return true;
        }

    }
}