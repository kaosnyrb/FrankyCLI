using System.Collections.Generic;
using System.Text;

namespace Retrograde.Story
{
    /// <summary>
    /// Sealed context for an air-gapped AI call. Contains everything the AI
    /// needs for a single generation — and nothing more.
    ///
    /// Multiple envelopes per beat: log/dialogue get the full envelope;
    /// pickup messages get a narrow envelope (BridgeText + destination only);
    /// bridges get the narrowest (just two location names).
    /// </summary>
    public class ContextEnvelope
    {
        /// <summary>
        /// Frozen lore summary — curated subset: target name, crime,
        /// personality, factions involved. NOT the full LoreContext.
        /// </summary>
        public string LoreSummary = "";

        /// <summary>
        /// Cast sheet — names, roles, traits. Read-only reference.
        /// </summary>
        public string CastSheet = "";

        /// <summary>
        /// This beat's explicit facts — the ONLY specific details the AI
        /// may reference.
        /// </summary>
        public BeatFacts Facts = new();

        /// <summary>
        /// What the player knows at this point in the story.
        /// Built incrementally from BeatFacts.ClueLearned in story order.
        /// Contains NO information about future stages.
        /// </summary>
        public List<string> PlayerKnowledge = new();

        /// <summary>
        /// Schema-level rules for this content type.
        /// E.g., "You are writing for a bounty hunter. Style: terse field notes."
        /// </summary>
        public string ContentRules = "";

        /// <summary>
        /// The target character's name — used in vocabulary blocks
        /// as the only proper noun the AI may reference by name.
        /// </summary>
        public string TargetName = "";

        /// <summary>
        /// Assembles the envelope into a system prompt string for RunIsolatedPrompt.
        /// </summary>
        public string BuildSystemPrompt()
        {
            var sb = new StringBuilder();

            // Content rules first — sets the tone for everything
            if (!string.IsNullOrWhiteSpace(ContentRules))
            {
                sb.AppendLine(ContentRules);
                sb.AppendLine();
            }

            // Lore summary — grounding context
            if (!string.IsNullOrWhiteSpace(LoreSummary))
            {
                sb.AppendLine("## Lore Context");
                sb.AppendLine(LoreSummary);
                sb.AppendLine();
            }

            // Cast sheet
            if (!string.IsNullOrWhiteSpace(CastSheet))
            {
                sb.AppendLine("## Characters");
                sb.AppendLine(CastSheet);
                sb.AppendLine();
            }

            // Player knowledge (story-order, up to this beat)
            if (PlayerKnowledge.Count > 0)
            {
                sb.AppendLine("## What the player knows so far");
                for (int i = 0; i < PlayerKnowledge.Count; i++)
                    sb.AppendLine($"{i + 1}. {PlayerKnowledge[i]}");
                sb.AppendLine();
            }

            // Beat facts — the allowed vocabulary
            sb.AppendLine("## This beat's allowed details");
            sb.Append(Facts.BuildVocabularyBlock(TargetName));

            return sb.ToString();
        }

        /// <summary>
        /// Create a narrow envelope for pickup/bridge messages.
        /// Only contains the bridge text and destination location.
        /// </summary>
        public static ContextEnvelope ForPickup(string bridgeText, string destinationLocation, string contentRules)
        {
            return new ContextEnvelope
            {
                ContentRules = contentRules,
                Facts = new BeatFacts
                {
                    Location = destinationLocation,
                    BridgeText = bridgeText,
                },
            };
        }

        /// <summary>
        /// Create the narrowest envelope for bridge generation.
        /// Only two location names — nothing else.
        /// </summary>
        public static ContextEnvelope ForBridge(string sourceLocation, string destinationLocation)
        {
            return new ContextEnvelope
            {
                ContentRules = "You are writing a 1-2 sentence clue that directs a player from one location to another. " +
                    "Be concrete: name a data file, overheard conversation, or contact by role only. " +
                    "Do not describe what the player will find at the destination. " +
                    "Do not invent proper nouns.",
                Facts = new BeatFacts
                {
                    Location = sourceLocation,
                    BridgeText = destinationLocation,
                },
            };
        }
    }
}
