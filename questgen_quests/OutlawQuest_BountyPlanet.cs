using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog.StructuredStrings.CSharp;
using OpenAI.Chat;
using OpenAI;
using System.Security.Policy;
using FrankyCLI.questgen_tools;

namespace FrankyCLI
{
    public class OutlawQuest_BountyPlanet : IOutlawQuest
    {
        public Quest Setup(StarfieldMod myMod, OutlawNpc outlawNpc, MissionTemplate missionTemplate, Quest nextQuest)
        {
            var questprompt = AITools.GetBackgroundPrompt() +
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";

            questprompt += outlawNpc.name + "\r\n";
            questprompt += outlawNpc.background + "\r\n";

            var questname = AITools.RunPrompt(questprompt);


            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            var logprompt = AITools.GetBackgroundPrompt() +
                "Include newline characters in your response.\r\n" +
            "Generate a short flavour text story explaination on why this character is at this location.\r\n\r\n" +            
            "Use the following information to build the explaination:\r\n\r\n";

            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Character background: " + outlawNpc.background + "\r\n";

            var logmessage = AITools.RunPrompt(logprompt);

            Console.WriteLine(logmessage);


            var Quest = myMod.Quests[new FormKey(myMod.ModKey, missionTemplate.formid)].DeepCopy();
            Quest newQuest = new Quest(myMod)
            {
                Name = questname,
                Aliases = Quest.Aliases,
                Components = Quest.Components,
                Data = Quest.Data,
                EditorID = "quest_" + questID,
                Keywords = Quest.Keywords,
                Location = Quest.Location,
                MajorFlags = Quest.MajorFlags,
                Objectives = Quest.Objectives,
                MissionTypeKeyword = Quest.MissionTypeKeyword,
                QuestType = Quest.QuestType,
                ScriptComment = Quest.ScriptComment,
                Stages = Quest.Stages,
                Summary = Quest.Summary,
                VirtualMachineAdapter = Quest.VirtualMachineAdapter
            };

            newQuest.Stages[0].LogEntries[0].Entry = logmessage; //"I've found a dataslate containing the location of <Alias=BountyTarget>, who is hiding out at <Alias=DungeonLocation> on <Alias=TargetPlanet>. The Trackers Alliance will pay for taking out the bounty.";

            //set quest alias to self in scripts
            ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
            newQuest.VirtualMachineAdapter.Aliases[0].Property.Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

            //Set the NPC to be the quest target
            ((IQuestReferenceAlias)Quest.Aliases[3]).CreateReferenceToObject.Object = outlawNpc.GeneratedNPC.ToLink<IStarfieldMajorRecordGetter>();
            myMod.Quests.Add(newQuest);
            // Book
            /*
            var Book = myMod.Books[new FormKey(myMod.ModKey, 0x000800)].DeepCopy();
            Book bountybook = new Book(myMod)
            {
                CNAM = Book.CNAM,
                Components = Book.Components,
                Description = outlawNpc.background + "\r\n" + logmessage,
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
                VirtualMachineAdapter = Book.VirtualMachineAdapter
            };
            //set  the  book to start the new quest
            ((ScriptObjectProperty)bountybook.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

            bountybook.ENAM = "Data Slate #" + questID;
            myMod.Books.Add(bountybook);
            */
            return newQuest;
        }

    }
}
