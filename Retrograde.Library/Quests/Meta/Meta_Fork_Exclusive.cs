using Retrograde.Nouns;
using Retrograde.Chains.Interfaces;
using Retrograde.Chains;
using Retrograde.Nouns;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests
{
    public class Meta_Fork_Exclusive : IOutlawQuest
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

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questID);

            //Quest 1
            var Quest1 = missionTemplate.Lib1.GetInvestigationMissionTemplate("", missionTemplate.Addons);
            Quest1.outlawQuest.Setup(myMod, outlawNpc, Quest1, nextQuest);

            //Quest 2
            var Quest2 = missionTemplate.Lib2.GetInvestigationMissionTemplate("", missionTemplate.Addons);            
            Quest2.outlawQuest.Setup(myMod, outlawNpc, Quest2, nextQuest);

            string choiceprompt =
                "You are writing an in-character quest journal entry for a spacefaring hunter at a branching point in their investigation.\r\n" +
                "The hunter has uncovered two different leads and must choose which one to follow next.\r\n\r\n" +

                "Write from the player's first-person perspective, as if they are thinking through their options in a private log.\r\n" +
                "Do not break the fourth wall. Do not mention 'quests', 'objectives', or game mechanics.\r\n" +
                "Keep the total output under 200 words.\r\n\r\n" +

                "Structure:\r\n" +
                "- Write two short paragraphs, one for each lead.\r\n" +
                "- Each paragraph should make that lead feel distinct in tone, risk, and potential payoff.\r\n" +
                "- Put a single blank line between the two paragraphs.\r\n\r\n" +

                "Use the Lore Context to shape factions, stakes, and atmosphere:\r\n" +
                "<LoreContext>\r\n" + AIRunner.LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Base the two leads on these briefing blurbs:\r\n" +
                "Lead 1:\r\n" + Quest1.outlawQuest.LogMessage + "\r\n\r\n" +
                "Lead 2:\r\n" + Quest2.outlawQuest.LogMessage + "\r\n";

            
            string description = AIRunner.RunPrompt(choiceprompt);

            var message = new MessageNoun(0x0008BA, description);

            string choice_one_prompt = "Convert this to a menu option saying you'll look into it. No more that 10 words.: " + Quest1.outlawQuest.LogMessage;
            string choice_two_prompt = "Convert this to a menu option saying you'll look into it. No more that 10 words.: " + Quest2.outlawQuest.LogMessage;

            var choice_one = AIRunner.RunPrompt(choice_one_prompt);
            var choice_two = AIRunner.RunPrompt(choice_two_prompt);

            message.SetChoice(0,choice_one);
            message.SetChoice(1,choice_two);

            newQuest.SetScriptProperty("duout_branching_quest", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_branching_quest", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_branching_quest", "nextquest_1", Quest1.outlawQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_branching_quest", "nextquest_2", Quest2.outlawQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = choiceprompt;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
