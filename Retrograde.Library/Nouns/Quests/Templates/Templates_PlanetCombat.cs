using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Retrograde.Quests
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
                Name = "Planet side Bounty - breathable atmosphere",
                Description = "Kill the target on a planet with a breathable atmosphere",
                Location = "A small remote civilan installation",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_nohostile_breathable"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - poor atmosphere",
                Description = "Kill the target on a planet with a poor atmosphere",
                Location = "A small remote civilan installation on a planet with a poor atmosphere",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_nohostile_unbreathable"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Breathable Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Dungeon",
                Location = "A Occupied Complex",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_dung_breathable"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Industrial Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Industrial themed Dungeon",
                Location = "A Occupied Industrial Complex",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_indust_dung_nonbreathable"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Military Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Military themed Dungeon",
                Location = "A Old Military Base",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_milt_dung_nonbreathable"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Mining Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Mining themed Dungeon",
                Location = "A Mining Operation",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_mining_dung_nonbreathable"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Breathable Dungeon boss marker",
                Description = "Kill the target on a planet with a breathable atmosphere at a protected Dungeon",
                Location = "A Occupied Base where they are meeting with a boss",
                formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_dung_breathable_boss"),
                outlawQuest = new Showdown_BountyPlanet(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false} }
            });

        }

    }
}

