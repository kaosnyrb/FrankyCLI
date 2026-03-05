using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class NpcSeedData
    {
        public static readonly List<string> Upbringings = new List<string>
        {
            "Grew up in the city of New Atlantis, their parents worked in MAST admin.",
            "Grew up in the city of New Atlantis, their parents worked in the UC Navy.",
            "Grew up in the city of Neon, as a streetrat on the Ebbside.",
            "Grew up in the city of Akila, as an orphan on The Stretch.",
            "Grew up drifting system to system as a spacer kid aboard a family owned hauler.",
        };

        public static readonly List<string> PersonalityFlaws = new List<string>
        {
            "Impulsive",
            "Overly stubborn",
            "Easily angered",
            "Overconfident",
            "Pessimistic",
        };

        public static readonly List<string> Traits = new List<string>
        {
            "Short temper",
            "Good hearing",
            "Night owl",
            "Tech savvy",
            "Fearless",
        };

        public static readonly List<string> HabitsAndBehaviors = new List<string>
        {
            "Always cleans their gear",
            "Talks with their hands",
            "Constantly taps their foot",
            "Writes everything down",
            "Double-checks all locks",
        };

        public static readonly List<string> FearsAndPhobias = new List<string>
        {
            "Fear of heights",
            "Fear of deep water",
            "Fear of small spaces",
            "Fear of the dark",
            "Fear of being alone",
        };

        public static readonly List<string> MotivationsAndGoals = new List<string>
        {
            "Seeking wealth",
            "Seeking fame",
            "Seeking revenge",
            "Searching for lost family",
            "Trying to escape their past",
        };

        public static readonly List<string> Nationalities = new List<string>
        {
            "American",
            "British",
            "Canadian",
            "Mexican",
            "Brazilian",
            "French",
            "German",
            "Japanese",
            "Chinese",
            "Russian",
        };

        public static string GetUpbringing()  => Upbringings[RandomProvider.Random.Next(Upbringings.Count)];
        public static string GetFlaws()       => PersonalityFlaws[RandomProvider.Random.Next(PersonalityFlaws.Count)];
        public static string GetTrait()       => Traits[RandomProvider.Random.Next(Traits.Count)];
        public static string GetHabit()       => HabitsAndBehaviors[RandomProvider.Random.Next(HabitsAndBehaviors.Count)];
        public static string GetFears()       => FearsAndPhobias[RandomProvider.Random.Next(FearsAndPhobias.Count)];
        public static string GetGoals()       => MotivationsAndGoals[RandomProvider.Random.Next(MotivationsAndGoals.Count)];
    }
}
