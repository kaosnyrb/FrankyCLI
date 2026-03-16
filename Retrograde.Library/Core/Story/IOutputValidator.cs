using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Retrograde.Story
{
    /// <summary>
    /// Validates AI-generated text against BeatFacts and style rules.
    /// Returns a list of specific violations for retry feedback.
    /// </summary>
    public interface IOutputValidator
    {
        /// <summary>
        /// Validate the given text. Returns an empty list if valid,
        /// or a list of specific violation messages for retry feedback.
        /// </summary>
        List<string> Validate(string text, BeatFacts facts, string targetName);
    }

    /// <summary>
    /// Checks for invented proper nouns — capitalized words that aren't
    /// the target name, sentence starters, or known location/faction names.
    /// </summary>
    public class ProperNounValidator : IOutputValidator
    {
        // Known Starfield proper nouns that are always allowed
        private static readonly HashSet<string> KnownProperNouns = new(StringComparer.OrdinalIgnoreCase)
        {
            // Factions
            "UC", "Freestar", "Crimson", "Fleet", "Va'ruun", "Ecliptic", "Spacers", "Trackers", "Alliance",
            "Vanguard", "SysDef", "Rangers", "Xenofresh", "Ryujin", "GalBank", "HopeTech", "Argos",

            // Major locations
            "Neon", "Akila", "Jemison", "Mars", "Cydonia", "Titan", "Polvo", "Volii",
            "New", "Atlantis", "City", "Den", "Clinic", "Paradiso", "Strip",

            // Common title words
            "The", "Captain", "Officer", "Commander", "Doctor", "Agent",

            // Setting words that get capitalized
            "Settled", "Systems", "Colony", "War", "Armistice",
        };

        public List<string> Validate(string text, BeatFacts facts, string targetName)
        {
            var violations = new List<string>();
            var allowed = BuildAllowedSet(facts, targetName);

            // Find capitalized words that aren't at sentence start
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < words.Length; i++) // skip first word (sentence start)
                {
                    var word = words[i].Trim(',', ':', ';', '"', '\'', '(', ')');
                    if (word.Length < 2) continue;
                    if (!char.IsUpper(word[0])) continue;
                    if (allowed.Contains(word)) continue;
                    if (KnownProperNouns.Contains(word)) continue;

                    violations.Add($"Possible invented proper noun: '{word}'. Only use names from the allowed vocabulary.");
                }
            }

            return violations.Distinct().Take(3).ToList(); // Cap feedback to avoid overwhelming
        }

        private HashSet<string> BuildAllowedSet(BeatFacts facts, string targetName)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Target name words
            foreach (var word in targetName.Split(' '))
                set.Add(word);

            // Location words
            foreach (var word in facts.Location.Split(' '))
                set.Add(word);

            // NPC role words (some may be capitalized, like "UC security officer")
            foreach (var role in facts.AllowedNpcRoles)
                foreach (var word in role.Split(' '))
                    if (char.IsUpper(word[0]))
                        set.Add(word);

            // Object words
            foreach (var obj in facts.AllowedObjects)
                foreach (var word in obj.Split(' '))
                    if (word.Length > 0 && char.IsUpper(word[0]))
                        set.Add(word);

            return set;
        }
    }

    /// <summary>
    /// Checks for literary/atmospheric language that violates the terse style rules.
    /// </summary>
    public class StyleValidator : IOutputValidator
    {
        private static readonly string[] BannedWords =
        {
            "mysterious", "ominous", "desperate", "shadows", "whispers", "echoes",
            "lurking", "haunting", "enigmatic", "cryptic", "sinister", "foreboding",
            "eerie", "unsettling", "chilling", "dread", "looming", "spectral",
            "ethereal", "brooding", "ominously", "desperately", "hauntingly",
            "neon-drenched", "war-torn", "battle-scarred",
        };

        private static readonly string[] BannedPatterns =
        {
            @"like a \w+",           // similes
            @"as if \w+",            // similes
            @"seemed to \w+",        // vague attribution
            @"couldn't help but",    // literary cliche
            @"little did \w+ know",  // omniscient narrator
        };

        public List<string> Validate(string text, BeatFacts facts, string targetName)
        {
            var violations = new List<string>();
            var lower = text.ToLowerInvariant();

            foreach (var word in BannedWords)
            {
                if (lower.Contains(word))
                    violations.Add($"Remove '{word}' — use factual language only.");
            }

            foreach (var pattern in BannedPatterns)
            {
                if (Regex.IsMatch(lower, pattern))
                    violations.Add($"Remove literary pattern matching '{pattern}' — be direct.");
            }

            return violations.Take(3).ToList();
        }
    }

    /// <summary>
    /// Validates quest names: 2-5 words, no punctuation beyond hyphens, concrete nouns.
    /// </summary>
    public class QuestNameValidator : IOutputValidator
    {
        public List<string> Validate(string text, BeatFacts facts, string targetName)
        {
            var violations = new List<string>();
            var trimmed = text.Trim().Trim('"');

            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
                violations.Add("Quest name too short — must be 2-5 words.");
            if (words.Length > 5)
                violations.Add("Quest name too long — must be 2-5 words.");

            if (Regex.IsMatch(trimmed, @"[.!?;:,]"))
                violations.Add("Quest name must not contain punctuation.");

            return violations;
        }
    }

    /// <summary>
    /// Validates log messages: must contain at least one proper noun from BeatFacts.
    /// </summary>
    public class LogMessageValidator : IOutputValidator
    {
        public List<string> Validate(string text, BeatFacts facts, string targetName)
        {
            var violations = new List<string>();

            // Must mention either the target name or the location
            var lower = text.ToLowerInvariant();
            bool hasTarget = targetName.Split(' ').Any(w => lower.Contains(w.ToLowerInvariant()));
            bool hasLocation = facts.Location.Split(' ')
                .Where(w => w.Length > 2)
                .Any(w => lower.Contains(w.ToLowerInvariant()));

            if (!hasTarget && !hasLocation)
                violations.Add($"Log must mention either '{targetName}' or '{facts.Location}'.");

            if (text.Length > 250)
                violations.Add("Log message too long — max 250 characters.");

            return violations;
        }
    }

    /// <summary>
    /// Composite validator that runs multiple validators in sequence.
    /// </summary>
    public class CompositeValidator : IOutputValidator
    {
        private readonly List<IOutputValidator> _validators;

        public CompositeValidator(params IOutputValidator[] validators)
        {
            _validators = validators.ToList();
        }

        public List<string> Validate(string text, BeatFacts facts, string targetName)
        {
            var all = new List<string>();
            foreach (var v in _validators)
                all.AddRange(v.Validate(text, facts, targetName));
            return all;
        }
    }
}
