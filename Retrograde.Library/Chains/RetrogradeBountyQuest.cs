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
    public class RetrogradeBountyQuest : IQuestchain
    {
        //Debugging Tools
        public string ShowdownTemplate = "";
        public string DeepTempalte = "";
        public string InvestigationTemplate = "";
        public string DiscoveryTemplate = "";

        public RetrogradeBountyQuest()
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

            var questFormKey = FormKeyLookup.GetFormKey("rg_station_BountySpace01Far");

            MessageNoun stationnamemessage = new MessageNoun(FormKeyLookup.GetFormKey("RG_SE_Name").ID, stationname);
            stationnamemessage.instance.Name = stationname;

            var questname = "rg_mb_" + stationname;
            //Clone Quest
            var newQuest = new QuestNoun(questFormKey.ID, questname);
            //Set Aliases
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptAlias(1, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            //SEScript
            newQuest.SetScriptProperty("retrograde_bounty_quest", "BountyTargetNPC", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("retrograde_bounty_quest", "SpaceMapMarker", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_bounty_quest", "PlayerShip", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_bounty_quest", "PlayerStarSystemLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_bounty_quest", "PrimaryRef", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_bounty_quest", "Station", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("retrograde_bounty_quest", "TargetPlanetLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_bounty_quest", "TargetSystemLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            newQuest.SetScriptProperty("retrograde_bounty_quest", "GangMembers", ShipTools.GetGangList(ShipTools.GetFactionID(faction)));

            //Debugging
            newQuest.SetScriptProperty("retrograde_bounty_quest", "MinGangMembers", 0);
            newQuest.SetScriptProperty("retrograde_bounty_quest", "MaxGangMembers", 0);

            // POI Name
            //newQuest.SetScriptAliasScriptObject("DefaultAliasMapMarkerScript", "UnexploredName", stationnamemessage.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Move station to outside any danger areas
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetSpaceMarkerCondition());

            //Generate station
            StationNoun stationNoun = new StationNoun(stationname, faction, size, stationDesign);

            //Set station
            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", stationNoun.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the Cell so we can reset when we leave
            newQuest.SetScriptProperty("retrograde_bounty_quest", "StationCell", stationNoun.InteriorCell.ToLink<IStarfieldMajorRecordGetter>());

            //Add to POI tree
            var RG_MissionNodeBountySpace = targetMod.StoryManagerQuestNodes[FormKeyLookup.GetFormKey("RG_MissionNodeBountySpace")];

            RG_MissionNodeBountySpace.Quests.Add(new StoryManagerQuest()
            {
                Quest = newQuest.instance.ToNullableLink<IQuestGetter>(),
            });

            // Set the boss NPC

            //Create the boss

            //newQuest.SetQuestReferenceAlias("BountyNpc",stationNoun.InteriorCell.Temporary)

            return true;
        }
    }
}
