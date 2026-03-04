using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class SpaceTemplateManager(ITemplateEngine engine) : BaseTemplateManager(engine)
    {
        protected override void BuildLibraries(TemplateLib lib)
        {
            lib.ImportTemplates(new Templates_SpaceActivator());
            lib.ImportTemplates(new Templates_SpaceInformant());
            lib.ImportTemplates(new Templates_SpaceDestroy());
            lib.ImportTemplates(new Templates_Spacestation());
            // Templates_Derelicts excluded intentionally
        }
    }
}
