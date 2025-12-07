using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
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
using YamlDotNet.Core;

namespace FrankyCLI
{
    public class Investigation_ActivatorSetDungeon : IOutlawQuest
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
            Console.WriteLine("Generating Activator Planet Quest...");

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
                "This quest is about finding the location of this character\r\n\r\n";
            questprompt += "Vital clue to their location: " + datasource + "\r\n";

            var questname = AITools.RunPrompt(questprompt);
            Console.WriteLine("questname: " + questname);

            OutlawGang outlawGang = new OutlawGang(myMod);

            //Log Entry
            var logprompt = 
            "Generate a short flavour text story which is an explaination on why the data needed to find this character is at this location.\r\n\r\n" +
            "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            logprompt += "Location:" + missionTemplate.Location + "\r\n";
            logprompt += "Vital clue to there location: " + datasource + "\r\n";
            logprompt += "the title of the Gang members who are helping the target: " + outlawGang.gangName + "\r\n";
            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);
            var logmessage = AITools.RunPrompt(logprompt);

            Console.WriteLine(logmessage);
            var newQuest = new QuestInstance(missionTemplate.formid, questname);
            newQuest.SetScriptAlias(0, newQuest.quest.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.quest.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "GangMembers", outlawGang.gangList.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetLogMessage(0, 0, logmessage);


            // We set the PCM keyword to be the param. We've build a tree with a 1 to 1 mapping of keywords and POIs
            if (missionTemplate.parameterformid != 0)
            {
                newQuest.SetQuestPCMTypeKeyword("DungeonLocation", myMod.Keywords[new FormKey(myMod.ModKey, missionTemplate.parameterformid)].ToNullableLink<IKeywordGetter>());
            }

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
            var message = new MessageBox(0x000844, pickupmessage);

            //Create the Activator
            var newActivator = new OutlawActivator(0x000836, datasource, questActivator.Model);
            newActivator.SetScriptProperty("duout_activator_completenstart", "messagetext", message.message.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_completenstart", "currentquest", newQuest.quest.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_completenstart", "nextquest", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceCreateAlias("BountyTarget", newActivator.newActivator.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.quest;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.quest;
        }

    }
}
