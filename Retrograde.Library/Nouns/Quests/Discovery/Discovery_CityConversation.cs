using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Interfaces;
using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Quests
{
    public class Discovery_CityConversation : IOutlawQuest
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
            Console.WriteLine("Discovery Quest - City Conversation...");
            questloc = missionTemplate.Location;

            if (missionTemplate.parameters != null && missionTemplate.parameters.ContainsKey("ExtraLore"))
                missionTemplate.Addons.Add(missionTemplate.parameters["ExtraLore"].ToString());

            var npcResult = NPCTools.CreateRandomNpc(myMod,
                false,
                "The name should feel appropriate for a city-dweller who passes on information about criminals — a contact, informant, or bystander with something to share.",
                isFriendly: true,
                factionId: FormKeyLookup.GetFormKey("PlayerAllyFaction"));
            var npc = npcResult.Npc;

            var questname = QuestPrompts.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "NPC name: " + npc.Name,
                "Location: " + missionTemplate.Location,
            });

            var logmessage = QuestPrompts.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location: " + missionTemplate.Location,
                "Speak to " + npc.Name + " in " + missionTemplate.Location + " to get a lead on " + outlawNpc.name,
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("duout_onstagenext_quest", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_onstagenext_quest", "nextquest", nextQuest.questform.ToLink());
            newQuest.SetScriptProperty("duout_onstagenext_quest", "CompleteStage", 500);

            newQuest.SetQuestReferenceCreateAlias("BountyTarget", npc.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetObjective(0, "Speak to " + npc.Name + " in " + missionTemplate.Location);

            // Place NPC at a pre-placed city NPC marker
            var markerused = RandomProvider.GetRandomMarker("doout_city_npc_marker_" + (string)missionTemplate.parameters["Label"] + "_");
            newQuest.SetQuestReferenceAlias("BountyTargetMarker", markerused.FormKey);

            // Set city location alias
            var locaform = RetrogradeContext.Current.StarfieldMod.Locations[new FormKey(RetrogradeContext.Current.StarfieldModKey, Convert.ToUInt32(missionTemplate.parameters["FormId"]))];
            newQuest.SetQuestLocationAlias("DungeonLocation", locaform.ToNullableLink<ILocationGetter>());

            var dialogueScript = DialoguePrompts.GetDialogueScript(new List<string>(missionTemplate.Addons)
            {
                "NPC name: " + npc.Name,
                "Bounty target name: " + outlawNpc.name,
                "Location: " + missionTemplate.Location,
                "Intrigue detail: " + FlavourSeedData.GetConversationIntrigueDetail(),
            });

            var voicePool = npcResult.IsFemale ? VoiceSeedData.FemaleVoices : VoiceSeedData.MaleVoices;
            var elevenLabsId = voicePool[RandomProvider.Random.Next(voicePool.Count)].Id;
            var npcSuffix = newQuest.instance.FormKey.ID.ToString("X8");
            new NPCDialogueNoun(npc.FormKey, npcResult.VoiceEditorId, dialogueScript, npcSuffix, elevenLabsId, existingQuest: newQuest.instance, completionStage: 500, aliasName: "BountyTarget");

            questform = newQuest.instance;
            logMessage = logmessage;

            return null;
        }
    }
}
