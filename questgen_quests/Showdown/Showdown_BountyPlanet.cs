using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
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

            var questname = PromptManager.GetQuestName(new List<string>());
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var logmessage = PromptManager.GetLogMessage(new List<string>()
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Kill the Outlaw target " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);


            var newQuest = new QuestNoun(missionTemplate.formid, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "DeathItems", myMod.FormLists[outlawNpc.deathItems].ToLink<IStarfieldMajorRecordGetter>());

            // We set the PCM keyword to be the param. We've build a tree with a 1 to 1 mapping of keywords and POIs
            if (missionTemplate.parameterformid != 0)
            {
                newQuest.SetQuestPCMTypeKeyword("DungeonLocation", myMod.Keywords[new FormKey(myMod.ModKey, missionTemplate.parameterformid)].ToNullableLink<IKeywordGetter>());
            }

            newQuest.SetQuestReferenceCreateAlias("BountyTarget", outlawNpc.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Generate Voice for the log
            //SpeechTools.AddVoice(outlawNpc.Logfile.ID, newQuest.FormKey.ID, "THIS IS A TEST");
            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }
    }
}
