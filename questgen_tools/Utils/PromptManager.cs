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
        public static string GetQuestName(List<string> Addons)
        {
            var questnameprompt =
                "A four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\n";
            foreach (var item in Addons)
            {
                questnameprompt += item;
            }
            var questname = AITools.RunPrompt(questnameprompt);
            return questname;
        }

        public static string GetActivatorName(List<string> Addons)
        {
            var datasourceprompt =
                "A three word or less object that contains a clue to the characters location.\r\n" +
                "Only include the data source name in the response.\r\n\r\n" +
                "This quest is about finding a lead on this character, this is the link to them.\r\n\r\n";
            foreach (var item in Addons)
            {
                datasourceprompt += item;
            }
            var datasource = AITools.RunPrompt(datasourceprompt);
            return datasource;
        }

        public static string GetDestroyActivatorName(List<string> Addons)
        {
            var datasourceprompt =
                "A three word or less contraband that must be destroyed.\r\n" +
                "Only include the data source name in the response.\r\n\r\n" +
                "This quest is about finding a lead on this character, this is the link to them.\r\n\r\n";
            foreach (var item in Addons)
            {
                datasourceprompt += item;
            }
            var datasource = AITools.RunPrompt(datasourceprompt);
            return datasource;
        }

        public static string GetDestroyMessage(List<string> Addons)
        {
            var pickuppromt =
            "Include newline characters in your response.\r\n" +
            "Generate a short flavour text story which explains to the player that they have found the location of the next stage via this clue.\r\n\r\n" +
            "The location must match the one that is provided below.\r\n\r\n" +
            "Keep it to one paragraph with newlines and under 50 words.\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            foreach (var item in Addons)
            {
                pickuppromt += item;
            }
            var pickupmessage = AITools.RunPrompt(pickuppromt);
            return pickupmessage;
        }

        public static string GetPickupMessage(List<string> Addons)
        {
            var pickuppromt =
            "Include newline characters in your response.\r\n" +
            "Generate a short flavour text story which explains to the player that they have found the location of the next stage via this clue.\r\n\r\n" +
            "The location must match the one that is provided below.\r\n\r\n" +
            "Keep it to one paragraph with newlines and under 50 words.\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            foreach (var item in Addons)
            {
                pickuppromt += item;
            }
            var pickupmessage = AITools.RunPrompt(pickuppromt);
            return pickupmessage;
        }

        public static string GetLogMessage(List<string> Addons)
        {
            var logprompt =
                "Generate a quest objective text which is an explaination on why we need to complete this objective at this location.\r\n\r\n" +
                "Refer to the Lore Entries we've generated.\r\n\r\n" +
                "Keep it to one paragraph under 100 words with newlines\r\n\r\n" +
                "Use the following information to build the explaination:\r\n\r\n";
            foreach (var item in Addons)
            {
                logprompt += item;
            }
            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);
            var logmessage = AITools.RunPrompt(logprompt);
            return logmessage;
        }

        public static string GetFirstPersonAccount(List<string> Addons)
        {
            var logprompt =
                "Generate a first person log entry.\r\n\r\n" +
                "Refer to the Lore Entries we've generated.\r\n\r\n" +
                "Use the following information to build the explaination:\r\n\r\n";
            foreach (var item in Addons)
            {
                logprompt += item;
            }
            logprompt = PromptFlavourTools.AddFlavourToLogMessage(logprompt);
            var logmessage = AITools.RunPrompt(logprompt);
            return logmessage;
        }


    }
}
