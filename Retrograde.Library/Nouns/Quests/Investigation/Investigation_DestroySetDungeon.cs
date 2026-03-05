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
    public class Investigation_DestroySetDungeon : IOutlawQuest
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
            Console.WriteLine("Generating Destroy Planet Quest...");
            var questActivator = ActivatorTools.GetRandomLargeGroundType();


            var destroytarget = ItemPrompts.GetDestroyActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Activator Base Type:" + questActivator.Name,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("destroytarget: " + destroytarget);


            var questname = QuestPrompts.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Item we must destroy: " + destroytarget + "\r\n"
            });
            Console.WriteLine("questname: " + destroytarget);

            IGang outlawGang = GangManager.GetGang();

            var logmessage = QuestPrompts.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Destroy the " + destroytarget + " to lead you to " + outlawNpc.name + "\r\n",
                "Gang protecting the target: " + outlawGang.gangName + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "GangMembers", outlawGang.gangList.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetQuestPCMTypeKeyword("DungeonLocation", myMod.Keywords[new FormKey(myMod.ModKey, Convert.ToUInt32(missionTemplate.parameters["FormId"]))].ToNullableLink<IKeywordGetter>());

            var pickupmessage = MessagePrompts.GetDestroyMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + nextQuest.QuestLocation + "\r\n",
                "Item we must destroy: " + destroytarget + "\r\n"
            });
            Console.WriteLine("pickupmessage: " + pickupmessage);

            var message = new MessageNoun(0x000844, pickupmessage);

            //Create the Activator
            var newActivator = new ActivatorNoun(0x0008BB, destroytarget, questActivator.Model);
            newActivator.SetScriptProperty("duout_destroy_completenstart", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_destroy_completenstart", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_destroy_completenstart", "nextquest", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            //Set the Activator to be the quest target
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", newActivator.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
