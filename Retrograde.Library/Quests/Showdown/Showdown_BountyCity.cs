using DynamicData;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests
{
    public class Showdown_BountyCity : IOutlawQuest
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
            Console.WriteLine("Generating Bounty Planet Quest...");
            if (missionTemplate.parameters != null)
            {
                if (missionTemplate.parameters.ContainsKey("ExtraLore"))
                {
                    missionTemplate.Addons.Add(missionTemplate.parameters["ExtraLore"].ToString());
                }
            }

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons));
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Kill the Outlaw target " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);

            newQuest.SetScriptProperty("duout_ground_bounty_quest", "DeathItems", myMod.FormLists[outlawNpc.deathItems].ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", outlawNpc.instance.ToLink<IStarfieldMajorRecordGetter>());
            
            var marker = RandomProvider.GetRandomMarker("doout_city_showdown_marker_" + (string)missionTemplate.parameters["Label"] + "_");
            newQuest.SetQuestReferenceAlias("BountyTargetMarker", marker.FormKey);

            var locaform = RetrogradeContext.Current.StarfieldMod.Locations[new FormKey(RetrogradeContext.Current.StarfieldModKey, Convert.ToUInt32(missionTemplate.parameters["FormId"]))];
            newQuest.SetQuestLocationAlias("DungeonLocation", locaform.ToNullableLink<ILocationGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
