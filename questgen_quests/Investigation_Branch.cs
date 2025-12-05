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
    internal class Investigation_Branch : IOutlawQuest
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
            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            var Quest = myMod.Quests[new FormKey(myMod.ModKey, missionTemplate.formid)].DeepCopy();
            Quest newQuest = new Quest(myMod)
            {
                Name = questID,
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

            MissionLib missionLib = new MissionLib();

            //Quest 1
            var Quest1 = missionLib.GetInvestigationMissionTemplate("");
            Quest1.outlawQuest.Setup(myMod, outlawNpc, Quest1, nextQuest);

            //Quest 2
            var Quest2 = missionLib.GetInvestigationMissionTemplate("");
            Quest2.outlawQuest.Setup(myMod, outlawNpc, Quest2, nextQuest);

            string choiceprompt = "Generate a paragraph that explains that the player has a choice on which lead to follow. " +
                "The choices are: \r\n" +
                "1. " + Quest1.outlawQuest.LogMessage + " \r\n" +
                "2. " + Quest2.outlawQuest.LogMessage + " \r\n";
            string description = AITools.RunPrompt(choiceprompt);

            var messageClone = myMod.Messages[new FormKey(myMod.ModKey, 0x0008BA)].DeepCopy();
            Message message = new Message(myMod)
            {
                Name = messageClone.Name,
                BNAM = messageClone.BNAM,
                EditorID = "message_" + questID,
                Description = description,
                Flags = messageClone.Flags,
                MenuButtons = messageClone.MenuButtons,
            };

            string choice_one_prompt = "Convert this to a menu option saying you'll look into it. No more that 10 words.: " + Quest1.outlawQuest.LogMessage;
            string choice_two_prompt = "Convert this to a menu option saying you'll look into it. No more that 10 words.: " + Quest2.outlawQuest.LogMessage;


            var choice_one = AITools.RunPrompt(choice_one_prompt);
            var choice_two = AITools.RunPrompt(choice_two_prompt);

            message.MenuButtons[0].Text = choice_one;
            message.MenuButtons[1].Text = choice_two;


            myMod.Messages.Add(message);



            //Set the enemy gang to the new gang
            var properties = newQuest.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < properties.Count; i++)
            {
                if (properties[i].Name == "messagetext")
                {
                    ((ScriptObjectProperty)properties[i]).Object = message.ToLink<IStarfieldMajorRecordGetter>();
                }
                if (properties[i].Name == "currentquest")
                {
                    ((ScriptObjectProperty)properties[i]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
                }
                if (properties[i].Name == "nextquest_1")
                {
                    ((ScriptObjectProperty)properties[i]).Object = Quest1.outlawQuest.questform.ToLink<IStarfieldMajorRecordGetter>();
                }
                if (properties[i].Name == "nextquest_2")
                {
                    ((ScriptObjectProperty)properties[i]).Object = Quest2.outlawQuest.questform.ToLink<IStarfieldMajorRecordGetter>();
                }
            }
            myMod.Quests.Add(newQuest);

            //Set the interfaces
            questform = newQuest;
            logMessage = description;
            return newQuest;
        }
    }
}
