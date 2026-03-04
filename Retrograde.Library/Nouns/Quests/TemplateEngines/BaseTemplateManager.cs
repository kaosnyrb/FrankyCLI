using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public abstract class BaseTemplateManager : ITemplateManager
    {
        private readonly ITemplateEngine _engine;

        protected BaseTemplateManager(ITemplateEngine engine)
        {
            _engine = engine;
            var lib = new TemplateLib();
            BuildLibraries(lib);
            _engine.AvailableTemplateLib = lib;
        }

        protected abstract void BuildLibraries(TemplateLib lib);

        public MissionTemplate GetShowdownMissionTemplate(string missionName, List<string> addons = null) =>
            _engine.GetShowdownMissionTemplate(missionName, addons);

        public MissionTemplate GetInvestigationMissionTemplate(string missionName, List<string> addons = null) =>
            _engine.GetInvestigationMissionTemplate(missionName, addons);

        public MissionTemplate GetDiscoveryMissionTemplate(string missionName, List<string> addons = null) =>
            _engine.GetDiscoveryMissionTemplate(missionName, addons);
    }
}
