using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class NameSeedData
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
            // Korean
            "Daehyun", "Jaemin", "Minjun", "Seunghyun", "Woojin",
            // Indian
            "Arjun", "Dev", "Kiran", "Rohan", "Vikram",
            // Nigerian / West African
            "Adebayo", "Emeka", "Femi", "Kalu", "Tunde",
            // More American / British
            "Brent", "Colt", "Finn", "Knox", "Ryker",
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
            // Korean
            "Chaeyeon", "Jisoo", "Minji", "Seulgi", "Soyeon",
            // Indian
            "Ananya", "Divya", "Kavya", "Priya", "Shreya",
            // Nigerian / West African
            "Adaeze", "Chioma", "Ngozi", "Nneka", "Temi",
            // More American / British
            "Aria", "Brynn", "Juniper", "Lyra", "Piper",
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
            // Korean
            "Cho", "Han", "Jeon", "Jung", "Kwon", "Oh", "Shin", "Yoon",
            // Indian
            "Nair", "Rao", "Sharma", "Singh", "Choudhary",
            // Nigerian / West African
            "Adeyemi", "Eze", "Nwosu", "Obi", "Okeke",
            // More British / American
            "Greer", "Harlow", "McKenna", "Whitmore", "Draper",
        };

        public static string GenerateName(bool female)
        {
            var rng = RandomProvider.Random;
            var firstNames = female ? FemaleFirstNames : MaleFirstNames;
            string first = firstNames[rng.Next(firstNames.Count)];
            string last  = Surnames[rng.Next(Surnames.Count)];
            return $"{first} {last}";
        }
    }
}
