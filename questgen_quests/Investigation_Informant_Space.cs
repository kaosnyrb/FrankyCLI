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
    public class Investigation_Informant_Space : IOutlawQuest
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
            Console.WriteLine("Generating Informant Space Quest...");
            questloc = missionTemplate.Location;

            var factionID = ShipTools.GetFactionID(missionTemplate.parameter1);


            string shipname = ShipTools.GetFactionShipName(missionTemplate.parameter1);
            Console.WriteLine("shipname: " + shipname);
            var ship = ShipTools.GenShip(shipname, missionTemplate.parameterformid, factionID);

            //Create the datasource
            var datasourceprompt =
                "A three word or less log file that contains a clue to the characters location.\r\n" +
                "Only include the data source name in the response.\r\n\r\n" +
                "This quest is about finding a lead on this character, this is the link to them.\r\n\r\n";
            var datasource = AITools.RunPrompt(datasourceprompt);
            Console.WriteLine("datasource: " + datasource);

            var questprompt = 
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n" +
                "This quest is about finding the location of this character\r\n\r\n" +
                "Keep it to one paragraph with newlines\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";
            questprompt += "Vital clue to their location: " + datasource + "\r\n";
            questprompt += "Spaceship holding the information: " + shipname + "\r\n";

            var questname = AITools.RunPrompt(questprompt);
            Console.WriteLine("questname: " + questname);


            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            
            //Log Entry
            var logprompt = 
            "Generate a short flavour text story which is an explaination on why the data needed to find this character is at this location.\r\n\r\n" +
            "Explain why the ship in this stage is guarding it and there connection with the bounty.\r\n\r\n" +
            
            "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Vital clue to there location: " + datasource + "\r\n";
            logprompt += "Spaceship guarding the information: " + shipname + "\r\n";
            logprompt += "Faction this ship belongs to: " + missionTemplate.parameter1 + "\r\n";
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

            newQuest.Objectives[0].DisplayText = "Recover the " + datasource + " from the " + shipname + " cargo hold";
            //set quest alias to self in scripts
            newQuest.VirtualMachineAdapter.Aliases[0].Property.Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

            //Set the gang members
            var questproperties = newQuest.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < questproperties.Count; i++)
            {
                if (questproperties[i].Name == "BountyTarget")
                {
                    ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
                }
                if (questproperties[i].Name == "GangMembers")
                {
                    ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = ShipTools.GetGangList(factionID);
                }
            }

            //Set the target ship
            ((IQuestReferenceAlias)Quest.Aliases[4]).CreateReferenceToObject.Object = ship.ToLink<IStarfieldMajorRecordGetter>();
            myMod.Quests.Add(newQuest);


            //Log Entry
            var booklogprompt =
            "Generate a short log which is a first person account from the target.\r\n\r\n" +
            "This log explains why the target is at the next location and is incrimidating.\r\n\r\n" +
            "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            booklogprompt += "Location:" + nextQuest.QuestLocation + "\r\n";
            booklogprompt += "Write in the style of a " + datasource + "\r\n";
            var booklogmessage = AITools.RunPrompt(booklogprompt);

            var Book = myMod.Books[new FormKey(myMod.ModKey, 0x000800)].DeepCopy();
            Book bountybook = new Book(myMod)
            {
                CNAM = Book.CNAM,
                Components = Book.Components,
                Description = booklogmessage,
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
                Name = datasource,
                ODTY = Book.ODTY,
                Value = Book.Value,
                Weight = Book.Weight,
                VirtualMachineAdapter = Book.VirtualMachineAdapter,
                Transforms = Book.Transforms,
            };

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };

            frmlst.Items.Add(bountybook);
            myMod.FormLists.Add(frmlst);

            //set  the  book to start the new quest
            ((ScriptObjectProperty)bountybook.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>();

            bountybook.ENAM = "Data Slate #" + questID;
            myMod.Books.Add(bountybook);

            
            // Death Items
            var questprops = newQuest.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < questprops.Count; i++)
            {
                if (questprops[i].Name == "DeathItems")
                {                    
                    ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = frmlst.ToLink<IStarfieldMajorRecordGetter>();
                }
            }            

            //Set the interfaces
            questform = newQuest;
            logMessage = logmessage;

            return Quest;
        }

    }
}
