using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class PlanetTemplateManager(ITemplateEngine engine) : BaseTemplateManager(engine)
    {
        protected override void BuildLibraries(TemplateLib lib)
        {
            lib.ImportTemplates(new Templates_PlanetInvestigate());
            lib.ImportTemplates(new Templates_PlanetCombat());
            lib.ImportTemplates(new Templates_PlanetSmallBaseDestroy());
            lib.ImportTemplates(new Templates_PlanetSmallBaseInformant());
            lib.ImportTemplates(new Templates_SpecificDungeons());
        }
    }
}
