using Retrograde.AI.Utils;
using Retrograde.Story;
using Retrograde.Utils;
using System.Text;

namespace Retrograde.Nouns
{
    public class OutlawTraits : CharacterTraits
    {
        // Backward-compatible aliases — delegate to base class fields
        public string Crime
        {
            get => DefiningEvent;
            set => DefiningEvent = value;
        }

        public string HuntingFaction
        {
            get => AssociatedFaction;
            set => AssociatedFaction = value;
        }

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

        public override void AppendToPrompt(StringBuilder sb)
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
