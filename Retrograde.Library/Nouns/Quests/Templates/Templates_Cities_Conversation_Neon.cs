using Retrograde.Utils;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using System.Collections.Generic;

namespace Retrograde.Quests
{
    public class Templates_Cities_Conversation_Neon : TemplateLib
    {
        // ── Character definition ─────────────────────────────────────────────────
        private record NeonChar(
            string Role,
            string SubLocation,
            string OutfitId,
            string Personality,
            string[]? ExtraTags = null
        );

        // ── Sub-location → Label mapping ─────────────────────────────────────────
        private static readonly Dictionary<string, string> _labelFor = new()
        {
            ["Neon Starport"]      = "neonstarport",
            ["Neon Core"]          = "neoncore",
            ["Neon Ryujin HQ"]     = "neonryujin",
            ["Neon Ebbside"]       = "neonebbside",
            ["Neon Rooftops"]      = "neonrooftops",
            ["Neon Underbelly"]    = "neonunderbelly",
            ["Neon Astrall Lounge"] = "neonastrallounge",
        };

        // ── Character table ───────────────────────────────────────────────────────
        // Each line is one character type. Add as many as needed.
        // ExtraTags is optional — omit it for plain city/conversation characters.
        private static readonly NeonChar[] _chars =
        {
            new("Starport Mechanic",    "Neon Starport",       "Outfit_UtilityOveralls_SSO",  "practical and direct, more comfortable with machinery than conversation, gives facts not feelings"),
            new("Port Official",        "Neon Starport",       "Outfit_Citizen_UC",            "bureaucratic and formal, follows procedure, volunteers nothing beyond what is asked"),

            new("Street Trader",        "Neon Core",           "Outfit_Citizen",              "chatty and entrepreneurial, notices everything, will share information if it costs them nothing"),
            new("Street Hustler",       "Neon Core",           "Outfit_Citizen",              "street-smart and cagey, reads people fast, never gives more than they have to",              new[] { "underworld" }),

            new("Corporate Executive",  "Neon Ryujin HQ",      "Outfit_Clothes_BusinessSuit",             "polished and guarded, every word is measured, discomfort with anything off the books", new[] { "corporate" }),
            new("Research Scientist",   "Neon Ryujin HQ",      "Outfit_Clothes_ScienceLabTec",            "precise and clinical, speaks in observable facts, quietly unsettled by what they know",  new[] { "corporate" }),

            new("Bartender",            "Neon Ebbside",        "Outfit_Clothes_ShirtAndSlacks",           "seen everything, judges nothing, gives information in short sentences between tasks",     new[] { "underworld" }),
            new("Drug Dealer",          "Neon Ebbside",        "Outfit_Clothes_Seokguh_Syndicate_Member", "paranoid and transactional, every answer has a price implied, nothing volunteered",       new[] { "underworld" }),

            new("Gang Lookout",         "Neon Rooftops",       "Outfit_Clothes_Seokguh_Syndicate_Member", "terse and suspicious, watches more than talks, minimal words chosen for maximum warning",  new[] { "underworld", "gang" }),
            new("Gang Enforcer",        "Neon Rooftops",       "Outfit_Clothes_Seokguh_Syndicate_Member", "blunt and intimidating, not interested in conversation, answers because it is easier",      new[] { "underworld", "gang" }),

            new("Underworld Fixer",     "Neon Underbelly",     "Outfit_Clothes_Seokguh_Syndicate_Member", "calculating and professional, treats information as currency, no wasted words",             new[] { "underworld" }),
            new("Smuggler",             "Neon Underbelly",     "Outfit_Clothes_CrimsonFleet_Any",         "evasive and pragmatic, habit of answering questions with questions, wary of bounty hunters", new[] { "underworld" }),

            new("Club Hostess",         "Neon Astrall Lounge", "Outfit_Clothes_ShirtAndSlacks",           "charming and observant, reads clients for a living, gives just enough to keep you talking"),
            new("Bouncer",              "Neon Astrall Lounge", "Outfit_Worker",                           "blunt and minimal, has seen too much to be surprised, answers in the fewest words possible", new[] { "underworld" }),
        };

        // ── Constructor ───────────────────────────────────────────────────────────
        public Templates_Cities_Conversation_Neon()
        {
            DiscoveryTemplates     = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates      = new List<MissionTemplate>();

            var baseFormId = FormKeyLookup.GetFormKey("duout_info_city_conversation_neon");

            foreach (var c in _chars)
            {
                var tags = new List<string> { "follow_clue", "conversation", "planetside", "city" };
                if (c.ExtraTags != null) tags.AddRange(c.ExtraTags);

                var t = new MissionTemplate()
                {
                    Name        = $"City Conversation - Neon {c.Role}",
                    Description = $"Speak to a {c.Role} at {c.SubLocation} about the target",
                    Location    = c.SubLocation,
                    formid      = baseFormId,
                    outlawQuest = new Investigation_ConversationCity(),
                    MissionTags = tags,
                    parameters  = new Dictionary<string, object>()
                    {
                        { "NeedSpacesuit", false },
                        { "Outfit",        FormKeyLookup.GetFormKey(c.OutfitId) },
                        { "Label",         _labelFor.GetValueOrDefault(c.SubLocation, "neon") },
                    },
                    Addons = new List<string>()
                    {
                        FlavourSeedData.GetNpcRelationshipToTarget(),
                        FlavourSeedData.GetNpcConversationTone(),
                        $"The NPC works in {c.SubLocation} as a {c.Role}."
                    },
                    NpcBackground = $"{c.Role} in {c.SubLocation} — {c.Personality}",
                };
                InvestigationTemplates.Add(t);
            }
        }
    }
}
