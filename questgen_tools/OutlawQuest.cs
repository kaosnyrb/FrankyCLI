using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrankyCLI.questgen_quests;

namespace FrankyCLI.questgen_tools
{
    public interface IOutlawQuest
    {
        public Quest Setup(StarfieldMod myMod,OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest);
        public string LogMessage { get; set; }
        public string QuestLocation { get; set; }
        public Quest questform { get; set; }
    }

    public class MissionLib
    {
        public List<MissionTemplate> DiscoveryTemplates;
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> ShowdownTemplates;
        public MissionLib()
        {
            DiscoveryTemplates = new List<MissionTemplate>()
            {
                new MissionTemplate()
                {
                    Name = "Dataslate in levelled item",
                    Description = "This creates a dataslate which starts the mission",
                    Location = "A remote location",
                    formid = 0,
                    needSpacesuit = true,
                    outlawQuest = new Discovery_Dataslate()
                }
            };

            InvestigationTemplates = new List<MissionTemplate>()
            {

            };

            ShowdownTemplates = new List<MissionTemplate>
            {
            };

            //We load our template libs here
            Templates_PlanetPCM.AddTemplates(DiscoveryTemplates, InvestigationTemplates, ShowdownTemplates);
            Templates_Cities.AddTemplates(DiscoveryTemplates, InvestigationTemplates, ShowdownTemplates);
            Templates_SpaceActivator.AddTemplates(DiscoveryTemplates, InvestigationTemplates, ShowdownTemplates);
            Templates_Derelicts.AddTemplates(DiscoveryTemplates, InvestigationTemplates, ShowdownTemplates);
        }

        public MissionTemplate GetShowdownMissionTemplate(string mission)
        {
            if (mission != "")
            {
                return ShowdownTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                Random random = new Random();
                return ShowdownTemplates[random.Next(ShowdownTemplates.Count)];
            }
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission)
        {
            if (mission != "")
            {
                //Don't really care about deleting as this is for testing
                return InvestigationTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                Random random = new Random();
                int selected = random.Next(InvestigationTemplates.Count);
                var template = InvestigationTemplates[selected];
                InvestigationTemplates.RemoveAt(selected);
                return template;
            }
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            Random random = new Random();
            return DiscoveryTemplates[random.Next(DiscoveryTemplates.Count)];
        }

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
}
