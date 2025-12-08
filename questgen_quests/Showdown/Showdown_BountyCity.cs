using DynamicData;
using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
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
            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);
            var logmessage = AITools.RunPrompt(logprompt);

            Console.WriteLine("logmessage: " + logmessage);
            var newQuest = new QuestNoun(missionTemplate.formid, questname);
            newQuest.SetLogMessage(0, 0, logmessage);

            newQuest.SetScriptProperty("duout_ground_bounty_quest", "DeathItems", myMod.FormLists[outlawNpc.deathItems].ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", outlawNpc.instance.ToLink<IStarfieldMajorRecordGetter>());
            
            var marker = RandomUtils.GetRandomMarker("doout_city_showdown_marker_" + missionTemplate.parameter1);
            newQuest.SetQuestReferenceAlias("BountyTargetMarker", marker.FormKey);

            var locaform = gen_quest_main._StarfieldMod.Locations[new FormKey(gen_quest_main.StarfieldModKey, missionTemplate.parameterformid)];
            newQuest.SetQuestLocationAlias("DungeonLocation", locaform.ToNullableLink<ILocationGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
