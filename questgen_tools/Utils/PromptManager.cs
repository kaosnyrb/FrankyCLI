using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Starfield;
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

namespace FrankyCLI.questgen_tools.Utils
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
            string randomFile = files[RandomUtils.random.Next(files.Length)];

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
            sb.AppendLine("        Possible example categories include:");
            sb.AppendLine("        - \"War-scarred UC Marine suffering untreated combat trauma\"");
            sb.AppendLine("        - \"Former Freestar Ranger who broke the code and fled\"");
            sb.AppendLine("        - \"Corporate researcher accused of sabotaging a rival in Xenofresh\"");
            sb.AppendLine("        - \"Crimson Fleet defector carrying sensitive intel\"");
            sb.AppendLine("        - \"Spacer lieutenant building their own breakaway cell\"");
            sb.AppendLine("        - \"Illegal cybernetics technician modifying settlers off the grid\"");
            sb.AppendLine("        - \"Ex-Armistice negotiator manipulating factions from the shadows\"");
            sb.AppendLine("        - \"Frontier homesteader who retaliated violently against land claims\"");
            sb.AppendLine("        - \"Retired mercenary acting on old war grudges\"");
            sb.AppendLine("        - \"Former Ryujin operative blackmailing corporate executives\"");
            sb.AppendLine("        - \"Deep-space miner who abandoned their crew after a cave-in\"");
            sb.AppendLine("        - \"Smuggler caught stealing from the Trade Authority\"");
            sb.AppendLine("        - \"Runaway Chrysalis Pharmaceuticals bio-tech assistant\"");
            sb.AppendLine("        - \"Terraforming technician accused of sabotaging colony habitats\"");
            sb.AppendLine("        - \"Freestar colonist who formed a small outlaw family militia\"");
            sb.AppendLine("        - \"UC scientific analyst who falsified research and fled with samples\"");
            sb.AppendLine("        - \"Combat medic selling stolen military-grade stimulants\"");
            sb.AppendLine("        - \"Salvager who looted a classified UC wreck and panicked\"");
            sb.AppendLine("        - \"Disgraced starship engineer responsible for fatal drive failures\"");
            sb.AppendLine("        - \"Ranger academy dropout spreading anti-Freestar propaganda\"");
            sb.AppendLine("        - \"Former security contractor running illegal protection rackets\"");
            sb.AppendLine("        - \"Prospector who hid a major mineral find and murdered rivals\"");
            sb.AppendLine("        - \"Asteroid hauler captain smuggling cryo-stolen goods\"");
            sb.AppendLine("        - \"Trade Authority junior broker who sold counterfeit permits\"");
            sb.AppendLine("        - \"House Va’ruun convert spreading dangerous extremist beliefs\"");
            sb.AppendLine("        - \"Ex-constellation hopeful who fabricated star anomalies\"");
            sb.AppendLine("        - \"Agritech scientist distributing unlicensed crop mutagens\"");
            sb.AppendLine("        - \"Former UC investigator covering up their own corruption\"");
            sb.AppendLine("        - \"Illegal surveyor mapping restricted military zones\"");
            sb.AppendLine("        - \"Freestar militia deserter hiding from bounty crews\"");
            sb.AppendLine("        - \"Former spacer mechanic building improvised warships\"");
            sb.AppendLine("        - \"Pirate saboteur planting tracking beacons on merchant vessels\"");
            sb.AppendLine("        - \"Disowned corporate heir using hired muscle to reclaim assets\"");
            sb.AppendLine("        - \"Smuggler pilot who transports runaway clones for profit\"");
            sb.AppendLine("        - \"Ex-Va’ruun cult dropout who still believes in cryptic omens\"");
            sb.AppendLine("        - \"Deep-space researcher who hid a dangerous field report\"");
            sb.AppendLine("        - \"Freestar settler who poisoned a rival ranch’s water supply\"");
            sb.AppendLine("        - \"Former UC Navy technician running contraband through war zones\"");
            sb.AppendLine("        - \"Crimson Fleet quartermaster skimming ship parts\"");
            sb.AppendLine("        - \"Runaway mech technician selling black-market exosuit mods\"");
            sb.AppendLine("        - \"Outpost supervisor who covered up accidental crew deaths\"");
            sb.AppendLine("        - \"Biomedical archivist leaking classified patient data\"");
            sb.AppendLine("        - \"Orbital tug pilot hijacking abandoned cargo containers\"");
            sb.AppendLine("        - \"Rogue starship architect whose unlicensed designs exploded\"");
            sb.AppendLine("        - \"Research intern who smuggled experimental grav-drive components\"");
            sb.AppendLine("        - \"Disillusioned missionary spreading volatile ideologies\"");
            sb.AppendLine("        - \"UC veteran convinced hostile powers still operate sleeper cells\"");
            sb.AppendLine("        - \"Former Freestar marshal hiding evidence from an unsolved case\"");
            sb.AppendLine("        - \"Outland freelancer replaced by cheaper robots and retaliating\"");
            sb.AppendLine("        - \"Failed xenobiology grad student illegally collecting wildlife DNA\"");
            sb.AppendLine("        - \"Shipbreaker who dismantled the wrong classified vessel\"");
            sb.AppendLine("        - \"Frontier judge who turned their settlement into a personal fiefdom\"");
            sb.AppendLine("        - \"Black-market starmap dealer fabricating false route data\"");
            sb.AppendLine("        - \"Deimos shipyard worker accused of espionage and fled instead of trial\"");
            sb.AppendLine("        - \"Medical courier stealing and reselling restricted pharmaceuticals\"");
            sb.AppendLine("        - \"Former colony warden running a rogue penal settlement\"");
            sb.AppendLine("        - \"Ryujin data analyst who disappeared with proprietary algorithms\"");
            sb.AppendLine("        - \"Spacer recruiter exploiting desperate colonists\"");
            sb.AppendLine("        - \"Freestar homesteader claiming UC agents are stalking them\"");
            sb.AppendLine("        - \"UC bioweapons tech smuggling classified petri samples\"");
            sb.AppendLine("        - \"Low-tier pirate captain whose mutinous crew wants them dead\"");
            sb.AppendLine("        - \"Starmap cartographer falsifying charts to mislead competitors\"");
            sb.AppendLine("        - \"Crimson Fleet spy embedded in a peaceful agrarian colony\"");
            sb.AppendLine("        - \"Once-respected Terrabrew supplier now running a stimulant ring\"");
            sb.AppendLine("        These are examples ONLY — you must generate a fresh archetype.");
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
            sb.AppendLine("    < Rumors >");
            sb.AppendLine("        Provide 3–6 conflicting rumors circulating about the fugitive.");
            sb.AppendLine("        Include misinformation, half-truths, and red herrings.");
            sb.AppendLine("    </ Rumors >");
            sb.AppendLine();
            sb.AppendLine("    < Leads >");
            sb.AppendLine("        Provide 3–5 actionable investigative leads or threads.");
            sb.AppendLine("        These should map naturally to mission templates: ");
            sb.AppendLine("        informants, scenes of violence, abandoned tech, hacked logs, ");
            sb.AppendLine("        family ties, shady factions, medical cover-ups, etc.");
            sb.AppendLine("    </ Leads >");
            sb.AppendLine();
            sb.AppendLine("    < Locations >");
            sb.AppendLine("        Provide 3–6 interesting locations connected to the fugitive’s journey.");
            sb.AppendLine("        Include contrasts: frontier towns, corporate sites, derelicts, outposts, ");
            sb.AppendLine("        cult shrines, black-market haunts, or war debris fields.");
            sb.AppendLine("        Add 1–2 \"hidden\" or \"unknown\" locations as potential twists.");
            sb.AppendLine("    </ Locations >");
            sb.AppendLine();
            sb.AppendLine("    < Threats >");
            sb.AppendLine("        Describe the dangers surrounding the fugitive:");
            sb.AppendLine("        - Environmental hazards");
            sb.AppendLine("        - Faction enemies");
            sb.AppendLine("        - Loyal allies");
            sb.AppendLine("        - Traps, paranoia, or psychological breakdowns");
            sb.AppendLine("    </ Threats >");
            sb.AppendLine();
            sb.AppendLine("    < MysteryElements >");
            sb.AppendLine("        Add 2–3 unresolved questions or secrets about the fugitive’s past.");
            sb.AppendLine("        These should be open-ended so different quests can resolve them differently.");
            sb.AppendLine("    </ MysteryElements >");
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
                "Generate a quest name inspired by the lore and themes provided.\r\n" +
                "Constraints:\r\n" +
                "- Four words or less.\r\n" +
                "- Only output the quest name (no punctuation or explanation).\r\n\r\n" +

                "Use the Lore Context model below for tone, theme, factions, mystery, and narrative flavor.\r\n" +
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
                "Only output the object name.\r\n\r\n" +

                "If a <QuestStage> tag is present, shape the name to match that stage of an investigation:\r\n" +
                "- InitialInvestigations: vague, suggestive names that raise questions.\r\n" +
                "- ForkInvestigations: names that imply conflicting routes, choices, or double meanings.\r\n" +
                "- DeepInvestigations: names that hint at deeper patterns, handlers, or systems.\r\n" +
                "- FinalShowdown: names that feel direct, risky, or closely tied to the target.\r\n\r\n" +

                "If a <QuestProgress> tag is present:\r\n" +
                "- Low values (0–25): keep the clue name mysterious and indirect.\r\n" +
                "- Mid values (26–75): allow clearer hints about routes, assets, or people.\r\n" +
                "- High values (76–100): allow the name to strongly imply what or where the clue points to.\r\n\r\n" +

                "If one or more <QuestStageLocation> tags are present:\r\n" +
                "- Treat them as the investigative trail the player has followed so far.\r\n" +
                "- Each entry describes a past mission stage and the location where it occurred.\r\n" +
                "- Use this history to maintain narrative continuity and acknowledge where previous clues were found.\r\n" +
                "- Refer to past locations subtly—do not quote tag names or restate them verbatim.\r\n" +
                "- Early-stage locations (InitialInvestigation) should influence the tone with uncertainty or fragmented clues.\r\n" +
                "- ForkInvestigation locations should imply conflicting leads or divergent paths.\r\n" +
                "- DeepInvestigation locations should reinforce emerging patterns or connections.\r\n" +
                "- FinalShowdown outputs may treat earlier locations as foreshadowing or context for the target’s plans.\r\n\r\n" +


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
                "Only output the contraband name.\r\n\r\n" +

                "If a <QuestStage> tag is present, let it influence how explicit the contraband feels:\r\n" +
                "- InitialInvestigations: ambiguous contraband that could be one part of a larger operation.\r\n" +
                "- ForkInvestigations: contraband that could plausibly point to more than one faction or route.\r\n" +
                "- DeepInvestigations: contraband that clearly ties to the emerging conspiracy.\r\n" +
                "- FinalShowdown: contraband that feels central, dangerous, or directly linked to the target's endgame.\r\n\r\n" +

                "If a <QuestProgress> tag is present:\r\n" +
                "- Low values (0–25): keep the name suggestive, without revealing the true function.\r\n" +
                "- Mid values (26–75): let the name imply purpose or risk more clearly.\r\n" +
                "- High values (76–100): the name can strongly signal why destroying it matters.\r\n\r\n" +

                " If one or more <QuestStageLocation> tags are present:\r\n" +
                "- Treat them as the investigative trail the player has followed so far.\r\n" +
                "- Each entry describes a past mission stage and the location where it occurred.\r\n" +
                "- Use this history to maintain narrative continuity and acknowledge where previous clues were found.\r\n" +
                "- Refer to past locations subtly—do not quote tag names or restate them verbatim.\r\n" +
                "- Early-stage locations (InitialInvestigation) should influence the tone with uncertainty or fragmented clues.\r\n" +
                "- ForkInvestigation locations should imply conflicting leads or divergent paths.\r\n" +
                "- DeepInvestigation locations should reinforce emerging patterns or connections.\r\n" +
                "- FinalShowdown outputs may treat earlier locations as foreshadowing or context for the target’s plans.\r\n\r\n" +


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
                "One paragraph, under 50 words.\r\n\r\n" +

                "Use the Lore Context to influence atmosphere, mystery, faction tension, stakes, and the sense of uncovering a deeper plot.\r\n" +
                "Do NOT quote the lore directly—blend it subtly.\r\n\r\n" +

                "If a <QuestStage> tag is present, adjust how much this destruction reveals:\r\n" +
                "- InitialInvestigations: mostly raise new questions, only hint at a pattern.\r\n" +
                "- ForkInvestigations: suggest that the destroyed item ruled out one path or confirmed another.\r\n" +
                "- DeepInvestigations: show how this removal exposes a key link or vulnerable route.\r\n" +
                "- FinalShowdown: destroying it should feel like cutting off the last safeguard or mask.\r\n\r\n" +

                "If a <QuestProgress> tag is present:\r\n" +
                "- Low values (0–25): keep the consequences subtle and uncertain.\r\n" +
                "- Mid values (26–75): let the outcome clearly advance the investigation without fully explaining everything.\r\n" +
                "- High values (76–100): make the destruction feel decisive and close to the final truth.\r\n\r\n" +

                " If one or more <QuestStageLocation> tags are present:\r\n" +
                "- Treat them as the investigative trail the player has followed so far.\r\n" +
                "- Each entry describes a past mission stage and the location where it occurred.\r\n" +
                "- Use this history to maintain narrative continuity and acknowledge where previous clues were found.\r\n" +
                "- Refer to past locations subtly—do not quote tag names or restate them verbatim.\r\n" +
                "- Early-stage locations (InitialInvestigation) should influence the tone with uncertainty or fragmented clues.\r\n" +
                "- ForkInvestigation locations should imply conflicting leads or divergent paths.\r\n" +
                "- DeepInvestigation locations should reinforce emerging patterns or connections.\r\n" +
                "- FinalShowdown outputs may treat earlier locations as foreshadowing or context for the target’s plans.\r\n\r\n" +


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
                "One paragraph, under 50 words.\r\n\r\n" +

                "Use the Lore Context to shape tone, mystery, symbolism, faction behaviour, and how this clue fits into the wider conflict or hunt.\r\n" +
                "Do NOT quote lore—use it indirectly.\r\n\r\n" +

                "If a <QuestStage> tag is present, treat this as one step in an ongoing pursuit:\r\n" +
                "- InitialInvestigations: the clue should feel fragile or incomplete, hinting that more digging is needed.\r\n" +
                "- ForkInvestigations: show how the clue pushes the hunter toward one of several conflicting leads.\r\n" +
                "- DeepInvestigations: make the clue feel like a connector between earlier fragments.\r\n" +
                "- FinalShowdown: the clue should point almost directly at the confrontation or final location.\r\n\r\n" +

                "If a <QuestProgress> tag is present:\r\n" +
                "- Low values (0–25): focus on uncertainty and possibility.\r\n" +
                "- Mid values (26–75): highlight patterns, recurring names, or locations.\r\n" +
                "- High values (76–100): highlight urgency and how little room is left to maneuver.\r\n\r\n" +

                " If one or more <QuestStageLocation> tags are present:\r\n" +
                "- Treat them as the investigative trail the player has followed so far.\r\n" +
                "- Each entry describes a past mission stage and the location where it occurred.\r\n" +
                "- Use this history to maintain narrative continuity and acknowledge where previous clues were found.\r\n" +
                "- Refer to past locations subtly—do not quote tag names or restate them verbatim.\r\n" +
                "- Early-stage locations (InitialInvestigation) should influence the tone with uncertainty or fragmented clues.\r\n" +
                "- ForkInvestigation locations should imply conflicting leads or divergent paths.\r\n" +
                "- DeepInvestigation locations should reinforce emerging patterns or connections.\r\n" +
                "- FinalShowdown outputs may treat earlier locations as foreshadowing or context for the target’s plans.\r\n\r\n" +


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
                "Generate a short, atmospheric narrative explaining why the objective must be completed at this location.\r\n" +
                "Avoid naming the objective directly. Instead, imply its purpose through context.\r\n" +
                "Write one paragraph under 80 words with natural flow.\r\n" +
                "Do NOT introduce item names, quest titles, or made-up proper nouns unless they appear in the lore context.\r\n" +
                "Do NOT invent new proper nouns unless they exist in the Lore Context.\r\n" +
                "Write with subtle tension—never blunt exposition.\r\n" +
                "Focus on mood, motive, tension, and urgency.\r\n" +
                "Integrate lore naturally without quoting it.\r\n\r\n" +

                "If a <QuestStage> tag is present, frame the justification according to that stage:\r\n" +
                "- InitialInvestigations: this place feels like one of several uncertain starting points.\r\n" +
                "- ForkInvestigations: this place matters because choosing it means committing to a particular line of inquiry.\r\n" +
                "- DeepInvestigations: this place is where separate threads begin to cross.\r\n" +
                "- FinalShowdown: this place is where the target’s long-running actions finally come into focus." +
                "Clearly describe what the target has been doing up to this moment—whether manipulating factions, extracting illicit resources, exploiting locals, hiding evidence, preparing a weapon, or orchestrating a larger scheme." +
                "Show how every clue from earlier investigations points to their ongoing operation: mention the pattern behind their movements, the purpose of the items they stole, or the deeper motive behind the trail they left." +
                "Emphasize what the target is attempting right now as you arrive—securing a final asset, activating dangerous tech, eliminating a witness, fleeing with critical data, or destroying the last proof of their crimes." + 
                "Highlight why this exact location matters to their plan, and why stopping them here prevents the situation from escalating into something far worse." + 
                "The tone should convey culmination, rising danger, and the sense that their scheme is seconds away from succeeding if left unchecked."+


                "If a <QuestProgress> tag is present:\r\n" +
                "- Low values (0–25): emphasize confusion, rumor, and risk with limited insight.\r\n" +
                "- Mid values (26–75): emphasize connections, patterns, and rising pressure.\r\n" +
                "- High values (76–100): emphasize urgency, inevitability, and the narrow margin for action.\r\n\r\n" +

                "If one or more <QuestStageLocation> tags are present:\r\n" +
                "- Treat them as the investigative trail the player has followed so far.\r\n" +
                "- Each entry describes a past mission stage and the location where it occurred.\r\n" +
                "- Use this history to maintain narrative continuity and acknowledge where previous clues were found.\r\n" +
                "- Refer to past locations subtly—do not quote tag names or restate them verbatim.\r\n" +
                "- Early-stage locations (InitialInvestigation) should influence the tone with uncertainty or fragmented clues.\r\n" +
                "- ForkInvestigation locations should imply conflicting leads or divergent paths.\r\n" +
                "- DeepInvestigation locations should reinforce emerging patterns or connections.\r\n" +
                "- FinalShowdown outputs may treat earlier locations as foreshadowing or context for the target’s plans.\r\n\r\n" +

                "Summary Requirement:\r\n" +
                "- After the atmospheric narrative, the hunter sets down a brief recap in their notes—a compact paragraph under fifty words capturing where the investigation currently stands.\r\n" +
                "- Don't include a header..\r\n" +
                "- This recap gathers the trail so far: clues uncovered, rumors traded, patterns glimpsed, and suspicions that have started to take shape.\r\n" +
                "- It stays inside the fiction: no system labels, no tag names, only details the hunter or their sources could plausibly put into words.\r\n" +
                "- The summary should feel like an investigator’s quick briefing: what the trail has revealed, what seems to connect, and which conclusions the chase is drifting toward.\r\n" +
                "- Keep this recap factual, tight, and grounded in what the records, memories, and prior steps have already established—no guesses about what has not yet been discovered.\r\n\r\n" +

                "\r\nStyle Guidelines:\r\n" +
                "- Do not start with phrases like 'The objective is...' or 'You must...'\r\n" +
                "- Describe the situation as if briefing an experienced operative.\r\n" +
                "- Keep it immersive, subtle, and diegetic.\r\n" + 
                "Draw inspiration from any relevant sections of the Lore Context:\r\n" +
                "- Factions and their agendas\r\n" +
                "- Characters or targets\r\n" +
                "- Rumors, leads, or mysteries\r\n" +
                "- The location’s atmosphere and history\r\n" +
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
                "Use the Lore Context to guide tone, personality, emotion, and perspective—especially mystery, fear, resentment, greed, or ambition.\r\n" +
                "Do NOT quote lore directly; reflect it through lived experience.\r\n\r\n" +

                "Date Instructions:\r\n" +
                "- Begin the entry with a date.\r\n" +
                "- The date must fall within the year leading up to " + dateTime.ToString("yyyy-MM-dd") + ".\r\n" +
                "- Choose any date between " +
                    dateTime.AddYears(-1).ToString("yyyy-MM-dd") +
                    " and " +
                    dateTime.ToString("yyyy-MM-dd") +
                    " inclusive.\r\n" +
                "- Format the date like: YYYY-MM-DD.\r\n" +
                "- After the date, write the narrative as normal.\r\n\r\n" +

                "If a <QuestStage> tag is present, shape what the speaker understands:\r\n" +
                "- InitialInvestigations: the speaker senses something is wrong, but details are unclear.\r\n" +
                "- ForkInvestigations: the speaker has seen conflicting signs and is unsure who to trust.\r\n" +
                "- DeepInvestigations: the speaker recognizes patterns or suspects a larger plan.\r\n" +
                "- FinalShowdown: the speaker understands how serious the situation is, even if they still lack full answers.\r\n\r\n" +

                "If a <QuestProgress> tag is present:\r\n" +
                "- Low values (0–25): the speaker should be mostly confused or anxious.\r\n" +
                "- Mid values (26–75): the speaker should be troubled by how pieces are starting to fit together.\r\n" +
                "- High values (76–100): the speaker should feel trapped between what they know and what they cannot stop.\r\n\r\n" +

                "If one or more <QuestStageLocation> tags are present:\r\n" +
                "- Treat them as the investigative trail the player has followed so far.\r\n" +
                "- Each entry describes a past mission stage and the location where it occurred.\r\n" +
                "- Use this history to maintain narrative continuity and acknowledge where previous clues were found.\r\n" +
                "- Refer to past locations subtly—do not quote tag names or restate them verbatim.\r\n" +
                "- Early-stage locations (InitialInvestigation) should influence the tone with uncertainty or fragmented clues.\r\n" +
                "- ForkInvestigation locations should imply conflicting leads or divergent paths.\r\n" +
                "- DeepInvestigation locations should reinforce emerging patterns or connections.\r\n" +
                "- FinalShowdown outputs may treat earlier locations as foreshadowing or context for the target’s plans.\r\n\r\n" +

                "Length and Style Requirements:\r\n" +
                "- The entire log entry must stay under 250 words; do not exceed this limit.\r\n" + 
                "- Favor tight, precise language over filler—every sentence should reveal character, world, or stakes.\r\n" +
                "- Avoid repeating the same idea in different words; once something is established, build on it instead of restating it.\r\n" +
                "- Do not spend words summarizing the LoreContext; assume it exists off-page and focus on what the speaker feels, remembers, or is living through right now.\r\n" +
                "- Prefer concrete details, specific memories, and sharp impressions over vague generalities or broad statements.\r\n" +
                "- If in doubt, cut adjectives, hedging, or restated thoughts before cutting sensory or emotional beats.\r\n\r\n" +
                "- It may vary slightly (e.g., 100–250 words) as long as the narrative flows naturally.\r\n" +
                "- The entry should read like a personal, intimate account—raw, unpolished, and emotionally grounded.\r\n" +
                "- Prioritize sensory impressions, half-understood implications, and the speaker’s internal conflict.\r\n" +
                "- Maintain first-person perspective throughout.\r\n" +
                "- Do NOT summarize; immerse the reader in the moment as the speaker lived it.\r\n" +
                "- Avoid melodrama, but allow quiet dread, tension, or determination to emerge from the speaker’s voice.\r\n" +
                "- The log should feel like a real spacers’ or settlers’ journal entry, not a formal report.\r\n" +
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

    }

}
