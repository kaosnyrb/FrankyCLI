using FrankyCLI.questgen_tools;
using Retrograde.Nouns;
using FrankyCLI.questgen_tools.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public static class RandomUtils
    {
        // Make a set random instance that we can share.
        public static Random random = new Random();

        public static IMajorRecord GetRandomMarker(string name)
        {
            List<IMajorRecord> rec = new List<IMajorRecord>();
            foreach (var record in gen_quest_main.myMod.EnumerateMajorRecords())
            {
                if (record.EditorID != null)
                {
                    if (record.EditorID.Contains(name))
                    {
                        rec.Add(record);
                    }
                }
            }
            return rec[random.Next(rec.Count)];
        }

        public static string GetLogSynonym()
        {
            Random r = RandomUtils.random;

            var synonyms = new List<string>
            {
                "Ship Notes",
                "Crew Notes",
                "Duty Records",
                "Mission Notes",
                "Mission Records",
                "Daily Entries",
                "Service Entries",
                "Personal Records",
                "Crew Journals",
                "Field Notes",
                "Status Reports",
                "Shift Reports",
                "Voyage Notes",
                "Travel Records",
                "Operations Logs",
                "Observation Notes",
                "Shipboard Records",
                "Work Entries",
                "Duty Journals",
                "Activity Reports",
                "Notes",
                "Ledger",
                "Recordings",
                "Memoranda",
                "Transcript",
                "Summary",
                "Documentation",
                "Overview",
                "Statement",
                "Recount",
                "Diary"
            };

            return synonyms[r.Next(synonyms.Count)];
        }
    }



}
