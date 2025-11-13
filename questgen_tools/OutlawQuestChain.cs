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
            var ShowdownMissionTemplate = lib.GetShowdownMissionTemplate();
            //Missiontemplate = lib.FinalEncounterTemplates[3];
            
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
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var Quest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate,null);

            //Now build an investigation step before
            var InvestigationMissionTemplate = lib.GetInvestigationMissionTemplate();
            var InvestigationMission = InvestigationMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, InvestigationMissionTemplate, ShowdownMissionTemplate.outlawQuest);
            Console.WriteLine("Investigation: " + InvestigationMissionTemplate.Name);

            //Second invesitiation test  - works fine
            var invest2 = lib.GetInvestigationMissionTemplate();
            Quest investmission2 = invest2.outlawQuest.Setup(myMod, outlawNpc, invest2, InvestigationMissionTemplate.outlawQuest);
            Console.WriteLine("Investigation: " + invest2.Name);

            // Finally build the discovery step
            var DiscoveryMissionTemplate = lib.GetDiscoveryMissionTemplate();
            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, DiscoveryMissionTemplate, invest2.outlawQuest);

            return true;
        }

    }
}