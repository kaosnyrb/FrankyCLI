using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Nouns;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Retrograde.Grammar
{
    public enum CharacterRole
    {
        Target,
        Fixer,
        Witness,
        Informant,
        Complicator
    }

    public class WebCharacter
    {
        public string Name = "";
        public CharacterRole Role;
        public string Relationship = "";
        public string Secret = "";
        public string Agenda = "";
        public float Composure = 0.5f;
    }

    public class CharacterWeb
    {
        public List<WebCharacter> Characters = new List<WebCharacter>();

        public WebCharacter Target => Characters.Find(c => c.Role == CharacterRole.Target);

        /// <summary>
        /// Generate a character web from the existing OutlawNpc and LoreContext.
        /// The Target is built from the OutlawNpc. Supporting cast (2-3 characters)
        /// is generated via a single AI call.
        /// </summary>
        public static CharacterWeb Generate(OutlawNpc outlawNpc, MoralFraming framing)
        {
            var web = new CharacterWeb();

            // The target is always the OutlawNpc
            string targetSecret = framing switch
            {
                MoralFraming.ClearGuilt => "Knows they are guilty and has no defense.",
                MoralFraming.GuiltyButJustified => "Had a compelling reason for the crime that the player has not yet learned.",
                MoralFraming.Framed => "Did not commit the crime. Someone else in the story is responsible.",
                MoralFraming.SympatheticFugitive => "Is running from something worse than justice.",
                _ => "Has a secret the player does not yet know."
            };

            web.Characters.Add(new WebCharacter
            {
                Name = outlawNpc.name,
                Role = CharacterRole.Target,
                Relationship = "self",
                Secret = targetSecret,
                Agenda = "Evade capture.",
                Composure = 0.4f
            });

            // Generate supporting cast via AI
            var sb = new StringBuilder();
            sb.AppendLine("Generate a supporting cast for a bounty hunt quest chain.");
            sb.AppendLine("The target outlaw has already been established in the LoreContext.");
            sb.AppendLine();
            sb.AppendLine($"Target: {outlawNpc.name}");
            sb.AppendLine($"Moral framing: {DescribeFraming(framing)}");
            sb.AppendLine();
            sb.AppendLine("Generate exactly 3 supporting characters. For each, output one XML block:");
            sb.AppendLine();
            sb.AppendLine("<Character>");
            sb.AppendLine("  <Name>full name</Name>");
            sb.AppendLine("  <Role>one of: Fixer, Witness, Informant, Complicator</Role>");
            sb.AppendLine("  <Relationship>specific connection to the target (e.g. former partner, estranged sibling)</Relationship>");
            sb.AppendLine("  <Secret>what they know but won't share freely</Secret>");
            sb.AppendLine("  <Agenda>what they want from the interaction with the player</Agenda>");
            sb.AppendLine("  <Composure>decimal 0.0 to 1.0 — how well they keep their cool (0.0 = nervous wreck, 1.0 = ice cold)</Composure>");
            sb.AppendLine("</Character>");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Use ONLY locations and factions from the LoreContext. Do NOT invent new ones.");
            sb.AppendLine("- Each character must have a DIFFERENT Role.");
            sb.AppendLine("- Names must be distinct from the target's name.");
            if (framing == MoralFraming.Framed)
                sb.AppendLine("- One character (the Complicator) is the real villain who framed the target. Their Secret should reflect this.");
            if (framing == MoralFraming.GuiltyButJustified)
                sb.AppendLine("- The Informant or Witness should know the real reason behind the crime but not volunteer it.");
            sb.AppendLine("- Output only the XML blocks. No commentary.");

            string response = AITools.RunPrompt(sb.ToString());

            // Parse the response
            var characters = ParseCharacters(response);
            web.Characters.AddRange(characters);

            // Inject character web summary into AI history
            AITools.InjectContextIntoHistory(
                "The following character web has been established for this quest chain. " +
                "These are the ONLY named characters in this story — do not invent others.\r\n\r\n" +
                FormatWebSummary(web));

            Console.WriteLine($"Character Web: {web.Characters.Count} characters generated");
            foreach (var c in web.Characters)
                Console.WriteLine($"  - {c.Name} ({c.Role}): {c.Relationship}");

            return web;
        }

        private static string DescribeFraming(MoralFraming framing)
        {
            return framing switch
            {
                MoralFraming.ClearGuilt => "The target is clearly guilty. No moral ambiguity.",
                MoralFraming.GuiltyButJustified => "The target committed the crime, but had a sympathetic reason.",
                MoralFraming.Framed => "The target was framed. The real villain is one of the supporting characters.",
                MoralFraming.SympatheticFugitive => "The target is running from something worse than justice.",
                _ => "Standard bounty hunt."
            };
        }

        internal static List<WebCharacter> ParseCharacters(string xml)
        {
            var result = new List<WebCharacter>();
            var matches = Regex.Matches(xml, @"<Character>(.*?)</Character>", RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                var block = m.Groups[1].Value;
                var character = new WebCharacter
                {
                    Name = ExtractTag(block, "Name"),
                    Relationship = ExtractTag(block, "Relationship"),
                    Secret = ExtractTag(block, "Secret"),
                    Agenda = ExtractTag(block, "Agenda"),
                };

                string roleStr = ExtractTag(block, "Role").Trim();
                character.Role = roleStr.ToLowerInvariant() switch
                {
                    "fixer" => CharacterRole.Fixer,
                    "witness" => CharacterRole.Witness,
                    "informant" => CharacterRole.Informant,
                    "complicator" => CharacterRole.Complicator,
                    _ => CharacterRole.Witness
                };

                string compStr = ExtractTag(block, "Composure");
                if (float.TryParse(compStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float comp))
                    character.Composure = Math.Clamp(comp, 0f, 1f);

                result.Add(character);
            }

            return result;
        }

        private static string ExtractTag(string block, string tag)
        {
            var match = Regex.Match(block, $@"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        /// <summary>
        /// Format the full web as a summary for AI history injection.
        /// </summary>
        public static string FormatWebSummary(CharacterWeb web)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<CharacterWeb>");
            foreach (var c in web.Characters)
            {
                sb.AppendLine($"  <Character name=\"{c.Name}\" role=\"{c.Role}\">");
                sb.AppendLine($"    Relationship: {c.Relationship}");
                sb.AppendLine($"    Secret: {c.Secret}");
                sb.AppendLine($"    Agenda: {c.Agenda}");
                sb.AppendLine($"    Composure: {c.Composure:F1}");
                sb.AppendLine($"  </Character>");
            }
            sb.AppendLine("</CharacterWeb>");
            return sb.ToString();
        }

        /// <summary>
        /// Format a single character for inclusion in stage Addons.
        /// </summary>
        public static string FormatCharacterAddon(WebCharacter c)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<CharacterInScene name=\"{c.Name}\" role=\"{c.Role}\">");
            sb.AppendLine($"  Relationship: {c.Relationship}");
            sb.AppendLine($"  Secret: {c.Secret}");
            sb.AppendLine($"  Agenda: {c.Agenda}");
            sb.AppendLine($"  Composure: {c.Composure:F1}");
            sb.Append($"</CharacterInScene>");
            return sb.ToString();
        }
    }
}
