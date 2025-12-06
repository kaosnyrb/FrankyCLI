using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class Templates_Cities_Cydonia : TemplateLib
    {
        public Templates_Cities_Cydonia()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Outskirts of Cydonia",
                Description = "Find info about the target at the Outskirts of Cydonia",
                Location = "Outskirts of Cydonia",
                formid = 0x001379,
                parameterformid = 0x00015FF7,
                needSpacesuit = true,
                parameter1 = "cydoniaoutskirts",
                outlawQuest = new Investigation_ActivatorCity()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Cydonia Central Hub",
                Description = "Find info about the target at the Cydonia Central Hub",
                Location = "Cydonia Central Hub",
                formid = 0x001379,
                parameterformid = 0x00015FF7,
                needSpacesuit = true,
                parameter1 = "cydoniacentralhub",
                outlawQuest = new Investigation_ActivatorCity()
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Cydonia Residential Level",
                Description = "Find info about the target at the Cydonia Residential Level",
                Location = "Cydonia Residential Level",
                formid = 0x001379,
                parameterformid = 0x00015FF7,
                needSpacesuit = true,
                parameter1 = "cydoniaresidential",
                outlawQuest = new Investigation_ActivatorCity()
            });

            //-------------------------------  SHOWDOWN ------------------------------------------            
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Outskirts of Cydonia",
                Description = "Kill the target at Outskirts of Cydonia",
                Location = "Outskirts of Cydonia",
                formid = 0x000917,
                needSpacesuit = true,
                parameter1 = "cydoniaoutskirts",
                parameterformid = 0x00015FF7,
                outlawQuest = new Showdown_BountyCity()
            });

        }
    }
}
