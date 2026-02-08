using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
using Retrograde.Utils;
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

            if(missionTemplate.parameters != null)
            {
                if (missionTemplate.parameters.ContainsKey("ExtraLore"))
                {
                    missionTemplate.Addons.Add(missionTemplate.parameters["ExtraLore"].ToString());
                }
            }

            var questActivator = ActivatorTools.GetRandomGroundType();

            var datasource = PromptManager.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Activator Base Type:" + questActivator.Name,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("datasource: " + datasource);

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Vital clue to their location:" + datasource,
                "Location:" + missionTemplate.Location + "\r\n",
            });
            Console.WriteLine("questname: " + questname);

            // Quest
            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            var markerused = RandomUtils.GetRandomMarker("doout_city_activator_marker_" + missionTemplate.parameter1 + "_");
            newQuest.SetQuestReferenceAlias("BountyTargetMarker", markerused.FormKey);

            var locaform = gen_quest_main._StarfieldMod.Locations[new FormKey(gen_quest_main.StarfieldModKey, missionTemplate.parameterformid)];
            newQuest.SetQuestLocationAlias("DungeonLocation", locaform.ToNullableLink<ILocationGetter>());
            //Log Entry
            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Find the " + datasource + " to lead you to " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetObjective(0, "Locate the <Alias=BountyTarget> At " + missionTemplate.Location);

            //Create the activation message
            var pickupmessage = PromptManager.GetPickupMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + nextQuest.QuestLocation + "\r\n",
                "Vital clue to there location: " + datasource + "\r\n"
            });
            Console.WriteLine("pickupmessage: " + pickupmessage);
            var message = new MessageNoun(0x000844, pickupmessage);

            //Create the Activator
            var newActivator = new ActivatorNoun(0x000836, datasource, questActivator.Model);
            newActivator.SetScriptProperty("duout_activator_completenstart", "messagetext", message.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_completenstart", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newActivator.SetScriptProperty("duout_activator_completenstart", "nextquest", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceCreateAlias("BountyTarget", newActivator.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;

            return newQuest.instance;
        }

    }
}
