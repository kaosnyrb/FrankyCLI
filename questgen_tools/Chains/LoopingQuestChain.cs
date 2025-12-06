using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
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

        public bool GenerateQuest(ITemplateManager templateManager)
        {
            Console.WriteLine("LoopingLayoutQuestChain");

            templateManager.PrintManagerInfo();

            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate("");

            Random random = new Random();
            bool isfemale = false;

            if (random.Next(100) > 50)
            {
                isfemale = true;
            }
            MissionTemplate ForkInvestigationMissionTemplate = new MissionTemplate();

            OutlawNpc outlawNpc = new OutlawNpc(myMod, isfemale, ShowdownMissionTemplate.needSpacesuit);

            // NPC Target                
            outlawNpc.GenerateNPC();
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
                        //Run on Even as things get wierd if we get a choice at the start or in a row.
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