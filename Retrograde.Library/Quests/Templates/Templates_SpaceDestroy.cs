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
    public class Templates_SpaceDestroy : TemplateLib
    {
        public Templates_SpaceDestroy()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------

            // --- Unguarded ---
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>(),
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true}
                }
            });

            //Crimson ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Crimson Fleet"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });

            //Spacer ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Spacer"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });

            //Ecliptic ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetAClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetBClassShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo Rocky Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a rocky asteroid field around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Rocky},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo Icy Asteroids",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of icy asteroids around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Icy},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo Ice Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of ice shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo IceCrystals",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of Ice Crystals around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.IceCrystals},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo Rock Shards",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of rock shards around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.RockShards},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo Wisps",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in a field of coralline wisps around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>()
                {
                    {"SpaceCell", SpaceCellDesignType.Wisp},
                    {"NeedSpacesuit", true},
                    {"Label", "Ecliptic"},
                    {"FormId", ShipTools.GetCargoShip()}
                }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}
