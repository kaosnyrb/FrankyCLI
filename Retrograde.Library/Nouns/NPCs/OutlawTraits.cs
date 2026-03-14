using Retrograde.AI.Utils;
using Retrograde.Utils;
using System.Text;

namespace Retrograde.Nouns
{
    public class OutlawTraits
    {
        public string Upbringing           = string.Empty;
        public string Fear                 = string.Empty;
        public string Goal                 = string.Empty;
        public string Flaw                 = string.Empty;
        public string Quirk                = string.Empty;
        public string Occupation           = string.Empty;
        public string Crime                = string.Empty;
        public string HuntingFaction       = string.Empty;
        public string CurrentPreoccupation = string.Empty;

        public static OutlawTraits Generate()
        {
            return new OutlawTraits
            {
                Upbringing           = NpcSeedData.GetUpbringing(),
                Fear                 = NpcSeedData.GetFears(),
                Goal                 = NpcSeedData.GetGoals(),
                Flaw                 = NpcSeedData.GetFlaws(),
                Quirk                = NpcSeedData.GetQuirk(),
                Occupation           = StorySeedData.Occupations[RandomProvider.Random.Next(StorySeedData.Occupations.Count)],
                Crime                = StorySeedData.Crimes[RandomProvider.Random.Next(StorySeedData.Crimes.Count)],
                HuntingFaction       = FactionSeedData.GetCombatFaction(),
                CurrentPreoccupation = NarrativeSeedData.LogFocusPoints[RandomProvider.Random.Next(NarrativeSeedData.LogFocusPoints.Count)],
            };
        }

        public void AppendToPrompt(StringBuilder sb)
        {
            sb.AppendLine($"- Background: {Upbringing}");
            sb.AppendLine($"- Core fear: {Fear}");
            sb.AppendLine($"- Goal: {Goal}");
            sb.AppendLine($"- Personality flaw: {Flaw}");
            sb.AppendLine($"- Behavioural quirk: {Quirk}");
            sb.AppendLine($"- Former occupation: {Occupation}");
            sb.AppendLine($"- Crime: {Crime}");
            sb.AppendLine($"- Being hunted by: {HuntingFaction}");
            sb.AppendLine($"- Currently preoccupied with: {CurrentPreoccupation}");
        }
    }
}
