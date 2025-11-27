using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_SpaceActivator : TemplateLib
    {
        public Templates_SpaceActivator()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded",
                Description = "Find info about the target from a beacon in orbit around a planet",
                Location = "An old space beacon",
                formid = 0x000900,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator  - Guarded by custom",
                Description = "Find info about the target from a beacon in orbit around a planet guarded by a ship",
                Location = "An old space beacon",
                formid = 0x00090D,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap",
                Description = "Find info about the target from a beacon in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "An space beacon in an asteroid field",
                formid = 0x00090F,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_trapped_crimson()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap",
                Description = "Find info about the target from a beacon in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "An space beacon in an asteroid field",
                formid = 0x000912,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_trapped_spacer()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap",
                Description = "Find info about the target from a beacon in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "An space beacon in an asteroid field",
                formid = 0x000915,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_trapped_ecliptic()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship",
                parameterformid = ShipTools.GetCargoShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer  - cargo",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship",
                parameterformid = ShipTools.GetCargoShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship",
                parameterformid = ShipTools.GetCargoShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship",
                parameterformid = ShipTools.GetAClassShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer  - Class A",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship",
                parameterformid = ShipTools.GetAClassShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship",
                parameterformid = ShipTools.GetAClassShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship",
                parameterformid = ShipTools.GetBClassShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer  - Class B",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship",
                parameterformid = ShipTools.GetBClassShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship",
                parameterformid = ShipTools.GetBClassShip(),
                formid = 0x000808,
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space()
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}
