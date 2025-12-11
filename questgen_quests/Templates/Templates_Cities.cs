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
        }
    }
}
