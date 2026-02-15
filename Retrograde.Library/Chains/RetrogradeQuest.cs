using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Nouns.Stations;
using Retrograde.StationDesigns;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
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
            newQuest.SetScriptProperty("retrograde_quest", "MinGangMembers", 0);
            newQuest.SetScriptProperty("retrograde_quest", "MaxGangMembers", 0);

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

            
            //Add to POI tree
            var rg_se_poi_node = targetMod.StoryManagerQuestNodes[FormKeyLookup.GetFormKey("RG_SE_POI_Node")];

            //Remove the template quest from the node
            if (rg_se_poi_node.Quests.Count > 0)
            {
                if (targetMod.Quests[rg_se_poi_node.Quests[0].Quest.FormKey].EditorID == "RG_station_quest")
                {
                    rg_se_poi_node.Quests.RemoveAt(0);
                }
            }

            rg_se_poi_node.Quests.Add(new StoryManagerQuest()
            {
                Quest = newQuest.instance.ToNullableLink<IQuestGetter>(),
            });

            return true;
        }
    }
}
