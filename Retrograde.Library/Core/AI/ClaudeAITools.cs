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
        private const string Model            = "claude-haiku-4-5-20251001";
        private const string HighQualityModel = "claude-sonnet-4-6";
        private const int MaxTokens = 8096;
        private const int MaxRetries = 4;
        private const int RetryBaseDelayMs = 15_000; // 15s — rate limit window is per-minute
        private const int CompressionThresholdChars = 4_000;

        private readonly AnthropicClient _client;
        private readonly List<Message> _messages = new();
        private readonly List<(string Prompt, string Response)> _statelessLog = new();
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
            MaybeCompressHistory();
            string textres = CallApi(_messages);
            _messages.Add(new Message(RoleType.Assistant, textres));
            return textres;
        }

        public string RunPromptHighQuality(string prompt)
        {
            _messages.Add(new Message(RoleType.User, prompt));
            MaybeCompressHistory();
            string textres = CallApi(_messages, HighQualityModel);
            _messages.Add(new Message(RoleType.Assistant, textres));
            return textres;
        }

        public string RunStatelessPrompt(string prompt)
        {
            var messages = new List<Message>(_messages) { new Message(RoleType.User, prompt) };
            string response = CallApi(messages);
            _statelessLog.Add((prompt, response));
            return response;
        }

        public string RunIsolatedPrompt(string systemContext, string prompt)
        {
            var messages = new List<Message> { new Message(RoleType.User, prompt) };
            string response = CallApiWithSystem(messages, systemContext);
            _statelessLog.Add(($"[isolated] {prompt}", response));
            return response;
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

            if (_statelessLog.Count > 0)
            {
                sb.AppendLine("--- stateless ---");
                sb.AppendLine();
                foreach (var (prompt, response) in _statelessLog)
                {
                    sb.AppendLine("[user]");
                    sb.AppendLine(prompt);
                    sb.AppendLine();
                    sb.AppendLine("[assistant]");
                    sb.AppendLine(response);
                    sb.AppendLine();
                }
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

        private void MaybeCompressHistory()
        {
            int assistantChars = _messages
                .Where(m => m.Role == RoleType.Assistant)
                .Sum(m => string.Concat(m.Content.OfType<TextContent>().Select(c => c.Text)).Length);

            if (assistantChars < CompressionThresholdChars) return;

            Console.WriteLine($"[AITools] Compressing history ({assistantChars} assistant chars)...");

            var historyText = new StringBuilder();
            foreach (var msg in _messages)
            {
                string role = msg.Role == RoleType.User ? "[user]" : "[assistant]";
                string text = string.Concat(msg.Content.OfType<TextContent>().Select(c => c.Text));
                historyText.AppendLine(role);
                historyText.AppendLine(text);
                historyText.AppendLine();
            }

            string summary = CallApi(new List<Message> { new Message(RoleType.User, BuildCompressionPrompt(historyText.ToString())) });

            var currentPrompt = _messages[^1];
            _messages.Clear();
            _messages.Add(new Message(RoleType.User, "[context compressed]"));
            _messages.Add(new Message(RoleType.Assistant, summary));
            _messages.Add(currentPrompt);

            Console.WriteLine($"[AITools] Compression complete ({summary.Length} chars).");
        }

        private static string BuildCompressionPrompt(string historyText) =>
            "You are a factual context compressor. The following is a conversation generating a Starfield quest. " +
            "Extract all concrete facts into a structured summary. Do NOT paraphrase or add interpretation.\n\n" +
            "You MUST preserve exactly:\n" +
            "- The outlaw's full name on the very first line, wrapped exactly as: <Summary>First Last</Summary>\n" +
            "- All proper nouns: character names, location names, faction names, item names (exact spelling)\n" +
            "- Arc type and template names (exact strings)\n" +
            "- Per-stage details: location, objective, named NPCs, clue items, quest stage indices\n" +
            "- StageBridge clues linking stages\n" +
            "- Any generated dialogue, log text, or book text (verbatim if short, summarized if long)\n" +
            "- Faction, role, crime, personality, and motives\n\n" +
            "Output format: structured list of facts, not prose. Use short labeled entries like:\n" +
            "  OUTLAW: First Last\n  FACTION: ...\n  STAGE 0: location=..., objective=..., npc=...\n\n" +
            "Maximum 600 words. Prefer exact quotes over paraphrase. " +
            "The very first line must be <Summary>First Last</Summary> with the outlaw's exact full name.\n\n" +
            "[CONVERSATION]\n" + historyText;

        private static List<Message> TrimUserHistory(List<Message> messages)
        {
            if (messages.Count <= 1) return messages;
            var trimmed = new List<Message>(messages.Count);
            for (int i = 0; i < messages.Count - 1; i++)
            {
                var msg = messages[i];
                trimmed.Add(msg.Role == RoleType.User
                    ? new Message(RoleType.User, "[omitted]")
                    : msg);
            }
            trimmed.Add(messages[^1]); // last message (current prompt) sent in full
            return trimmed;
        }

        private string CallApiWithSystem(List<Message> messages, string systemPrompt, string model = Model)
        {
            var parameters = new MessageParameters
            {
                Model = model,
                MaxTokens = MaxTokens,
                System = new List<SystemMessage>
                {
                    new SystemMessage(systemPrompt)
                },
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
                    int delayMs = RetryBaseDelayMs * (1 << attempt);
                    Console.WriteLine($"[AITools] Rate limit hit, retrying in {delayMs / 1000}s (attempt {attempt + 1}/{MaxRetries})...");
                    Thread.Sleep(delayMs);
                }
            }

            var finalResponse = _client.Messages.GetClaudeMessageAsync(parameters).GetAwaiter().GetResult();
            return CleanText(((TextContent)finalResponse.Content[0]).Text);
        }

        private string CallApi(List<Message> messages, string model = Model)
        {
            messages = TrimUserHistory(messages);
            var parameters = new MessageParameters
            {
                Model = model,
                MaxTokens = MaxTokens,
                System = new List<SystemMessage>
                {
                    new SystemMessage(_systemPrompt, new CacheControl { Type = CacheControlType.ephemeral })
                },
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
