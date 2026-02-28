using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.SpaceCellDesigns;
using Retrograde.Nouns.SpaceCells;

namespace Retrograde.Quests
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
            var datasource = PromptManager.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Type: Data tablet \r\n",

            });
            Console.WriteLine("datasource: " + datasource);

            string shipname = ShipTools.GetFactionShipName(missionTemplate.parameter1);
            Console.WriteLine("shipname: " + shipname);
            var ship = new SpaceShipNoun(shipname, missionTemplate.parameterformid, factionID);

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n"
            });
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Recover the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetObjective(0, "Recover the " + datasource + " from the " + shipname + " cargo hold");
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "GangMembers", ShipTools.GetGangList(factionID));
            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", ship.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Log Entry
            var booklogmessage = PromptManager.GetFirstPersonAccount(new List<string>(missionTemplate.Addons)
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
                "Objective: Recover the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n",
            });

            var bountybook = new BookNoun("duout_book_test", datasource, booklogmessage);

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };

            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);
            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());
            //Create the space cell
            if (missionTemplate.parameters.ContainsKey("SpaceCell"))
            {
                SpaceCellDesignType celldesign = (SpaceCellDesignType) missionTemplate.parameters["SpaceCell"];
                var noun = new SpaceCellNoun(questname.ToLower(), SpaceCellDesignRegistry.Designs[celldesign]());
                newQuest.SetQuestLevelledSpaceCellAlias(1, noun.LeveledSpaceCell.ToNullableLink());                
            }
            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
