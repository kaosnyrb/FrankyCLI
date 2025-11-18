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

        // Make sure to set your API key in an environment variable: OPENAI_API_KEY
        public static string RunPrompt(string prompt)
        {
            //Dumb switch for fast testing
            //return "Test";

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

            result += "Include newline characters in your response if there are mulitple sentences.\r\n";
            result += "Don't use the folowing characters: — \r\n";

            result += "The following is background information about the universe the story is set in, don't quote any back directly.\r\n\r\n";

            result += "By the year 2330, humanity has expanded into a region of space known as the Settled Systems \r\n. The technology level is advanced yet grounded: interstellar travel relies on Grav Drives that enable faster-than-light jumps \r\n";

            result += "Within this frontier, major factions vie for power. The United Colonies (UC) – a centralized republic founded in 2161 – is the largest and most militarily powerful human government, projecting an idealized “future of space republic” with its capital at New Atlantis on Jemison (Alpha Centauri)\r\n ";

            result += "The Freestar Collective, a loose confederation of three star systems (Cheyenne, Volii, and Narion) with its capital in Akila City; founded on principles of personal freedom and individuality, this faction embodies a more libertarian, frontier spirit\r\n ";
            result += "The UC and Freestar Collective fought a bloody Colony War two decades ago, and though a treaty ended open conflict, an uneasy tension persists between them\r\n ";

            result += "Beyond these two superpowers, the Settled Systems contends with lawless elements. The Crimson Fleet – a notorious coalition of space pirates – operates as a loose alliance of pirate captains under one banner, originating from the Kryx system and now extending its raids into multiple sectors (even establishing footholds in systems like Sagan, Cheyenne, and Narion)\r\n ";

            result += "Meanwhile, the secretive House Va’ruun lurks on the fringes: a fanatical theocracy of zealots worshipping the Great Serpent, known for violently imposing its faith on non-believers\r\n ";

            result += "Key locations reflect the diverse character of this spacefaring era: New Atlantis is a gleaming ultramodern metropolis and bastion of the UC’s governance and trade; Akila City is a walled frontier settlement where Freestar citizens fiercely uphold their independence; Neon is a seedy neon-lit pleasure city built on a giant ocean platform, infamous for its legalized trade in the psychotropic drug Aurora and rife with corporate intrigue and smuggling.\r\n ";

            result += "Across the Settled Systems, themes of colonization and frontier survival, political rivalry and uneasy alliances, flourishing commerce and black-market dealings, rampant piracy, and the enduring human drive to explore the unknown all intermingle. It is a future where humanity’s colonies stand scattered among the stars – full of opportunity and danger in equal measure – as factions compete and adventurers chart new horizons in the vast expanse of Starfield.\r\n ";

            result += "This marks the end of the background information section. Following this is more detail on the prompt to carry out.\r\n\r\n";

            return result;
        }
    }
}
