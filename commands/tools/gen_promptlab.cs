using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FrankyCLI
{
    /// <summary>
    /// Prompt iteration tool. Loads a conversation file in [role] block format,
    /// sends it to the Claude API, and prints the next assistant response.
    ///
    /// File format:
    ///   [system]
    ///   You are a Starfield quest writer...
    ///
    ///   [user]
    ///   Write me a quest hook about a missing cargo ship.
    ///
    ///   [assistant]
    ///   The cargo ship Meridian's Wake went dark three days ago...
    ///
    ///   [user]
    ///   Now make it more tense.
    ///
    /// Usage: gen_promptlab &lt;conversationfile&gt;
    /// </summary>
    public class gen_promptlab
    {
        private const string Model = "claude-sonnet-4-6";
        private const int MaxTokens = 8096;

        public static int Run(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return 1;
            }

            var (systemPrompt, messages) = ParseConversationFile(filePath);

            if (messages.Count == 0)
            {
                Console.WriteLine("No messages found in conversation file.");
                return 1;
            }

            if (messages[messages.Count - 1].Role != RoleType.User)
            {
                Console.WriteLine("Last message must be a [user] block.");
                return 1;
            }

            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                         ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");

            var client = new AnthropicClient(apiKey);

            var parameters = new MessageParameters
            {
                Model = Model,
                MaxTokens = MaxTokens,
                SystemMessage = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
                Messages = messages
            };

            Console.WriteLine($"[gen_promptlab] {messages.Count} message(s) | model: {Model}");
            Console.WriteLine();

            var response = client.Messages.GetClaudeMessageAsync(parameters).GetAwaiter().GetResult();
            var text = ((TextContent)response.Content[0]).Text;

            Console.WriteLine("[assistant]");
            Console.WriteLine(text);

            return 0;
        }

        private static (string systemPrompt, List<Message> messages) ParseConversationFile(string path)
        {
            var content = File.ReadAllText(path, Encoding.UTF8);
            var sections = new List<(string role, string body)>();

            string? currentRole = null;
            var currentBody = new StringBuilder();

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');

                if (line == "[system]" || line == "[user]" || line == "[assistant]")
                {
                    if (currentRole != null)
                        sections.Add((currentRole, currentBody.ToString().Trim()));
                    currentRole = line[1..^1];
                    currentBody.Clear();
                }
                else if (currentRole != null)
                {
                    currentBody.AppendLine(line);
                }
            }

            if (currentRole != null)
                sections.Add((currentRole, currentBody.ToString().Trim()));

            string systemPrompt = "";
            var messages = new List<Message>();

            foreach (var (role, body) in sections)
            {
                if (role == "system")
                    systemPrompt = body;
                else
                    messages.Add(new Message(role == "user" ? RoleType.User : RoleType.Assistant, body));
            }

            return (systemPrompt, messages);
        }
    }
}
