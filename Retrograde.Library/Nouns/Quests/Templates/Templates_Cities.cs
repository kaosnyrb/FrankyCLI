using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests
{
    public class Templates_Cities : TemplateLib
    {
        public Templates_Cities()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Waggoner Farm",
                Description = "Find info about the target in Waggoner Farm",
                Location = "Waggoner Farm",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "waggonerfarm"}, {"FormId", 0x002CC1EF} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - New Homestead",
                Description = "Find info about the target in New Homestead",
                Location = "New Homestead",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "newhomestead"}, {"FormId", 0x0021702B} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Gagarin Landing",
                Description = "Find info about the target in Gagarin Landing",
                Location = "Gagarin Landing",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "gagarinlanding"}, {"FormId", 0x00265018} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - New Atlantis",
                Description = "Find info about the target in New Atlantis",
                Location = "New Atlantis",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "newatlantis"}, {"FormId", 0x0001295A} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - The Well",
                Description = "Find info about the target in The Well",
                Location = "The Well",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "thewell"}, {"FormId", 0x0019A5C2} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - HopeTown",
                Description = "Find info about the target in HopeTown",
                Location = "HopeTown",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "hopetown"}, {"FormId", 0x00016027} }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Red Mile",
                Description = "Find info about the target in The Red Mile",
                Location = "Red Mile",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "redmile"}, {"FormId", 0x002CE0C9} }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Red Mile Crater",
                Description = "Find info about the target in The Red Mile Crater",
                Location = "Red Mile Crater",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters= new Dictionary<string, object>()
                {
                    {"ExtraLore","The Red Mile Run is a dangerous wilderness survival challenge and blood sport held at the Red Mile outpost on Porrima III, where contestants must sprint through a predator-infested valley to activate a distant beacon and return alive. Overseen by proprietor Mei Devine, the event has become a notorious attraction, known for the high-stakes bets spectators place on its runners� survival and the extreme danger posed by the local Red Mile Mauler predators." },
                    {"NeedSpacesuit", false},
                    {"Label", "redmilecrater"},
                    {"FormId", 0x002CE0C9}
                }
                
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - The Den",
                Description = "Find info about the target in The Den",
                Location = "The Den",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "spacestation",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","The Den is a United Colonies star station that was built by the UC Navy during the Serpent's Crusade to replace The Old Den after it was destroyed by House Va'ruun. Following the Colony War, the UC Navy turned the station over to the UC Vanguard to be used as an outpost. Vanguards prefer to avoid being assigned to The Den, because the Wolf system sees almost no criminal activity in 2330." },
                    {"NeedSpacesuit", false},
                    {"Label", "theden"},
                    {"FormId", 0x002A0EF4}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - The Clinic",
                Description = "Find info about the target in The Clinic",
                Location = "The Clinic",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "spacestation",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","In 2194, the United Colonies moved a medical star station named The Clinic into orbit around the planet Deepala in Narion. The residents of Narion considered The Clinic's presence to be a prelude to their annexation by the UC, and demanded the star station's removal. When the UC refused to comply, Narion voted to join the Freestar Collective, and in 2196, the Narion War broke out for control of the system. The war ended in 2216 after the UC destroyed Freestar's entire fleet, but two decades of brutal conflict had turned the UC's citizens against their own government. To save face and give its citizens the humane conclusion to the war that they demanded, the UC recognized Narion as a member of the Freestar Collective and turned The Clinic over as well.[1][2]\r\n\r\nThe Clinic still orbits Deepala in 2330. Its main mission continues to be to restore and improve people's health, and it is one of the most respected medical facilities in the Settled Systems. In addition to providing high quality care for VIP patients, The Clinic is also the Settled Systems' leading research and treatment facility for alien diseases. The station is of such importance to the Freestar Collective that its Chief of Medicine, Dr. Lara Darvish, holds a seat on the Council of Governors." },
                    {"NeedSpacesuit", false},
                    {"Label", "theclinic"},
                    {"FormId", 0x001DE8C0}
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Paradiso",
                Description = "Find info about the target in Paradiso",
                Location = "Paradiso",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","Paradiso is a luxury beach resort located on the planet Porrima II in the Porrima system. It is run by the Paradiso Group; A cutthroat, cheapskate corporate board and operates outside the jurisdiction of the United Colonies and the Freestar Collective." },
                    {"NeedSpacesuit", false},
                    {"Label", "paradiso"},
                    {"FormId", 0x0026310D}
                }
            });
            //-------------------------------  SHOWDOWN ------------------------------------------
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Waggoner Farm",
                Description = "Kill the target at Waggoner Farm",
                Location = "Waggoner Farm",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "waggonerfarm"}, {"FormId", 0x002CC1EF} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - New Homestead",
                Description = "Kill the target at New Homestead",
                Location = "New Homestead",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "newhomestead"}, {"FormId", 0x0021702B} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Gagarin Landing",
                Description = "Kill the target at Gagarin Landing",
                Location = "Gagarin Landing",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "gagarinlanding"}, {"FormId", 0x00265018} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - New Atlantis",
                Description = "Kill the target at New Atlantis",
                Location = "New Atlantis",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "newatlantis"}, {"FormId", 0x0001295A} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - The Well",
                Description = "Kill the target at The Well",
                Location = "The Well",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "thewell"}, {"FormId", 0x0019A5C2} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - HopeTown",
                Description = "Kill the target at HopeTown",
                Location = "HopeTown",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "hopetown"}, {"FormId", 0x00016027} }
            });

            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Red Mile",
                Description = "Kill the target at The Red Mile",
                Location = "Red Mile",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "redmile"}, {"FormId", 0x002CE0C9} }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Red Mile Crater",
                Description = "Kill the target at The Red Mile Crater",
                Location = "Red Mile",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","The Red Mile Run is a dangerous wilderness survival challenge and blood sport held at the Red Mile outpost on Porrima III, where contestants must sprint through a predator-infested valley to activate a distant beacon and return alive. Overseen by proprietor Mei Devine, the event has become a notorious attraction, known for the high-stakes bets spectators place on its runners� survival and the extreme danger posed by the local Red Mile Mauler predators." },
                    {"NeedSpacesuit", true},
                    {"Label", "redmilecrater"},
                    {"FormId", 0x002CE0C9}
                }
            });

            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Paradiso",
                Description = "Kill the target at Paradiso",
                Location = "Paradiso",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","Paradiso is a luxury beach resort located on the planet Porrima II in the Porrima system. It is run by the Paradiso Group; A cutthroat, cheapskate corporate board and operates outside the jurisdiction of the United Colonies and the Freestar Collective." },
                    {"NeedSpacesuit", false},
                    {"Label", "paradiso"},
                    {"FormId", 0x0026310D}
                }
            });
        }
    }
}

