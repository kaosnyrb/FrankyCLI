using DynamicData;
using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class TemplateLib
    {
        public List<MissionTemplate> DiscoveryTemplates;
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> ShowdownTemplates;

        public TemplateLib() { 
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
        }

        public void ImportTemplates(TemplateLib template)
        {
            DiscoveryTemplates.Add(template.DiscoveryTemplates);
            InvestigationTemplates.Add(template.InvestigationTemplates);
            ShowdownTemplates.Add(template.ShowdownTemplates);
        }        
    }
}
