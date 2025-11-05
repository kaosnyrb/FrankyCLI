using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public interface IOutlawQuest
    {
        public Quest Setup(StarfieldMod myMod,OutlawNpc outlawNpc, MissionTemplate missionTemplate, Quest nextQuest);
    }

    public class MissionLib
    {
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> FinalEncounterTemplates;
        public MissionLib()
        {
            FinalEncounterTemplates = new List<MissionTemplate>
            {
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a breathable atmosphere",
                    Location = "A small remote civilan installation",
                    formid = 0x000803,
                    needSpacesuit = false,
                    outlawQuest = new OutlawQuest_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a poor atmosphere",
                    Location = "A small remote civilan installation",
                    formid = 0x000830,
                    needSpacesuit = true,
                    outlawQuest = new OutlawQuest_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a breathable atmosphere Dungeon",
                    Location = "A Occupied Industrial Complex",
                    formid = 0x000831,
                    needSpacesuit = false,
                    outlawQuest = new OutlawQuest_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a Dungeon Industrial",
                    Location = "A Occupied Industrial Complex",
                    formid = 0x000834,
                    needSpacesuit = true,
                    outlawQuest = new OutlawQuest_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a Dungeon Military",
                    Location = "A Old Military Base",
                    formid = 0x000840,
                    needSpacesuit = true,
                    outlawQuest = new OutlawQuest_BountyPlanet()
                },
                new MissionTemplate()
                {
                    Name = "Planet side Bounty",
                    Description = "Kill the target on a planet with a Dungeon Mining",
                    Location = "A Mining Operation",
                    formid = 0x000841,
                    needSpacesuit = true,
                    outlawQuest = new OutlawQuest_BountyPlanet()
                }
            };

            InvestigationTemplates = new List<MissionTemplate>()
            {
                new MissionTemplate()
                {
                    Name = "Planet side Activator",
                    Description = "Find info about the target on a planet with a Dungeon Industrial",
                    Location = "A remote location",
                    formid = 0x000835,
                    needSpacesuit = true,
                    outlawQuest = new OutlawQuest_ActivatorPlanet()
                }
            };
        }
        public MissionTemplate GetInvestigationMissionTemplate()
        {
            Random random = new Random();
            return InvestigationTemplates[random.Next(InvestigationTemplates.Count)];
        }

        public MissionTemplate GetFinalMissionTemplate()
        {
            Random random = new Random();
            return FinalEncounterTemplates[random.Next(FinalEncounterTemplates.Count)];
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
