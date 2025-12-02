using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static Mutagen.Bethesda.FormKeys.Starfield.Starfield;

namespace FrankyCLI
{
    public class Investigation_ActivatorCity : IOutlawQuest
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
            Console.WriteLine("Generating Activator City Quest...");
            questloc = missionTemplate.Location;

            var questActivator = ActivatorTools.GetRandomGroundType();

            var datasourceprompt =
                "A three word or less digital file that contains a clue to the characters location.\r\n" +
                "The base type of the activator is." + questActivator.Name + "\r\n\r\n" +
                "Only include the data source name in the response.\r\n\r\n" +
                "This quest is about finding a lead on this character, this is the link to them.\r\n\r\n";
            var datasource = AITools.RunPrompt(datasourceprompt);
            Console.WriteLine("datasource: " + datasource);

            var questprompt =
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n" +
                "This quest is about finding the location of this character\r\n\r\n"+
                "Keep it to one paragraph with newlines\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";
            questprompt += "Vital clue to their location: " + datasource + "\r\n";

            var questname = AITools.RunPrompt(questprompt);
            Console.WriteLine("questname: " + questname);


            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            var logprompt = 
            "Generate a short flavour text story which is an explaination on why the data needed to find this character is at this location.\r\n\r\n" +
            "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Vital clue to there location: " + datasource + "\r\n";

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
            newQuest.VirtualMachineAdapter.Aliases[0].Property.Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

            //Set the enemy gang to the new gang
            var properties = newQuest.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < properties.Count; i++)
            {
                if (properties[i].Name == "BountyTarget")
                {
                    ((ScriptObjectProperty)properties[i]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
                }
            }

            //Find a target marker to use
            List<IMajorRecord> rec = new List<IMajorRecord>();
            foreach (var record in myMod.EnumerateMajorRecords())
            {
                if (record.EditorID != null)
                {
                    if (record.EditorID.Contains("doout_city_activator_marker_" + missionTemplate.parameter1))
                    {
                        rec.Add(record);
                    }
                }
            }

            Random rand = new Random();
            var markerused = rec[rand.Next(rec.Count)];
            ((IQuestReferenceAlias)Quest.Aliases[1]).ForcedReference.FormKey = markerused.FormKey;

            //Set Location            
            var locaform = gen_quest._StarfieldMod.Locations[new FormKey(gen_quest.StarfieldModKey, missionTemplate.parameterformid)];
            ((IQuestLocationAlias)Quest.Aliases[0]).SpecificLocation = locaform.ToNullableLink<ILocationGetter>();


            //Create the activation message
            var pickuppromt = 
            "Include newline characters in your response.\r\n" +
            "Generate a short flavour text story which explains to the player that they have found the location of the next stage via this clue.\r\n\r\n" +
            "The location must match the one that is provided below.\r\n\r\n" +
            "Keep it to one paragraph with newlines and under 50 words.\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";


            pickuppromt += "Location:" + nextQuest.QuestLocation + "\r\n";
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

            var ActivatorClone = myMod.Activators[new FormKey(myMod.ModKey, 0x000836)].DeepCopy();
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
            newActivator.Model.File = questActivator.Model;

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
            ((IQuestReferenceAlias)Quest.Aliases[3]).CreateReferenceToObject.Object = newActivator.ToLink<IStarfieldMajorRecordGetter>();
            myMod.Quests.Add(newQuest);
            
            //Set the interfaces
            questform = newQuest;
            logMessage = logmessage;

            return Quest;
        }

    }
}
