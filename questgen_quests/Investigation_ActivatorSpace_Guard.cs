using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class Investigation_ActivatorSpace_Guard : IOutlawQuest
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


        public Quest Setup(StarfieldMod myMod, OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest)
        {
            Console.WriteLine("Generating Activator Guarded Space Quest...");

            //Create the ship
            //var shipnameprompt = 
            //    "A three word or less spaceship name.\r\nOnly include the spaceship source name in the response.\r\n\r\n" +
            //    "Try and make the ship name iconic like the falcon or firefly.\r\n\r\n" +
            //    "Use the following information to build the Ship name:\r\n\r\n";
            //shipnameprompt += "Faction: " + "Ecliptic Mercenary Corp" + "\r\n";
            //shipnameprompt += "Ship Type: " + "An old cargo hauler that has seen better days." + "\r\n";

            var shipname = ShipTools.GetShipName();
            Console.WriteLine("shipname: " + shipname);
            var ship = ShipTools.GenShip(shipname, ShipTools.GetCargoShip(), 0x000AE4F3);

            //Create the datasource
            var datasourceprompt =
                "A three word or less space beacon name that contains a clue to the characters location. Examples are a Damaged comms sattelle or Scanning Beacon\r\nOnly include the data source name in the response.\r\n\r\n" +
                "This quest is about finding a lead on this character, this is the link to them.\r\n\r\n" +
                "Keep it to one paragraph with newlines\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";
            datasourceprompt += "Name: " + outlawNpc.name + "\r\n";
            datasourceprompt += "Background: " + outlawNpc.background + "\r\n";
            var datasource = AITools.RunPrompt(datasourceprompt);
            Console.WriteLine("datasource: " + datasource);

            var questprompt = 
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n" +
                "This quest is about finding the location of this character\r\n\r\n" +
                "Keep it to one paragraph with newlines\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";

            questprompt += "Name:" + outlawNpc.name + "\r\n";
            questprompt += "Background: " + outlawNpc.background + "\r\n";
            questprompt += "Vital clue to their location: " + datasource + "\r\n";
            questprompt += "Spaceship guarding the information: " + shipname + "\r\n";

            var questname = AITools.RunPrompt(questprompt);
            Console.WriteLine("questname: " + questname);


            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            
            //Log Entry
            var logprompt = 
            "Generate a short flavour text story which is an explaination on why the data needed to find this character is at this location.\r\n\r\n" +
            "Explain why the ship in this stage is gaurding it and there connection with the bounty.\r\n\r\n" +
            "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Character background: " + outlawNpc.background + "\r\n";
            logprompt += "Vital clue to there location: " + datasource + "\r\n";
            questprompt += "Spaceship guarding the information: " + shipname + "\r\n";
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
            //newQuest.VirtualMachineAdapter.Aliases[0].Property.Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

            //Set the guard ship
            ((IQuestReferenceAlias)Quest.Aliases[6]).CreateReferenceToObject.Object = ship.ToLink<IStarfieldMajorRecordGetter>();


            //Create the activation message
            var pickuppromt = 
            "Include newline characters in your response.\r\n" +
            "Generate a short flavour text story which explains to the player that they have found the location of the next stage via this clue.\r\n\r\n" +
            "Keep it to one paragraph with newlines and under 50 words.\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            pickuppromt += "Location:" + missionTemplate.Location + "\r\n";
            pickuppromt += "Character background: " + outlawNpc.background + "\r\n";
            pickuppromt += "Vital clue to there location: " + datasource + "\r\n";

            var pickupmessage = AITools.RunPrompt(pickuppromt);
            Console.WriteLine("pickupmessage: " + pickupmessage);

            var messageClone = myMod.Messages[new FormKey(myMod.ModKey, 0x000844)].DeepCopy();
            Message message = new Message(myMod)
            {
                Name = messageClone.Name,
                BNAM = messageClone.BNAM,
                EditorID = "message_" + questID,
                Description = pickupmessage,
                Flags = messageClone.Flags
            };

            myMod.Messages.Add(message);


            //Create the Activator

            var ActivatorClone = myMod.Activators[new FormKey(myMod.ModKey, 0x000901)].DeepCopy();
            Mutagen.Bethesda.Starfield.Activator newActivator = new Mutagen.Bethesda.Starfield.Activator(myMod)
            {
                ActivateSound = ActivatorClone.ActivateSound,
                Properties = ActivatorClone.Properties,
                VirtualMachineAdapter = ActivatorClone.VirtualMachineAdapter,
                ActivateTextOverride = ActivatorClone.ActivateTextOverride,
                ActivationAngle = ActivatorClone.ActivationAngle,
                Components = ActivatorClone.Components,
                Flags = ActivatorClone.Flags,
                Conditions = ActivatorClone.Conditions,
                EditorID = "activator_" + questID,
                Destructible = ActivatorClone.Destructible,
                Keywords = ActivatorClone.Keywords,
                Name = datasource,
                ObjectBounds = ActivatorClone.ObjectBounds,
                ODTY = ActivatorClone.ODTY,
                Model = ActivatorClone.Model,
                XALG = ActivatorClone.XALG
            };

            //Set the Current quest and next quest so when you use the activator it progresses the mission
            var activatorproperties = newActivator.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < activatorproperties.Count; i++)
            {
                if (activatorproperties[i].Name == "messagetext")
                {
                    ((ScriptObjectProperty)newActivator.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = message.ToLink<IStarfieldMajorRecordGetter>();
                }
                if (activatorproperties[i].Name == "currentquest")
                {
                    ((ScriptObjectProperty)newActivator.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
                }
                if (activatorproperties[i].Name == "nextquest")
                {
                    ((ScriptObjectProperty)newActivator.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>();
                }
            }

            myMod.Activators.Add(newActivator);

            //Set the Activator to be the quest target
            ((IQuestReferenceAlias)Quest.Aliases[4]).CreateReferenceToObject.Object = newActivator.ToLink<IStarfieldMajorRecordGetter>();
            myMod.Quests.Add(newQuest);

            //Set the interfaces
            questform = newQuest;
            logMessage = logmessage;

            return Quest;
        }

    }
}
