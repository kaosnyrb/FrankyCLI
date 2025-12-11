using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_Cities : TemplateLib
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
                formid = 0x001379,
                parameterformid = 0x002CC1EF,
                needSpacesuit = false,
                parameter1 = "waggonerfarm",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - New Homestead",
                Description = "Find info about the target in New Homestead",
                Location = "New Homestead",
                formid = 0x001379,
                parameterformid = 0x0021702B,
                needSpacesuit = false,
                parameter1 = "newhomestead",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Gagarin Landing",
                Description = "Find info about the target in Gagarin Landing",
                Location = "Gagarin Landing",
                formid = 0x001379,
                parameterformid = 0x00265018,
                needSpacesuit = false,
                parameter1 = "gagarinlanding",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - New Atlantis",
                Description = "Find info about the target in New Atlantis",
                Location = "New Atlantis",
                formid = 0x001379,
                parameterformid = 0x0001295A,
                needSpacesuit = false,
                parameter1 = "newatlantis",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - The Well",
                Description = "Find info about the target in The Well",
                Location = "The Well",
                formid = 0x001379,
                parameterformid = 0x0019A5C2,
                needSpacesuit = false,
                parameter1 = "thewell",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - HopeTown",
                Description = "Find info about the target in HopeTown",
                Location = "HopeTown",
                formid = 0x001379,
                parameterformid = 0x00016027,
                needSpacesuit = false,
                parameter1 = "hopetown",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Red Mile",
                Description = "Find info about the target in The Red Mile",
                Location = "Red Mile",
                formid = 0x001379,
                parameterformid = 0x002CE0C9,
                needSpacesuit = false,
                parameter1 = "redmile",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Red Mile Crater",
                Description = "Find info about the target in The Red Mile Crater",
                Location = "Red Mile Crater",
                formid = 0x001379,
                parameterformid = 0x002CE0C9,
                needSpacesuit = false,
                parameter1 = "redmilecrater",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters= new Dictionary<string, object>()
                {
                    {"ExtraLore","The Red Mile Run is a dangerous wilderness survival challenge and blood sport held at the Red Mile outpost on Porrima III, where contestants must sprint through a predator-infested valley to activate a distant beacon and return alive. Overseen by proprietor Mei Devine, the event has become a notorious attraction, known for the high-stakes bets spectators place on its runners’ survival and the extreme danger posed by the local Red Mile Mauler predators." }
                }
                
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - The Den",
                Description = "Find info about the target in The Den",
                Location = "The Den",
                formid = 0x001379,
                parameterformid = 0x002A0EF4,
                needSpacesuit = false,
                parameter1 = "theden",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "spacestation",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","The Den is a United Colonies star station that was built by the UC Navy during the Serpent's Crusade to replace The Old Den after it was destroyed by House Va'ruun. Following the Colony War, the UC Navy turned the station over to the UC Vanguard to be used as an outpost. Vanguards prefer to avoid being assigned to The Den, because the Wolf system sees almost no criminal activity in 2330." }
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - The Clinic",
                Description = "Find info about the target in The Clinic",
                Location = "The Clinic",
                formid = 0x001379,
                parameterformid = 0x001DE8C0,
                needSpacesuit = false,
                parameter1 = "theclinic",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "spacestation",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","In 2194, the United Colonies moved a medical star station named The Clinic into orbit around the planet Deepala in Narion. The residents of Narion considered The Clinic's presence to be a prelude to their annexation by the UC, and demanded the star station's removal. When the UC refused to comply, Narion voted to join the Freestar Collective, and in 2196, the Narion War broke out for control of the system. The war ended in 2216 after the UC destroyed Freestar's entire fleet, but two decades of brutal conflict had turned the UC's citizens against their own government. To save face and give its citizens the humane conclusion to the war that they demanded, the UC recognized Narion as a member of the Freestar Collective and turned The Clinic over as well.[1][2]\r\n\r\nThe Clinic still orbits Deepala in 2330. Its main mission continues to be to restore and improve people's health, and it is one of the most respected medical facilities in the Settled Systems. In addition to providing high quality care for VIP patients, The Clinic is also the Settled Systems' leading research and treatment facility for alien diseases. The station is of such importance to the Freestar Collective that its Chief of Medicine, Dr. Lara Darvish, holds a seat on the Council of Governors." }
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Paradiso",
                Description = "Find info about the target in Paradiso",
                Location = "Paradiso",
                formid = 0x001379,
                parameterformid = 0x0026310D,
                needSpacesuit = false,
                parameter1 = "paradiso",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","Paradiso is a luxury beach resort located on the planet Porrima II in the Porrima system. It is run by the Paradiso Group; A cutthroat, cheapskate corporate board and operates outside the jurisdiction of the United Colonies and the Freestar Collective." }
                }
            });
            //-------------------------------  SHOWDOWN ------------------------------------------
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Waggoner Farm",
                Description = "Kill the target at Waggoner Farm",
                Location = "Waggoner Farm",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "waggonerfarm",
                parameterformid = 0x002CC1EF,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - New Homestead",
                Description = "Kill the target at New Homestead",
                Location = "New Homestead",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "newhomestead",
                parameterformid = 0x0021702B,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Gagarin Landing",
                Description = "Kill the target at Gagarin Landing",
                Location = "Gagarin Landing",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "gagarinlanding",
                parameterformid = 0x00265018,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - New Atlantis",
                Description = "Kill the target at New Atlantis",
                Location = "New Atlantis",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "newatlantis",
                parameterformid = 0x0001295A,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - The Well",
                Description = "Kill the target at The Well",
                Location = "The Well",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "thewell",
                parameterformid = 0x0019A5C2,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - HopeTown",
                Description = "Kill the target at HopeTown",
                Location = "HopeTown",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "hopetown",
                parameterformid = 0x00016027,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                }
            });

            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Red Mile",
                Description = "Kill the target at The Red Mile",
                Location = "Red Mile",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "redmile",
                parameterformid = 0x002CE0C9,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Red Mile Crater",
                Description = "Kill the target at The Red Mile Crater",
                Location = "Red Mile",
                formid = 0x0012BE,
                needSpacesuit = true,
                parameter1 = "redmilecrater",
                parameterformid = 0x002CE0C9,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","The Red Mile Run is a dangerous wilderness survival challenge and blood sport held at the Red Mile outpost on Porrima III, where contestants must sprint through a predator-infested valley to activate a distant beacon and return alive. Overseen by proprietor Mei Devine, the event has become a notorious attraction, known for the high-stakes bets spectators place on its runners’ survival and the extreme danger posed by the local Red Mile Mauler predators." }
                }
            });

            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Paradiso",
                Description = "Kill the target at Paradiso",
                Location = "Paradiso",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "paradiso",
                parameterformid = 0x0026310D,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "settlement",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","Paradiso is a luxury beach resort located on the planet Porrima II in the Porrima system. It is run by the Paradiso Group; A cutthroat, cheapskate corporate board and operates outside the jurisdiction of the United Colonies and the Freestar Collective." }
                }
            });
        }
    }
}
