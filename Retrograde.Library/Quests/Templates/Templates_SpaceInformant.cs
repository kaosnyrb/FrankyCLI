using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests
{
    public class Templates_SpaceInformant : TemplateLib
    {
        public Templates_SpaceInformant()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer  - cargo",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer  - Class A",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer  - Class B",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>()
                {
                    "kill_target",
                    "space",
                    "ecliptic"
                }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}


