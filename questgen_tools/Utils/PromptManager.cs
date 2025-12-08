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
