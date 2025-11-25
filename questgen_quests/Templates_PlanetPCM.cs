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
    internal class Templates_PlanetPCM
    {
        public static void AddTemplates(List<MissionTemplate> discovery, List<MissionTemplate> investiation, List<MissionTemplate>  showdown)
        {
            //-------------------------------  INVESTIGATION ------------------------------------------
            investiation.Add(new MissionTemplate()
            {
                Name = "Planet side Activator Small Marker",
                Description = "Find info about the target on a planet POI",
                Location = "A remote location",
                formid = 0x000835,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorPlanet()
            });
            investiation.Add(new MissionTemplate()
            {
                Name = "Planet side Activator Captive",
                Description = "Find info about the target on a planet POI",
                Location = "A remote location",
                formid = 0x000907,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorPlanet()
            });
            investiation.Add(new MissionTemplate()
            {
                Name = "Planet side Activator Large Marker",
                Description = "Find info about the target on a planet POI",
                Location = "A remote location",
                formid = 0x000908,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorPlanet()
            });
            investiation.Add(new MissionTemplate()
            {
                Name = "Planet side Activator Important Marker Breathable",
                Description = "Find info about the target on a planet POI",
                Location = "A remote location",
                formid = 0x000909,
                needSpacesuit = false,
                outlawQuest = new Investigation_ActivatorPlanet()
            });

            //-------------------------------  SHOWDOWN ------------------------------------------
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty",
                Description = "Kill the target on a planet with a breathable atmosphere",
                Location = "A small remote civilan installation",
                formid = 0x000803,
                needSpacesuit = false,
                outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty",
                Description = "Kill the target on a planet with a poor atmosphere",
                Location = "A small remote civilan installation on a planet with a poor atmosphere",
                formid = 0x000830,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Breathable Dungeon",
                Description = "Kill the target on a planet with a breathable atmosphere Dungeon",
                Location = "A Occupied Complex",
                formid = 0x000831,
                needSpacesuit = false,
                outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Industrial Dungeon",
                Description = "Kill the target on a planet with a Dungeon Industrial",
                Location = "A Occupied Industrial Complex",
                formid = 0x000834,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Military Dungeon",
                Description = "Kill the target on a planet with a Dungeon Military",
                Location = "A Old Military Base",
                formid = 0x000840,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            { 
                    Name = "Planet side Bounty - Mining Dungeon",
                    Description = "Kill the target on a planet with a Dungeon Mining",
                    Location = "A Mining Operation",
                    formid = 0x000841,
                    needSpacesuit = true,
                    outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Breathable Dungeon boss marker",
                Description = "Kill the target on a planet with a breathable atmosphere Dungeon",
                Location = "A Occupied Base where they are meeting with a boss",
                formid = 0x000916,
                needSpacesuit = false,
                outlawQuest = new Showdown_BountyPlanet()
            });
            showdown.Add(new MissionTemplate()
            {
                Name = "Planet side Bounty - Industrial Dungeon",
                Description = "Kill the target on a planet with a Dungeon Industrial",
                Location = "A Occupied Industrial Complex",
                formid = 0x000834,
                needSpacesuit = true,
                outlawQuest = new Showdown_BountyPlanet()
            });

        }
    }
}
