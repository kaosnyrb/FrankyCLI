using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Nouns.Stations;
using Retrograde.StationDesigns;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Chains
{
    public class RetrogradeQuest : IQuestchain
    {
        //Debugging Tools
        public string ShowdownTemplate = "";
        public string DeepTempalte = "";
        public string InvestigationTemplate = "";
        public string DiscoveryTemplate = "";

        public RetrogradeQuest()
        {
        }

        public bool GenerateQuest()
        {
            List<string> Factions = new List<string>()
            {
                "Crimsonfleet","Ecliptic","Varuun","Spacer"
            };

            var faction = Factions[RandomProvider.Random.Next(Factions.Count)];
            var size = "Large";
            IStationDesign stationDesign = new HabStation();
            var stationname = stationDesign.GenerateStationName(faction);

            return GenerateQuest(stationname, faction, size, stationDesign);
        }

        public bool GenerateQuest(string stationname, string faction, string size, IStationDesign stationDesign)
        {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var questFormKey = FormKeyLookup.GetFormKey("RG_station_quest");

            MessageNoun stationnamemessage = new MessageNoun(FormKeyLookup.GetFormKey("RG_SE_Name").ID, stationname);
            stationnamemessage.instance.Name = stationname;

            string stationsafename = System.Text.RegularExpressions.Regex.Replace(stationname.ToLower(), "[^a-z0-9]", "");

            var questname = "rg_poi_" + stationsafename;
            //Clone Quest
            var newQuest = new QuestNoun(questFormKey.ID, questname, questname);
            //Set Aliases
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            //SEScript
            newQuest.SetScriptProperty("retrograde_quest", "MapMarker", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_quest", "PlayerShip", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_quest", "Station", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_quest", "GangMembers", ShipTools.GetGangList(ShipTools.GetFactionID(faction)));

            //Debugging
            newQuest.SetScriptProperty("retrograde_quest", "MinGangMembers", 1);
            newQuest.SetScriptProperty("retrograde_quest", "MaxGangMembers", 3);

            // POI Name
            newQuest.SetScriptAliasScriptObject("DefaultAliasMapMarkerScript", "UnexploredName", stationnamemessage.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Move station to outside any danger areas
            newQuest.SetQuestReferenceSpaceLocationAlias("GeneralMarker05", SpaceCellTools.GetSpaceMarkerCondition());

            //Generate station
            StationNoun stationNoun = new StationNoun(stationname, faction, size, stationDesign);

            //Set station
            newQuest.SetQuestReferenceCreateAlias("Enemy01", stationNoun.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the Cell so we can reset when we leave
            newQuest.SetScriptProperty("retrograde_quest", "StationCell", stationNoun.InteriorCell.ToLink<IStarfieldMajorRecordGetter>());

            //Add any items to remove to a list if we need to. These are keys etc
            //Note we always do this to stop template dependainces.
            var frmlst = new FormList(targetMod)
            {
                EditorID = stationname + "_cleanupitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            for (int i = 0; i < stationNoun.dungeonState.ItemsToRemove.Count; i++)
            {
                frmlst.Items.Add(stationNoun.dungeonState.ItemsToRemove[i]);
            }
            targetMod.FormLists.Add(frmlst);
            newQuest.SetScriptProperty("retrograde_quest", "ItemsToRemove", frmlst.ToLink());

            //Add to POI tree
            var rg_se_poi_node = FindOrCreateNode(targetMod, "RG_SE_POI_Node");

            rg_se_poi_node.Quests.Add(new StoryManagerQuest()
            {
                Quest = newQuest.instance.ToNullableLink<IQuestGetter>(),
            });

            return true;
        }

        private static StoryManagerQuestNode FindOrCreateNode(StarfieldMod targetMod, string editorId)
        {
            var existing = targetMod.StoryManagerQuestNodes.FirstOrDefault(r => r.EditorID == editorId);
            if (existing != null) return existing;
            foreach (var tm in RetrogradeContext.Current.TemplateMods)
            {
                var src = tm.StoryManagerQuestNodes.FirstOrDefault(r => r.EditorID == editorId);
                if (src != null)
                {
                    var node = new StoryManagerQuestNode(targetMod)
                    {
                        EditorID = editorId,
                        Parent = src.Parent.FormKey.ToNullableLink<IAStoryManagerNodeGetter>(),
                        PreviousSibling = src.PreviousSibling.FormKey.ToNullableLink<IAStoryManagerNodeGetter>(),
                        Flags = src.Flags,
                        QuestFlags = src.QuestFlags,
                        MaxConcurrentQuests = src.MaxConcurrentQuests,
                        MaxNumQuestsToRun = src.MaxNumQuestsToRun,
                        HoursUntilReset = src.HoursUntilReset,
                    };
                    foreach (var c in src.Conditions)
                        node.Conditions.Add(c.DeepCopy());
                    targetMod.StoryManagerQuestNodes.Add(node);
                    return node;
                }
            }
            throw new KeyNotFoundException($"RetrogradeQuest: StoryManagerQuestNode '{editorId}' not found in any template mod.");
        }
    }
}
