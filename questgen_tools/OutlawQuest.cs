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

        public MissionLib()
        {
            TemplateLibs.Add(new Templates_Dataslate());
            //TemplateLibs.Add(new Templates_Cities());
            TemplateLibs.Add(new Templates_PlanetPCM());
            TemplateLibs.Add(new Templates_SpaceActivator());
            TemplateLibs.Add(new Templates_Derelicts());
        }

        public MissionTemplate GetShowdownMissionTemplate(string missionName)
        {
            Random random = new Random();
            //Choose a deck
            bool drawn = false;
            while (!drawn)
            {
                //Choose a deck and draw a card
                int decknumber = random.Next(TemplateLibs.Count);
                var mission = TemplateLibs[decknumber].GetShowdownMissionTemplate("");
                // Note that not all decks have all mission types.
                if (mission != null)
                {
                    drawn = true;
                    //Now remove the deck from the set
                    TemplateLibs.RemoveAt(decknumber);
                    return mission;
                }
            }
            return null;
        }

        public MissionTemplate GetInvestigationMissionTemplate(string missionName)
        {
            Random random = new Random();
            //Choose a deck
            bool drawn = false;
            while (!drawn)
            {
                //Choose a deck and draw a card
                int decknumber = random.Next(TemplateLibs.Count);
                var mission = TemplateLibs[decknumber].GetInvestigationMissionTemplate("");
                // Note that not all decks have all mission types.
                if (mission != null)
                {
                    drawn = true;
                    //Now remove the deck from the set
                    TemplateLibs.RemoveAt(decknumber);
                    return mission;
                }
            }
            return null;
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            Random random = new Random();
            //Choose a deck
            bool drawn = false;
            while(!drawn)
            {
                //Choose a deck and draw a card
                int decknumber = random.Next(TemplateLibs.Count);
                var mission = TemplateLibs[decknumber].GetDiscoveryMissionTemplate();
                // Note that not all decks have all mission types.
                if (mission != null)
                {
                    drawn = true;
                    //Now remove the deck from the set
                    TemplateLibs.RemoveAt(decknumber);
                    return mission;
                }
            }
            return null;
       }

    }


}
