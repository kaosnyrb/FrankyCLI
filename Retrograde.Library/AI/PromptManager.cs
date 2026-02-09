using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Joins;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;

namespace Retrograde.AI.Utils
{
    public class PromptManager
    {
        public static string LoreContext;
        public static string LoadRandomLoreFile()
        {
            string loreDir = @".\questgen_quests\Lorefiles";

            // Get all .md files
            var files = Directory.GetFiles(loreDir, "*.md", SearchOption.TopDirectoryOnly);

            if (files.Length == 0)
            {
                throw new FileNotFoundException($"No .md files found in: {Path.GetFullPath(loreDir)}");
            }

            // Pick random file
            string randomFile = files[RandomProvider.Random.Next(files.Length)];

            // Return contents
            return File.ReadAllText(randomFile);
        }

        public static string GenerateLoreFile()
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are generating a new LoreFile for a procedurally driven Starfield-style bounty hunting mission system.");
            sb.AppendLine();
            sb.AppendLine("Your task is to create a COMPLETE lore entry following the exact structure below.");
            sb.AppendLine();
            sb.AppendLine("The LoreFile must be self-contained, reusable, and open-ended enough to generate many different quests from it.");
            sb.AppendLine();
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine("STRUCTURE (output MUST follow this format exactly):");
            sb.AppendLine();
            sb.AppendLine("< LoreFile >");
            sb.AppendLine();
            sb.AppendLine("    < Summary >");
            sb.AppendLine("        A short overview (3–5 sentences) describing the fugitive, the core narrative theme, ");
            sb.AppendLine("        and the tone of the story.");
            sb.AppendLine("    </ Summary >");
            sb.AppendLine();
            sb.AppendLine("    < StorySeed >");
            sb.AppendLine("        Describe the narrative direction YOU have chosen for this LoreFile.");
            sb.AppendLine("        Randomly select or invent an outlaw archetype from a broad conceptual pool.");            
            sb.AppendLine("        Explain why this archetype creates strong story hooks.");
            sb.AppendLine("    </ StorySeed >");
            sb.AppendLine();
            sb.AppendLine("    < TargetProfile >");
            sb.AppendLine("        Flesh out the fugitive’s identity:");
            sb.AppendLine("        - Former occupation, affiliations, skills");
            sb.AppendLine("        - Psychological traits");
            sb.AppendLine("        - Behaviors or tells");
            sb.AppendLine("        - What pushed them onto the outlaw path");
            sb.AppendLine("    </ TargetProfile >");
            sb.AppendLine();
            sb.AppendLine("    < Motives >");
            sb.AppendLine("        Describe their deeper goals, fears, obsessions, unresolved guilt,");
            sb.AppendLine("        or long-term plans. Give 2–4 key drivers that explain their actions.");
            sb.AppendLine("    </ Motives >");
            sb.AppendLine();
            sb.AppendLine("</ LoreFile >");
            sb.AppendLine();
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine("GENERATION RULES:");
            sb.AppendLine();
            sb.AppendLine("- Produce ONLY the LoreFile in the structure above.");
            sb.AppendLine("- Pick the story direction yourself (do NOT ask the user).");
            sb.AppendLine("- The tone should support procedural bounty/mission generation.");
            sb.AppendLine("- Avoid length bloat—each section should be concise but rich.");
            sb.AppendLine("- The lore must feel expandable into dozens of missions.");
            sb.AppendLine("- No copyrighted text; fully original content.");

            var prompt = sb.ToString();

            var results = AITools.RunPrompt(prompt);

            for (int i = 0; i < 10 && results.Length < 200; i++)
            {
                results = AITools.RunPrompt(prompt);
            }
            return results;
        }

        // ------------------------------
        // Quest Name
        // ------------------------------
        public static string GetQuestName(List<string> Addons)
        {
            var questnameprompt =
                "Generate a quest name grounded in the lore and themes provided.\r\n" +
                "Constraints:\r\n" +
                "- 2-4 clear words in everyday language.\r\n" +
                "- Only output the quest name (no punctuation or explanation).\r\n" +
                "- Reflect one concrete element from <LoreContext> (faction, place, action); avoid vague mood words.\r\n" +
                "- Do not invent new names or factions beyond <LoreContext>.\r\n" +
                "- Style: plain, declarative; no metaphor, riddles, or mysterious phrasing.\r\n" +
                "- Flavor: prefer a strong action verb or specific noun from <LoreContext> to add punch (e.g., \"Seize\", \"Amber Smelter\", \"Dock Raid\").\r\n" +
                "- If using an adjective, make it concrete (e.g., \"Rust\", \"Frozen\", \"Broken\") not abstract (no \"Eternal\", \"Mysterious\").\r\n\r\n" +

                "Use the Lore Context model below for tone, theme and narrative flavor.\r\n" +
                "You may draw on any relevant parts (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n" +
                "Do NOT quote the lore; derive meaning and style from it.\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                questnameprompt += item;


            var results = AITools.RunPrompt(questnameprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(questnameprompt);
            }
            return results;

        }

