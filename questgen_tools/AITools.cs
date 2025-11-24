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
            result += "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\n";
            //            result += "Write in space pulp fiction style.\r\n\r\n";
            result += "You will be generating the story from the final encounter backwards, try and link things together in way that makes sense.\r\n\r\n";
            
            result += "The final order is as follows: Discovery of a lead on the target bounty, Inital investigation where you find the location of a descive  clue, Investigation where you find the location of the target and then the final showdown with the bounty.\r\n\r\n";

            result += "You will recieve a tag like <InitalInvestigation> etc to tell you when we start generating each stage.\r\n\r\n";
            result += "Use the information generated in the last step to inform the current step.\r\n\r\n";
            result += "Avoid mentioning the bounty targets final location until the end of the <DeepInvestigation> step.\r\n\r\n";

            result += "You will recheive new <Lore> entries as things are created, use these to flesh out the story.\r\n\r\n";

            result += "Include newline characters in your response if there are mulitple sentences.\r\n";
            result += "Don't use the folowing characters: — \r\n";

            result += "The following is background information about the universe the story is set in, don't quote any back directly.\r\n\r\n";

            result += "By the year 2330, humanity has expanded into a region of space known as the Settled Systems \r\n. The technology level is advanced yet grounded: interstellar travel relies on Grav Drives that enable faster-than-light jumps \r\n";

            result += "Within this frontier, major factions vie for power. The United Colonies (UC) – a centralized republic founded in 2161 – is the largest and most militarily powerful human government, projecting an idealized “future of space republic” with its capital at New Atlantis on Jemison (Alpha Centauri)\r\n ";

            result += "The Freestar Collective, a loose confederation of three star systems (Cheyenne, Volii, and Narion) with its capital in Akila City; founded on principles of personal freedom and individuality, this faction embodies a more libertarian, frontier spirit\r\n ";
            result += "The UC and Freestar Collective fought a bloody Colony War two decades ago, and though a treaty ended open conflict, an uneasy tension persists between them\r\n ";

            result += "Beyond these two superpowers, the Settled Systems contends with lawless elements. The Crimson Fleet – a notorious coalition of space pirates – operates as a loose alliance of pirate captains under one banner, originating from the Kryx system and now extending its raids into multiple sectors (even establishing footholds in systems like Sagan, Cheyenne, and Narion)\r\n ";

            result += "Meanwhile, the secretive House Va’ruun lurks on the fringes: a fanatical theocracy of zealots worshipping the Great Serpent, known for violently imposing its faith on non-believers\r\n ";

            result += "Waggoner Farm is a farm on Montara Luna, Cheyenne System. It is inhabited by Mikaela Waggoner and her father, Waylon Waggoner.\r\n\r\nThey mainly grow grains and root vegetables, rotating between varieties to benefit the soil. They also have livestock and other animals, primarily to keep them fed, although they do sell any excess meat and eggs.";
            result += "New Homestead is a settlement on Titan, a moon of Saturn in the Sol system, and is considered one of the oldest colonies. Initially founded by NASA as the \"Titan Astrobase\" for xenobiology research, it was later transferred to the public as a historical site and is now a tourist destination and settlement. You can find it by navigating to the Sol system, locating Saturn, and then finding the moon Titan, where you will see the New Homestead landing marker. ";

            result += "Key locations reflect the diverse character of this spacefaring era: New Atlantis is a gleaming ultramodern metropolis and bastion of the UC’s governance and trade; Akila City is a walled frontier settlement where Freestar citizens fiercely uphold their independence; Neon is a seedy neon-lit pleasure city built on a giant ocean platform, infamous for its legalized trade in the psychotropic drug Aurora and rife with corporate intrigue and smuggling.\r\n ";

            result += "During the early years of the colonization of Alpha Centauri, Gagarin Landing was originally founded as a mining settlement in a canyon on the planet Gagarin. Over time, the mines were abandoned as the settlement grew into a bustling industrial center and dedicated itself to the manufacture of mechs for the United Colonies. When the Colony War ended in 2311 and mech production was banned by the Armistice, Gagarin Landing's mech planets were shuttered and the settlement's economy was gutted. A large number of residents left to find work elsewhere, and most local businesses were forced to close, causing Gagarin Landing to lapse into squalor and crime over the following decade. Around 2320, UC Security officer Dalitso Pretorius was assigned to the settlement's security force and oversaw successful policies to reduce crime levels.\r\n\r\nBy 2330, Gagarin Landing's fortunes finally began to turn. Heavy machinery manufacturer Arc Might purchased the settlement's largest mech plant and refurbished it to produce robotics. Arc Might also promoted Gagarin Landing to other corporations as an ideal location for investment and expansion due to the presence of established infrastructure that could be bought up at rock-bottom prices. Other major corporations operating in the settlement include Reliant Medical, which had set up shop decades earlier and weathered the economic downturn, and Centauri Mills, another recent arrival like Arc Might.\r\n\r\nThe off-world corporations have not been universally welcomed by Gagarin Landing residents. Arc Might is criticized for promising to bring jobs to the settlement, only to immediately automate half of its production facility to keep costs low. Rumors also swirl that the corporations plan significant changes for the settlement's layout, such as building \"priority catwalks\" above the settlement so that corporate employees and executives can avoid mingling with the citizenry. Disgruntled residents have occasionally been known to vandalize corporate structures and steal their property as a means of protest, but UC Security has been careful to prevent tensions from erupting into outright violence.";

            result += "Across the Settled Systems, themes of colonization and frontier survival, political rivalry and uneasy alliances, flourishing commerce and black-market dealings, rampant piracy, and the enduring human drive to explore the unknown all intermingle. It is a future where humanity’s colonies stand scattered among the stars – full of opportunity and danger in equal measure – as factions compete and adventurers chart new horizons in the vast expanse of Starfield.\r\n ";

            result += "This marks the end of the background information section. Following this is more detail on the prompt to carry out.\r\n\r\n";

            return result;
        }
    }
}
