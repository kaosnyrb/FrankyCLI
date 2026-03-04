using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class CityTemplateManager(ITemplateEngine engine) : BaseTemplateManager(engine)
    {
        protected override void BuildLibraries(TemplateLib lib)
        {
            lib.ImportTemplates(new Templates_Dataslate());
            lib.ImportTemplates(new Templates_Cities());
            lib.ImportTemplates(new Templates_Cities_Neon());
            lib.ImportTemplates(new Templates_Cities_Cydonia());
            lib.ImportTemplates(new Templates_Cities_Akila());
        }
    }
}
