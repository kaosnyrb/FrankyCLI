using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog.StructuredStrings.CSharp;
using OpenAI.Chat;
using OpenAI;
using System.Security.Policy;
using FrankyCLI.questgen_tools;

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
            if (random.Next(100) > 50)
            {
                isfemale = true;
            }            
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
            var InvestigationMissionTemplate = lib.GetInvestigationMissionTemplate("");
            var DiscoveryMissionTemplate = lib.GetDiscoveryMissionTemplate();

            AITools.RunPrompt("<Showdown Summary>" + ShowdownMissionTemplate.Description  +  " Location: " + ShowdownMissionTemplate.Location);
            AITools.RunPrompt("<DeepInvestigation Summary>" + DeepInvestigationMissionTemplate.Description + " Location: " + DeepInvestigationMissionTemplate.Location);
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

            //InitialInvestigation
            AITools.RunPrompt("<InitialInvestigation>");
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
            Quest investmission2 = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, DeepInvestigationMissionTemplate.outlawQuest);

            // Finally build the discovery step
            AITools.RunPrompt("<Discovery>");
            Console.WriteLine("---------------------------------------------------------------------------------");

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, InvestigationMissionTemplate.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            return true;
        }

    }
}