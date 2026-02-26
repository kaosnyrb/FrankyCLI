using Retrograde.AI.Utils;
using Retrograde.Nouns;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
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
using Retrograde.Utils;

namespace Retrograde.Quests
{
    public class Showdown_BountyPlanet : IOutlawQuest
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
            questloc = missionTemplate.Location;

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons));
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Kill the Outlaw target " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);


            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "DeathItems", myMod.FormLists[outlawNpc.deathItems].ToLink<IStarfieldMajorRecordGetter>());

            // We set the PCM keyword to be the param. We've build a tree with a 1 to 1 mapping of keywords and POIs
            if (missionTemplate.parameterformid != 0)
            {
                newQuest.SetQuestPCMTypeKeyword("DungeonLocation", myMod.Keywords[new FormKey(myMod.ModKey, missionTemplate.parameterformid)].ToNullableLink<IKeywordGetter>());
            }

            newQuest.SetQuestReferenceCreateAlias("BountyTarget", outlawNpc.instance.ToLink<IStarfieldMajorRecordGetter>());


            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
