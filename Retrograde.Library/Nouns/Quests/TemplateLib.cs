using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Chains
{
    public class TemplateLib
    {
        public List<MissionTemplate> DiscoveryTemplates;
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> ShowdownTemplates;

        public TemplateLib()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
        }

        public void ImportTemplates(TemplateLib template)
        {
            DiscoveryTemplates.AddRange(template.DiscoveryTemplates);
            InvestigationTemplates.AddRange(template.InvestigationTemplates);
            ShowdownTemplates.AddRange(template.ShowdownTemplates);
        }
    }
}
