using Retrograde.AI.Utils;
using Retrograde.Nouns;
using Retrograde.Utils;
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

            string bookcontents = NarrativePrompts.GetMissionBriefingDataslate(new List<string>(missionTemplate.Addons) {
                "LogMessage: " + nextQuest.LogMessage,
            });

            var bookname = QuestPrompts.GetQuestName(new List<string>(missionTemplate.Addons) {
                "LogMessage: " + nextQuest.LogMessage,
            });

            var bountybook = new BookNoun("duout_book_test", bookname, bookcontents);
            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            // Voice the dataslate: a witness NPC who wrote or recorded this briefing.
            bool speakerIsFemale = RandomProvider.Random.Next(100) > 50;
            var speakerTemplate = NPCTools.FindTemplateDeadNpc(speakerIsFemale);
            Npc speakerNpc = NPCTools.CloneNPC(myMod, speakerTemplate);
            speakerNpc.Name = NameSeedData.GenerateName(speakerIsFemale);
            speakerNpc.EditorID = "npc_disc_" + bountybook.instance.FormKey.ID.ToString("X6");
            var speakerVoice = NPCTools.GetVoice("", speakerIsFemale);
            string speakerVoiceEditorId = string.Empty;
            if (!speakerVoice.IsNull)
            {
                speakerNpc.Voice.SetTo(speakerVoice.FormKey);
                var vtRec = RetrogradeContext.Current.StarfieldMod.VoiceTypes.FirstOrDefault(v => v.FormKey == speakerVoice.FormKey);
                speakerVoiceEditorId = vtRec?.EditorID ?? speakerVoice.FormKey.ID.ToString("X6");
            }
            myMod.Npcs.Add(speakerNpc);

            var txVoicePool = speakerIsFemale ? VoiceSeedData.FemaleVoices : VoiceSeedData.MaleVoices;
            var txVoice = txVoicePool[RandomProvider.Random.Next(txVoicePool.Count)];
            SpeechTools.AddVoice(bountybook.instance.FormKey.ID, speakerNpc.FormKey, bookcontents, speakerVoiceEditorId, txVoice.Id);

            // Conditional entry: dataslate only drops until the next quest is completed.

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
