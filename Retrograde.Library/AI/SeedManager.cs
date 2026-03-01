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


        // Seed pools — rolled in C# so the AI isn't left to pick its own "random" archetype
        public static readonly List<string> Occupations = new List<string>
        {
            "cargo loader", "medical technician", "shuttle pilot", "crop farmer",
            "port customs inspector", "ship mechanic", "water reclamation tech",
            "mine surveyor", "freight coordinator", "food vendor",
            "colony maintenance worker", "transit scheduler", "fuel depot operator",
            "lab assistant", "livestock handler", "dockmaster clerk",
            "waste processing operator", "colony supply runner", "security guard",
            "planetary soil tester",
            "salvage crew hand", "prospector on low-yield claims", "cold storage clerk",
            "data relay operator", "radiation safety monitor", "field medic",
            "habitat construction worker", "power cell recycler", "seed vault technician",
            "repair bay welder", "colony census recorder", "ship parts inspector",
            "outpost perimeter guard", "hazmat disposal worker", "communications relay operator",
            "terraforming support tech", "settlement cook", "water purification technician",
            "transit dock hand", "livestock geneticist"
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
            "impersonated a licensed contractor to pocket payment",
            "falsified safety inspection reports in exchange for a bribe",
            "diverted emergency supplies to sell privately",
            "smuggled contraband aboard a colony transport",
            "ran an unlicensed gambling operation from their workplace",
            "staged a theft to collect the insurance payout",
            "sold classified colony survey data to a rival faction",
            "laundered credits through a shell business",
            "sabotaged a competitor's equipment to win a contract",
            "coerced a junior worker into covering for their mistakes",
            "collected a dead colleague's wages using their stolen identity",
            "skimmed fuel quotas over a prolonged period",
            "leaked proprietary data to an outside buyer",
            "filed fraudulent expense claims over several years",
            "planted false evidence to frame a colleague for their own crime",
            "threatened a witness into staying silent",
            "misappropriated relief funds sent for disaster victims",
            "operated a grey-market parts depot out of their workplace",
            "bribed a port official to clear irregular cargo",
            "stole personal effects from a deceased settler's quarters",
            "poisoned a rival's supply cache to drive them out of business"
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
            "getting caught in someone else's scheme and panicking",
            "trying to escape an abusive situation with no other options",
            "funding an off-world relocation for their children",
            "blackmailed by someone who knew about a past mistake",
            "too proud to admit failure and kept digging deeper",
            "acting on orders from someone they were afraid to refuse",
            "protecting a secret that would have ended their career",
            "trying to reclaim something they believed was stolen from them",
            "caught between two criminal groups with no safe exit",
            "a slow slide from bending rules to breaking them outright",
            "manipulated by someone they trusted completely",
            "grief that made them stop caring about consequences",
            "simply convinced they wouldn't get caught",
            "an addiction that consumed their savings and then their ethics",
            "a bitter rivalry that escalated beyond what they intended",
            "making a split-second decision they couldn't take back",
            "believing the corporation owed them for years of exploitation",
            "covering for a family member who made the first mistake",
            "convinced the target of their crime deserved everything they got",
            "chasing a rumour of easy credits that turned far more complicated",
            "believing they were helping someone who was actually using them"
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
            "deeply ashamed but committed to seeing it through",
            "quick to deflect blame onto anyone nearby",
            "quietly desperate but maintains a calm facade",
            "genuinely remorseful but unwilling to hand themselves in",
            "prone to over-explaining to fill nervous silences",
            "fiercely protective of anyone they consider family",
            "bitter and fatalistic — expects to get caught eventually",
            "obsessively careful about small details, blind to bigger risks",
            "easily led by stronger personalities",
            "tends to underestimate others and talk too much",
            "keeps their own counsel — shares nothing they don't have to",
            "cold and transactional, rarely shows emotion",
            "superstitious — believes fate is actively working against them",
            "uses humour to deflect serious questions",
            "highly adaptive — reads people quickly and adjusts accordingly",
            "stubborn — refuses to admit a plan is failing even when it is",
            "prone to impulsive decisions followed by elaborate rationalisations",
            "compassionate toward strangers but ruthless toward institutions",
            "nostalgic — often references how things used to be before all this",
            "speaks in half-truths — never quite lying, never quite honest",
            "carefully maintains small routines that mask deeper unease"
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
            "a person who found something the target left behind when they fled",
            "a security contractor brought in after the target disappeared",
            "a former business partner now worried about their own exposure",
            "a child or younger relative who noticed things adults missed",
            "a medic who treated the target for injuries they couldn't explain",
            "someone who received a strange message from the target just before they vanished",
            "a rival who quietly benefited from the target's troubles and feels uneasy about it",
            "a courier who made regular deliveries and noticed things changing",
            "a salvager who found materials at a site that shouldn't have been there",
            "a bartender who overheard conversations they didn't understand at the time",
            "a port official who processed an irregular shipment now linked to the target",
            "a former roommate who noticed unexplained cash and goods appearing",
            "a repair technician who spotted unexplained modifications on the target's equipment",
            "a community leader trying to make sense of what the target did to their settlement",
            "someone the target tried to recruit, who refused and doesn't know what to do with that",
            "a retired law enforcement contact with an old grudge and new evidence",
            "a family member torn between loyalty and the harm the target caused",
            "a bureaucrat who signed off on documents they now deeply regret",
            "an eyewitness who left the area and has been reluctant to speak until now",
            "an automated system log that captured anomalies no one thought to check",
            "a journalist or information broker who has been quietly pulling threads"
        };
    }
}
