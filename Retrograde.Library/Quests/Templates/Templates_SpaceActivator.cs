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
    public class Templates_SpaceActivator : TemplateLib
    {
        public Templates_SpaceActivator()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------

            // --- Unguarded ---
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded Wisps",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });

            //Crimson Fleet ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });

            //Spacer ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });

            //Ecliptic ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo Wisps",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });

            // Traps ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in an asteroid field",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap Wisps",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in an asteroid field",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "space"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap Wisps",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in an asteroid field",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap Rocky Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap Icy Asteroids",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap Ice Shards",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in a field of ice shards around a planet",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap IceCrystals",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap Rock Shards",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in a field of rock shards around a planet",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap Wisps",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp}
                }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}
