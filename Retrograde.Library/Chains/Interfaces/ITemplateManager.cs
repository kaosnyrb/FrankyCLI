using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Chains.Interfaces
{
    public interface ITemplateManager
    {
        public MissionTemplate GetShowdownMissionTemplate(string missionName, List<string> addons = null);
        public MissionTemplate GetInvestigationMissionTemplate(string missionName, List<string> addons = null);
        public MissionTemplate GetDiscoveryMissionTemplate(string missionName, List<string> addons = null);
    }
}
