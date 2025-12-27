using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FrankyCLI.Retrograde
{
    public class RoomUtils
    {
        public FormList roomlist;

        public Dictionary<string, FormList> roomTemplates;

        public RoomUtils() {
            roomlist = gen_quest_main.myMod.FormLists.FirstOrDefault(fl => fl.EditorID == "rg_roomlist");

            roomTemplates = new Dictionary<string, FormList>();
            foreach (var f in roomlist.Items)
            {
                ; var list = gen_quest_main.myMod.FormLists.FirstOrDefault(fl => fl.FormKey == f.FormKey);
                roomTemplates.Add(list.EditorID, list);
            }

            //Load the prefabs for the theme

            Console.WriteLine("Lists: " + roomTemplates.Count);
        }

        public string GetRoom(string theme, string type = null)
        {
            var listKey = "rg_roomlist_" + theme;

            if (!roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
                throw new Exception($"Room theme list not found or empty: {listKey}");

            // Resolve all candidate EditorIDs once
            var candidates = new List<string>(formList.Items.Count);

            foreach (var item in formList.Items)
            {
                // Defensive: skip unresolved keys
                if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) || packIn?.EditorID == null)
                    continue;

                if (type != null)
                {
                    if (!packIn.EditorID.Contains(type))
                        continue;
                }

                candidates.Add(packIn.EditorID);
            }

            if (candidates.Count == 0)
                throw new Exception($"No PackIns resolved for list: {listKey}");

            // Prefer rooms over blockers (simple heuristic: exclude blocker IDs first)
            var rooms = candidates
                .Where(id => id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();

            if (rooms.Count > 0)
                return rooms[RandomUtils.random.Next(rooms.Count)];

            // Fallback: if the list only contains blockers, return one of them
            return candidates[RandomUtils.random.Next(candidates.Count)];
        }
    }
}
