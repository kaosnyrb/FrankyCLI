using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retrograde.SpaceCellDesigns;

namespace Retrograde.Quests
{
    public class Templates_Derelicts : TemplateLib
    {
        public Templates_Derelicts()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------

            //UC Navy ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo",
                Location = "A UC Navy Cargo Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo Rocky Asteroids",
                Location = "A UC Navy Cargo Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo Icy Asteroids",
                Location = "A UC Navy Cargo Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo Ice Shards",
                Location = "A UC Navy Cargo Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo IceCrystals",
                Location = "A UC Navy Cargo Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo Rock Shards",
                Location = "A UC Navy Cargo Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Cargo Wisps",
                Location = "A UC Navy Cargo Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A",
                Location = "A UC Navy Light Scout Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A Rocky Asteroids",
                Location = "A UC Navy Light Scout Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A Icy Asteroids",
                Location = "A UC Navy Light Scout Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A Ice Shards",
                Location = "A UC Navy Light Scout Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A IceCrystals",
                Location = "A UC Navy Light Scout Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A Rock Shards",
                Location = "A UC Navy Light Scout Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship A Wisps",
                Location = "A UC Navy Light Scout Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B",
                Location = "A UC Navy Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B Rocky Asteroids",
                Location = "A UC Navy Large Combat Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B Icy Asteroids",
                Location = "A UC Navy Large Combat Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B Ice Shards",
                Location = "A UC Navy Large Combat Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B IceCrystals",
                Location = "A UC Navy Large Combat Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B Rock Shards",
                Location = "A UC Navy Large Combat Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Navy Ship B Wisps",
                Location = "A UC Navy Large Combat Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_navy" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC Navy"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            //UC Vanguard ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A",
                Location = "A UC Vanguard Light Scout Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A Rocky Asteroids",
                Location = "A UC Vanguard Light Scout Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A Icy Asteroids",
                Location = "A UC Vanguard Light Scout Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A Ice Shards",
                Location = "A UC Vanguard Light Scout Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A IceCrystals",
                Location = "A UC Vanguard Light Scout Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A Rock Shards",
                Location = "A UC Vanguard Light Scout Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship A Wisps",
                Location = "A UC Vanguard Light Scout Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B",
                Location = "A UC Vanguard Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B Rocky Asteroids",
                Location = "A UC Vanguard Large Combat Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B Icy Asteroids",
                Location = "A UC Vanguard Large Combat Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B Ice Shards",
                Location = "A UC Vanguard Large Combat Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B IceCrystals",
                Location = "A UC Vanguard Large Combat Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B Rock Shards",
                Location = "A UC Vanguard Large Combat Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC Vanguard Ship B Wisps",
                Location = "A UC Vanguard Large Combat Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_vanguard" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC Vanguard"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            //UC SysDef ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A",
                Location = "A UC SysDef Light Tactical Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A Rocky Asteroids",
                Location = "A UC SysDef Light Tactical Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A Icy Asteroids",
                Location = "A UC SysDef Light Tactical Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A Ice Shards",
                Location = "A UC SysDef Light Tactical Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A IceCrystals",
                Location = "A UC SysDef Light Tactical Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A Rock Shards",
                Location = "A UC SysDef Light Tactical Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship A Wisps",
                Location = "A UC SysDef Light Tactical Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B",
                Location = "A UC SysDef Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B Rocky Asteroids",
                Location = "A UC SysDef Large Combat Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B Icy Asteroids",
                Location = "A UC SysDef Large Combat Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B Ice Shards",
                Location = "A UC SysDef Large Combat Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B IceCrystals",
                Location = "A UC SysDef Large Combat Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B Rock Shards",
                Location = "A UC SysDef Large Combat Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - UC SysDef Ship B Wisps",
                Location = "A UC SysDef Large Combat Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "uc_sysdef" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "UC SysDef"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            //Freestar Security ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo",
                Location = "A Freestar Security Cargo Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo Rocky Asteroids",
                Location = "A Freestar Security Cargo Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo Icy Asteroids",
                Location = "A Freestar Security Cargo Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo Ice Shards",
                Location = "A Freestar Security Cargo Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo IceCrystals",
                Location = "A Freestar Security Cargo Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo Rock Shards",
                Location = "A Freestar Security Cargo Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Cargo Wisps",
                Location = "A Freestar Security Cargo Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A",
                Location = "A Freestar Security Light Patrol Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A Rocky Asteroids",
                Location = "A Freestar Security Light Patrol Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A Icy Asteroids",
                Location = "A Freestar Security Light Patrol Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A Ice Shards",
                Location = "A Freestar Security Light Patrol Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A IceCrystals",
                Location = "A Freestar Security Light Patrol Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A Rock Shards",
                Location = "A Freestar Security Light Patrol Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship A Wisps",
                Location = "A Freestar Security Light Patrol Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B",
                Location = "A Freestar Security Large Combat Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B Rocky Asteroids",
                Location = "A Freestar Security Large Combat Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B Icy Asteroids",
                Location = "A Freestar Security Large Combat Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B Ice Shards",
                Location = "A Freestar Security Large Combat Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B IceCrystals",
                Location = "A Freestar Security Large Combat Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B Rock Shards",
                Location = "A Freestar Security Large Combat Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Freestar Security Ship B Wisps",
                Location = "A Freestar Security Large Combat Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "freestar_security" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Freestar Security"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            // Trade Authority ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo",
                Location = "A Trade Authority Cargo Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo Rocky Asteroids",
                Location = "A Trade Authority Cargo Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo Icy Asteroids",
                Location = "A Trade Authority Cargo Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo Ice Shards",
                Location = "A Trade Authority Cargo Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo IceCrystals",
                Location = "A Trade Authority Cargo Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo Rock Shards",
                Location = "A Trade Authority Cargo Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Cargo Wisps",
                Location = "A Trade Authority Cargo Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A",
                Location = "A Trade Authority Fast Courier Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A Rocky Asteroids",
                Location = "A Trade Authority Fast Courier Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A Icy Asteroids",
                Location = "A Trade Authority Fast Courier Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A Ice Shards",
                Location = "A Trade Authority Fast Courier Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A IceCrystals",
                Location = "A Trade Authority Fast Courier Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A Rock Shards",
                Location = "A Trade Authority Fast Courier Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship A Wisps",
                Location = "A Trade Authority Fast Courier Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B",
                Location = "A Trade Authority Large Support Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B Rocky Asteroids",
                Location = "A Trade Authority Large Support Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B Icy Asteroids",
                Location = "A Trade Authority Large Support Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B Ice Shards",
                Location = "A Trade Authority Large Support Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B IceCrystals",
                Location = "A Trade Authority Large Support Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B Rock Shards",
                Location = "A Trade Authority Large Support Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trade Authority Ship B Wisps",
                Location = "A Trade Authority Large Support Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trade_authority" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Trade Authority"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            // Galbank ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo",
                Location = "A Galbank Vault Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo Rocky Asteroids",
                Location = "A Galbank Vault Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo Icy Asteroids",
                Location = "A Galbank Vault Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo Ice Shards",
                Location = "A Galbank Vault Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo IceCrystals",
                Location = "A Galbank Vault Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo Rock Shards",
                Location = "A Galbank Vault Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Cargo Wisps",
                Location = "A Galbank Vault Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetCargoShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A",
                Location = "A Galbank Contract Transport Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A Rocky Asteroids",
                Location = "A Galbank Contract Transport Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A Icy Asteroids",
                Location = "A Galbank Contract Transport Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A Ice Shards",
                Location = "A Galbank Contract Transport Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A IceCrystals",
                Location = "A Galbank Contract Transport Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A Rock Shards",
                Location = "A Galbank Contract Transport Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship A Wisps",
                Location = "A Galbank Contract Transport Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B",
                Location = "A Galbank Large Investment Protection Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B Rocky Asteroids",
                Location = "A Galbank Large Investment Protection Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B Icy Asteroids",
                Location = "A Galbank Large Investment Protection Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B Ice Shards",
                Location = "A Galbank Large Investment Protection Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B IceCrystals",
                Location = "A Galbank Large Investment Protection Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B Rock Shards",
                Location = "A Galbank Large Investment Protection Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Galbank Ship B Wisps",
                Location = "A Galbank Large Investment Protection Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "galbank" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Galbank"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            //Trackers Alliance ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A",
                Location = "A Trackers Alliance Bounty Tracker Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A Rocky Asteroids",
                Location = "A Trackers Alliance Bounty Tracker Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A Icy Asteroids",
                Location = "A Trackers Alliance Bounty Tracker Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A Ice Shards",
                Location = "A Trackers Alliance Bounty Tracker Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A IceCrystals",
                Location = "A Trackers Alliance Bounty Tracker Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A Rock Shards",
                Location = "A Trackers Alliance Bounty Tracker Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship A Wisps",
                Location = "A Trackers Alliance Bounty Tracker Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetAClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B",
                Location = "A Trackers Alliance Bounty Hunter Ship",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B Rocky Asteroids",
                Location = "A Trackers Alliance Bounty Hunter Ship in a rocky asteroid field",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B Icy Asteroids",
                Location = "A Trackers Alliance Bounty Hunter Ship in a field of icy asteroids",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B Ice Shards",
                Location = "A Trackers Alliance Bounty Hunter Ship in a field of ice shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B IceCrystals",
                Location = "A Trackers Alliance Bounty Hunter Ship in a field of Ice Crystals",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B Rock Shards",
                Location = "A Trackers Alliance Bounty Hunter Ship in a field of rock shards",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Derelict - Trackers Alliance Ship B Wisps",
                Location = "A Trackers Alliance Bounty Hunter Ship in a field of coralline wisps",
                Description = "A Derelict ship contains the clue",
                formid = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                outlawQuest = new Investigation_Derelict_Space(),
                MissionTags = new List<string>() { "space", "derelict_ship", "follow_clue", "trackers_alliance" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp}, {"NeedSpacesuit", true}, {"Label", "Trackers Alliance"}, {"FormId", ShipTools.GetBClassShip()} }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}
