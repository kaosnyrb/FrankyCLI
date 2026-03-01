using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class SeedManager
    {
        private static readonly List<string> MaleFirstNames = new List<string>
        {
            // American / British / Canadian
            "Aaron", "Blake", "Cole", "Dale", "Evan", "Grant", "Hayes", "Ivan", "Joel", "Kane",
            "Lance", "Miles", "Nash", "Owen", "Reed", "Scott", "Troy", "Wade", "Zane",
            // Mexican / Brazilian
            "Carlos", "Diego", "Emilio", "Felipe", "Hector", "Javier", "Luis", "Marco", "Rafael", "Victor",
            // French
            "Bastien", "Cedric", "Etienne", "Florian", "Mathis", "Remi", "Theo",
            // German
            "Fabian", "Henrik", "Jonas", "Kai", "Lars", "Nils", "Stefan",
            // Japanese
            "Daichi", "Haruki", "Kenji", "Naoto", "Riku", "Takeshi", "Yuki",
            // Chinese
            "Bo", "Chen", "Fang", "Hao", "Jun", "Lei", "Ming", "Peng", "Wei",
            // Russian
            "Alexei", "Dmitri", "Igor", "Mikhail", "Pavel", "Sergei", "Vadim",
        };

        private static readonly List<string> FemaleFirstNames = new List<string>
        {
            // American / British / Canadian
            "Ava", "Blair", "Casey", "Dana", "Elise", "Faye", "Harper", "Iris", "Jana", "Kira",
            "Leah", "Morgan", "Nova", "Quinn", "Rena", "Sloane", "Tara", "Vera", "Wren",
            // Mexican / Brazilian
            "Camila", "Elena", "Fernanda", "Isadora", "Lucia", "Marisol", "Renata", "Sofia", "Valentina",
            // French
            "Amelie", "Chloe", "Eloise", "Juliette", "Laure", "Noemie", "Solene",
            // German
            "Britta", "Hanna", "Ida", "Lena", "Maren", "Petra", "Sylva",
            // Japanese
            "Aiko", "Hana", "Keiko", "Maki", "Nami", "Riko", "Saya", "Yuna",
            // Chinese
            "Fen", "Jia", "Lin", "Mei", "Ning", "Rui", "Xia", "Yan",
            // Russian
            "Anya", "Dasha", "Irina", "Katya", "Mila", "Nadia", "Sonya", "Tasha",
        };

        private static readonly List<string> Surnames = new List<string>
        {
            // American / British / Canadian
            "Ashby", "Beckett", "Crane", "Decker", "Ellison", "Frost", "Garrett", "Hale", "Ingram",
            "Keane", "Lawson", "Mercer", "Novak", "Paxton", "Quill", "Reeves", "Slater", "Thorne",
            "Vance", "Weston", "York",
            // Mexican / Brazilian
            "Aguilar", "Castillo", "Ferreira", "Gomes", "Herrera", "Leal", "Medina", "Reyes", "Santos",
            // French
            "Aubert", "Blanchard", "Carnot", "Dufour", "Faure", "Girard", "Morel", "Renard",
            // German
            "Bauer", "Brandt", "Fuchs", "Gruber", "Haas", "Kohler", "Meier", "Richter", "Vogel",
            // Japanese
            "Fujita", "Hayashi", "Inoue", "Kimura", "Matsuda", "Nishida", "Ohara", "Saito", "Tanaka", "Yamamoto",
            // Chinese
            "Bai", "Cao", "Gao", "Hu", "Liang", "Liu", "Sun", "Tang", "Wu", "Xiao", "Zhang",
            // Russian
            "Bokov", "Dragunov", "Kozlov", "Lebedev", "Morozov", "Petrov", "Sokolov", "Volkov",
        };

        public static string GenerateName(bool female)
        {
            var rng = RandomProvider.Random;
            var firstNames = female ? FemaleFirstNames : MaleFirstNames;
            string first = firstNames[rng.Next(firstNames.Count)];
            string last  = Surnames[rng.Next(Surnames.Count)];
            return $"{first} {last}";
        }


        // Seed pools — rolled in C# so the AI isn't left to pick its own "random" archetype
        public static readonly List<string> Occupations = new List<string>
        {
            "cargo loader", "medical technician", "shuttle pilot", "crop farmer",
            "port customs inspector", "ship mechanic", "water reclamation tech",
            "mine surveyor", "freight coordinator", "food vendor",
            "colony maintenance worker", "transit scheduler", "fuel depot operator",
            "lab assistant", "livestock handler", "dockmaster clerk",
            "waste processing operator", "colony supply runner", "security guard",
            "planetary soil tester"
        };

        public static readonly List<string> Crimes = new List<string>
        {
            "embezzled employer funds over several months",
            "assaulted a co-worker and fled before authorities arrived",
            "stole equipment from their worksite and sold it on",
            "ran a low-level protection racket on local traders",
            "forged shipping manifests to cover missing cargo",
            "sold stolen medical supplies on the black market",
            "blackmailed a supervisor using personal information",
            "skimmed credits from payroll records",
            "fenced stolen colony equipment through a third party",
            "defrauded settlers with a fake land-claim scheme",
            "destroyed company property to hide a costly mistake",
            "sold access credentials to an outside buyer",
            "extorted a business competitor",
            "tampered with inventory records for personal gain",
            "impersonated a licensed contractor to pocket payment"
        };

        public static readonly List<string> Motives = new List<string>
        {
            "debt they could not repay",
            "desperation to cover a family member's medical costs",
            "anger over unpaid wages and broken promises",
            "a failed attempt to buy passage off-planet",
            "covering up an earlier smaller mistake that spiralled",
            "paying off a local gang that threatened their family",
            "a gambling habit that got out of control",
            "deep resentment toward a specific person who wronged them",
            "fear of losing their colony housing",
            "misplaced loyalty to someone who exploited them",
            "revenge for being passed over for a promotion they deserved",
            "getting caught in someone else's scheme and panicking"
        };

        public static readonly List<string> PersonalityTraits = new List<string>
        {
            "cautious, but panics when cornered",
            "overconfident and dismissive of consequences",
            "loyal to people they trust, ruthless to everyone else",
            "methodical — leaves few traces but hates improvising",
            "reckless — banks on luck holding out",
            "paranoid, convinced they are constantly being watched",
            "meek in person, calculating in planning",
            "charming on the surface, self-serving underneath",
            "genuinely convinced what they did was justified",
            "deeply ashamed but committed to seeing it through"
        };

        // Who is writing the first-person account found in the world
        public static readonly List<string> SpeakerTypes = new List<string>
        {
            "a co-worker who noticed the target acting strangely before they disappeared",
            "someone who was directly defrauded or harmed by the target",
            "a neighbour or local who witnessed something they couldn't explain",
            "a supervisor who discovered something was missing after the target left",
            "someone who unknowingly helped the target cover their tracks",
            "a local trader or contact who was pressured or threatened by the target",
            "a person who was owed money by the target and never got paid",
            "a friend or associate who is now avoiding questions about the target",
            "someone who shared a workspace with the target and noticed too late",
            "a person who found something the target left behind when they fled"
        };
    }
}
