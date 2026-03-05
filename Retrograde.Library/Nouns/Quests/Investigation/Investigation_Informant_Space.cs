using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Utils;
using Mutagen.Bethesda;
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
using Retrograde.SpaceCellDesigns;
using Retrograde.Nouns.SpaceCells;

namespace Retrograde.Quests
{
    public class Investigation_Informant_Space : IOutlawQuest
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
            Console.WriteLine("Generating Informant Space Quest...");
            questloc = missionTemplate.Location;

            var factionID = ShipTools.GetFactionID((string)missionTemplate.parameters["Label"]);
            var datasource = ItemPrompts.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Type: Data tablet \r\n",

            });
            Console.WriteLine("datasource: " + datasource);

            string shipname = ShipTools.GetFactionShipName((string)missionTemplate.parameters["Label"]);
            Console.WriteLine("shipname: " + shipname);
            //var ship = new SpaceShipNoun(shipname, Convert.ToUInt32(missionTemplate.parameters["FormId"]), factionID);

            var questname = QuestPrompts.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n"
            });
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var logmessage = QuestPrompts.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Recover the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetObjective(0, "Recover the " + datasource + " from the " + shipname + " cargo hold");
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "GangMembers", ShipTools.GetGangList(factionID));

            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", RetrogradeContext.Current.StarfieldMod.GenericBaseForms[new FormKey(RetrogradeContext.Current.StarfieldModKey, Convert.ToUInt32(missionTemplate.parameters["FormId"]))].ToLink<IStarfieldMajorRecordGetter>());

            //newQuest.SetQuestReferenceCreateAlias("PrimaryRef", ship.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Log Entry
            var booklogmessage = NarrativePrompts.GetFirstPersonAccount(new List<string>(missionTemplate.Addons)
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
                "Recover the " + datasource + " to lead you to " + outlawNpc.name + "\r\n",
                "Spaceship holding the information: " + shipname + "\r\n",
            });

            var bountybook = new BookNoun("duout_book_test", datasource, booklogmessage);

            // Create a crew member NPC as the dataslate author / voice speaker.
            bool speakerIsFemale = RandomProvider.Random.Next(100) > 50;
            var speakerTemplate = NPCTools.FindTemplateDeadNpc(speakerIsFemale);
            Npc speakerNpc = NPCTools.CloneNPC(myMod, speakerTemplate);
            speakerNpc.Name = SeedManager.GenerateName(speakerIsFemale);
            speakerNpc.EditorID = "npc_crewlog_" + questID;
            var speakerVoice = NPCTools.GetVoice((string)missionTemplate.parameters["Label"], speakerIsFemale);
            string speakerVoiceEditorId = string.Empty;
            if (!speakerVoice.IsNull)
            {
                speakerNpc.Voice.SetTo(speakerVoice.FormKey);
                var vtRec = RetrogradeContext.Current.StarfieldMod.VoiceTypes.FirstOrDefault(v => v.FormKey == speakerVoice.FormKey);
                speakerVoiceEditorId = vtRec?.EditorID ?? speakerVoice.FormKey.ID.ToString("X6");
            }
            myMod.Npcs.Add(speakerNpc);

            var txVoicePool = speakerIsFemale ? SeedManager.FemaleVoices : SeedManager.MaleVoices;
            var txVoice = txVoicePool[RandomProvider.Random.Next(txVoicePool.Count)];
            SpeechTools.AddVoice(bountybook.instance.FormKey.ID, speakerNpc.FormKey, booklogmessage, speakerVoiceEditorId, txVoice.Id);

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };

            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);
            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_bounty_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());
            //Create the space cell
            if (missionTemplate.parameters.ContainsKey("SpaceCell"))
            {
                SpaceCellDesignType celldesign = (SpaceCellDesignType) missionTemplate.parameters["SpaceCell"];
                var noun = new SpaceCellNoun(questname.ToLower(), SpaceCellDesignRegistry.Designs[celldesign]());
                newQuest.SetQuestLevelledSpaceCellAlias(1, noun.LeveledSpaceCell.ToNullableLink());                
            }
            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
