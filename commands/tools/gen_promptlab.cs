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
    /// If the file ends with an [assistant] block (e.g. a full generated quest log),
    /// that trailing block is dropped and the last [user] prompt is replayed against
    /// the same conversation history — useful for re-running a specific stage.
    ///
    /// Usage:
    ///   gen_promptlab <file>         replay last [user] block
    ///   gen_promptlab <file> --list  list all [user] blocks with previews
    ///   gen_promptlab <file> <N>     replay Nth [user] block (1-based)
    /// </summary>
    public class gen_promptlab
    {
        private const string Model = "claude-sonnet-4-6";
        private const int MaxTokens = 8096;

        public static int Run(string filePath, string? selector = null)
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

            // Drop trailing [assistant] so the last [user] becomes the replay target.
            if (messages[messages.Count - 1].Role == RoleType.Assistant)
                messages.RemoveAt(messages.Count - 1);

            if (messages.Count == 0)
            {
                Console.WriteLine("No [user] block found to replay.");
                return 1;
            }

            // Build the ordered list of user-block positions in the message list.
            var userPositions = new List<int>();
            for (int i = 0; i < messages.Count; i++)
                if (messages[i].Role == RoleType.User)
                    userPositions.Add(i);

            if (userPositions.Count == 0)
            {
                Console.WriteLine("No [user] block found to replay.");
                return 1;
            }

            // --list: print all user blocks with a short preview and exit.
            if (selector == "--list")
            {
                Console.WriteLine($"[gen_promptlab] {userPositions.Count} user block(s) in {Path.GetFileName(filePath)}:");
                for (int n = 0; n < userPositions.Count; n++)
                {
                    var body = GetMessageText(messages[userPositions[n]]);
                    var preview = body.Length > 80 ? body.Substring(0, 80).Replace('\n', ' ') + "…" : body.Replace('\n', ' ');
                    Console.WriteLine($"  [{n + 1}] {preview}");
                }
                return 0;
            }

            // Select the target user block: default = last, otherwise parse N.
            int targetPos;
            if (string.IsNullOrEmpty(selector))
            {
                targetPos = userPositions[userPositions.Count - 1];
            }
            else if (int.TryParse(selector, out int n) && n >= 1 && n <= userPositions.Count)
            {
                targetPos = userPositions[n - 1];
                Console.WriteLine($"[gen_promptlab] Replaying user block [{n}/{userPositions.Count}].");
            }
            else
            {
                Console.WriteLine($"Invalid selector '{selector}'. Use --list to see available blocks, or pass a number 1–{userPositions.Count}.");
                return 1;
            }

            // Truncate messages to end at the selected user block.
            var replayMessages = messages.GetRange(0, targetPos + 1);

            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                         ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");

            var client = new AnthropicClient(apiKey);

            var parameters = new MessageParameters
            {
                Model = Model,
                MaxTokens = MaxTokens,
                System = string.IsNullOrWhiteSpace(systemPrompt) ? null : new List<SystemMessage> { new SystemMessage(systemPrompt) },
                Messages = replayMessages
            };

            Console.WriteLine($"[gen_promptlab] {replayMessages.Count} message(s) | model: {Model}");
            Console.WriteLine();

            var response = client.Messages.GetClaudeMessageAsync(parameters).GetAwaiter().GetResult();
            var text = ((TextContent)response.Content[0]).Text;

            Console.WriteLine("[assistant]");
            Console.WriteLine(text);

            return 0;
        }

        private static string GetMessageText(Message msg)
        {
            foreach (var part in msg.Content)
                if (part is TextContent tc) return tc.Text;
            return "";
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
