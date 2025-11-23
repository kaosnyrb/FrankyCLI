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
            Console.WriteLine("ShowdownTemplates: " + lib.ShowdownTemplates.Count);
            Console.WriteLine("InvestigationTemplates: " + lib.InvestigationTemplates.Count);
            Console.WriteLine("DiscoveryTemplates: " + lib.DiscoveryTemplates.Count);


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
            
            Console.WriteLine(outlawNpc.name);
            Console.WriteLine(outlawNpc.background);

            //Quest Step

            AITools.RunPrompt("<Showdown>");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate,null);

            //Now build an investigation step before

            AITools.RunPrompt("<DeepInvestigation>");
            var InvestigationMissionTemplate = lib.GetInvestigationMissionTemplate("");
            Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);
            var InvestigationMission = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);

            //Second invesitiation test  - works fine
            AITools.RunPrompt("<InitalInvestigation>");
            var invest2 = lib.GetInvestigationMissionTemplate("");
            Console.WriteLine("Investigation: " + invest2.Name);
            //invest2 = lib.InvestigationTemplates[6];
            Quest investmission2 = invest2.outlawQuest.Setup(myMod, outlawNpc, invest2, InvestigationMissionTemplate.outlawQuest);

            // Finally build the discovery step
            AITools.RunPrompt("<Discovery>");
            var DiscoveryMissionTemplate = lib.GetDiscoveryMissionTemplate();
            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, invest2.outlawQuest);

            //We have now generated all the stages. Do any final linking steps
            outlawNpc.GenerateLog();

            return true;
        }

    }
}