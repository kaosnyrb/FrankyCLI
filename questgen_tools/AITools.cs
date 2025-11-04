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
        // Make sure to set your API key in an environment variable: OPENAI_API_KEY
        public static string RunPrompt(string prompt)
        {
            //Dumb switch for fast testing
            return "Test";

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var client = new OpenAIClient(apiKey);

            var chat = client.GetChatClient("gpt-5");
            //var chat = client.GetChatClient("gpt-5-mini");
            var res = chat.CompleteChat(new UserChatMessage(prompt));
            return res.Value.Content[0].Text;
        }
    }
}
