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
    internal class Discovery_Dataslate : IOutlawQuest
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
            Console.WriteLine("Discovery Quest - Dataslate.");
            questloc = missionTemplate.Location;

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Merge the background and log message.

            string prompt = "Merge the background info the bounty and the first investigation mission Log message into the contents of a dataslate that tells the player who they are chasing and how they'll find them. " +
                "Keep it to 200 words.";
            prompt = PromptFlavourTools.AddFlavourToLogMessage(prompt);

            prompt += "Background: " + outlawNpc.background;
            prompt += "LogMessage: " + nextQuest.LogMessage;
            string bookcontents = AITools.RunPrompt(prompt);

            var bountybook = new BookNoun(0x000800, "Bounty: " + outlawNpc.name, "Data Slate #" + questID, bookcontents);
            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            //We have a condictional so that the dataslate only drops until you complete the next quest.
            //This means you more likely to find missions you haven't done.
            var condition = myMod.LeveledItems[new FormKey(myMod.ModKey, 0x000843)].Entries[0].Conditions[0].DeepCopy();
            ((GetQuestCompletedConditionData)condition.Data).FirstParameter = new FormLinkOrIndex<IQuestGetter>(condition.Data, nextQuest.questform.FormKey);

            myMod.LeveledItems[new FormKey(myMod.ModKey, 0x000843)].Entries.Add(new LeveledItemEntry()
            {
                Count = 1,
                Reference = bountybook.instance.ToLink<IItemGetter>(),
                ChanceNone = new Percent(0),
                Level = 1,
                Conditions = new ExtendedList<Condition>() { condition }
            });
            
            return null;
        }
    }
}
