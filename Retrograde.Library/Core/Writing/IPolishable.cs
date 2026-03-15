using Mutagen.Bethesda.Starfield;
using Retrograde.Models;
using Retrograde.Nouns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Retrograde.Writing
{
    /// <summary>
    /// Represents a single piece of generated text that can be improved by the writing polish pass.
    /// </summary>
    public interface IPolishable
    {
        /// <summary>Unique label used to identify this piece in prompts and responses.</summary>
        string Label { get; }

        /// <summary>
        /// Hard character limit for this piece (0 = no limit).
        /// Passed to the AI as a constraint reminder.
        /// </summary>
        int MaxChars { get; }

        /// <summary>Content category hint for the AI ("log", "book", "dialogue").</summary>
        string ContentType { get; }

        /// <summary>
        /// Narrative position in the quest chain (e.g. "Act 1: Discovery", "Act 2: Investigation 1 of 3").
        /// Empty string means unknown. Used by the polish prompt to enforce narrative flow.
        /// </summary>
        string StoryStage => "";

        string GetText();
        void SetText(string improved);
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decorates any IPolishable with a story-stage label so the polish prompt
    /// knows where each piece falls in the narrative arc.
    /// </summary>
    public class StageAnnotatedPolishable : IPolishable
    {
        private readonly IPolishable _inner;

        public StageAnnotatedPolishable(IPolishable inner, string storyStage)
        {
            _inner    = inner;
            StoryStage = storyStage;
        }

        public string Label       => _inner.Label;
        public int    MaxChars    => _inner.MaxChars;
        public string ContentType => _inner.ContentType;
        public string StoryStage  { get; }

        public string GetText()              => _inner.GetText();
        public void   SetText(string improved) => _inner.SetText(improved);
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wraps a single quest stage log entry.</summary>
    public class QuestLogPolishable : IPolishable
    {
        private readonly Quest _quest;

        public QuestLogPolishable(Quest quest) => _quest = quest;

        public string Label       => "QUEST_LOG:" + (_quest.EditorID ?? _quest.FormKey.ID.ToString("X6"));
        public int    MaxChars    => 0;
        public string ContentType => "log";

        public string GetText()
        {
            var entry = _quest.Stages
                .FirstOrDefault(s => s.Index == 0)?
                .LogEntries.FirstOrDefault();
            return entry?.Entry?.String ?? "";
        }

        public void SetText(string improved)
        {
            var entry = _quest.Stages
                .FirstOrDefault(s => s.Index == 0)?
                .LogEntries.FirstOrDefault();
            if (entry != null)
                entry.Entry = improved;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wraps a Book record's text field.</summary>
    public class BookPolishable : IPolishable
    {
        private readonly Book _book;

        public BookPolishable(Book book) => _book = book;

        public string Label       => "BOOK:" + (_book.EditorID ?? _book.FormKey.ID.ToString("X6"));
        public int    MaxChars    => 0;
        public string ContentType => "book";

        public string GetText()   => _book.Text?.String ?? "";
        public void   SetText(string improved) => _book.Text = improved;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps an NPCDialogueNoun's full conversation. SetText parses the labeled
    /// GREETING/PLAYER/NPC format and calls NPCDialogueNoun.Rewrite() to update
    /// both the Mutagen records and any staged WAV requests.
    /// </summary>
    public class DialogueScriptPolishable : IPolishable
    {
        private readonly NPCDialogueNoun _noun;
        private readonly int             _exchangeCount;

        public DialogueScriptPolishable(NPCDialogueNoun noun)
        {
            _noun          = noun;
            _exchangeCount = noun.Script.Exchanges.Count;
        }

        public string Label       => "DIALOGUE:" + (_noun.QuestRecord.EditorID ?? _noun.QuestRecord.FormKey.ID.ToString("X6"));
        public int    MaxChars    => 0;
        public string ContentType => "dialogue";

        public string GetText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"GREETING: {_noun.Script.NpcGreeting}");
            for (int b = 0; b < _noun.Script.Exchanges.Count; b++)
            {
                var ex = _noun.Script.Exchanges[b];
                sb.AppendLine($"PLAYER{b + 1}: {ex.PlayerPrompt}");
                for (int j = 0; j < ex.NpcReply.Count; j++)
                    sb.AppendLine($"NPC{b + 1}{(char)('a' + j)}: {ex.NpcReply[j]}");
            }
            if (!string.IsNullOrWhiteSpace(_noun.Script.CompletionDismissal))
                sb.AppendLine($"DISMISSAL: {_noun.Script.CompletionDismissal}");
            return sb.ToString().TrimEnd();
        }

        public void SetText(string improved)
        {
            var updated = ParseDialogue(improved);
            if (updated != null)
                _noun.Rewrite(updated);
        }

        private DialogueScript? ParseDialogue(string text)
        {
            string Extract(string label)
            {
                var prefix = label + ":";
                foreach (var line in text.Split('\n'))
                {
                    var t = line.Trim();
                    if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return t[prefix.Length..].Trim();
                }
                return "";
            }

            var script = new DialogueScript { NpcBackground = _noun.Script.NpcBackground };

            script.NpcGreeting = Extract("GREETING");
            if (string.IsNullOrWhiteSpace(script.NpcGreeting))
                return null;

            for (int b = 1; b <= _exchangeCount; b++)
            {
                var playerPrompt = Extract($"PLAYER{b}");
                var npcA         = Extract($"NPC{b}a");
                var npcB         = Extract($"NPC{b}b");
                var replies      = new List<string>();
                if (!string.IsNullOrWhiteSpace(npcA)) replies.Add(npcA);
                if (!string.IsNullOrWhiteSpace(npcB)) replies.Add(npcB);
                if (replies.Count == 0) replies.Add("");
                script.Exchanges.Add(new DialogueExchange { PlayerPrompt = playerPrompt, NpcReply = replies });
            }

            // Preserve existing side options — the polish pass does not rewrite them
            for (int i = 0; i < Math.Min(script.Exchanges.Count, _noun.Script.Exchanges.Count); i++)
                script.Exchanges[i].SideOptions = _noun.Script.Exchanges[i].SideOptions;

            var dismissal = Extract("DISMISSAL");
            if (!string.IsNullOrWhiteSpace(dismissal))
                script.CompletionDismissal = dismissal;
            else
                script.CompletionDismissal = _noun.Script.CompletionDismissal;

            return script;
        }
    }
}
