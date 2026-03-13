using Anthropic.SDK;
using Anthropic.SDK.Messaging; // Message, MessageParameters, RoleType, TextContent
using Retrograde.AI.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Retrograde.AI
{
    public partial class ClaudeAITools : IAITools
    {
        private const string Model = "claude-haiku-4-5-20251001";
        private const int MaxTokens = 8096;
        private const int MaxRetries = 4;
        private const int RetryBaseDelayMs = 15_000; // 15s — rate limit window is per-minute

        private readonly AnthropicClient _client;
        private readonly List<Message> _messages = new();
        private string _systemPrompt;

        public ClaudeAITools()
        {
            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                         ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");

            _client = new AnthropicClient(apiKey);
            _systemPrompt = AISeedData.GetBackgroundPrompt();
        }

        public string RunPrompt(string prompt)
        {
            _messages.Add(new Message(RoleType.User, prompt));
            string textres = CallApi(_messages);
            _messages.Add(new Message(RoleType.Assistant, textres));
            return textres;
        }

        public string RunStatelessPrompt(string prompt)
        {
            var messages = new List<Message>(_messages) { new Message(RoleType.User, prompt) };
            return CallApi(messages);
        }

        // Appends to the system prompt rather than the message list — matches how Claude's API separates system from conversation turns.
        public void InjectContextIntoHistory(string content)
        {
            _systemPrompt += "\n\n" + content;
        }

        public bool ExportConversation()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var name = ExtractOutlawName();
            var slug = string.IsNullOrEmpty(name) ? Guid.NewGuid().ToString("N").Substring(0, 8) : name;
            var loc = $"{timestamp}_{slug}.txt";
            var sb = new StringBuilder();
            sb.AppendLine("[system]");
            sb.AppendLine(_systemPrompt);
            sb.AppendLine();

            foreach (var msg in _messages)
            {
                string role = msg.Role == RoleType.User ? "[user]" : "[assistant]";
                string text = string.Concat(msg.Content.OfType<TextContent>().Select(c => c.Text));
                sb.AppendLine(role);
                sb.AppendLine(text);
                sb.AppendLine();
            }

            try
            {
                File.WriteAllText(loc, sb.ToString());
                Console.WriteLine("Conversation written to " + loc);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing: " + ex.Message);
            }

            return true;
        }

        private string CallApi(List<Message> messages)
        {
            var parameters = new MessageParameters
            {
                Model = Model,
                MaxTokens = MaxTokens,
                SystemMessage = _systemPrompt,
                Messages = messages
            };

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var response = _client.Messages.GetClaudeMessageAsync(parameters).GetAwaiter().GetResult();
                    return CleanText(((TextContent)response.Content[0]).Text);
                }
                catch (Exception ex) when (attempt < MaxRetries && ex.Message.Contains("rate_limit"))
                {
                    int delayMs = RetryBaseDelayMs * (1 << attempt); // 15s, 30s, 60s, 120s
                    Console.WriteLine($"[AITools] Rate limit hit, retrying in {delayMs / 1000}s (attempt {attempt + 1}/{MaxRetries})...");
                    Thread.Sleep(delayMs);
                }
            }

            // Final attempt — let exception propagate
            var finalResponse = _client.Messages.GetClaudeMessageAsync(parameters).GetAwaiter().GetResult();
            return CleanText(((TextContent)finalResponse.Content[0]).Text);
        }

        [GeneratedRegex(@"<\s*Summary\s*>\s*(\w+\s+\w+)")]
        private static partial Regex SummaryNameRegex();

        private string ExtractOutlawName()
        {
            foreach (var msg in _messages)
            {
                if (msg.Role != RoleType.Assistant) continue;
                var text = string.Concat(msg.Content.OfType<TextContent>().Select(c => c.Text));
                var match = SummaryNameRegex().Match(text);
                if (match.Success)
                    return match.Groups[1].Value.Replace(" ", "_");
                break;
            }
            return "";
        }

        private static string CleanText(string text) => text
            .Replace(" — ", " ")
            .Replace("—", " ")
            .Replace("\u201c", "\"")
            .Replace("\u201d", "\"")
            .Replace("\u2019", "'");
    }
}
