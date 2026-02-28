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
    public class Templates_SpaceInformant : TemplateLib
    {
        public Templates_SpaceInformant()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------

            // Crimson Fleet ------------------------------------------------
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
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo Rocky Asteroids",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship in a rocky asteroid field",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo Icy Asteroids",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship in a field of icy asteroids",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo Ice Shards",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship in a field of ice shards",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo IceCrystals",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo Rock Shards",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship in a field of rock shards",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - cargo Wisps",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A cargo ship in a field of coralline wisps",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
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
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A Rocky Asteroids",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship in a rocky asteroid field",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A Icy Asteroids",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship in a field of icy asteroids",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A Ice Shards",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship in a field of ice shards",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A IceCrystals",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A Rock Shards",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship in a field of rock shards",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class A Wisps",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A small ship in a field of coralline wisps",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
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
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B Rocky Asteroids",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship in a rocky asteroid field",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B Icy Asteroids",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship in a field of icy asteroids",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B Ice Shards",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship in a field of ice shards",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B IceCrystals",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B Rock Shards",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship in a field of rock shards",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Crimson Fleet - Class B Wisps",
                Description = "A Crimson Fleet ship has the data in there hold",
                Location = "A strong medium sized ship in a field of coralline wisps",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "crimson_fleet" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
            });

            // Spacer ------------------------------------------------
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
                MissionTags = new List<string>() { "kill_target", "space", "spacer" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - cargo Rocky Asteroids",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship in a rocky asteroid field",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - cargo Icy Asteroids",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship in a field of icy asteroids",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - cargo Ice Shards",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship in a field of ice shards",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - cargo IceCrystals",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - cargo Rock Shards",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship in a field of rock shards",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - cargo Wisps",
                Description = "A Spacer ship has the data in there hold",
                Location = "A cargo ship in a field of coralline wisps",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
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
                MissionTags = new List<string>() { "kill_target", "space", "spacer" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class A Rocky Asteroids",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship in a rocky asteroid field",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class A Icy Asteroids",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship in a field of icy asteroids",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class A Ice Shards",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship in a field of ice shards",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class A IceCrystals",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class A Rock Shards",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship in a field of rock shards",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class A Wisps",
                Description = "A Spacer ship has the data in there hold",
                Location = "A small ship in a field of coralline wisps",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
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
                MissionTags = new List<string>() { "kill_target", "space", "spacer" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class B Rocky Asteroids",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship in a rocky asteroid field",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class B Icy Asteroids",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship in a field of icy asteroids",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class B Ice Shards",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship in a field of ice shards",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class B IceCrystals",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class B Rock Shards",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship in a field of rock shards",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Spacer - Class B Wisps",
                Description = "A Spacer ship has the data in there hold",
                Location = "A strong medium sized ship in a field of coralline wisps",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Spacer",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "spacer" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
            });

            // Ecliptic ------------------------------------------------
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
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo Rocky Asteroids",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship in a rocky asteroid field",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo Icy Asteroids",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship in a field of icy asteroids",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo Ice Shards",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship in a field of ice shards",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo IceCrystals",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo Rock Shards",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship in a field of rock shards",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - cargo Wisps",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A cargo ship in a field of coralline wisps",
                parameterformid = ShipTools.GetCargoShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
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
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A Rocky Asteroids",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship in a rocky asteroid field",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A Icy Asteroids",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship in a field of icy asteroids",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A Ice Shards",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship in a field of ice shards",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A IceCrystals",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A Rock Shards",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship in a field of rock shards",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class A Wisps",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A small ship in a field of coralline wisps",
                parameterformid = ShipTools.GetAClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
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
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B Rocky Asteroids",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship in a rocky asteroid field",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Rocky} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B Icy Asteroids",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship in a field of icy asteroids",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Icy} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B Ice Shards",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship in a field of ice shards",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B IceCrystals",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship in a field of Ice Crystals",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.IceCrystals} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B Rock Shards",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship in a field of rock shards",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.RockShards} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Informant - Ecliptic - Class B Wisps",
                Description = "A Ecliptic ship has the data in there hold",
                Location = "A strong medium sized ship in a field of coralline wisps",
                parameterformid = ShipTools.GetBClassShip(),
                formid = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                needSpacesuit = true,
                parameter1 = "Ecliptic",
                outlawQuest = new Investigation_Informant_Space(),
                MissionTags = new List<string>() { "kill_target", "space", "ecliptic" },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>() { {"SpaceCell", SpaceCellDesignType.Wisp} }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}
