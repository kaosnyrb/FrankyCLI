using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FrankyCLI.questgen_tools.Utils
{
    public class PromptManager
    {
        public static string LoreContext;
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

            return AITools.RunPrompt(questnameprompt);
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

            return AITools.RunPrompt(datasourceprompt);
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

            return AITools.RunPrompt(datasourceprompt);
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

            return AITools.RunPrompt(pickuppromt);
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

            return AITools.RunPrompt(pickuppromt);
        }

        // ------------------------------
        // Objective Log Text
        // ------------------------------
        public static string GetLogMessage(List<string> Addons)
        {
            var logprompt =
                "Generate a short, atmospheric narrative explaining why the objective must be completed at this location.\r\n" +
                "Avoid naming the objective directly. Instead, imply its purpose through context.\r\n" +
                "Write one paragraph under 100 words with natural flow.\r\n" +
                "Do NOT introduce item names, quest titles, or made-up proper nouns unless they appear in the lore context.\r\n" +
                "Do NOT invent new proper nouns unless they exist in the Lore Context.\r\n" +
                "Write with subtle tension—never blunt exposition.\r\n" +
                "Focus on mood, motive, tension, and urgency.\r\n" +
                "Integrate lore naturally without quoting it.\r\n\r\n" +

                "If a <QuestStage> tag is present, frame the justification according to that stage:\r\n" +
                "- InitialInvestigations: this place feels like one of several uncertain starting points.\r\n" +
                "- ForkInvestigations: this place matters because choosing it means committing to a particular line of inquiry.\r\n" +
                "- DeepInvestigations: this place is where separate threads begin to cross.\r\n" +
                "- FinalShowdown: this place is where consequences can no longer be delayed.\r\n\r\n" +

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
            return AITools.RunPrompt(logprompt);
        }

        // ------------------------------
        // First Person Account
        // ------------------------------
        public static string GetFirstPersonAccount(List<string> Addons)
        {
            var logprompt =
                "Generate a first-person log entry from someone directly affected by the events described in the Lore Context.\r\n" +
                "Use the Lore Context to guide tone, personality, emotion, and perspective—especially mystery, fear, resentment, greed, or ambition.\r\n" +
                "Do NOT quote lore directly; reflect it through lived experience.\r\n\r\n" +

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


                "You may draw on any relevant sections (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "<LoreContext>\r\n" + LoreContext + "\r\n</LoreContext>\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);
            return AITools.RunPrompt(logprompt);
        }

    }

}
