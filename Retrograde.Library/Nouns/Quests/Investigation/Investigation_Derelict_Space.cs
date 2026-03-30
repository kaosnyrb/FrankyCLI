using Retrograde.Nouns;
using Retrograde.AI.Utils;
using Retrograde.Nouns.Crew;
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
using Retrograde.Writing;
using Retrograde.SpaceCellDesigns;
using Retrograde.Nouns.SpaceCells;

namespace Retrograde.Quests
{
    public class Investigation_Derelict_Space : IOutlawQuest
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
            Console.WriteLine("Generating Informant Space Quest...");

            var factionID = ShipSeedData.GetFactionID((string)missionTemplate.parameters["Label"]);
            string shipname = ShipSeedData.GetFactionShipName((string)missionTemplate.parameters["Label"]);
            Console.WriteLine("shipname: " + shipname);
            //var ship = new SpaceShipNoun(shipname, Convert.ToUInt32(missionTemplate.parameters["FormId"]), factionID);

            var datasource = ItemMadlibs.GetActivatorName();
            Console.WriteLine("datasource: " + datasource);

            var questname = QuestMadlibs.GetQuestName(outlawNpc, missionTemplate, shipname);
            Console.WriteLine("questname: " + questname);

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Log Entry
            var logmessage = QuestMadlibs.GetLogMessage(outlawNpc, missionTemplate, shipname);
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetObjective(0, "Recover the " + datasource + " from the " + shipname);
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the script values

            newQuest.SetScriptProperty("duout_space_derelict_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "EnemyShipInteriorLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "CrewSpawnMarkers", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "ItemSpawnMarkers", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            
            var (corpses, crewSpeaker, crewIsFemale) = CrewManager.GetCrew((string)missionTemplate.parameters["Label"], shipname);
            newQuest.SetScriptProperty("duout_space_derelict_quest", "Corpses", corpses);

            newQuest.SetScriptProperty("duout_space_derelict_quest", "GangMembers", ShipSeedData.GetGangList(factionID));

            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", RetrogradeContext.Current.StarfieldMod.GenericBaseForms[new FormKey(RetrogradeContext.Current.StarfieldModKey, Convert.ToUInt32(missionTemplate.parameters["FormId"]))].ToLink<IStarfieldMajorRecordGetter>());

            //newQuest.SetQuestReferenceCreateAlias("PrimaryRef", ship.instance.ToLink<IStarfieldMajorRecordGetter>());


            var booklogmessage = NarrativePrompts.GetTransmission(new List<string>(missionTemplate.Addons)
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
                "Derelict Spaceship: " + shipname + "\r\n",
                "Faction this ship belongs to: " + (string)missionTemplate.parameters["Label"] + "\r\n"
            });
            var bountybook = new BookNoun("duout_book_test", datasource, booklogmessage);

            // Voice the data-slate as a transmission left on the derelict ship.
            // Speaker is the last crew NPC (the one that carries the log entry).
            var txVoicePool = crewIsFemale ? VoiceSeedData.FemaleVoices : VoiceSeedData.MaleVoices;
            var txVoice = txVoicePool[RandomProvider.Random.Next(txVoicePool.Count)];
            var crewVoice = NPCTools.GetVoice((string)missionTemplate.parameters["Label"], crewIsFemale);
            string crewVoiceEditorId = string.Empty;
            if (!crewVoice.IsNull)
            {
                crewSpeaker.Voice.SetTo(crewVoice.FormKey);
                var vtRec = RetrogradeContext.Current.StarfieldMod.VoiceTypes.FirstOrDefault(v => v.FormKey == crewVoice.FormKey);
                crewVoiceEditorId = vtRec?.EditorID ?? crewVoice.FormKey.ID.ToString("X6");
            }
            _book                 = bountybook.instance;
            _speakerFormKey       = crewSpeaker.FormKey;
            _speakerVoiceEditorId = crewVoiceEditorId;
            _elevenLabsId         = txVoice.Id;

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);

            bountybook.SetScriptProperty("duout_queststart", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_space_derelict_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());
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
