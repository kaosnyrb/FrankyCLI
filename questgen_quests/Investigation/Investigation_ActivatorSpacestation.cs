using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Nouns.Crew;
using FrankyCLI.questgen_tools.Utils;
using Retrograde.StationDesigns;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class Investigation_ActivatorSpacestation : IOutlawQuest
    {
        private Quest questform;

        public string logMessage { get; set; }

        public string LogMessage
        {
            get => logMessage;
            set => logMessage = value;
        }
        Quest IOutlawQuest.questform
        {
            get => questform;
            set => questform = value;
        }
        string questloc { get; set; }
        string IOutlawQuest.QuestLocation { get => questloc; set => questloc = value; }

        public Quest Setup(StarfieldMod myMod, OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest)
        {
            Console.WriteLine("Generating Activator Space Station Quest...");

            var datasource = PromptManager.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Station Size:" + missionTemplate.parameters["StationSize"].ToString(),
                "Station Faction:" + missionTemplate.parameters["Faction"].ToString()
            });
            Console.WriteLine("datasource: " + datasource);

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Vital clue to their location:" + datasource,
                "Location:" + missionTemplate.Location + "\r\n",
                "Station Size:" + missionTemplate.parameters["StationSize"].ToString(),
                "Station Faction:" + missionTemplate.parameters["Faction"].ToString()
            });
            Console.WriteLine("questname: " + questname);

            //Select the Station Design
            IStationDesign stationDesign = new HabStation();

            var stationname = stationDesign.GenerateStationName(missionTemplate.parameters["Faction"].ToString());

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Find the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Station Size:" + missionTemplate.parameters["StationSize"].ToString(),
                "Station Faction:" + missionTemplate.parameters["Faction"].ToString()
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);

            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());


            newQuest.SetScriptProperty("duout_space_station_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_station_quest", "EnemyShipInteriorLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_station_quest", "CrewSpawnMarkers", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_station_quest", "ItemSpawnMarkers", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_station_quest", "GangMembers", ShipTools.GetGangList(ShipTools.GetFactionID(missionTemplate.parameters["Faction"].ToString())));

            newQuest.SetScriptProperty("duout_space_station_quest", "MinGangMembers", (int)missionTemplate.parameters["DefendingShipCountMin"]);
            newQuest.SetScriptProperty("duout_space_station_quest", "MaxGangMembers", (int)missionTemplate.parameters["DefendingShipCountMax"]);

            var booklogmessage = PromptManager.GetFirstPersonAccount(new List<string>(missionTemplate.Addons)
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
                "Objective: Board the " + stationname + " and find the " + datasource + "\r\n",
                "Spacestation containing the Objective: " + stationname + "\r\n",
                "Faction this station belongs to: " + missionTemplate.parameters["Faction"].ToString() + "\r\n"
            });
            var bountybook = new BookNoun(0x000800, datasource, "Data Slate #" + questID, booklogmessage);

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);

            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_station_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());


            //NPCS
            var gang = GangManager.GetGang();
            var npclst = gang.GenerateGang();
            newQuest.SetScriptProperty("duout_space_station_quest", "Corpses", npclst.ToLink());

            //We set the spawn marker to one of random ones so the target is in different places
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());



            //Create the spacestation;
            StationNoun stationNoun = new StationNoun(stationname, missionTemplate.parameters["Faction"].ToString(), missionTemplate.parameters["StationSize"].ToString(), stationDesign);
            
            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", stationNoun.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
