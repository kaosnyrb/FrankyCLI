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
            var Missiontemplate = lib.GetFinalMissionTemplate();
            //Missiontemplate = lib.FinalEncounterTemplates[3];
            
            Random random = new Random();
            bool isfemale = false;
            if (random.Next(100) > 50)
            {
                isfemale = true;
            }
            
            OutlawNpc outlawNpc = new OutlawNpc(myMod, isfemale, Missiontemplate.needSpacesuit);
            // NPC Target                
            outlawNpc.GenerateNPC();
            
            Console.WriteLine(outlawNpc.name);
            Console.WriteLine(outlawNpc.background);

            //Quest Step
            var Quest = Missiontemplate.outlawQuest.Setup(myMod, outlawNpc, Missiontemplate,null);

            //Now build an investigation step before
            var invest = lib.GetInvestigationMissionTemplate();
            var InvestQuest = invest.outlawQuest.Setup(myMod, outlawNpc, invest, Quest);

            return true;
        }

    }
}