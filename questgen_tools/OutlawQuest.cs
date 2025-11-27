using DynamicData;
using FrankyCLI.questgen_quests;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public interface IOutlawQuest
    {
        public Quest Setup(StarfieldMod myMod,OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest);
        public string LogMessage { get; set; }
        public string QuestLocation { get; set; }
        public Quest questform { get; set; }
    }
    public class MissionTemplate
    {
        public string Name;
        public string Description;
        public string Location;
        public string parameter1;
        public uint parameterformid;
        public uint formid;
        public bool needSpacesuit;
        public IOutlawQuest outlawQuest;  //This is an interface that wraps the actual quest template implementation
    }


    public class MissionLib
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        TemplateLib MergedLib = new TemplateLib();

        public MissionLib()
        {

            TemplateLibs.Add(new Templates_Dataslate());
            TemplateLibs.Add(new Templates_Cities());
            TemplateLibs.Add(new Templates_PlanetPCM());
            TemplateLibs.Add(new Templates_SpaceActivator());
            TemplateLibs.Add(new Templates_Derelicts());

            MergedLib.DiscoveryTemplates = new List<MissionTemplate>();
            MergedLib.InvestigationTemplates = new List<MissionTemplate>();
            MergedLib.ShowdownTemplates = new List<MissionTemplate>();

            foreach (TemplateLib templateLib in TemplateLibs)
            {
                foreach (var dis in templateLib.DiscoveryTemplates)
                {
                    MergedLib.DiscoveryTemplates.Add(dis);
                }
                foreach (var dis in templateLib.InvestigationTemplates)
                {
                    MergedLib.InvestigationTemplates.Add(dis);
                }
                foreach(var dis in templateLib.ShowdownTemplates)
                {
                    MergedLib.ShowdownTemplates.Add(dis);
                }
            }
        }

        public MissionTemplate GetShowdownMissionTemplate(string missionName)
        {

            return MergedLib.GetShowdownMissionTemplate("");
        }

        public MissionTemplate GetInvestigationMissionTemplate(string missionName)
        {
            return MergedLib.GetInvestigationMissionTemplate("");
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            return MergedLib.GetDiscoveryMissionTemplate();
       }

    }


}
