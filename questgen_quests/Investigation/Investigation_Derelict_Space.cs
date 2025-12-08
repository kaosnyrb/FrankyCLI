using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
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
    public class Investigation_Derelict_Space : IOutlawQuest
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

            var factionID = ShipTools.GetFactionID(missionTemplate.parameter1);
            string shipname = ShipTools.GetFactionShipName(missionTemplate.parameter1);
            Console.WriteLine("shipname: " + shipname);
            var ship = new SpaceShipNoun(shipname, missionTemplate.parameterformid, factionID);

            //Create the datasource
            var datasourceprompt =
                "A three word or less log file that contains a clue to the characters location.\r\n" +
                "Only include the data source name in the response.\r\n\r\n" +
                "This quest is about finding a lead on this character, this is the link to them.\r\n\r\n";
            var datasource = AITools.RunPrompt(datasourceprompt);
            Console.WriteLine("datasource: " + datasource);

            var questprompt = 
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n" +
                "This quest is about finding the location of this character\r\n\r\n" +
                "Keep it to one paragraph with newlines\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";
            questprompt += "Vital clue to their location: " + datasource + "\r\n";
            questprompt += "Spaceship holding the information: " + shipname + "\r\n";

            var questname = AITools.RunPrompt(questprompt);
            Console.WriteLine("questname: " + questname);
            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            
            //Log Entry
            var logprompt = 
            "Generate a short flavour text story which is an explaination on why the data needed to find this character is at this location.\r\n\r\n" +
            "Explain why the ship is derelict and how the crew died.\r\n\r\n" +            
            "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Vital clue to there location: " + datasource + "\r\n";
            logprompt += "Spaceship guarding the information: " + shipname + "\r\n";
            logprompt += "Faction this ship belongs to: " + missionTemplate.parameter1 + "\r\n";

            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);
            var logmessage = AITools.RunPrompt(logprompt);

            Console.WriteLine(logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetObjective(0, "Recover the " + datasource + " from the " + shipname);
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the script values

            newQuest.SetScriptProperty("duout_space_derelict_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "EnemyShipInteriorLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "CrewSpawnMarkers", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "ItemSpawnMarkers", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "Corpses", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("duout_space_derelict_quest", "GangMembers", ShipTools.GetGangList(factionID));
            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", ship.instance.ToLink<IStarfieldMajorRecordGetter>());


            //Log Entry
            var booklogprompt =
            "Generate a short data file which is a log stream from the target.\r\n\r\n" +
            "This log explains why the target is at the next location\r\n\r\n" +
            "Keep it to two paragraphs under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            booklogprompt += "Location:" + nextQuest.QuestLocation + "\r\n";
            booklogprompt = PromptFlavourTools.AddFlavourToTargetBook(booklogprompt);
            var booklogmessage = AITools.RunPrompt(booklogprompt);
            var bountybook = new BookNoun(0x000800, datasource, "Data Slate #" + questID, booklogmessage);

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);

            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