        // ------------------------------
        // Clue Object Name
        // ------------------------------
        public static string GetActivatorName(List<string> Addons)
        {
            var datasourceprompt =
                "Generate a three-word-or-less object name that contains a clue to a character's location, intentions, or next move.\r\n" +
                "Use tone, themes, symbols, and motifs from the Lore Context.\r\n" +
                "Do NOT quote lore; infer from it.\r\n" +
                "Only output the object name.\r\n" +
                "- Use literal descriptors pulled from <LoreContext> (e.g., 'Dockmaster Ledger', 'Sealed Cargo Case').\r\n" +
                "- No cryptic phrases or invented names beyond <LoreContext>.\r\n" +
                "- Style: plain and concrete.\r\n\r\n" +

                "You may use any relevant elements in the Lore Context model (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                datasourceprompt += item;


            var results = AITools.RunPrompt(datasourceprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(datasourceprompt);
            }
            return results;
        }

        // ------------------------------
        // Contraband to Destroy
        // ------------------------------
        public static string GetDestroyActivatorName(List<string> Addons)
        {
            var datasourceprompt =
                "Generate a three-word-or-less contraband item name.\r\n" +
                "It should thematically match the Lore Context and feel appropriate for the kind of conflict, intrigue, and stakes described there.\r\n" +
                "Think in terms of illicit items, forbidden data, compromised artifacts, or black-market goods that could drive the story forward.\r\n" +
                "Only output the contraband name.\r\n" +
                "- Use literal descriptors tied to <LoreContext>; avoid cryptic wording.\r\n" +
                "- No invented proper nouns beyond <LoreContext>.\r\n" +
                "- Style: plain and direct.\r\n\r\n" +

                "Use any relevant parts of the Lore Context model (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements) for tone and flavor.\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                datasourceprompt += item;

            var results = AITools.RunPrompt(datasourceprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(datasourceprompt);
            }

            return results;

        }

        // ------------------------------
        // Destroy Message
        // ------------------------------
        public static string GetDestroyMessage(List<string> Addons)
        {
            var pickuppromt =
                "Generate a short flavour-text story explaining how destroying this contraband reveals the next step of the quest.\r\n" +
                "Use newline characters.\r\n" +
                "One paragraph, under 50 words.\r\n" +
                "Clarity:\r\n" +
                "- State what was destroyed, what it revealed, and the immediate next lead in plain language.\r\n" +
                "- Avoid mood adjectives, riddles, or teasing lines.\r\n" +
                "- Use only concrete details present in <LoreContext>; do not invent new names.\r\n\r\n" +

                "Use the Lore Context to influence atmosphere, mystery, faction tension, stakes, and the sense of uncovering a deeper plot.\r\n" +
                "Do NOT quote the lore directly-blend it subtly.\r\n\r\n" +

                "You may draw on any relevant parts of the Lore Context model (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                pickuppromt += item;


            var results = AITools.RunPrompt(pickuppromt);
            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(pickuppromt);
            }

