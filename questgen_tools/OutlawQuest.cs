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
                    Name = "City Activator - Cydonia",
                    Description = "Find info about the target in a city",
                    Location = "Neon",
                    formid = 0x0012C0,
                    parameterformid = 0x00015FF7,
                    needSpacesuit = false,
                    parameter1 = "cydonia",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Neon",
                    Description = "Find info about the target in a city",
                    Location = "Neon",
                    formid = 0x001379,
                    parameterformid = 0x00015FFE,
                    needSpacesuit = false,
                    parameter1 = "neon",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Akila City",
                    Description = "Find info about the target in a city",
                    Location = "Akila City",
                    formid = 0x001379,
                    parameterformid = 0x00010DFB,
                    needSpacesuit = false,
                    parameter1 = "akila",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Waggoner Farm",
                    Description = "Find info about the target in a farm",
                    Location = "Waggoner Farm",
                    formid = 0x001379,
                    parameterformid = 0x002CC1EF,
                    needSpacesuit = false,
                    parameter1 = "waggonerfarm",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - New Homestead",
                    Description = "Find info about the target in a farm",
                    Location = "New Homestead",
                    formid = 0x001379,
                    parameterformid = 0x0021702B,
                    needSpacesuit = false,
                    parameter1 = "newhomestead",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - Gagarin Landing",
                    Description = "Find info about the target in a farm",
                    Location = "Gagarin Landing",
                    formid = 0x001379,
                    parameterformid = 0x00265018,
                    needSpacesuit = false,
                    parameter1 = "gagarinlanding",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - New Atlantis",
                    Description = "Find info about the target in a city",
                    Location = "New Atlantis",
                    formid = 0x001379,
                    parameterformid = 0x0001295A,
                    needSpacesuit = false,
                    parameter1 = "newatlantis",
                    outlawQuest = new Investigation_ActivatorCity()
                },
                new MissionTemplate()
                {
                    Name = "City Activator - The Well",
                    Description = "Find info about the target in a city",
                    Location = "The Well",
                    formid = 0x001379,
                    parameterformid = 0x0019A5C2,
                    needSpacesuit = false,
                    parameter1 = "thewell",
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
                    Name = "City Bounty - Cydonia",
                    Description = "Kill the target at the mining city of Cydonia",
                    Location = "Cydonia is a colony on Mars in the Sol system. It is the most important mining settlement in United Colonies territory.",
                    formid = 0x000917,
                    needSpacesuit = true,
                    parameter1 = "cydonia",
                    parameterformid = 0x00015FF7,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Neon",
                    Description = "Kill the target at Neon",
                    Location = "Neon",
                    formid = 0x0012BE,
                    needSpacesuit = false,
                    parameter1 = "neon",
                    parameterformid = 0x00015FFE,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Akila",
                    Description = "Kill the target at Akila",
                    Location = "Akila City",
                    formid = 0x0012BE,
                    needSpacesuit = false,
                    parameter1 = "akila",
                    parameterformid = 0x00010DFB,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Waggoner Farm",
                    Description = "Kill the target at Waggoner Farm",
                    Location = "Waggoner Farm",
                    formid = 0x0012BE,
                    needSpacesuit = false,
                    parameter1 = "waggonerfarm",
                    parameterformid = 0x002CC1EF,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - New Homestead",
                    Description = "Kill the target at New Homestead",
                    Location = "New Homestead",
                    formid = 0x0012BE,
                    needSpacesuit = true,
                    parameter1 = "newhomestead",
                    parameterformid = 0x0021702B,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - Gagarin Landing",
                    Description = "Kill the target at Gagarin Landing",
                    Location = "Gagarin Landing",
                    formid = 0x0012BE,
                    needSpacesuit = true,
                    parameter1 = "gagarinlanding",
                    parameterformid = 0x00265018,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - New Atlantis",
                    Description = "Kill the target at New Atlantis",
                    Location = "New Atlantis",
                    formid = 0x0012BE,
                    needSpacesuit = true,
                    parameter1 = "newatlantis",
                    parameterformid = 0x0001295A,
                    outlawQuest = new Showdown_BountyCity()
                },
                new MissionTemplate()
                {
                    Name = "City Bounty - The Well",
                    Description = "Kill the target at The Well",
                    Location = "The Well",
                    formid = 0x0012BE,
                    needSpacesuit = true,
                    parameter1 = "thewell",
                    parameterformid = 0x0019A5C2,
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
        public string parameter1;
        public uint parameterformid;
        public uint formid;
        public bool needSpacesuit;
        public IOutlawQuest outlawQuest;  //This is an interface that wraps the actual quest template implementation
    }
}
