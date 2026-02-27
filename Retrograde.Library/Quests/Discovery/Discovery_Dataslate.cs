using Retrograde.AI.Utils;
using Retrograde.Nouns;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System.Linq;

namespace Retrograde.Quests
{
    public class Discovery_Dataslate : IOutlawQuest
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

            string bookcontents = PromptManager.GetMissionBriefingDataslate(new List<string>(missionTemplate.Addons) {
                //"Background: " + outlawNpc.background,
                "LogMessage: " + nextQuest.LogMessage,                
            });

            var bookname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons) {
                //"Background: " + outlawNpc.background,
                "LogMessage: " + nextQuest.LogMessage,
            });

            var bountybook = new BookNoun("duout_book_test", bookname, bookcontents);
            Console.WriteLine($"[Discovery_Dataslate] Book FormKey: {bountybook.instance.FormKey}");
            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            //We have a condictional so that the dataslate only drops until you complete the next quest.
            //This means you more likely to find missions you haven't done.

            var questBooksLL = myMod.LeveledItems.FirstOrDefault(li => li.EditorID == "duout_LL_QuestBooks");
            if (questBooksLL == null)
            {
                questBooksLL = new LeveledItem(myMod) { EditorID = "duout_LL_QuestBooks" };
                myMod.LeveledItems.Add(questBooksLL);
            }

            Condition condition;
            if (questBooksLL.Entries?.Count > 0 && questBooksLL.Entries[0].Conditions?.Count > 0)
            {
                condition = questBooksLL.Entries[0].Conditions[0].DeepCopy();
                ((GetQuestCompletedConditionData)condition.Data).FirstParameter = new FormLinkOrIndex<IQuestGetter>(condition.Data, nextQuest.questform.FormKey);
            }
            else
            {
                var condData = new GetQuestCompletedConditionData();
                condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, nextQuest.questform.FormKey);
                condition = new ConditionFloat()
                {
                    CompareOperator = CompareOperator.EqualTo,
                    ComparisonValue = 0f,
                    Data = condData
                };
            }

            questBooksLL.Entries ??= new ExtendedList<LeveledItemEntry>();
            questBooksLL.Entries.Add(new LeveledItemEntry()
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
