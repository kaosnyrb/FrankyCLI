using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
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
    public class Investigation_Informant_Space : IOutlawQuest
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
            Console.WriteLine("Generating Informant Space Quest...");
            questloc = missionTemplate.Location;

            var factionID = ShipTools.GetFactionID(missionTemplate.parameter1);
            var datasource = PromptManager.GetActivatorName(new List<string>()
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Type: Data tablet \r\n",

            });
            Console.WriteLine("datasource: " + datasource);

            string shipname = ShipTools.GetFactionShipName(missionTemplate.parameter1);
            Console.WriteLine("shipname: " + shipname);
            var ship = new SpaceShipNoun(shipname, missionTemplate.parameterformid, factionID);

            var questname = PromptManager.GetQuestName(new List<string>()
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n"
            });
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var logmessage = PromptManager.GetLogMessage(new List<string>()
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Recover the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetObjective(0, "Recover the " + datasource + " from the " + shipname + " cargo hold");
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "GangMembers", ShipTools.GetGangList(factionID));
            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", ship.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Log Entry
            var booklogmessage = PromptManager.GetFirstPersonAccount(new List<string>()
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
                "Objective: Recover the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n",
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
            newQuest.SetScriptProperty("duout_space_bounty_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
