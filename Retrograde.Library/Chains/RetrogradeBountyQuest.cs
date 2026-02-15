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

            int key = RandomProvider.Random.Next(StationDesignRegistry.Designs.Keys.Count());
            IStationDesign random = StationDesignRegistry.Designs.Values.ToList()[key]();
            IStationDesign stationDesign = random;


            //IStationDesign stationDesign = new HabStation();


            var stationname = stationDesign.GenerateStationName(faction);

            return GenerateQuest(stationname, faction, size, stationDesign);
        }

        public bool GenerateQuest(string stationname, string faction, string size, IStationDesign stationDesign)
        {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var questFormKey = FormKeyLookup.GetFormKey("rg_station_BountySpace01Far");

            MessageNoun stationnamemessage = new MessageNoun(FormKeyLookup.GetFormKey("RG_SE_Name").ID, stationname);
            stationnamemessage.instance.Name = stationname;

            string stationsafename = System.Text.RegularExpressions.Regex.Replace(stationname.ToLower(), "[^a-z0-9]", "");

            var questeditorid = "rg_mb_" + stationsafename;
            //Clone Quest
            string QuestName = GenerateQuestName(faction);
            Console.WriteLine(QuestName);
            var newQuest = new QuestNoun(questFormKey.ID, QuestName, questeditorid);

            //Set Aliases
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptAlias(1, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            //SEScript

            newQuest.SetScriptPropertyStruct("defaultquestchangelocationscript", "ChangeLocationStages", "targetLocationAlias",newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());

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
            newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", SpaceCellTools.GetBountySpaceMarkerCondition());

            //Generate station
            StationNoun stationNoun = new StationNoun(stationname, faction, size, stationDesign);

            //Set station
            newQuest.SetQuestReferenceCreateAlias("PrimaryRef", stationNoun.instance.ToLink<IStarfieldMajorRecordGetter>());

            //Set the Cell so we can reset when we leave
            newQuest.SetScriptProperty("retrograde_bounty_quest", "StationCell", stationNoun.InteriorCell.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("retrograde_bounty_quest", "StationInteriorLocation", stationNoun.dungeonState.location.ToLink());

            //Add to POI tree
            var RG_MissionNodeBountySpace = targetMod.StoryManagerQuestNodes[FormKeyLookup.GetFormKey("RG_MissionNodeBountySpace")];

            RG_MissionNodeBountySpace.Quests.Add(new StoryManagerQuest()
            {
                Quest = newQuest.instance.ToNullableLink<IQuestGetter>(),
            });

            // Set the boss NPC

            newQuest.SetQuestReferenceAlias("BountyNpc", stationNoun.dungeonState.BossPlacedNpc.FormKey);

            //Add any items to remove to a list if we need to. These are keys etc
            if (stationNoun.dungeonState.ItemsToRemove.Count > 0 )
            {
                var frmlst = new FormList(targetMod)
                {
                    EditorID = stationname + "_cleanupitems",
                    Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
                };
                for(int i = 0; i < stationNoun.dungeonState.ItemsToRemove.Count; i++)
                {
                    frmlst.Items.Add(stationNoun.dungeonState.ItemsToRemove[i]);
                }
                targetMod.FormLists.Add(frmlst);
                newQuest.SetScriptProperty("retrograde_bounty_quest", "ItemsToRemove", frmlst.ToLink());
            }

            newQuest.instance.MissionBoardDescription = GenerateMissionDescription(faction, stationDesign);

            return true;
        }

        private string GenerateQuestName(string faction)
        {
            var names = GetFactionQuestNames(faction);
            return names[RandomProvider.Random.Next(names.Count)];
        }

        private List<string> GetFactionQuestNames(string faction)
        {
            switch (faction)
            {
                case "Crimsonfleet":
                    return new List<string>
                    {
                        "Eliminate <Alias=BountyNpc> on <Alias=PrimaryRef>",
                        "Hunt <Alias=BountyNpc> at <Alias=PrimaryRef>",
                        "Pirate Bounty: <Alias=BountyNpc>",
                        "End <Alias=BountyNpc>'s Pirate Reign",
                        "Clear Pirates from <Alias=PrimaryRef>",
                        "Crimson Fleet: <Alias=BountyNpc>",
                        "Take Down <Alias=BountyNpc>",
                        "Raid on <Alias=PrimaryRef>",
                        "Bounty: <Alias=BountyNpc>, Crimson Fleet",
                        "Scuttle <Alias=BountyNpc>'s Operation",
                    };
                case "Ecliptic":
                    return new List<string>
                    {
                        "Eliminate <Alias=BountyNpc> on <Alias=PrimaryRef>",
                        "Hunt <Alias=BountyNpc> at <Alias=PrimaryRef>",
                        "Mercenary Bounty: <Alias=BountyNpc>",
                        "Neutralize <Alias=BountyNpc>'s Ecliptic Cell",
                        "Breach <Alias=PrimaryRef>'s Defenses",
                        "Ecliptic Target: <Alias=BountyNpc>",
                        "Dismantle <Alias=BountyNpc>'s Operation",
                        "Assault on <Alias=PrimaryRef>",
                        "Bounty: <Alias=BountyNpc>, Ecliptic",
                        "Strike Against <Alias=BountyNpc>",
                    };
                case "Varuun":
                    return new List<string>
                    {
                        "Eliminate <Alias=BountyNpc> on <Alias=PrimaryRef>",
                        "Hunt <Alias=BountyNpc> at <Alias=PrimaryRef>",
                        "Zealot Bounty: <Alias=BountyNpc>",
                        "Purge <Alias=BountyNpc>'s Va'ruun Cult",
                        "Silence <Alias=PrimaryRef>'s Fanatics",
                        "Va'ruun Target: <Alias=BountyNpc>",
                        "End <Alias=BountyNpc>'s Crusade",
                        "Cleanse <Alias=PrimaryRef>",
                        "Bounty: <Alias=BountyNpc>, Va'ruun",
                        "Serpent's End: <Alias=BountyNpc>",
                    };
                case "Spacer":
                default:
                    return new List<string>
                    {
                        "Eliminate <Alias=BountyNpc> on <Alias=PrimaryRef>",
                        "Hunt <Alias=BountyNpc> at <Alias=PrimaryRef>",
                        "Spacer Bounty: <Alias=BountyNpc>",
                        "Clear <Alias=BountyNpc>'s Gang",
                        "Evict Spacers from <Alias=PrimaryRef>",
                        "Spacer Target: <Alias=BountyNpc>",
                        "Take Out <Alias=BountyNpc>",
                        "Sweep <Alias=PrimaryRef>",
                        "Bounty: <Alias=BountyNpc>, Spacer",
                        "Deal With <Alias=BountyNpc>",
                    };
            }
        }

        private string GenerateMissionDescription(string faction, IStationDesign stationDesign)
        {
            var descriptions = GetFactionStationDescriptions(faction, stationDesign);
            return descriptions[RandomProvider.Random.Next(descriptions.Count)];
        }

        private List<string> GetFactionStationDescriptions(string faction, IStationDesign stationDesign)
        {
            bool isHab = stationDesign is HabStation;
            bool isOre = stationDesign is OreStation;
            // Warren or fallback

            switch (faction)
            {
                case "Crimsonfleet":
                    if (isHab)
                        return new List<string>
                        {
                            "<Alias=BountyNpc> has turned <Alias=PrimaryRef> into a pirate haven in the <Alias=TargetSystemLocation> system. Clear them out before more ships go missing.",
                            "Crimson Fleet pirates under <Alias=BountyNpc> are using <Alias=PrimaryRef> as a base to raid shipping lanes. Board the station and eliminate their leader.",
                            "<Alias=BountyNpc> has been running a smuggling ring from <Alias=PrimaryRef>. Put an end to their operation in the <Alias=TargetSystemLocation> system.",
                            "Residents of <Alias=PrimaryRef> are being forced to fence stolen goods by <Alias=BountyNpc>. Free the station from Crimson Fleet control.",
                            "<Alias=BountyNpc> is harboring wanted pirates aboard <Alias=PrimaryRef> in the <Alias=TargetSystemLocation> system. Storm the habitat decks and bring them to justice.",
                            "Crimson Fleet operatives under <Alias=BountyNpc> have turned <Alias=PrimaryRef> into a black market hub. Shut it down.",
                            "Distress calls from <Alias=PrimaryRef> confirm <Alias=BountyNpc> has taken the crew hostage. Board the station and end the pirate occupation.",
                            "Intelligence reports link <Alias=BountyNpc> on <Alias=PrimaryRef> to a string of freighter hijackings in the <Alias=TargetSystemLocation> system. Eliminate the threat.",
                            "<Alias=BountyNpc> is recruiting new Crimson Fleet members from the population of <Alias=PrimaryRef>. Put a stop to it before their numbers grow.",
                            "Merchants are refusing to enter the <Alias=TargetSystemLocation> system while <Alias=BountyNpc> operates from <Alias=PrimaryRef>. Remove them from the station.",
                        };
                    if (isOre)
                        return new List<string>
                        {
                            "<Alias=BountyNpc> seized a mining station at <Alias=PrimaryRef> and is extorting ore shipments in the <Alias=TargetSystemLocation> system. Take them down.",
                            "Crimson Fleet raiders led by <Alias=BountyNpc> have hijacked <Alias=PrimaryRef> and are stripping its ore reserves. Board the station and stop them.",
                            "<Alias=BountyNpc> is using the mining rigs on <Alias=PrimaryRef> to refine stolen cargo. End their salvage operation.",
                            "Ore shipments from <Alias=PrimaryRef> have stopped since <Alias=BountyNpc> and the Crimson Fleet moved in. Retake the facility.",
                            "<Alias=BountyNpc> is selling stolen minerals from <Alias=PrimaryRef> to fund Crimson Fleet raids across the <Alias=TargetSystemLocation> system. Cut off their income.",
                            "The mining crews at <Alias=PrimaryRef> are being worked to death by <Alias=BountyNpc>. Board the station and liberate the workers.",
                            "Crimson Fleet smugglers under <Alias=BountyNpc> are hiding contraband in the ore holds of <Alias=PrimaryRef>. Seize the station and confiscate the goods.",
                            "<Alias=BountyNpc> has rigged the refinery on <Alias=PrimaryRef> to process stolen ship components. Shut down their chop shop in the <Alias=TargetSystemLocation> system.",
                            "Pirates led by <Alias=BountyNpc> are using <Alias=PrimaryRef> to strip captured vessels for parts. Board the mining platform and stop them.",
                            "<Alias=BountyNpc> has claimed <Alias=PrimaryRef> and is charging a toll on every mining hauler in the <Alias=TargetSystemLocation> system. End their racket.",
                        };
                    return new List<string>
                    {
                        "<Alias=BountyNpc> has fortified a derelict station at <Alias=PrimaryRef> for the Crimson Fleet. Breach the corridors and take them out.",
                        "Pirates under <Alias=BountyNpc> are holed up in the tight passages of <Alias=PrimaryRef>. Flush them out before they ambush more traders.",
                        "<Alias=BountyNpc> has rigged <Alias=PrimaryRef> into a pirate stronghold deep in the <Alias=TargetSystemLocation> system. Infiltrate and neutralize them.",
                        "Crimson Fleet scouts report to <Alias=BountyNpc> from the hidden decks of <Alias=PrimaryRef>. Find them in the wreckage and eliminate them.",
                        "<Alias=BountyNpc> has booby-trapped the corridors of <Alias=PrimaryRef> to protect a Crimson Fleet stash. Fight through and take them out.",
                        "A Crimson Fleet crew under <Alias=BountyNpc> is hiding in the gutted sections of <Alias=PrimaryRef>. Root them out of the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> is using the maze-like layout of <Alias=PrimaryRef> to stage ambushes on passing ships. Board the station and end the threat.",
                        "Stolen cargo has been traced to <Alias=PrimaryRef> where <Alias=BountyNpc> runs a Crimson Fleet fence operation. Recover what you can and eliminate them.",
                        "The wreck of <Alias=PrimaryRef> is crawling with Crimson Fleet raiders loyal to <Alias=BountyNpc>. Clear every corridor in the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> has turned the tangled passages of <Alias=PrimaryRef> into a death trap for bounty hunters. Prove them wrong.",
                    };

                case "Ecliptic":
                    if (isHab)
                        return new List<string>
                        {
                            "Ecliptic mercenaries under <Alias=BountyNpc> have occupied <Alias=PrimaryRef> and are holding its crew hostage. Move in and secure the station.",
                            "<Alias=BountyNpc> is running an Ecliptic command post from <Alias=PrimaryRef>. Eliminate their leadership before they launch further attacks.",
                            "An Ecliptic cell led by <Alias=BountyNpc> has seized <Alias=PrimaryRef> in the <Alias=TargetSystemLocation> system. Board the station and restore order.",
                            "<Alias=BountyNpc> has fortified <Alias=PrimaryRef> as an Ecliptic forward base. Dismantle their operation before reinforcements arrive.",
                            "Ecliptic enforcers loyal to <Alias=BountyNpc> are shaking down the residents of <Alias=PrimaryRef>. Liberate the station in the <Alias=TargetSystemLocation> system.",
                            "<Alias=BountyNpc> is coordinating Ecliptic strikes from the living quarters of <Alias=PrimaryRef>. Board the station and cut off their command structure.",
                            "The crew of <Alias=PrimaryRef> sent a distress signal before <Alias=BountyNpc> jammed their comms. Move in and rescue the survivors.",
                            "Ecliptic operatives under <Alias=BountyNpc> have converted <Alias=PrimaryRef> into a recruitment center. Shut it down before they grow stronger.",
                            "<Alias=BountyNpc> is holding <Alias=PrimaryRef> as leverage in a ransom scheme targeting the <Alias=TargetSystemLocation> system. Neutralize them.",
                            "Reports indicate <Alias=BountyNpc> is stockpiling weapons aboard <Alias=PrimaryRef> for an Ecliptic offensive. Intervene now.",
                        };
                    if (isOre)
                        return new List<string>
                        {
                            "<Alias=BountyNpc> has contracted Ecliptic mercs to hold <Alias=PrimaryRef> and control its mining output. Break their grip on the facility.",
                            "Ecliptic forces under <Alias=BountyNpc> are stripping <Alias=PrimaryRef> for military resources. Shut down their operation in the <Alias=TargetSystemLocation> system.",
                            "<Alias=BountyNpc> is using <Alias=PrimaryRef> as an Ecliptic weapons depot. Board the mining station and put an end to it.",
                            "Ecliptic engineers under <Alias=BountyNpc> are retrofitting <Alias=PrimaryRef> to manufacture armaments. Destroy their production line.",
                            "Raw materials from <Alias=PrimaryRef> are being diverted to Ecliptic shipyards by <Alias=BountyNpc>. Seize the station and cut the supply chain.",
                            "<Alias=BountyNpc> is running an illegal smelting operation on <Alias=PrimaryRef> to arm Ecliptic squads. Board the facility and shut it down.",
                            "The refinery at <Alias=PrimaryRef> is producing military-grade alloys for <Alias=BountyNpc> and the Ecliptic. Reclaim the station.",
                            "<Alias=BountyNpc> has turned <Alias=PrimaryRef> into an Ecliptic supply depot in the <Alias=TargetSystemLocation> system. Sever their logistics network.",
                        };
                    return new List<string>
                    {
                        "<Alias=BountyNpc> has dug Ecliptic forces into the corridors of <Alias=PrimaryRef>. Clear them out before they can regroup.",
                        "Ecliptic soldiers led by <Alias=BountyNpc> are using the cramped passages of <Alias=PrimaryRef> as a staging ground. Move in and take them out.",
                        "<Alias=BountyNpc> has turned <Alias=PrimaryRef> into an Ecliptic bunker in the <Alias=TargetSystemLocation> system. Storm the station and end their campaign.",
                        "Ecliptic scouts operating from <Alias=PrimaryRef> are reporting ship movements to <Alias=BountyNpc>. Silence their outpost in the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> has set up kill zones throughout <Alias=PrimaryRef> for Ecliptic ambush teams. Push through and eliminate their commander.",
                        "An Ecliptic garrison under <Alias=BountyNpc> has entrenched in the narrow corridors of <Alias=PrimaryRef>. Dislodge them before they expand.",
                        "<Alias=BountyNpc> is running hit-and-fade operations from the wreckage of <Alias=PrimaryRef>. Track them down and finish the job.",
                        "Ecliptic holdouts loyal to <Alias=BountyNpc> refuse to abandon <Alias=PrimaryRef>. Sweep the station and clear them out.",
                        "Intercepted comms reveal <Alias=BountyNpc> is planning an Ecliptic strike from <Alias=PrimaryRef>. Board the station and stop them cold.",
                        "<Alias=BountyNpc> has wired <Alias=PrimaryRef> with defensive turrets for the Ecliptic. Fight through the corridors and take them down.",
                    };

                case "Varuun":
                    if (isHab)
                        return new List<string>
                        {
                            "<Alias=BountyNpc> and their Va'ruun zealots have claimed <Alias=PrimaryRef> as a temple. Purge the station before their influence spreads.",
                            "A Va'ruun cult led by <Alias=BountyNpc> has taken <Alias=PrimaryRef> in the <Alias=TargetSystemLocation> system. Board the station and stop their rituals.",
                            "<Alias=BountyNpc> is converting the inhabitants of <Alias=PrimaryRef> to the Great Serpent's cause. Intervene before it's too late.",
                            "Va'ruun missionaries under <Alias=BountyNpc> have seized <Alias=PrimaryRef> and are broadcasting sermons across the <Alias=TargetSystemLocation> system. Silence them.",
                            "Strange signals from <Alias=PrimaryRef> suggest <Alias=BountyNpc> is performing Va'ruun rites on the crew. Investigate and put an end to it.",
                            "Va'ruun pilgrims loyal to <Alias=BountyNpc> are fortifying <Alias=PrimaryRef> as a sanctuary. Breach the hab decks and disband them.",
                            "<Alias=BountyNpc> has declared <Alias=PrimaryRef> sacred ground for the Great Serpent in the <Alias=TargetSystemLocation> system. Reclaim the station by force.",
                            "The crew of <Alias=PrimaryRef> have gone silent since <Alias=BountyNpc> and the Va'ruun arrived. Board the station and find out why.",
                            "<Alias=BountyNpc> is indoctrinating captives aboard <Alias=PrimaryRef> into the Va'ruun faith. Storm the living quarters and stop the brainwashing.",
                        };
                    if (isOre)
                        return new List<string>
                        {
                            "<Alias=BountyNpc> has commandeered <Alias=PrimaryRef> to harvest resources for a Va'ruun crusade. Seize the mining station and cut off their supply.",
                            "Va'ruun fanatics under <Alias=BountyNpc> have overrun the mining rigs at <Alias=PrimaryRef>. Drive them out of the <Alias=TargetSystemLocation> system.",
                            "<Alias=BountyNpc> is funneling ore from <Alias=PrimaryRef> to fund Va'ruun operations. Board the station and shut them down.",
                            "The Va'ruun under <Alias=BountyNpc> believe the minerals at <Alias=PrimaryRef> are blessed by the Great Serpent. Disabuse them of the notion.",
                            "Va'ruun zealots led by <Alias=BountyNpc> are extracting rare compounds from <Alias=PrimaryRef> for unknown rituals. Seize the facility.",
                            "<Alias=BountyNpc> has converted the refinery at <Alias=PrimaryRef> into a Va'ruun forge in the <Alias=TargetSystemLocation> system. Dismantle their operation.",
                            "Mining haulers report Va'ruun patrols around <Alias=PrimaryRef> enforced by <Alias=BountyNpc>. Break through and reclaim the station.",
                            "<Alias=BountyNpc> is stockpiling processed ore at <Alias=PrimaryRef> for a Va'ruun war effort. Board the station and confiscate their reserves.",
                        };
                    return new List<string>
                    {
                        "<Alias=BountyNpc> has turned the winding corridors of <Alias=PrimaryRef> into a Va'ruun shrine. Fight through and eliminate them.",
                        "Va'ruun devotees under <Alias=BountyNpc> are lurking in the depths of <Alias=PrimaryRef>. Hunt them down in the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> has sealed themselves inside <Alias=PrimaryRef> with their Va'ruun followers. Breach the station and end their threat.",
                        "Eerie chanting echoes from <Alias=PrimaryRef> where <Alias=BountyNpc> leads Va'ruun prayers. Silence the station in the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> has covered the walls of <Alias=PrimaryRef> with Great Serpent markings. Push through the passages and eliminate the Va'ruun.",
                        "Va'ruun sentinels under <Alias=BountyNpc> guard every junction of <Alias=PrimaryRef>. Fight through their defenses and take out the leader.",
                        "<Alias=BountyNpc> has rigged <Alias=PrimaryRef> with Va'ruun traps to protect their inner sanctum. Navigate the corridors and end the cult's presence.",
                        "Disappearances near <Alias=PrimaryRef> are linked to <Alias=BountyNpc> and a Va'ruun abduction ring. Board the station and rescue the missing.",
                        "The Great Serpent's influence is spreading from <Alias=PrimaryRef> under <Alias=BountyNpc>. Infiltrate the station and cut off the head.",
                        "<Alias=BountyNpc> has declared a Va'ruun holy war from the depths of <Alias=PrimaryRef>. Enter the station and prove the Serpent wrong.",
                    };

                case "Spacer":
                default:
                    if (isHab)
                        return new List<string>
                        {
                            "Spacers led by <Alias=BountyNpc> have taken over <Alias=PrimaryRef> and are raiding nearby traffic. Board the station and deal with them.",
                            "<Alias=BountyNpc> has turned <Alias=PrimaryRef> into a Spacer den in the <Alias=TargetSystemLocation> system. Clear the station before they hit more ships.",
                            "A gang of Spacers under <Alias=BountyNpc> are squatting on <Alias=PrimaryRef>. Evict them permanently.",
                            "<Alias=BountyNpc> and a pack of Spacers have been terrorizing the residents of <Alias=PrimaryRef>. Board the station and restore order.",
                            "Spacer thugs under <Alias=BountyNpc> are stripping <Alias=PrimaryRef> for anything they can sell. Stop them before the station is gutted.",
                            "<Alias=BountyNpc> has claimed <Alias=PrimaryRef> as personal territory in the <Alias=TargetSystemLocation> system. Show them it's not theirs to take.",
                            "The hab decks of <Alias=PrimaryRef> have been overrun by Spacers loyal to <Alias=BountyNpc>. Clear the station room by room.",
                            "<Alias=BountyNpc> is running a chop shop on <Alias=PrimaryRef>, scrapping stolen ships for parts. Board the station and end their enterprise.",
                            "Spacers under <Alias=BountyNpc> are using <Alias=PrimaryRef> to ambush travelers passing through the <Alias=TargetSystemLocation> system. Shut them down.",
                            "No one docks at <Alias=PrimaryRef> anymore since <Alias=BountyNpc> took over with their Spacer crew. Take back the station.",
                        };
                    if (isOre)
                        return new List<string>
                        {
                            "<Alias=BountyNpc> and a crew of Spacers have seized <Alias=PrimaryRef> for its ore. Reclaim the mining station in the <Alias=TargetSystemLocation> system.",
                            "Spacers under <Alias=BountyNpc> are looting <Alias=PrimaryRef> and selling off its mining equipment. Put a stop to it.",
                            "<Alias=BountyNpc> is hoarding stolen ore at <Alias=PrimaryRef>. Board the station and take them out before they move the goods.",
                            "Spacer scavengers led by <Alias=BountyNpc> are tearing apart the mining rigs at <Alias=PrimaryRef>. Salvage what you can and eliminate them.",
                            "<Alias=BountyNpc> has turned <Alias=PrimaryRef> into a Spacer junkyard in the <Alias=TargetSystemLocation> system. Reclaim the mining facility.",
                            "The ore processors at <Alias=PrimaryRef> are running day and night under <Alias=BountyNpc>. The profits are funding Spacer raids across the sector.",
                            "Spacers under <Alias=BountyNpc> killed the mining crew at <Alias=PrimaryRef> and took the operation for themselves. Make them pay.",
                            "<Alias=BountyNpc> is auctioning off the mineral rights to <Alias=PrimaryRef> to the highest bidder. Board the station and cancel the sale.",
                            "Haulers are being hijacked near <Alias=PrimaryRef> by Spacers working for <Alias=BountyNpc>. Eliminate the source of the problem.",
                            "<Alias=BountyNpc> has rigged the mining charges at <Alias=PrimaryRef> as weapons against intruders. Board carefully and take them out.",
                        };
                    return new List<string>
                    {
                        "<Alias=BountyNpc> has barricaded Spacers inside the corridors of <Alias=PrimaryRef>. Go in and clean them out.",
                        "A Spacer gang led by <Alias=BountyNpc> is hiding in the narrow passages of <Alias=PrimaryRef>. Hunt them down in the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> and their Spacer crew have infested <Alias=PrimaryRef>. Board the station and eliminate their leader.",
                        "Spacer lookouts on <Alias=PrimaryRef> are spotting targets for <Alias=BountyNpc> to ambush. Storm the corridors and wipe them out.",
                        "<Alias=BountyNpc> has turned every junction of <Alias=PrimaryRef> into a Spacer checkpoint. Fight through and take them down.",
                        "The twisted corridors of <Alias=PrimaryRef> are perfect cover for <Alias=BountyNpc> and their Spacer gang. Flush them out.",
                        "Spacer raiders under <Alias=BountyNpc> are stashing loot in the hidden compartments of <Alias=PrimaryRef>. Clear the station in the <Alias=TargetSystemLocation> system.",
                        "<Alias=BountyNpc> has welded shut most exits on <Alias=PrimaryRef> to funnel intruders into kill zones. Find another way in and end them.",
                        "A bounty hunter went after <Alias=BountyNpc> on <Alias=PrimaryRef> and never came back. Finish what they started.",
                        "<Alias=BountyNpc> and their Spacers have turned <Alias=PrimaryRef> into a fortress of scrap and salvage. Tear it apart.",
                    };
            }
        }
    }
}
