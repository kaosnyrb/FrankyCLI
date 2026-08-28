using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Retrograde.Grammar
{
    /// <summary>
    /// Post-generation entity grounding check. Extracts proper nouns from generated text
    /// and compares against the stage's entity manifest. Logs violations as warnings.
    /// Observational only — does not block generation.
    /// </summary>
    public static class EntityValidator
    {
        // Common proper nouns that are always allowed (planets, factions, game terms)
        private static readonly HashSet<string> Allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UC", "United Colonies", "Freestar Collective", "Freestar", "Crimson Fleet",
            "Ecliptic", "Spacer", "Spacers", "Va'ruun", "House Va'ruun",
            "Trade Authority", "Constellation", "Ryujin Industries", "Ryujin",
            "Neon", "Akila", "Cydonia", "New Atlantis", "The Well", "The Key",
            "Jemison", "Volii", "Cheyenne", "Alpha Centauri", "Sol", "Mars",
            "Gagarin", "Paradiso", "HopeTown", "Hope Town",
            "Starfield", "Settled Systems", "The Den"
        };

        /// <summary>
        /// Check generated text against the stage's entity manifest.
        /// Logs warnings for any proper nouns not in the manifest or allowlist.
        /// </summary>
        public static void Check(StageSpec spec, string generatedText)
        {
            if (string.IsNullOrWhiteSpace(generatedText) || string.IsNullOrWhiteSpace(spec.EntityManifest))
                return;

            var allowedEntities = ExtractManifestEntities(spec.EntityManifest);
            var foundEntities = ExtractProperNouns(generatedText);

            foreach (var entity in foundEntities)
            {
                if (Allowlist.Contains(entity))
                    continue;
                if (allowedEntities.Any(a => a.IndexOf(entity, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                if (entity.Split(' ').Any(word => allowedEntities.Any(a =>
                    a.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)))
                    continue;

                Console.WriteLine($"[EntityValidator] Stage '{spec.Role}': found '{entity}' — not in manifest");
            }
        }

        /// <summary>
        /// Extract entity names from the EntityManifest XML block.
        /// Looks for the Characters: and Locations: lines.
        /// </summary>
        private static List<string> ExtractManifestEntities(string manifest)
        {
            var entities = new List<string>();

            // Match "Characters: ..." and "Locations: ..." lines
            var linePattern = new Regex(@"(?:Characters|Locations|Factions):\s*(.+)", RegexOptions.IgnoreCase);
            foreach (Match m in linePattern.Matches(manifest))
            {
                string line = m.Groups[1].Value;
                // Split by comma, strip parenthetical notes
                var parts = line.Split(',');
                foreach (var part in parts)
                {
                    string cleaned = Regex.Replace(part, @"\([^)]*\)", "").Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                        entities.Add(cleaned);
                }
            }

            return entities;
        }

        /// <summary>
        /// Extract likely proper nouns from generated text.
        /// Finds capitalized multi-word phrases and single capitalized words
        /// that aren't sentence starters.
        /// </summary>
        private static HashSet<string> ExtractProperNouns(string text)
        {
            var nouns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Multi-word proper nouns (e.g. "Sarah Chen", "Crimson Fleet")
            var multiWord = Regex.Matches(text, @"(?<![.!?]\s)(?<![""']\s)\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)\b");
            foreach (Match m in multiWord)
                nouns.Add(m.Groups[1].Value);

            // Single capitalized words mid-sentence (not after sentence-ending punctuation)
            var singleWord = Regex.Matches(text, @"(?<=[a-z,;:]\s)([A-Z][a-z]{2,})\b");
            foreach (Match m in singleWord)
            {
                string word = m.Groups[1].Value;
                // Filter out common words that happen to be capitalized in certain contexts
                if (!IsCommonWord(word))
                    nouns.Add(word);
            }

            return nouns;
        }

        private static bool IsCommonWord(string word)
        {
            var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "The", "This", "That", "These", "Those", "There", "Their", "They",
                "Here", "Where", "When", "What", "Which", "While", "With",
                "From", "Into", "Upon", "After", "Before", "During", "Between",
                "However", "Moreover", "Furthermore", "Meanwhile", "Therefore",
                "Perhaps", "Already", "Almost", "Always", "Never", "Sometimes"
            };
            return common.Contains(word);
        }
    }
}
