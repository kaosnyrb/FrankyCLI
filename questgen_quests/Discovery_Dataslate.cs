using FrankyCLI.questgen_tools;
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

            // Book
            var Book = myMod.Books[new FormKey(myMod.ModKey, 0x000800)].DeepCopy();
            Book bountybook = new Book(myMod)
            {
                CNAM = Book.CNAM,
                Components = Book.Components,
                Description = outlawNpc.background + "\r\n\r\n" + nextQuest.LogMessage,
                DNAMUnknown = Book.DNAMUnknown,
                DropdownSound = Book.DropdownSound,
                EditorID = "book_" + questID,
                Keywords = Book.Keywords,
                ENAM = Book.ENAM,
                FeaturedItemMessage = Book.FeaturedItemMessage,
                Flags = Book.Flags,
                FNAM = Book.FNAM,
                InventoryArt = Book.InventoryArt,
                Model = Book.Model,
                Name = "Bounty: " + outlawNpc.name,
                ODTY = Book.ODTY,
                Value = Book.Value,
                Weight = Book.Weight,
                VirtualMachineAdapter = Book.VirtualMachineAdapter,
                Transforms = Book.Transforms,
            };
            
            //set  the  book to start the new quest
            ((ScriptObjectProperty)bountybook.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>();

            bountybook.ENAM = "Data Slate #" + questID;
            myMod.Books.Add(bountybook);

            //Find the levelled list
            //duout_LL_QuestBooks [LVLI:02000843]

            //We have a condictional so that the dataslate only drops until you complete the next quest.
            //This means you more likely to find missions you haven't done.
            var condition = myMod.LeveledItems[new FormKey(myMod.ModKey, 0x000843)].Entries[0].Conditions[0].DeepCopy();
            ((GetQuestCompletedConditionData)condition.Data).FirstParameter = new FormLinkOrIndex<IQuestGetter>(condition.Data, nextQuest.questform.FormKey);

            myMod.LeveledItems[new FormKey(myMod.ModKey, 0x000843)].Entries.Add(new LeveledItemEntry()
            {
                Count = 1,
                Reference = bountybook.ToLink<IItemGetter>(),
                ChanceNone = new Percent(0),
                Level = 1,
                Conditions = new ExtendedList<Condition>() { condition }
            });
            
            return null;
        }
    }
}
