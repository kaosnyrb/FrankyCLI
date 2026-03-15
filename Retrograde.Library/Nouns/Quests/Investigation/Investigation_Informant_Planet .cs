using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.AI;
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
using Retrograde.Writing;

namespace Retrograde.Quests
{
    public class Investigation_Informant_Planet : IOutlawQuest
    {
        private Quest questform;
        private Book?    _book;
        private FormKey  _speakerFormKey;
        private string   _speakerVoiceEditorId = "";
        private string   _elevenLabsId = "";

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
            Console.WriteLine("Generating Informant Planet Quest...");

            if (missionTemplate.parameters != null)
            {
                if (missionTemplate.parameters.ContainsKey("ExtraLore"))
                {
                    missionTemplate.Addons.Add(missionTemplate.parameters["ExtraLore"].ToString());
                }
            }

            var questActivator = ActivatorSeedData.GetRandomLargeGroundType();

            var datasource = ItemPrompts.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Type: Data tablet \r\n",

            });
            Console.WriteLine("datasource: " + datasource);

            var questname = QuestPrompts.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Vital clue to their location:" + datasource,
                "Location:" + missionTemplate.Location + "\r\n",
            });

            Console.WriteLine("questname: " + questname);
            IGang outlawGang = GangManager.GetGang();
            
            //Log Entry
            var logmessage = QuestPrompts.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Find the " + datasource + " to lead you to " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            //newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "GangMembers", outlawGang.gangList.ToLink<IStarfieldMajorRecordGetter>());

            //Create Boss NPC
            bool isDead = missionTemplate.parameters != null
                && missionTemplate.parameters.ContainsKey("IsTargetDead")
                && (bool)missionTemplate.parameters["IsTargetDead"];
            var npcResult = NPCTools.CreateRandomNpc(myMod, isDead,
                "The name should feel appropriate for someone living and operating within a criminal gang culture—gritty, believable, and grounded.");
            var npc = npcResult.Npc;
            var isfemale = npcResult.IsFemale;
            var npcVoiceEditorId = npcResult.VoiceEditorId;
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", npc.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetObjective(0, "Read the " + datasource + " from " + npc.Name);

            //Book

            var booklogmessage = NarrativePrompts.GetFirstPersonAccount(new List<string>(missionTemplate.Addons)
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Log Entry should mention how this character has located the next clue on the target.\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
            });
            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            var bountybook = new BookNoun("duout_book_completeandstart", datasource, booklogmessage);
            bountybook.SetScriptProperty("duout_queststartandend", "questtoend", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            bountybook.SetScriptProperty("duout_queststartandend", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            // Voice the data-slate as a transmission left by the informant.
            // ExtraLore is already folded into booklogmessage via missionTemplate.Addons.
            var txVoicePool = isfemale ? VoiceSeedData.FemaleVoices : VoiceSeedData.MaleVoices;
            var txVoice = txVoicePool[RandomProvider.Random.Next(txVoicePool.Count)];
            _book                 = bountybook.instance;
            _speakerFormKey       = npc.FormKey;
            _speakerVoiceEditorId = npcVoiceEditorId;
            _elevenLabsId         = txVoice.Id;

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());

            
            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

        public void StageAudio()
        {
            if (_book != null)
                SpeechTools.AddVoice(_book.FormKey.ID, _speakerFormKey, _book.Text?.String ?? "", _speakerVoiceEditorId, _elevenLabsId);
        }

        public IEnumerable<IPolishable> GetPolishables()
        {
            if (questform != null)
                yield return new QuestLogPolishable(questform);
            if (_book != null)
                yield return new BookPolishable(_book);
        }

    }
}
