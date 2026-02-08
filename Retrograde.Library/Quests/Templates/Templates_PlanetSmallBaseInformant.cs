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
    public class Templates_PlanetSmallBaseInformant : TemplateLib
    {
        public Templates_PlanetSmallBaseInformant()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Smallbase Informant - Ageis Agent",
                Description = "Collect a vital clue from an Informant",
                Location = "A remote location",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","This Informant is a UC Ageis Agent who was tracking the target." },
                    {"IsTargetDead",true }
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Marshal Operative",
                Description = "Gather intel from a dead Marshal Operative.",
                Location = "Frontier planetside shelter",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The Marshal Operative had traced the target’s movements across several frontier claims."},
                    {"IsTargetDead", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Crimson Fleet Defector",
                Description = "Meet a defector hiding from their former crew.",
                Location = "Derelict planetary industrial node",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "criminal",
                    "crimsonfleet"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The defector claims the target stole valuable Fleet tech that their old crew wants back."},
                    {"IsTargetDead", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Ryujin Espionage Asset",
                Description = "Acquire corporate intel from a discreet Ryujin contact.",
                Location = "Remote research module",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = false,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "corporate",
                    "espionage"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "They were tracking a data-theft chain the target disrupted—possibly intentionally."},
                    {"IsTargetDead", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Sanctum Universal Missioner",
                Description = "Speak with a Missioner who gathered testimonies from locals.",
                Location = "Rugged planetside retreat",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "spiritual",
                    "sanctumuniversal"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The Missioner kept records of settlers who vanished after encountering the target."},
                    {"IsTargetDead", true}
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Trade Authority Broker",
                Description = "Recover contraband manifests from a TA broker in hiding.",
                Location = "Moonside cargo relay module",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "tradeauthority",
                    "smuggling"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The broker has been fencing items tied to the fugitive’s smuggling route and holds fragments of their manifest."},
                    {"IsTargetDead", true}
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Crimson Fleet Info Broker",
                Description = "Confront a Crimson Fleet information broker holding key intel.",
                Location = "Hidden pirate relay shack",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "criminal",
                    "crimsonfleet",
                    "hostile_contact"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "This broker intercepted comms traffic involving the fugitive and cached the decoded fragments for leverage."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Crimson Fleet Black-Market Quartermaster",
                Description = "Force a Fleet quartermaster to yield contraband records tied to the fugitive.",
                Location = "Makeshift supply cavern",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "criminal",
                    "crimsonfleet",
                    "contraband"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The quartermaster handled illicit gear the fugitive offloaded during a previous deal and kept a ledger of the transaction."},
                    {"IsTargetDead", false}
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Crimson Fleet Smuggling Liaison",
                Description = "Extract information from a Fleet liaison who coordinated smuggling routes.",
                Location = "Run-down orbital-drop shelter",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "criminal",
                    "crimsonfleet",
                    "smuggling"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The liaison arranged transport corridors used by the fugitive and retained route maps showing their most recent movement."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Spacer Raid Planner",
                Description = "Confront a Spacer strategist who mapped out ambush routes used by the fugitive.",
                Location = "Collapsed raider command post",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "spacers",
                    "hostile_contact"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The raid planner studied the fugitive’s movements to prepare an ambush but lost their team in the attempt—leaving behind tactical notes the player can recover."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Spacer Scrap Dealer",
                Description = "Recover stolen data from a Spacer who pulled hardware off a derelict visited by the fugitive.",
                Location = "Jury-rigged salvage pit",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "spacers",
                    "salvage"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The scrap dealer lifted a damaged datapack the fugitive discarded, unaware it still contained fragments of navigational intel."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Spacer Enforcer",
                Description = "Force a wounded Spacer enforcer to give up the clues they overheard during a failed ambush.",
                Location = "Makeshift medical hideout",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "spacers",
                    "interrogation"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "This enforcer was part of a crew that tried to intercept the fugitive. The attack failed, but he caught fragments of conversation before being left behind."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - UC Navy Deserter Technician",
                Description = "Confront a former UC field tech who fled with restricted sensor data.",
                Location = "Abandoned UC relay bunker",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "uc_deserter",
                    "military",
                    "hostile_contact"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "This deserter stole atmospheric scan logs from a UC patrol grid—logs that captured the fugitive’s movement through restricted airspace."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - UC Marine Deserter Scout",
                Description = "Force a former UC recon scout to hand over their surveillance notes.",
                Location = "Derelict recon hide",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "uc_deserter",
                    "recon",
                    "military"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "During desertion, the scout tapped into UC overwatch feeds. They captured a partial tracking sequence showing the fugitive evading pursuit."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - UC Black Ops Analyst",
                Description = "Interrogate a covert analyst who abandoned a classified assignment.",
                Location = "Sealed subterranean monitoring pod",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "uc_deserter",
                    "covert_ops",
                    "hostile_contact"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The analyst decrypted fragments of a clandestine UC operations file—one that also logged the fugitive crossing paths with an unauthorized asset."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Ranger Deserter Tracker",
                Description = "Track down a former Ranger who abandoned their post and kept unauthorized pursuit logs.",
                Location = "Deserted frontier watchpost",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar",
                    "deserter",
                    "hostile_contact"
                },
                            parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The deserter recorded a private tracking effort after going rogue, following faint sign trails that crossed paths with the fugitive’s movements."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Ranger Deserter Peacekeeper",
                Description = "Confront a disgraced peacekeeper who fled after refusing an official order.",
                Location = "Abandoned settler homestead",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = false,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar",
                    "deserter",
                    "settlement"
                },
                            parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "While drifting between settlements, the deserter gathered whispered accounts and scraps of testimony that reveal the fugitive’s earlier movements."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Ranger Deserter Scout Sniper",
                Description = "Force a former Ranger sniper to surrender their reconnaissance data.",
                Location = "Overlooked ridge bunker",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar",
                    "deserter",
                    "recon"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The sniper monitored movement across the frontier using long-range optics. Their logs captured a fleeting glimpse of the fugitive heading toward a hidden trail."},
                    {"IsTargetDead", false}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Ranger Frontier Liaison",
                Description = "Meet a Ranger serving as a liaison to frontier settlers who gathered key information.",
                Location = "Ranger support outpost",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = false,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar",
                    "law_enforcement"
                },
                            parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The liaison collected accounts from settlers who witnessed unusual activity matching the fugitive’s trail."},
                    {"IsTargetDead", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Ranger Environmental Scout",
                Description = "Speak with a Ranger scout who tracked environmental disturbances linked to recent movement.",
                Location = "Rugged trail-monitoring hut",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar",
                    "recon",
                    "frontier"
                },
                            parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "The scout identified disrupted terrain and thermal anomalies that align with the fugitive’s passage across the region."},
                    {"IsTargetDead", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planetside Smallbase Informant - Freestar Ranger Deputy Investigator",
                Description = "Retrieve investigative notes from a Ranger deputy working a parallel case.",
                Location = "Freestar field command trailer",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_informant_small"),
                needSpacesuit = false,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                    "freestar",
                    "investigation",
                    "law_enforcement"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore", "While pursuing a separate case, the deputy documented a lead that intersects with the fugitive’s known pattern of movement."},
                    {"IsTargetDead", true}
                }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------            

        }

    }
}


