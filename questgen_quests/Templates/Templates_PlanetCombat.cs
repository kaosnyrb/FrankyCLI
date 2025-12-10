using FrankyCLI.questgen_tools;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FrankyCLI.questgen_quests
{
    public class Templates_PlanetCombat : TemplateLib
    {
        public Templates_PlanetCombat()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------

            //-------------------------------  SHOWDOWN ------------------------------------------
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty",
                Description = "Kill the target on a planet with a breathable atmosphere",
                Location = "A small remote civilan installation",
                formid = 0x000803,
                needSpacesuit = false,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty",
                Description = "Kill the target on a planet with a poor atmosphere",
                Location = "A small remote civilan installation on a planet with a poor atmosphere",
                formid = 0x000830,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Breathable Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Dungeon",
                Location = "A Occupied Complex",
                formid = 0x000831,
                needSpacesuit = false,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Industrial Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Industrial themed Dungeon",
                Location = "A Occupied Industrial Complex",
                formid = 0x000834,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Military Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Military themed Dungeon",
                Location = "A Old Military Base",
                formid = 0x000840,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Mining Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Mining themed Dungeon",
                Location = "A Mining Operation",
                formid = 0x000841,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Breathable Dungeon boss marker",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Dungeon",
                Location = "A Occupied Base where they are meeting with a boss",
                formid = 0x000916,
                needSpacesuit = false,
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                }
            });

        }

    }
}
