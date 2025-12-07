using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools.Interfaces
{
    public interface ITemplateManager
    {
        public MissionTemplate GetShowdownMissionTemplate(string missionName);
        public MissionTemplate GetInvestigationMissionTemplate(string missionName);
        public MissionTemplate GetDiscoveryMissionTemplate();

    }
}
