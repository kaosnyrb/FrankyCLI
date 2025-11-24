using OpenAI.Chat;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class AITools
    {
        private static readonly ChatClient _chatClient;
        private static readonly List<ChatMessage> _history = new();

        static AITools()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                         ?? throw new InvalidOperationException("OPENAI_API_KEY not set");

            var client = new OpenAIClient(apiKey);
            _chatClient = client.GetChatClient("gpt-5.1");

            // Optional: seed with a system prompt so the model knows its role
            _history.Add(new SystemChatMessage(
                GetBackgroundPrompt()
            ));
        }

        public static string TestPrompt()
        {
            string prompt = File.ReadAllText("aipromt.txt");

            string results = RunPrompt(prompt);
            Console.WriteLine(results);

            return results;
        }

        public static bool ExportConversation()
        {
            var loc = Guid.NewGuid().ToString().Substring(0, 8) + ".txt";

            string conversation = "";

            foreach (var item in _history)
            {
                if (item is UserChatMessage)
                {
                    conversation += "USER:" +  ((UserChatMessage)item).Content[0].Text.ToString();
                }
                if (item is SystemChatMessage)
                {
                    conversation += "USER:" + ((SystemChatMessage)item).Content[0].Text.ToString();
                }
                if (item is AssistantChatMessage)
                {
                    conversation += "AI:" + ((AssistantChatMessage)item).Content[0].Text.ToString();
                }
                conversation += Environment.NewLine;
            }

            try
            {
                // Create or overwrite the file with the specified content.
                File.WriteAllText(loc, conversation);

                Console.WriteLine("String successfully written to " + loc);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to " + ex.Message);
            }
            return true;
        }

        // Make sure to set your API key in an environment variable: OPENAI_API_KEY
        public static string RunPrompt(string prompt)
        {
            //Dumb switch for fast testing
            //return Guid.NewGuid().ToString().Substring(0, 8);

            // 1. Add user message to history
            _history.Add(new UserChatMessage(prompt));

            // 2. Call the model with the full history
            var res = _chatClient.CompleteChat(_history);

            // 3. Get the assistant’s reply text
            string textres = res.Value.Content[0].Text;

            // 4. Do your existing cleanup
            textres = textres.Replace(" — ", " "); // No EM-dashes with spaces
            textres = textres.Replace("—", "");    // No EM-dashes
            textres = textres.Replace("“", "\"");  // Weird quotes
            textres = textres.Replace("”", "\"");
            textres = textres.Replace("’", "'");

            // 5. Add assistant reply back into history so context is preserved
            _history.Add(new AssistantChatMessage(textres));

            return textres;
        }

        public static string GetBackgroundPrompt()
        {
            string result = "";
            result += "You are a Starfield quest writer crafting an in-game mission narrative.\r\n\r\n";

            result += "You will be generating the story from the final encounter backwards, try and link things together in way that makes sense.\r\n\r\n";
            
            result += "The final order is as follows: Discovery of a lead on the target bounty, Inital investigation where you find the location of a descive  clue, Investigation where you find the location of the target and then the final showdown with the bounty.\r\n\r\n";

            result += "You will recieve a tag like <InitialInvestigation> etc to tell you when we start generating each stage.\r\n\r\n";
            result += "Use the information generated in the last step to inform the current step.\r\n\r\n";
            result += "Avoid mentioning the bounty targets final location until the end of the <DeepInvestigation> step.\r\n\r\n";

            result += "You will recheive new <Lore> entries as things are created, use these to flesh out the story. Incorporate at least one relevant lore detail (faction, tech, or city) to ground the scene.\r\n\r\n";

            result += "Include newline characters in your response if there are mulitple sentences.\r\n";
            result += "Don't use the folowing characters: — \r\n";

            result += "The following is background information about the universe the story is set in, don't quote any back directly.\r\n\r\n";

            result += "By the year 2330, humanity has expanded into a region of space known as the Settled Systems \r\n. The technology level is advanced yet grounded: interstellar travel relies on Grav Drives that enable faster-than-light jumps \r\n";

            result += "Within this frontier, major factions vie for power. The United Colonies (UC) – a centralized republic founded in 2161 – is the largest and most militarily powerful human government, projecting an idealized “future of space republic” with its capital at New Atlantis on Jemison (Alpha Centauri)\r\n ";

            result += "The Freestar Collective, a loose confederation of three star systems (Cheyenne, Volii, and Narion) with its capital in Akila City; founded on principles of personal freedom and individuality, this faction embodies a more libertarian, frontier spirit\r\n ";
            result += "The UC and Freestar Collective fought a bloody Colony War two decades ago, and though a treaty ended open conflict, an uneasy tension persists between them\r\n ";

            result += "Beyond these two superpowers, the Settled Systems contends with lawless elements. The Crimson Fleet – a notorious coalition of space pirates – operates as a loose alliance of pirate captains under one banner, originating from the Kryx system and now extending its raids into multiple sectors (even establishing footholds in systems like Sagan, Cheyenne, and Narion)\r\n ";

            result += "Meanwhile, the secretive House Va’ruun lurks on the fringes: a fanatical theocracy of zealots worshipping the Great Serpent, known for violently imposing its faith on non-believers\r\n ";

            result += "New Atlantis was founded on the planet Jemison in 2156, the same year that humans first arrived in the Alpha Centauri system and confirmed that Jemison was a habitable world and suitable for colonization. The original colony was constructed underground on the edge of a plateau, beneath where the first colony ship, the Galileo, made landfall. The site was chosen to take advantage of a natural waterfall to generate hydroelectric power, and in later years, this district would become known as The Well. In 2160, Jemison was determined to be safe enough, and the colony's need for expansion to house evacuees from Earth pressing enough, that construction of residential and commercial structures on the planet's surface was approved and got underway. The United Colonies designated New Atlantis as its capital in 2161.[3][4]\r\n\r\nIn 2330, New Atlantis is the largest and most populous colony in the Settled Systems. The MAST Building, headquarters of the UC government, dominates the skyline, and is flanked by the skyscrapers of the Residential and Commercial districts. The central MAST District includes the MAST Building, embassies of the Freestar Collective and House Va'ruun, a sprawling park, and The Lodge, which is the headquarters of the private exploration group Constellation. The Spaceport sits below the city proper at the foot of the cliffs, and is connected to it by the New Atlantis Transit network";

            result += "The pleasure city of Neon was originally a fishing platform constructed on Volii Alpha by Xenofresh Fisheries. The corporation discovered that the planet's native Chasmbass produced a substance with psychotropic effects, and decided to switch the entire operation to creating the drug Aurora and selling it on the platform.[1] This put Neon on the map, and in 2187 the fishing platform revamped itself into a full-fledged colony dedicated to tourism and commerce. When the Volii system joined the Freestar Collective in 2189, Neon alone retained the rights to produce and sell Aurora, while the drug was criminalized everywhere else in the Settled Systems.\r\n\r\nIn 2232, Phylicia Corbin founded Generdyne Corporation to construct the Conduction Grid over Neon, an undertaking that required almost 25 years to complete. Also known as “the Span”, Neon's Conduction Grid has a unique electromagnetic absorption system that converts lightning strikes into energy that is then stored within power cells in Neon's Underbelly for later distribution. No other city in the Settled Systems has a Conduction Grid, as the structure requires a lightning-rich environment like that found on Volii Alpha in order to function effectively.\r\n\r\nRyujin Industries, which grew into the Settled Systems' most powerful megacorporation in only four years thanks to the invention of its flagship product, the neuroamp, finished construction of its Ryujin Tower headquarters in Neon in 2311. It was built on the opposite end of the platform from the Trade Tower, where Xenofresh Fisheries and other major businesses are based.\r\n\r\nIn 2330, Neon is the largest commercial hub in the Settled Systems, as well as a popular tourist destination due to the legal Aurora trade. Xenofresh Fisheries retains full ownership of the platform, and Xenofresh CEO Benjamin Bayu holds the position of Administrator of Neon, giving him absolute authority over the city's laws. Neon's security service ruthlessly guards the city's status as the sole legitimate purveyor of Aurora, and anyone caught attempting to smuggle any amount of Aurora outside the city will receive harsh punishment. While the corporations take advantage of Neon's lax regulations to endlessly vie for market dominance through espionage and other cutthroat tactics, a number of rival gangs have also flourished in Neon. These include the Seokguh Syndicate, the Ebbside Strikers, and the Disciples. The city is rife with criminal activity.\r\n\r\nDistricts of Neon include Bayu Plaza, Ikuchi Market, Ebbside, Underbelly, and the Spaceport. The Spaceport occupies two small secondary platforms just outside the main Neon platform. The Underbelly consists of the underside of the main platform. Bayu Plaza, Ikuchi Market, and Ebbside are all located on the Upper Platform, with the first two districts inside the enclosed Neon Core section, and the third district encompassing the Upper Platform's exterior.";

            result += "Akila City was founded in 2167 by Solomon Coe, at the site of his first camp on Akila. Solomon named the settlement “Akila City” despite being its only inhabitant at the time because he was confident that other settlers would soon follow him. He was correct, and soon other families, such as the Cartwrights and Hasanovs, arrived at Akila and helped construct Akila City. Akila City grew into a bustling regional hub, and when Solomon engineered an alliance between the Cheyenne and Volii systems, the city was made the capital of Freestar Collective.\r\n\r\nThe oldest part of Akila City is The Core, an elevated and fortified settlement where the founding families built their homes. Since countless Ashta roamed the planet and proved to be a cunning and deadly threat, Akila City had to be walled to keep them out. The center of the settlement was The Rock, which initially served as both a cantina and a meeting hall. Over time, new sets of walls were built around The Core, with the first expansion creating the Midtown district and the spaceport, and the third and most recent expansion creating The Stretch. In 2231, following the creation of the Freestar Rangers by the Council of Governors, the upper floors of The Rock were set aside for the Rangers' use. By 2330, The Core had become an exclusive neighborhood where descendants of the founding families resided, though vacated homes could be sold to outsiders with the approval of the city's historical society. Midtown and Coe Plaza hosted most of the city's businesses, services, and residences, while the poor and unfortunate lived in improvised shacks built in The Stretch.";

            result += "During the early years of the colonization of Alpha Centauri, Gagarin Landing was originally founded as a mining settlement in a canyon on the planet Gagarin. Over time, the mines were abandoned as the settlement grew into a bustling industrial center and dedicated itself to the manufacture of mechs for the United Colonies. When the Colony War ended in 2311 and mech production was banned by the Armistice, Gagarin Landing's mech planets were shuttered and the settlement's economy was gutted. A large number of residents left to find work elsewhere, and most local businesses were forced to close, causing Gagarin Landing to lapse into squalor and crime over the following decade. Around 2320, UC Security officer Dalitso Pretorius was assigned to the settlement's security force and oversaw successful policies to reduce crime levels.\r\n\r\nBy 2330, Gagarin Landing's fortunes finally began to turn. Heavy machinery manufacturer Arc Might purchased the settlement's largest mech plant and refurbished it to produce robotics. Arc Might also promoted Gagarin Landing to other corporations as an ideal location for investment and expansion due to the presence of established infrastructure that could be bought up at rock-bottom prices. Other major corporations operating in the settlement include Reliant Medical, which had set up shop decades earlier and weathered the economic downturn, and Centauri Mills, another recent arrival like Arc Might.\r\n\r\nThe off-world corporations have not been universally welcomed by Gagarin Landing residents. Arc Might is criticized for promising to bring jobs to the settlement, only to immediately automate half of its production facility to keep costs low. Rumors also swirl that the corporations plan significant changes for the settlement's layout, such as building \"priority catwalks\" above the settlement so that corporate employees and executives can avoid mingling with the citizenry. Disgruntled residents have occasionally been known to vandalize corporate structures and steal their property as a means of protest, but UC Security has been careful to prevent tensions from erupting into outright violence.";

            result += "Waggoner Farm is a farm on Montara Luna, Cheyenne System. It is inhabited by Mikaela Waggoner and her father, Waylon Waggoner.\r\n\r\nThey mainly grow grains and root vegetables, rotating between varieties to benefit the soil. They also have livestock and other animals, primarily to keep them fed, although they do sell any excess meat and eggs.";

            result += "New Homestead is a settlement on Titan, a moon of Saturn in the Sol system, and is considered one of the oldest colonies. Initially founded by NASA as the \"Titan Astrobase\" for xenobiology research, it was later transferred to the public as a historical site and is now a tourist destination and settlement. You can find it by navigating to the Sol system, locating Saturn, and then finding the moon Titan, where you will see the New Homestead landing marker. ";

            result += "HopeTown was the first permanent colony to be built on Polvo, which up to that point had been left almost completely undeveloped aside from a handful of hard-scrabble farms. The settlement is the headquarters of starship manufacturer HopeTech, as well as the corporation's main production facility and housing for its employees. HopeTech founder and president Ron Hope manages both the company and the settlement, and holds a seat on the Freestar Collective's Council of Governors. As HopeTown's residents and employees will frequently remind visitors, HopeTown would be nothing without Ron Hope.";

            result += "Paradiso is a luxury beach resort located on the planet Porrima II in the Porrima system. It is run by the Paradiso Group; A cutthroat, cheapskate corporate board and operates outside the jurisdiction of the United Colonies and the Freestar Collective.";

            result += "Across the Settled Systems, themes of colonization and frontier survival, political rivalry and uneasy alliances, flourishing commerce and black-market dealings, rampant piracy, and the enduring human drive to explore the unknown all intermingle. It is a future where humanity’s colonies stand scattered among the stars – full of opportunity and danger in equal measure – as factions compete and adventurers chart new horizons in the vast expanse of Starfield.\r\n ";

            result += "This marks the end of the background information section. Following this is more detail on the prompt to carry out.\r\n\r\n";

            return result;
        }
    }
}
