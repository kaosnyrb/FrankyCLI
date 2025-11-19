using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrankyCLI.questgen_quests;

namespace FrankyCLI.questgen_tools
{
    public interface IOutlawQuest
    {
        public Quest Setup(StarfieldMod myMod,OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest);
        public string LogMessage { get; set; }
        public Quest questform { get; set; }
    }

    public class MissionLib
    {
        public List<MissionTemplate> DiscoveryTemplates;
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> ShowdownTemplates;
        public MissionLib()
        {
            DiscoveryTemplates = new List<MissionTemplate>()
            {
                new MissionTemplate()
                {
                    Name = "Dataslate in levelled item",
                    Description = "This creates a dataslate which starts the mission",
                    Location = "A remote location",
                    formid = 0,
                    needSpacesuit = true,
                    outlawQuest = new Discovery_Dataslate()
                }
            };

            InvestigationTemplates = new List<MissionTemplate>()
            {
                new MissionTemplate()
                {
                    Name = "Planet side Activator Small Marker",
                    Description = "Find info about the target on a planet POI",
                    Location = "A remote location",
                    formid = 0x000835,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Activator Captive",
                    Description = "Find info about the target on a planet POI",
                    Location = "A remote location",
                    formid = 0x000907,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Activator Large Marker",
                    Description = "Find info about the target on a planet POI",
                    Location = "A remote location",
                    formid = 0x000908,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Activator Important Marker Breathable",
                    Description = "Find info about the target on a planet POI",
                    Location = "A remote location",
                    formid = 0x000909,
                    needSpacesuit = false,
                    outlawQuest = new Investigation_ActivatorPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Space Activator - unguarded",
                    Description = "Find info about the target from a beacon in orbit around a planet",
                    Location = "An old space beacon",
                    formid = 0x000900,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorSpace()
                },
                new MissionTemplate()
                {
                    Name = "Space Activator  - Guarded by custom",
                    Description = "Find info about the target from a beacon in orbit around a planet guarded by a ship",
                    Location = "An old space beacon",
                    formid = 0x00090D,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorSpace_Guard()
                },
                new MissionTemplate()
                {
                    Name = "Space Activator - Crimson Fleet Trap",
                    Description = "Find info about the target from a beacon in orbit around a planet",
                    Location = "An space beacon in an asteroid field",
                    formid = 0x00090F,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorSpace_trapped_crimson()
                },
                new MissionTemplate()
                {
                    Name = "Space Activator - Spacer Trap",
                    Description = "Find info about the target from a beacon in orbit around a planet",
                    Location = "An space beacon in an asteroid field",
                    formid = 0x000912,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorSpace_trapped_spacer()
                },
                new MissionTemplate()
                {
                    Name = "Space Activator - Ecliptic Trap",
                    Description = "Find info about the target from a beacon in orbit around a planet",
                    Location = "An space beacon in an asteroid field",
                    formid = 0x000915,
                    needSpacesuit = true,
                    outlawQuest = new Investigation_ActivatorSpace_trapped_ecliptic()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Cydonia 01",
                    Description = "Find info about the target on a planet POI",
                    Location = "Cydonia",
                    formid = 0x0012C0,
                    needSpacesuit = false,
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Cydonia 02",
                    Description = "Find info about the target on a planet POI",
                    Location = "Cydonia",
                    formid = 0x0012F7,
                    needSpacesuit = false,
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Cydonia 03",
                    Description = "Find info about the target on a planet POI",
                    Location = "Cydonia",
                    formid = 0x0012F9,
                    needSpacesuit = false,
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Cydonia 04",
                    Description = "Find info about the target on a planet POI",
                    Location = "Cydonia",
                    formid = 0x001372,
                    needSpacesuit = false,
                    outlawQuest = new Investigation_ActivatorCity()
                }
            };

            ShowdownTemplates = new List<MissionTemplate>
            {
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a breathable atmosphere",
                    Location = "A small remote civilan installation",
                    formid = 0x000803,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a poor atmosphere",
                    Location = "A small remote civilan installation on a planet with a poor atmosphere",
                    formid = 0x000830,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty - Breathable Dungeon",
                    Description = "Kill the target on a planet with a breathable atmosphere Dungeon",
                    Location = "A Occupied Complex",
                    formid = 0x000831,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty - Industrial Dungeon",
                    Description = "Kill the target on a planet with a Dungeon Industrial",
                    Location = "A Occupied Industrial Complex",
                    formid = 0x000834,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty - Military Dungeon",
                    Description = "Kill the target on a planet with a Dungeon Military",
                    Location = "A Old Military Base",
                    formid = 0x000840,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty - Mining Dungeon",
                    Description = "Kill the target on a planet with a Dungeon Mining",
                    Location = "A Mining Operation",
                    formid = 0x000841,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty - Breathable Dungeon boss marker",
                    Description = "Kill the target on a planet with a breathable atmosphere Dungeon",
                    Location = "A Occupied Base where they are meeting with a boss",
                    formid = 0x000916,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Cydonia Outside 01",
                    Description = "Kill the target at the mining city of Cydonia",
                    Location = "Cydonia is a colony on Mars in the Sol system. It is the most important mining settlement in United Colonies territory.",
                    formid = 0x000917,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Cydonia Outside 02",
                    Description = "Kill the target at the mining city of Cydonia",
                    Location = "Cydonia is a colony on Mars in the Sol system. It is the most important mining settlement in United Colonies territory.",
                    formid = 0x000926,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Cydonia Outside 03",
                    Description = "Kill the target at the mining city of Cydonia",
                    Location = "Cydonia is a colony on Mars in the Sol system. It is the most important mining settlement in United Colonies territory.",
                    formid = 0x001131,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Well 01",
                    Description = "Kill the target in The Well under New Atlantis",
                    Location = "The Well is an underground district of New Atlantis. It appears dim and unclean, compared to its above-ground counterparts.",
                    formid = 0x00118E,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - New Atlantis 01",
                    Description = "Kill the target at New Atlantis",
                    Location = "New Atlantis",
                    formid = 0x001196,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Neon 01",
                    Description = "Kill the target at Neon",
                    Location = "Neon",
                    formid = 0x0011F9,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Neon 02",
                    Description = "Kill the target at Neon",
                    Location = "Neon",
                    formid = 0x001200,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Neon 03",
                    Description = "Kill the target at Neon",
                    Location = "Neon",
                    formid = 0x001201,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Neon 04",
                    Description = "Kill the target at Neon",
                    Location = "Neon",
                    formid = 0x0012BE,
                    needSpacesuit = false,
                    outlawQuest = new Showdown_BountyCity()
                },
            };


        }


        public MissionTemplate GetShowdownMissionTemplate(string mission)
        {
            if (mission != "")
            {
                return ShowdownTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                Random random = new Random();
                return ShowdownTemplates[random.Next(ShowdownTemplates.Count)];
            }
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission)
        {
            if (mission != "")
            {
                //Don't really care about deleting as this is for testing
                return InvestigationTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                Random random = new Random();
                int selected = random.Next(InvestigationTemplates.Count);
                var template = InvestigationTemplates[selected];
                InvestigationTemplates.RemoveAt(selected);
                return template;
            }
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            Random random = new Random();
            return DiscoveryTemplates[random.Next(DiscoveryTemplates.Count)];
        }

    }

    public class MissionTemplate
    {
        public string Name;
        public string Description;
        public string Location;
        public uint formid;
        public bool needSpacesuit;
        public IOutlawQuest outlawQuest;  //This is an interface that wraps the actual quest template implementation
    }
}
