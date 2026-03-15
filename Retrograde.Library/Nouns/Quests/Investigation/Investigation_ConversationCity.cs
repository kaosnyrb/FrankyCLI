using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Interfaces;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests
{
    public class Investigation_ConversationCity : IOutlawQuest
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
            Console.WriteLine("Generating Conversation City Quest...");

            string npcNameHint = missionTemplate.parameters != null && missionTemplate.parameters.TryGetValue("NpcNameHint", out var hint)
                ? (string)hint
                : "appropriate for someone living in a city";
            var npcResult = NPCTools.CreateRandomNpc(myMod,
                false,
                "The name should feel " + npcNameHint,
                isFriendly: true,
                factionId: FormKeyLookup.GetFormKey("PlayerAllyFaction"));
            var npc = npcResult.Npc;

            if (missionTemplate.parameters != null && missionTemplate.parameters.TryGetValue("Outfit", out var outfitParam))
                npc.SpaceOutfit = ((FormKey)outfitParam).ToNullableLink<IOutfitGetter>();

            var questname = QuestPrompts.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Targets name:" + npc.Name,
                "Location:" + missionTemplate.Location + "\r\n",
            });

            //Log Entry
            var logmessage = QuestPrompts.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Speack to " + npc.Name + " to lead you to " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("duout_onstagenext_quest", "currentquest", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_onstagenext_quest", "nextquest", nextQuest.questform.ToLink());
            newQuest.SetScriptProperty("duout_onstagenext_quest", "CompleteStage", 500);
            
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", npc.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetObjective(0, "Speack to " + npc.Name);
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", npc.ToLink<IStarfieldMajorRecordGetter>());

            //Place the NPC on a random city marker
            string label = missionTemplate.parameters != null && missionTemplate.parameters.TryGetValue("Label", out var labelParam)
                ? (string)labelParam
                : "";
            var markerused = RandomProvider.GetRandomMarker("doout_city_activator_marker_" + label + "_");
            newQuest.SetQuestReferenceAlias("BountyTargetMarker", markerused.FormKey);
            
            //Add conversation, ending the quest on stage 500
            string npcBackground = string.IsNullOrEmpty(missionTemplate.NpcBackground)
                ? ""
                : $"{npc.Name} — {missionTemplate.NpcBackground}";

            var dialogueScript = DialoguePrompts.GetDialogueScript(new List<string>(missionTemplate.Addons)
            {
                "NPC name: " + npc.Name,
                "Bounty target name: " + outlawNpc.name,
                "Location: " + missionTemplate.Location,
                "Intrigue detail: " + FlavourSeedData.GetConversationIntrigueDetail(),
            }, npcBackground: npcBackground);

            var voicePool    = npcResult.IsFemale ? VoiceSeedData.FemaleVoices : VoiceSeedData.MaleVoices;
            var elevenLabsId = voicePool[RandomProvider.Random.Next(voicePool.Count)].Id;

            var npcSuffix = newQuest.instance.FormKey.ID.ToString("X8");
            NPCDialogueNoun Dialogue = new NPCDialogueNoun(npc.FormKey, npcResult.VoiceEditorId, dialogueScript, npcSuffix, elevenLabsId, existingQuest: newQuest.instance, completionStage: 500, aliasName: "BountyTarget");

            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
