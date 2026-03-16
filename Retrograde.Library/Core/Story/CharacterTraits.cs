using Retrograde.AI.Utils;
using Retrograde.Utils;
using System.Text;

namespace Retrograde.Story
{
    /// <summary>
    /// Generalized character traits — works for any story role, not just outlaws.
    /// OutlawTraits extends this with backward-compatible aliases (Crime, HuntingFaction).
    /// </summary>
    public class CharacterTraits
    {
        public string Upbringing           = string.Empty;
        public string Fear                 = string.Empty;
        public string Goal                 = string.Empty;
        public string Flaw                 = string.Empty;
        public string Quirk                = string.Empty;
        public string Occupation           = string.Empty;
        public string DefiningEvent        = string.Empty;
        public string AssociatedFaction    = string.Empty;
        public string CurrentPreoccupation = string.Empty;

        /// <summary>
        /// Generate traits using seed data pools appropriate for the given trait pool.
        /// </summary>
        public static CharacterTraits Generate(string traitPool = "outlaw")
        {
            var traits = new CharacterTraits
            {
                Upbringing           = NpcSeedData.GetUpbringing(),
                Fear                 = NpcSeedData.GetFears(),
                Goal                 = NpcSeedData.GetGoals(),
                Flaw                 = NpcSeedData.GetFlaws(),
                Quirk                = NpcSeedData.GetQuirk(),
                Occupation           = StorySeedData.Occupations[RandomProvider.Random.Next(StorySeedData.Occupations.Count)],
                AssociatedFaction    = FactionSeedData.GetCombatFaction(),
                CurrentPreoccupation = NarrativeSeedData.LogFocusPoints[RandomProvider.Random.Next(NarrativeSeedData.LogFocusPoints.Count)],
            };

            traits.DefiningEvent = traitPool switch
            {
                "outlaw" => StorySeedData.Crimes[RandomProvider.Random.Next(StorySeedData.Crimes.Count)],
                _        => StorySeedData.Crimes[RandomProvider.Random.Next(StorySeedData.Crimes.Count)],
            };

            return traits;
        }

        public virtual void AppendToPrompt(StringBuilder sb)
        {
            sb.AppendLine($"- Background: {Upbringing}");
            sb.AppendLine($"- Core fear: {Fear}");
            sb.AppendLine($"- Goal: {Goal}");
            sb.AppendLine($"- Personality flaw: {Flaw}");
            sb.AppendLine($"- Behavioural quirk: {Quirk}");
            sb.AppendLine($"- Former occupation: {Occupation}");
            sb.AppendLine($"- Defining event: {DefiningEvent}");
            sb.AppendLine($"- Associated with: {AssociatedFaction}");
            sb.AppendLine($"- Currently preoccupied with: {CurrentPreoccupation}");
        }
    }
}
