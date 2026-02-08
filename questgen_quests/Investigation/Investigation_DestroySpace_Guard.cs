using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class Investigation_DestroySpace_Guard : IOutlawQuest
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
            Console.WriteLine("Generating Destroy Space Quest...");
            
            var questActivator = ActivatorTools.GetRandomSpaceType();
            string shipname = ShipTools.GetFactionShipName(missionTemplate.parameter1);
            var ship = new SpaceShipNoun(shipname, missionTemplate.parameterformid, ShipTools.GetFactionID(missionTemplate.parameter1));

            var datasource = PromptManager.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Activator Base Type:" + questActivator.Name,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("datasource: " + datasource);

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Space object that must be destroyed:" + datasource,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("questname: " + questname);


            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Destroy the " + datasource + " to lead you to " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);

            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_activator_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            //We set the spawn marker to one of random ones so the target is in different places
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetQuestReferenceSpaceLocationAlias("PatrolMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            //Set the guard ship
            newQuest.SetQuestReferenceCreateAlias("GuardShip", ship.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Create the activation message
            var pickupmessage = PromptManager.GetPickupMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + nextQuest.QuestLocation + "\r\n",
                "Object we just destroyed: " + datasource + "\r\n"
            });
            Console.WriteLine("pickupmessage: " + pickupmessage);
            var message = new MessageNoun(0x000844, pickupmessage);

            //Create the Activator
            var newActivator = new ActivatorNoun(0x0008BE, datasource, questActivator.Model);

            //Set the Current quest and next quest so when you use the activator it progresses the mission
            newActivator.SetScriptProperty("duout_destroy_completenstart", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_destroy_completenstart", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_destroy_completenstart", "nextquest", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", newActivator.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
