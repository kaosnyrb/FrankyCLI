using OpenAI;
using OpenAI.Chat;
using Retrograde.AI.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace Retrograde.AI
{
    public class OpenAITools : IAITools
    {
        private readonly ChatClient _chatClient;
        private readonly List<ChatMessage> _history = new();

        public OpenAITools()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                         ?? throw new InvalidOperationException("OPENAI_API_KEY not set");

            var client = new OpenAIClient(apiKey);
            _chatClient = client.GetChatClient("gpt-5.1");

            _history.Add(new SystemChatMessage(AISeedData.GetBackgroundPrompt()));
        }

        public string RunPrompt(string prompt)
        {
            _history.Add(new UserChatMessage(prompt));
            var res = _chatClient.CompleteChat(_history);
            string textres = CleanText(res.Value.Content[0].Text);
            _history.Add(new AssistantChatMessage(textres));
            return textres;
        }

        public string RunStatelessPrompt(string prompt)
        {
            var messages = new List<ChatMessage>(_history) { new UserChatMessage(prompt) };
            var res = _chatClient.CompleteChat(messages);
            return CleanText(res.Value.Content[0].Text);
        }

        public void InjectContextIntoHistory(string content)
        {
            _history.Add(new SystemChatMessage(content));
        }

        public bool ExportConversation()
        {
            var loc = Guid.NewGuid().ToString().Substring(0, 8) + ".txt";
            string conversation = "";

            foreach (var item in _history)
            {
                if (item is UserChatMessage u)
                    conversation += "USER:" + u.Content[0].Text;
                else if (item is SystemChatMessage s)
                    conversation += "SYSTEM:" + s.Content[0].Text;
                else if (item is AssistantChatMessage a)
                    conversation += "AI:" + a.Content[0].Text;

                conversation += Environment.NewLine;
            }

            try
            {
                File.WriteAllText(loc, conversation);
                Console.WriteLine("Conversation written to " + loc);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing: " + ex.Message);
            }

            return true;
        }

        private static string CleanText(string text) => text
            .Replace(" — ", " ")
            .Replace("—", " ")
            .Replace("\u201c", "\"")
            .Replace("\u201d", "\"")
            .Replace("\u2019", "'");
    }
}
