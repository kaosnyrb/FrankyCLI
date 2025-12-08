using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
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
    internal class Meta_Fork_Exclusive : IOutlawQuest
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

            var newQuest = new QuestNoun(missionTemplate.formid, questID);

            //Quest 1
            var Quest1 = missionTemplate.Lib1.GetInvestigationMissionTemplate("");
            Quest1.outlawQuest.Setup(myMod, outlawNpc, Quest1, nextQuest);

            //Quest 2
            var Quest2 = missionTemplate.Lib2.GetInvestigationMissionTemplate("");
            Quest2.outlawQuest.Setup(myMod, outlawNpc, Quest2, nextQuest);

            string choiceprompt = "Generate a paragraph that explains that the player has a choice on which lead to follow. " +
                "The player will only do one of these missions." +
                "Write this from the players point of view and don't break the fourth wall." +
                "The choices are: \r\n" +
                "1. " + Quest1.outlawQuest.LogMessage + " \r\n" +
                "2. " + Quest2.outlawQuest.LogMessage + " \r\n";
            string description = AITools.RunPrompt(choiceprompt);

            var message = new MessageNoun(0x0008BA, description);

            string choice_one_prompt = "Convert this to a menu option saying you'll look into it. No more that 10 words.: " + Quest1.outlawQuest.LogMessage;
            string choice_two_prompt = "Convert this to a menu option saying you'll look into it. No more that 10 words.: " + Quest2.outlawQuest.LogMessage;

            var choice_one = AITools.RunPrompt(choice_one_prompt);
            var choice_two = AITools.RunPrompt(choice_two_prompt);

            message.SetChoice(0,choice_one);
            message.SetChoice(1,choice_two);

            newQuest.SetScriptProperty("duout_branching_quest", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_branching_quest", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_branching_quest", "nextquest_1", Quest1.outlawQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_branching_quest", "nextquest_2", Quest2.outlawQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = "";
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
