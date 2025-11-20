using DynamicData;
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

namespace FrankyCLI
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


        public Quest Setup(StarfieldMod myMod, OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest)
        {
            Console.WriteLine("Generating Bounty Planet Quest...");

            var questprompt =
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n" +
                "Use the following information to build the quest name:\r\n\r\n";

            questprompt += outlawNpc.name + "\r\n";
            questprompt += outlawNpc.background + "\r\n";

            var questname = AITools.RunPrompt(questprompt);
            Console.WriteLine("questname: " + questname);


            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            var logprompt = 
                "Include newline characters in your response.\r\n" +
                "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
                "Generate a short quest objective for the game Starfield on why this character is at this location.\r\n\r\n" +
                "It should explain why the character is at the location and that the player should kill them.\r\n\r\n" +            
                "Use the following information to build the explaination:\r\n\r\n";

            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Character background: " + outlawNpc.background + "\r\n";

            var logmessage = AITools.RunPrompt(logprompt);

            Console.WriteLine("logmessage: " + logmessage);

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

            //Find a target marker to use
            List<IMajorRecord> rec = new List<IMajorRecord>();
            foreach (var record in myMod.EnumerateMajorRecords())
            {
                if (record.EditorID != null)
                {
                    if (record.EditorID.Contains("doout_city_showdown_marker_" + missionTemplate.parameter1))
                    {
                        rec.Add(record);
                    }
                }
            }

            Random rand = new Random();
            ((IQuestReferenceAlias)Quest.Aliases[1]).ForcedReference.FormKey = rec[rand.Next(rec.Count)].FormKey;



            //Set the interfaces
            questform = newQuest;
            logMessage = logmessage;

            return newQuest;
        }
    }
}
