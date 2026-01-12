using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
using FrankyCLI.Utils;
using GameFinder.Common;
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

namespace FrankyCLI.questgen_tools
{
    public class RetrogradeQuest : IQuestchain
    {
        public StarfieldMod myMod;

        //Debugging Tools
        public string ShowdownTemplate = "";
        public string DeepTempalte = "";
        public string InvestigationTemplate = "";
        public string DiscoveryTemplate = "";

        public RetrogradeQuest(StarfieldMod myModparam) {
            myMod = myModparam;
        }

        public bool GenerateQuest()
        {
            //Retrograde creates a Space POI that is randomly discovered.
            //We only have one quest

            //                "Spacer":
           //     "Ecliptic":
            // "Crimsonfleet":
           //"Varuun":

            List<string> Factions = new List<string>()
            {
                "Crimsonfleet","Ecliptic","Varuun","Spacer"
            };
            List<string> Sizes = new List<string>()
            {
                "Small","Medium","Large"
            };
            var missionTemplate = new MissionTemplate()
            {
                formid = FormKeyLookup.GetFormKey("RG_station_quest"),
                parameters = new Dictionary<string, object>
                {
                    {"Faction",Factions[RandomUtils.random.Next(Factions.Count)] },
                    {"StationSize",Sizes[RandomUtils.random.Next(Sizes.Count)] },
                }
            };

            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            var stationname = StationNoun.GenerateStationName(missionTemplate.parameters["Faction"].ToString());

            MessageNoun stationnamemessage = new MessageNoun(FormKeyLookup.GetFormKey("RG_SE_Name").ID, stationname);
            stationnamemessage.instance.Name = stationname;

            var questname = "rg_poi_" + stationname;
            //Clone Quest
            var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
            //Set Aliases
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptAlias(1, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptAlias(2, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptAlias(3, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptAlias(4, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            //SEScript
            newQuest.SetScriptProperty("SEScript", "HailingShip", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("SEScript", "MapMarker", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("SEScript", "OrbitLocation", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("SEScript", "PlayerShip", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            //Script Fragments
            newQuest.SetScriptFragmentAlias("Alias_Enemy01", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptFragmentAlias("Alias_Enemy02", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptFragmentAlias("Alias_Enemy03", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptFragmentAlias("Alias_Enemy04", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

            // POI Name
            newQuest.SetScriptAliasScriptObject("DefaultAliasMapMarkerScript", "UnexploredName", stationnamemessage.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Move station to outside any danger areas
            newQuest.SetQuestReferenceSpaceLocationAlias("GeneralMarker05", SpaceCellTools.GetSafeSpaceMarkerCondition());

            //Generate station
            StationNoun stationNoun = new StationNoun(stationname, missionTemplate.parameters["Faction"].ToString(), missionTemplate.parameters["StationSize"].ToString());

            //Set station
            newQuest.SetQuestReferenceCreateAlias("Enemy01", stationNoun.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Setthe enemy ships

            var lvlship = ShipTools.GetFactionShipChance(missionTemplate.parameters["Faction"].ToString());

            newQuest.SetQuestReferenceCreateAlias("Enemy02", lvlship.ToLink());
            newQuest.SetQuestReferenceCreateAlias("Enemy03", lvlship.ToLink());
            newQuest.SetQuestReferenceCreateAlias("Enemy04", lvlship.ToLink());


            //Add to POI tree
            var rg_se_poi_node = myMod.StoryManagerQuestNodes[FormKeyLookup.GetFormKey("RG_SE_POI_Node")];
            rg_se_poi_node.Quests.Add(new StoryManagerQuest()
            {
                Quest = newQuest.instance.ToNullableLink<IQuestGetter>(),
            });

            return true;
        }
    }
}