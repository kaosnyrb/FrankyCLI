using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools;
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
    public class OutlawQuestChain
    {
        public StarfieldMod myMod;

        public OutlawQuestChain(StarfieldMod myModparam) {
            myMod = myModparam;
        }


        public bool GenerateQuest()
        {

            MissionLib lib = new MissionLib();
            Console.WriteLine("ShowdownTemplates: " + lib.MergedLib.ShowdownTemplates.Count);
            Console.WriteLine("InvestigationTemplates: " + lib.MergedLib.InvestigationTemplates.Count);
            Console.WriteLine("DiscoveryTemplates: " + lib.MergedLib.DiscoveryTemplates.Count);


            var ShowdownMissionTemplate = lib.GetShowdownMissionTemplate("");

            Random random = new Random();
            bool isfemale = false;
            bool fork = false;
            
            if (random.Next(100) > 50)
            {
                isfemale = true;
            }
            if (random.Next(100) > 50)
            {
                fork = true;
            }
            MissionTemplate ForkInvestigationMissionTemplate = new MissionTemplate();

            OutlawNpc outlawNpc = new OutlawNpc(myMod, isfemale, ShowdownMissionTemplate.needSpacesuit);

            // NPC Target                
            outlawNpc.GenerateNPC();
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);
            //Console.WriteLine(outlawNpc.background);

            //Quest Steps
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Feeding the stages into the AI...");

            AITools.RunPrompt("<Summary> The next section contains all the  locations and types of missions  that will be happening. Use this to tie things together.");
            var DeepInvestigationMissionTemplate = lib.GetInvestigationMissionTemplate("");
            if (fork)
            {
                ForkInvestigationMissionTemplate = new Templates_Fork().GetInvestigationMissionTemplate("Branching Node");
            }
            var InvestigationMissionTemplate = lib.GetInvestigationMissionTemplate("");
            var DiscoveryMissionTemplate = lib.GetDiscoveryMissionTemplate();

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
            //Now build an investigation step before

            AITools.RunPrompt("<DeepInvestigation>");
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Investigation: " + DeepInvestigationMissionTemplate.Name);
            var InvestigationMission = DeepInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DeepInvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);
            AITools.RunPrompt("When generating from this point on the player doesn't know where the <Showdown> will take place. Don't reveal it but you can hint at clues.");

            if (fork)
            {
                //ForkInvestigation
                AITools.RunPrompt("<ForkInvestigation>");
                Console.WriteLine("---------------------------------------------------------------------------------");
                Console.WriteLine("ForkInvestigation: " + ForkInvestigationMissionTemplate.Name);
                Quest formmission = ForkInvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ForkInvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);

                //InitialInvestigation
                AITools.RunPrompt("<InitialInvestigation>");
                Console.WriteLine("---------------------------------------------------------------------------------");
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ForkInvestigationMissionTemplate.outlawQuest);
            }
            else
            {
                //InitialInvestigation
                AITools.RunPrompt("<InitialInvestigation>");
                Console.WriteLine("---------------------------------------------------------------------------------");
                Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
                Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);
            }

            // Finally build the discovery step
            AITools.RunPrompt("<Discovery>");
            Console.WriteLine("---------------------------------------------------------------------------------");

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, InvestigationMissionTemplate.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            return true;
        }

        public bool GenerateQuestLoop()
        {

            MissionLib lib = new MissionLib();
            Console.WriteLine("ShowdownTemplates: " + lib.MergedLib.ShowdownTemplates.Count);
            Console.WriteLine("InvestigationTemplates: " + lib.MergedLib.InvestigationTemplates.Count);
            Console.WriteLine("DiscoveryTemplates: " + lib.MergedLib.DiscoveryTemplates.Count);


            var ShowdownMissionTemplate = lib.GetShowdownMissionTemplate("");

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
            int count = 5;
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var template = lib.GetInvestigationMissionTemplate("");
                //Don't run on the first and last.
                if (i != 0 && i != count - 1)
                {
                    if (i % 2 != 0 && random.Next(100) > 0)
                    {
                        //Run on Even as things get wierd if we get a choice at the start or in a row.
                        template = new Templates_Fork().GetInvestigationMissionTemplate("Branching Node");
                    }
                }
                Console.WriteLine("Investigation Template: " + template.Name);
                Quest formmission = template.outlawQuest.Setup(myMod, outlawNpc, template, lastoutlaw);
                lastoutlaw = template.outlawQuest;
            }

            // Finally build the discovery step
            Console.WriteLine("---------------------------------------------------------------------------------");
            var DiscoveryMissionTemplate = lib.GetDiscoveryMissionTemplate();
            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, lastoutlaw);

            //We have now generated all the stages. Do any final linking steps
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            return true;
        }

    }
}