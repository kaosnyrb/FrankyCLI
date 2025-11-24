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

            var questprompt =
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n";
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
                DialogTopics = new ExtendedList<DialogTopic>(),
                VirtualMachineAdapter = Quest.VirtualMachineAdapter
            };

            newQuest.Stages[0].LogEntries[0].Entry = logmessage; //"I've found a dataslate containing the location of <Alias=BountyTarget>, who is hiding out at <Alias=DungeonLocation> on <Alias=TargetPlanet>. The Trackers Alliance will pay for taking out the bounty.";

            //set quest alias to self in scripts
            ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
            newQuest.VirtualMachineAdapter.Aliases[0].Property.Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

            var questprops = newQuest.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < questprops.Count; i++)
            {
                if (questprops[i].Name == "DeathItems")
                {                    
                    ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = myMod.FormLists[outlawNpc.deathItems].ToLink<IStarfieldMajorRecordGetter>();
                }
            }


            //Set the NPC to be the quest target
            ((IQuestReferenceAlias)Quest.Aliases[3]).CreateReferenceToObject.Object = outlawNpc.GeneratedNPC.ToLink<IStarfieldMajorRecordGetter>();
            myMod.Quests.Add(newQuest);

            //Generate Voice for the log
            //SpeechTools.AddVoice(outlawNpc.Logfile.ID, newQuest.FormKey.ID, "THIS IS A TEST");


            //Set the interfaces
            questform = newQuest;
            logMessage = logmessage;

            return newQuest;
        }
    }
}
