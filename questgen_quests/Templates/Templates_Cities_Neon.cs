using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_Cities_Neon : TemplateLib
    {
        public Templates_Cities_Neon()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Core",
                Description = "Find info about the target in Neon Core District",
                Location = "Neon Core",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neoncore",
                outlawQuest = new Investigation_ActivatorCity()
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Ryujin",
                Description = "Find info about the target in Neon Ryujin HQ",
                Location = "Neon Ryujin HQ",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonryujin",
                outlawQuest = new Investigation_ActivatorCity()
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Ebbside",
                Description = "Find info about the target in Neon Ebbside",
                Location = "Neon Ebbside",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonebbside",
                outlawQuest = new Investigation_ActivatorCity()
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Rooftops",
                Description = "Find info about the target in Neon Rooftops",
                Location = "Neon Rooftops",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonrooftops",
                outlawQuest = new Investigation_ActivatorCity()
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Underbelly",
                Description = "Find info about the target in Neon Underbelly",
                Location = "Neon Underbelly",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonunderbelly",
                outlawQuest = new Investigation_ActivatorCity()
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Starport",
                Description = "Find info about the target in Neon Starport",
                Location = "Neon Starport",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonstarport",
                outlawQuest = new Investigation_ActivatorCity()
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Astrall Lounge",
                Description = "Find info about the target in Neon Astrall Lounge",
                Location = "Neon Astrall Lounge",
                formid = 0x001379,
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonastrallounge",
                outlawQuest = new Investigation_ActivatorCity()
            });
            //-------------------------------  SHOWDOWN ------------------------------------------            
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Starport",
                Description = "Kill the target at Neon Starport",
                Location = "Neon Starport",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "neonstarport",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity()
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Underbelly",
                Description = "Kill the target at Neon Underbelly",
                Location = "Neon Underbelly",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "neonunderbelly",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity()
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Ebbside",
                Description = "Kill the target at Neon Ebbside",
                Location = "Neon Ebbside",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "neonebbside",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity()
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Rooftops",
                Description = "Kill the target at Neon Rooftops",
                Location = "Neon Rooftops",
                formid = 0x0012BE,
                needSpacesuit = false,
                parameter1 = "neonrooftops",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity()
            });
        }
    }
}
