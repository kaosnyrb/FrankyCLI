using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class NameSeedData
    {
        private static readonly List<string> MaleFirstNames = new List<string>
        {
            // American / British / Canadian
            "Aaron", "Adam", "Alec", "Austin", "Blake", "Brent", "Calvin", "Chase", "Cole", "Colt",
            "Connor", "Dale", "Dawson", "Dean", "Drake", "Ethan", "Evan", "Finn", "Fletcher", "Gavin",
            "Graham", "Grant", "Griffin", "Hayes", "Hudson", "Hunter", "Ivan", "Joel", "Julian", "Kane",
            "Knox", "Lance", "Liam", "Logan", "Mason", "Miles", "Nash", "Nolan", "Owen", "Parker",
            "Reed", "Reid", "Ryker", "Scott", "Seth", "Spencer", "Travis", "Troy", "Tyler", "Wade",
            "Warren", "Wesley", "Wyatt", "Zane",
            // Hispanic / Latin American
            "Abel", "Alejandro", "Andres", "Antonio", "Bruno", "Carlos", "Cesar", "Diego", "Eduardo",
            "Emilio", "Esteban", "Felipe", "Gabriel", "Gonzalo", "Hector", "Javier", "Joaquin", "Jorge",
            "Juan", "Luis", "Marco", "Mateo", "Miguel", "Nicolas", "Pablo", "Rafael", "Ricardo",
            "Roberto", "Ruben", "Santiago", "Victor",
            // French
            "Adrien", "Antoine", "Arthur", "Baptiste", "Bastien", "Cedric", "Charles", "Clement",
            "Damien", "Etienne", "Florian", "Gael", "Guillaume", "Henri", "Hugo", "Julien",
            "Laurent", "Leon", "Louis", "Luc", "Mathis", "Maxime", "Noel", "Olivier",
            "Philippe", "Pierre", "Remi", "Simon", "Theo", "Thomas", "Tristan", "Xavier",
            // German
            "Christoph", "Dominik", "Emil", "Fabian", "Felix", "Franz", "Hans", "Henrik",
            "Jonas", "Kai", "Klaus", "Konrad", "Lars", "Lukas", "Markus", "Moritz",
            "Nils", "Otto", "Roland", "Stefan", "Tobias", "Werner",
            // Japanese
            "Akira", "Atsushi", "Daichi", "Haruki", "Hayato", "Hiroshi", "Isamu", "Kazuki",
            "Kenji", "Makoto", "Masaru", "Naoto", "Noboru", "Ren", "Riku", "Ryota",
            "Satoshi", "Shingo", "Takeshi", "Tatsuya", "Wataru", "Yamato", "Yuki", "Yuto",
            // Chinese
            "Anming", "Bo", "Chen", "Cheng", "Fang", "Haoran", "Hao", "Jiaming", "Jun",
            "Lei", "Ming", "Peng", "Tao", "Wei", "Xinyu", "Yifan", "Yuchen", "Zhen",
            // Russian
            "Alexander", "Alexei", "Andrei", "Boris", "Dmitri", "Evgeny", "Gleb", "Igor",
            "Ilya", "Kirill", "Maxim", "Mikhail", "Nikolai", "Oleg", "Pavel", "Roman",
            "Sergei", "Vadim", "Viktor", "Vladimir", "Yuri",
            // Korean
            "Daehyun", "Hyunjin", "Jaemin", "Jihoon", "Junho", "Minho", "Minjun",
            "Seojun", "Seunghyun", "Sungjin", "Taehyun", "Woojin", "Youngjun",
            // Indian
            "Aakash", "Ajay", "Amit", "Arjun", "Dev", "Dhruv", "Gaurav", "Karthik",
            "Kiran", "Naveen", "Nikhil", "Pranav", "Rahul", "Rohan", "Sachin", "Vijay", "Vikram",
            // Nigerian / West African
            "Ade", "Adebayo", "Babatunde", "Chidi", "Dele", "Ebuka", "Emeka", "Femi",
            "Ifeanyi", "Ikenna", "Kalu", "Kofi", "Lanre", "Obinna", "Seun", "Tunde",
        };

        private static readonly List<string> FemaleFirstNames = new List<string>
        {
            // American / British / Canadian
            "Abby", "Alexis", "Allison", "Amber", "Aria", "Audrey", "Ava", "Blair", "Brooke",
            "Brynn", "Casey", "Claire", "Cora", "Dana", "Elise", "Ellie", "Ember", "Faith",
            "Faye", "Gemma", "Grace", "Haley", "Hannah", "Harper", "Holly", "Iris", "Jana",
            "Julia", "Juniper", "Kira", "Lauren", "Leah", "Lily", "Lyra", "Mia", "Morgan",
            "Naomi", "Natalie", "Nicole", "Nova", "Paige", "Peyton", "Piper", "Quinn",
            "Reese", "Rena", "Riley", "Ruby", "Sara", "Savannah", "Sloane", "Stella",
            "Summer", "Sydney", "Tara", "Taylor", "Tessa", "Vera", "Wren",
            // Hispanic / Latin American
            "Adriana", "Alicia", "Andrea", "Camila", "Carmen", "Catalina", "Claudia", "Daniela",
            "Diana", "Elena", "Fernanda", "Gabriela", "Isabel", "Isadora", "Laura", "Lorena",
            "Lucia", "Marisol", "Monica", "Natalia", "Paloma", "Paula", "Renata", "Sofia",
            "Valentina", "Veronica",
            // French
            "Agnes", "Alice", "Amelie", "Aurelie", "Camille", "Charlotte", "Chloe", "Colette",
            "Delphine", "Eloise", "Estelle", "Genevieve", "Josephine", "Juliette", "Laure",
            "Lucie", "Manon", "Margot", "Marie", "Mathilde", "Noemie", "Nathalie",
            "Pauline", "Solene", "Sophie", "Valerie", "Vivienne",
            // German
            "Annelise", "Birgit", "Britta", "Christine", "Clara", "Dorothea", "Erika",
            "Franziska", "Greta", "Hanna", "Ida", "Johanna", "Katharina", "Kristin",
            "Lena", "Maren", "Marlene", "Monika", "Petra", "Sabine", "Sigrid", "Sylva",
            // Japanese
            "Aiko", "Akane", "Asuka", "Ayaka", "Chiaki", "Chihiro", "Eri", "Hana",
            "Haruka", "Hikaru", "Honoka", "Kaori", "Keiko", "Koharu", "Kyoko", "Maki",
            "Mayumi", "Megumi", "Miho", "Misaki", "Momoko", "Nami", "Natsuki", "Nozomi",
            "Riko", "Rina", "Risa", "Sakura", "Saya", "Shiori", "Yukiko", "Yuna", "Yuriko",
            // Chinese
            "Danhua", "Fen", "Hanyu", "Jia", "Jiaying", "Jingyi", "Lihua", "Lin", "Mei",
            "Meiling", "Ning", "Qing", "Rui", "Shanshan", "Siyu", "Tingting", "Wanying",
            "Wenjing", "Xia", "Xinyi", "Yan", "Yulan",
            // Russian
            "Alina", "Anastasia", "Anya", "Darya", "Dasha", "Ekaterina", "Elena", "Inna",
            "Irina", "Katya", "Kseniya", "Lara", "Larisa", "Lena", "Marina", "Mila",
            "Nadia", "Oksana", "Olga", "Polina", "Sonya", "Svetlana", "Tamara", "Tasha",
            "Valentina", "Yulia",
            // Korean
            "Boyeon", "Chaeyeon", "Dahyun", "Dawon", "Eunji", "Haewon", "Hyuna", "Jieun",
            "Jisoo", "Jiyeon", "Joohyun", "Minji", "Nayeon", "Seulgi", "Sohee", "Somin",
            "Soyeon", "Sujeong", "Yerin", "Yoona",
            // Indian
            "Aarti", "Amrita", "Ananya", "Bindu", "Chitra", "Deepa", "Divya", "Hema",
            "Indira", "Kamala", "Kavya", "Lakshmi", "Madhuri", "Meera", "Nisha", "Pooja",
            "Parvathi", "Priya", "Radha", "Rekha", "Sandhya", "Savitha", "Shreya",
            "Swathi", "Tanvi", "Usha", "Vidya",
            // Nigerian / West African
            "Adaeze", "Aisha", "Ama", "Amara", "Bisi", "Bukola", "Chiamaka", "Chioma",
            "Chinwe", "Damilola", "Folake", "Ifeoma", "Kemi", "Nkechi", "Ngozi", "Nneka",
            "Temi", "Tope", "Wunmi", "Yewande", "Zainab",
        };

        private static readonly List<string> Surnames = new List<string>
        {
            // American / British / Canadian
            "Abbott", "Ashby", "Baldwin", "Barker", "Beckett", "Brady", "Burke", "Calloway",
            "Chapman", "Collins", "Cooper", "Crane", "Crawford", "Decker", "Doyle", "Draper",
            "Dunbar", "Duncan", "Ellis", "Ellison", "Ferguson", "Fielding", "Fleming",
            "Frost", "Fuller", "Garrett", "Gibson", "Gordon", "Grayson", "Greer",
            "Hale", "Hardy", "Harlow", "Harrison", "Hawkins", "Henderson", "Holt",
            "Ingram", "Jennings", "Keane", "Kirby", "Knight", "Langley", "Lawson",
            "Manning", "McKenna", "Mercer", "Morris", "Murdoch", "Murray", "Nelson",
            "Novak", "Palmer", "Paxton", "Porter", "Quill", "Reeves", "Reynolds",
            "Rogers", "Ryan", "Shaw", "Slater", "Sullivan", "Thorne", "Vance",
            "Walsh", "Weston", "Whitmore", "York",
            // Hispanic / Latin American
            "Acosta", "Aguilar", "Alvarado", "Castillo", "Cruz", "Delgado", "Dominguez",
            "Escobar", "Ferreira", "Flores", "Fuentes", "Garcia", "Gomes", "Gutierrez",
            "Herrera", "Leal", "Lopez", "Luna", "Medina", "Montoya", "Morales",
            "Munoz", "Navarro", "Ortega", "Perez", "Ramirez", "Reyes", "Rivera",
            "Rojas", "Romero", "Ruiz", "Santos", "Vargas",
            // French
            "Aubert", "Beaumont", "Bernard", "Blanchard", "Carnot", "Chevalier",
            "Dufour", "Dupont", "Durand", "Faure", "Fontaine", "Girard", "Leblanc",
            "Leclerc", "Lefevre", "Leroux", "Marchand", "Martin", "Moreau", "Morel",
            "Pelletier", "Petit", "Renard", "Richard", "Roux",
            // German
            "Bauer", "Becker", "Bergmann", "Brandt", "Braun", "Dietrich", "Fischer",
            "Fuchs", "Gruber", "Haas", "Hartmann", "Hoffmann", "Huber", "Kaiser",
            "Klein", "Koch", "Kohler", "Krause", "Meier", "Muller", "Richter",
            "Schmidt", "Schneider", "Schultz", "Schwarz", "Vogel",
            // Japanese
            "Aoki", "Endo", "Fujita", "Goto", "Hashimoto", "Hayashi", "Inoue",
            "Ishii", "Ito", "Iwata", "Kato", "Kawamoto", "Kimura", "Kobayashi",
            "Maeda", "Matsuda", "Matsumoto", "Miyamoto", "Mori", "Murata",
            "Nakagawa", "Nakamura", "Nishida", "Nishimura", "Ogawa", "Ohara",
            "Saito", "Sato", "Shimizu", "Suzuki", "Takahashi", "Tanaka", "Uchida",
            "Watanabe", "Yamada", "Yamamoto", "Yamashita", "Yoshida",
            // Chinese
            "Bai", "Cai", "Cao", "Chen", "Cui", "Deng", "Ding", "Dong", "Fan",
            "Fang", "Fu", "Gao", "Gong", "Gu", "Han", "He", "Hong", "Hou",
            "Hu", "Huang", "Jiang", "Jin", "Kong", "Li", "Liang", "Lin",
            "Liu", "Lu", "Luo", "Ma", "Mao", "Meng", "Mo", "Ni", "Pan",
            "Qian", "Qi", "Ran", "Shao", "Shen", "Shi", "Song", "Sun",
            "Tang", "Tian", "Wan", "Wang", "Wu", "Xiao", "Xu", "Yan",
            "Yang", "Yao", "Ye", "Yi", "Yu", "Yuan", "Yue", "Zhao",
            "Zheng", "Zhong", "Zhou", "Zhu", "Zhang",
            // Russian
            "Belov", "Bokov", "Chernov", "Dragunov", "Fedorov", "Frolov",
            "Ivanov", "Karpov", "Kovalev", "Kozlov", "Kuznetsov", "Lebedev",
            "Makarov", "Morozov", "Nikitin", "Orlov", "Petrov", "Romanov",
            "Ryabov", "Sidorov", "Smirnov", "Sokolov", "Stepanov", "Titov",
            "Vasiliev", "Volkov", "Zhukov",
            // Korean
            "Bae", "Baek", "Chae", "Cho", "Choi", "Chun", "Eom", "Ha",
            "Han", "Hwang", "Im", "Jang", "Jeon", "Jung", "Kang", "Kim",
            "Kwon", "Lee", "Lim", "Moon", "Nam", "Oh", "Pak", "Ryu",
            "Seo", "Seong", "Shin", "Song", "Woo", "Yang", "Yoo", "Yoon",
            // Indian
            "Ahuja", "Bajaj", "Banerjee", "Bose", "Chakraborty", "Chauhan",
            "Choudhary", "Das", "Desai", "Dey", "Ghosh", "Gupta", "Iyer",
            "Joshi", "Kumar", "Mehta", "Menon", "Mishra", "Mukherjee",
            "Nair", "Patel", "Pillai", "Rao", "Reddy", "Saxena", "Shah",
            "Sharma", "Singh", "Trivedi", "Verma", "Yadav",
            // Nigerian / West African
            "Adeyemi", "Afolabi", "Ajayi", "Akindele", "Alabi", "Ayodele",
            "Balogun", "Bankole", "Diallo", "Dike", "Eze", "Mensah",
            "Nkrumah", "Nwosu", "Obi", "Okeke", "Okafor", "Osei",
            "Owusu", "Oyedele",
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
