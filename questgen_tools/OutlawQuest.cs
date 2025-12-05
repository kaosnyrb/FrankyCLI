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

        public TemplateLib MergedLib = new TemplateLib();

        public MissionLib()
        {

            //TemplateLibs.Add(new Templates_Fork());

            TemplateLibs.Add(new Templates_Dataslate());

            var planetlib = new TemplateLib();
            planetlib.ImportTemplates(new Templates_PlanetPCM());
            planetlib.ImportTemplates(new Templates_SpecificDungeons());           
            TemplateLibs.Add(planetlib);

            var spacelib = new TemplateLib();
            spacelib.ImportTemplates(new Templates_SpaceActivator());
            spacelib.ImportTemplates(new Templates_SpaceInformant());
            spacelib.ImportTemplates(new Templates_Derelicts());
            TemplateLibs.Add(spacelib);


            var cities = new TemplateLib();
            cities.ImportTemplates(new Templates_Cities());
            cities.ImportTemplates(new Templates_Cities_Neon());
            cities.ImportTemplates(new Templates_Cities_Cydonia());
            cities.ImportTemplates(new Templates_Cities_Akila());
            TemplateLibs.Add(cities);

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
            return MergedLib.GetShowdownMissionTemplate(missionName);
        }

        public MissionTemplate GetInvestigationMissionTemplate(string missionName)
        {

            Random random = new Random();
            bool foundMission = false;

            if (missionName.Length > 0)
            {
                return MergedLib.GetInvestigationMissionTemplate(missionName);
            }

            while (!foundMission)
            {
                var chosenlib = TemplateLibs[random.Next(TemplateLibs.Count)];
                if (chosenlib.InvestigationTemplates.Count >0)
                {
                    var chosen = chosenlib.GetInvestigationMissionTemplate(missionName);
                    if (chosen != null)
                    {
                        return chosen;
                    }
                }

            }

            return MergedLib.GetInvestigationMissionTemplate(missionName);
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            return MergedLib.GetDiscoveryMissionTemplate();
       }

    }


}
