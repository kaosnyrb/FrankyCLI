using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class AllTemplateManager(ITemplateEngine engine) : BaseTemplateManager(engine)
    {

        protected override void BuildLibraries(TemplateLib lib)
        {
            lib.ImportTemplates(new Templates_Dataslate());
            lib.ImportTemplates(new Templates_PlanetInvestigate());
            lib.ImportTemplates(new Templates_PlanetCombat());
            lib.ImportTemplates(new Templates_PlanetSmallBaseDestroy());
            lib.ImportTemplates(new Templates_PlanetSmallBaseInformant());
            lib.ImportTemplates(new Templates_SpecificDungeons());
            lib.ImportTemplates(new Templates_SpaceActivator());
            lib.ImportTemplates(new Templates_SpaceInformant());
            lib.ImportTemplates(new Templates_SpaceDestroy());
            lib.ImportTemplates(new Templates_Spacestation());
            lib.ImportTemplates(new Templates_Derelicts());
            lib.ImportTemplates(new Templates_Cities());
            lib.ImportTemplates(new Templates_Cities_Neon());
            lib.ImportTemplates(new Templates_Cities_Cydonia());
            lib.ImportTemplates(new Templates_Cities_Akila());
        }
    }
}
