using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
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
    public class Investigation_ActivatorSpace_Trapped : IOutlawQuest
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
            Console.WriteLine("Generating Activator " + (string)missionTemplate.parameters["Label"] + " Trapped Space Quest...");

            var questActivator = ActivatorTools.GetRandomSpaceType();

            var datasource = PromptManager.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Activator Base Type:" + questActivator.Name,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("datasource: " + datasource);

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Vital clue to their location:" + datasource,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            //Log Entry
            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Find the " + datasource + " to lead you to " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            Console.WriteLine(logmessage);
            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Create the activation message
            var pickupmessage = PromptManager.GetPickupMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + nextQuest.QuestLocation + "\r\n",
                "Vital clue to there location: " + datasource + "\r\n"
            });
            Console.WriteLine("pickupmessage: " + pickupmessage);

            var message = new MessageNoun(0x000844, pickupmessage);

            //Create the Activator
            var newActivator = new ActivatorNoun(0x00090E, datasource, questActivator.Model);

            //Set the Current quest and next quest so when you use the activator it progresses the mission
            newActivator.SetScriptProperty("duout_activator_spacetrap", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_spacetrap", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_spacetrap", "nextquest", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_spacetrap", "GangMembers", ShipTools.GetGangList(ShipTools.GetFactionID((string)missionTemplate.parameters["Label"])));

            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", newActivator.instance.ToLink<IStarfieldMajorRecordGetter>());
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
