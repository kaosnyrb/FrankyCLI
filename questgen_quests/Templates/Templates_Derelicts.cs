using FrankyCLI.questgen_tools;
using FrankyCLI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class Templates_Derelicts : TemplateLib
    {
        public Templates_Derelicts()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            //UC Navy
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo",
                Location = "A UC Navy Cargo Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC Navy",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_navy"
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A",
                Location = "A UC Navy Light Scout Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC Navy",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_navy"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B",
                Location = "A UC Navy Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC Navy",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_navy"
                }
            });

            //UC Vanguard
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A",
                Location = "A UC Vanguard Light Scout Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC Vanguard",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_vanguard"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B",
                Location = "A UC Vanguard Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC Vanguard",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_vanguard"
                }
            });

            //UC SysDef
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A",
                Location = "A UC SysDef Light Tactical Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC SysDef",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_sysdef"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B",
                Location = "A UC SysDef Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "UC SysDef",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "uc_sysdef"
                }
            });
            //Freestar Security

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo",
                Location = "A Freestar Security Cargo Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Freestar Security",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "freestar_security"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A",
                Location = "A Freestar Security Light Patrol Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Freestar Security",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "freestar_security"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B",
                Location = "A Freestar Security Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Freestar Security",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "freestar_security"
                }
            });

            // Trade Authority
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo",
                Location = "A Trade Authority Cargo Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Trade Authority",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "trade_authority"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A",
                Location = "A Trade Authority Fast Courier Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Trade Authority",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "trade_authority"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B",
                Location = "A Trade Authority Large Support Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Trade Authority",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "trade_authority"
                }
            });

            // Galbank
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo",
                Location = "A Galbank Vault Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Galbank",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "galbank"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A",
                Location = "A Galbank Contract Transport Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Galbank",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "galbank"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B",
                Location = "A Galbank Large Investment Protection Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Galbank",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "galbank"
                }
            });

            //Trackers Alliance
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A",
                Location = "A Trackers Alliance Bounty Tracker Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Trackers Alliance",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "trackers_alliance"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B",
                Location = "A Trackers Alliance Bounty Hunter Ship",
                Description = "A Derelict ship contains the clue",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                needSpacesuit = true,
                parameter1 = "Trackers Alliance",
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>()
                {
                    "space",
                    "derelict_ship",
                    "follow_clue",
                    "trackers_alliance"
                }
            });
            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}

