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
            new("Starport Mechanic",    "Neon Starport",       "Outfit_UtilityOveralls_SSO"),
            new("Port Official",        "Neon Starport",       "Outfit_Citizen_UC"),

            new("Street Trader",        "Neon Core",           "Outfit_Citizen"),
            new("Street Hustler",       "Neon Core",           "Outfit_Citizen",                          new[] { "underworld" }),

            new("Corporate Executive",  "Neon Ryujin HQ",      "Outfit_Clothes_BusinessSuit",             new[] { "corporate" }),
            new("Research Scientist",   "Neon Ryujin HQ",      "Outfit_Clothes_ScienceLabTec",            new[] { "corporate" }),

            new("Bartender",            "Neon Ebbside",        "Outfit_Clothes_ShirtAndSlacks",           new[] { "underworld" }),
            new("Drug Dealer",          "Neon Ebbside",        "Outfit_Clothes_Seokguh_Syndicate_Member", new[] { "underworld" }),

            new("Gang Lookout",         "Neon Rooftops",       "Outfit_Clothes_Seokguh_Syndicate_Member", new[] { "underworld", "gang" }),
            new("Gang Enforcer",        "Neon Rooftops",       "Outfit_Clothes_Seokguh_Syndicate_Member", new[] { "underworld", "gang" }),

            new("Underworld Fixer",     "Neon Underbelly",     "Outfit_Clothes_Seokguh_Syndicate_Member", new[] { "underworld" }),
            new("Smuggler",             "Neon Underbelly",     "Outfit_Clothes_CrimsonFleet_Any",         new[] { "underworld" }),

            new("Club Hostess",         "Neon Astrall Lounge", "Outfit_Clothes_ShirtAndSlacks"),
            new("Bouncer",              "Neon Astrall Lounge", "Outfit_Worker",                           new[] { "underworld" }),
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
                };
                InvestigationTemplates.Add(t);
            }
        }
    }
}
