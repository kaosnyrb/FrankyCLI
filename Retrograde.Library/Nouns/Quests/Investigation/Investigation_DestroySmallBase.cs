using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Interfaces;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
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

namespace Retrograde.Quests
{
    public class Investigation_DestroySmallBase : IOutlawQuest
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
            Console.WriteLine("Generating Destroy Small Base Planet Quest...");

            var questActivator = ActivatorSeedData.GetRandomLargeGroundType();

            var datasource = ItemMadlibs.GetActivatorName();
            Console.WriteLine("datasource: " + datasource);

            var questname = QuestMadlibs.GetQuestName(outlawNpc, missionTemplate, datasource);

            Console.WriteLine("questname: " + questname);
            IGang outlawGang = GangManager.GetGang();

            //Log Entry
            var logmessage = QuestMadlibs.GetLogMessage(outlawNpc, missionTemplate, datasource);
            Console.WriteLine("logmessage: " + logmessage);


            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetObjective(0, "Destroy the <Alias=BountyTarget> At <Alias=DungeonLocation>");
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "GangMembers", outlawGang.gangList.ToLink<IStarfieldMajorRecordGetter>());

            //Create the activation message
            var pickupmessage = MessageMadlibs.GetPickupMessage(datasource, nextQuest.QuestLocation ?? "");
            Console.WriteLine("pickupmessage: " + pickupmessage);

            var message = new MessageNoun(0x000844, pickupmessage);

            //Create the Activator
            var newActivator = new ActivatorNoun(0x0008BB, datasource, questActivator.Model);
            newActivator.SetScriptProperty("duout_destroy_completenstart", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_destroy_completenstart", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_destroy_completenstart", "nextquest", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceCreateAlias("BountyTarget", newActivator.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
