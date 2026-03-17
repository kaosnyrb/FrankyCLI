using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class AllTemplateManager(ITemplateEngine engine) : BaseTemplateManager(engine)
    {

        protected override void BuildLibraries(TemplateLib lib)
        {
            lib.ImportTemplates(new Templates_Dataslate(),                    weight: 1.0f);
            lib.ImportTemplates(new Templates_PlanetInvestigate(),            weight: 1.0f);
            lib.ImportTemplates(new Templates_PlanetCombat(),                 weight: 1.0f);
            lib.ImportTemplates(new Templates_PlanetSmallBaseDestroy(),       weight: 1.0f);
            lib.ImportTemplates(new Templates_PlanetSmallBaseInformant(),     weight: 1.0f);
            lib.ImportTemplates(new Templates_PlanetConversation(),           weight: 1.0f);
            lib.ImportTemplates(new Templates_SpecificDungeons(),             weight: 1.0f);
            lib.ImportTemplates(new Templates_SpaceActivator(),               weight: 1.0f);
            lib.ImportTemplates(new Templates_SpaceInformant(),               weight: 1.0f);
            lib.ImportTemplates(new Templates_SpaceDestroy(),                 weight: 1.0f);
            lib.ImportTemplates(new Templates_Spacestation(),                 weight: 1.0f);
            lib.ImportTemplates(new Templates_Derelicts(),                    weight: 1.0f);
            lib.ImportTemplates(new Templates_Cities(),                       weight: 1.0f);
            lib.ImportTemplates(new Templates_Cities_Neon(),                  weight: 1.0f);
            lib.ImportTemplates(new Templates_Cities_Conversation_Neon(),     weight: 1.0f);
            lib.ImportTemplates(new Templates_Cities_Cydonia(),               weight: 1.0f);
            lib.ImportTemplates(new Templates_Cities_Akila(),                 weight: 1.0f);
        }
    }
}
