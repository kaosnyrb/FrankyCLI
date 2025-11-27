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
                Name = "City Activator - Cydonia",
                Description = "Find info about the target in Cydonia",
                Location = "Neon",
                formid = 0x0012C0,
                parameterformid = 0x00015FF7,
                needSpacesuit = false,
                parameter1 = "cydonia",
                outlawQuest = new Investigation_ActivatorCity()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City",
                Description = "Find info about the target in Akila City",
                Location = "Akila City",
                formid = 0x001379,
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akila",
                outlawQuest = new Investigation_ActivatorCity()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Waggoner Farm",
                Description = "Find info about the target in Waggoner Farm",
                Location = "Waggoner Farm",
                formid = 0x001379,
                parameterformid = 0x002CC1EF,
                needSpacesuit = false,
                parameter1 = "waggonerfarm",
                outlawQuest = new Investigation_ActivatorCity()
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
                outlawQuest = new Investigation_ActivatorCity()
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
                outlawQuest = new Investigation_ActivatorCity()
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
                outlawQuest = new Investigation_ActivatorCity()
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
                outlawQuest = new Investigation_ActivatorCity()
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
                outlawQuest = new Investigation_ActivatorCity()
            });
            //-------------------------------  SHOWDOWN ------------------------------------------
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Cydonia",
                Description = "Kill the target at the mining city of Cydonia",
                Location = "Cydonia is a colony on Mars in the Sol system. It is the most important mining settlement in United Colonies territory.",
                formid = 0x000917,
                needSpacesuit = true,
                parameter1 = "cydonia",
                parameterformid = 0x00015FF7,
                outlawQuest = new Showdown_BountyCity()
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Akila",
                Description = "Kill the target at Akila",
                Location = "Akila City",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "akila",
                parameterformid = 0x00010DFB,
                outlawQuest = new Showdown_BountyCity()
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Waggoner Farm",
                Description = "Kill the target at Waggoner Farm",
                Location = "Waggoner Farm",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "waggonerfarm",
                parameterformid = 0x002CC1EF,
                outlawQuest = new Showdown_BountyCity()
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
                outlawQuest = new Showdown_BountyCity()
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
                outlawQuest = new Showdown_BountyCity()
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
                outlawQuest = new Showdown_BountyCity()
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
                outlawQuest = new Showdown_BountyCity()
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
                outlawQuest = new Showdown_BountyCity()
            });
        }
    }
}