            return results;

        }

        // ------------------------------
        // Pickup Message
        // ------------------------------
        public static string GetPickupMessage(List<string> Addons)
        {
            var pickuppromt =
                "Generate a short flavour-text story explaining how this clue reveals the next stage of the quest.\r\n" +
                "Use newline characters.\r\n" +
                "One paragraph, under 30 words.\r\n" +
                "Clarity:\r\n" +
                "- State what was found and how it points to the next step.\r\n" +
                "- Avoid mood adjectives, riddles, or vague teasing.\r\n" +
                "- Use only concrete details present in <LoreContext>; do not invent new names.\r\n\r\n" +

                "Use the Lore Context to shape tone, mystery, symbolism, faction behaviour, and how this clue fits into the wider conflict or hunt.\r\n" +
                "Do NOT quote lore-use it indirectly.\r\n\r\n" +

                "You may draw on any relevant parts of the Lore Context model (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                pickuppromt += item;

            var results = AITools.RunPrompt(pickuppromt);
            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(pickuppromt);
            }

            return results;
        }

        // ------------------------------
        // Objective Log Text
        // ------------------------------
        public static string GetLogMessage(List<string> Addons)
        {
            
            var logprompt =
                "Generate a short, declarative briefing explaining why the objective must be completed at this location.\r\n" +
                "Avoid naming the objective directly. Instead, imply its purpose through context.\r\n" +
                "Write 50 words.\r\n" +
                "First justify why this objective must be completed here.\r\n" +
                "Explicitly mention the bounty target by name exactly as given in <LoreContext>; weave it naturally into the paragraph.\r\n" +
                "Do NOT introduce item names, quest titles, or proper nouns unless they appear in the lore context.\r\n" +
                "Style Guidelines:\r\n" +
                "- Plain, direct language; no metaphor, riddles, or ominous hints.\r\n" +
                "- Describe the situation as if briefing an experienced operative.\r\n" +
                "- Use one or two concrete details from <LoreContext>; if absent, leave them out rather than inventing.\r\n" +
                "Draw inspiration from any relevant sections of the Lore Context:\r\n" +
                "- Factions and their agendas\r\n" +
                "- Characters or targets\r\n" +
                "- Rumors, leads, or mysteries\r\n" +
                "- The location's atmosphere and history\r\n" +
                "- Motives, risks, and stakes\r\n" +
                "- Threats tied to the area\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";



            foreach (var item in Addons)
                logprompt += item;

            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);

            var results = AITools.RunPrompt(logprompt);
            for(int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }

        // ------------------------------
        // First Person Account
        // ------------------------------
        public static string GetFirstPersonAccount(List<string> Addons)
        {
            DateTime dateTime = new DateTime(2330, 5, 6);
            var logprompt =
                "Generate a first-person log entry from someone directly affected by the events described in the Lore Context.\r\n" +
                "Use the Lore Context to guide tone, personality, emotion, and perspective-especially mystery, fear, resentment, greed, or ambition.\r\n" +
                "Do NOT quote lore directly; reflect it through lived experience.\r\n" +
                "Style and Clarity:\r\n" +
                "- Under 100 words; tight, precise language.\r\n" +
                "- Plain speech; avoid metaphor, riddles, or vague dread.\r\n" +
                "- Describe one moment or observation; do not recap the whole context.\r\n" +
                "- Mention one or two concrete details (place, action, consequence) from <LoreContext> only; do not invent names.\r\n" +
                "- Maintain first-person perspective throughout.\r\n\r\n" +

                "Date Instructions:\r\n" +
                "- You may reference a date naturally within the narrative, but it is not required.\r\n" +
                "- If a date is used, it must fall within the three years leading up to " + dateTime.ToString("yyyy-MM-dd") + ".\r\n" +
                "- Valid dates may fall anywhere between " +
                    dateTime.AddYears(-3).ToString("yyyy-MM-dd") +
                    " and " +
                    dateTime.ToString("yyyy-MM-dd") +
                    " inclusive.\r\n" +
                "- Any date mentioned should feel incidental or diegetic—woven into memory, record-keeping, or spoken context rather than formatted as a header.\r\n\r\n" +


                "Length and Style Requirements:\r\n" +
                "- The entire log entry must stay under 100 words; do not exceed this limit.\r\n" + 
                "- Favor tight, precise language over filler-every sentence should reveal character, world, or stakes.\r\n" +
                "- Avoid repeating the same idea in different words; once something is established, build on it instead of restating it.\r\n" +
                "- Do not spend words summarizing the LoreContext; assume it exists off-page and focus on what the speaker feels, remembers, or is living through right now.\r\n" +
                "- Prefer concrete details, specific memories, and sharp impressions over vague generalities or broad statements.\r\n" +
                "- If in doubt, cut adjectives, hedging, or restated thoughts before cutting sensory or emotional beats.\r\n\r\n" +
                "- The entry should read like a personal, intimate account-raw, unpolished, and emotionally grounded.\r\n" +
                "- Prioritize sensory impressions, half-understood implications, and the speaker's internal conflict.\r\n" +
                "- Do NOT summarize; immerse the reader in the moment as the speaker lived it.\r\n" +
                "- Avoid melodrama, but allow quiet dread, tension, or determination to emerge from the speaker's voice.\r\n" +
                "- The log should feel like a real spacers' or settlers' journal entry, not a formal report.\r\n" +
                "- Ensure subtle continuity with the investigative history without repeating events verbatim.\r\n\r\n" + 

                "You may draw on any relevant sections (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";


            foreach (var item in Addons)
                logprompt += item;

            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);

            var results = AITools.RunPrompt(logprompt);

            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }

        // ------------------------------
        // Mission Briefing Dataslate
        // ------------------------------
        public static string GetMissionBriefingDataslate(List<string> Addons)
        {
            var logprompt =
                "Write a mission briefing dataslate for the player.\r\n" +
                "- Length: ~200 words (aim for 180-220).\r\n" +
                "- Tone: concise field report written for a professional hunter.\r\n" +
                "- Purpose: give the first breadcrumb to track the bounty target at the initial location.\r\n" +
                "- Explicitly name the target exactly as it appears in <LoreContext> and summarize who they are and why we're hunting them.\r\n" +
                "- Identify the first destination from provided context and explain why the target is likely there and what to do upon arrival.\r\n" +
                "- Include one or two concrete details from <LoreContext> (faction, habit, method, recent sighting) to ground the lead.\r\n" +
                "- No riddles, no mystery tone; clear actionable intelligence.\r\n" +
                "- Do NOT invent new names, factions, or locations beyond <LoreContext> and the provided add-ons.\r\n\r\n" +

                "Use any relevant sections (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            var results = AITools.RunPrompt(logprompt);

            // retry if the model undershoots the length significantly
            for (int i = 0; i < 5 && results.Length < 800; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }

    }

}
