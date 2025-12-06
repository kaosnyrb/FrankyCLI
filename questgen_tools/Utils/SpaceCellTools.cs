using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    struct MarkerLocation
    {
        public string EditorID;
        public uint QuestID;
        public int AliasID;
    }

    public class SpaceCellTools
    {
        public static Condition GetSpaceMarkerCondition()
        {
            //Yeah this looks stupid.
            //Basically we can't create conditions ourselves.
            //So we steal existing ones and clone them.
            //Works out the same in the end.

            //0x000277A4 the Robot Survey has loads of markers

            //se_PatrolStartMarkerLocRef01 [LCRT:001DEE29]
            var Markers = new List<MarkerLocation>
            {
                new MarkerLocation()
                {
                    EditorID = "se_DistantPatrolStartMarkerLocRef01",
                    QuestID = 0x00127FA8,
                    AliasID = 7,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef01",
                    QuestID = 0x00127FA8,
                    AliasID = 8,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef02",
                    QuestID = 0x000277A4,
                    AliasID = 9,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef03",
                    QuestID = 0x000277A4,
                    AliasID = 10,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef04",
                    QuestID = 0x000277A4,
                    AliasID = 11,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef05",
                    QuestID = 0x000277A4,
                    AliasID = 12,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef06",
                    QuestID = 0x000277A4,
                    AliasID = 13,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef07",
                    QuestID = 0x000277A4,
                    AliasID = 14,
                },
                new MarkerLocation()
                {
                    EditorID = "se_GeneralMarkerLocRef08",
                    QuestID = 0x000277A4,
                    AliasID = 15,
                },
                new MarkerLocation()
                {
                    EditorID = "se_TravelMarkerLocRefA01",
                    QuestID = 0x000277A4,
                    AliasID = 19,
                },
                new MarkerLocation()
                {
                    EditorID = "se_TravelMarkerLocRefA02",
                    QuestID = 0x000277A4,
                    AliasID = 20,
                },
                new MarkerLocation()
                {
                    EditorID = "se_TravelMarkerLocRefA03",
                    QuestID = 0x000277A4,
                    AliasID = 21,
                },
                new MarkerLocation()
                {
                    EditorID = "se_TravelMarkerLocRefB01",
                    QuestID = 0x000277A4,
                    AliasID = 22,
                },
                new MarkerLocation()
                {
                    EditorID = "se_TravelMarkerLocRefB02",
                    QuestID = 0x000277A4,
                    AliasID = 23,
                },
                new MarkerLocation()
                {
                    EditorID = "se_TravelMarkerLocRefB03",
                    QuestID = 0x000277A4,
                    AliasID = 24,
                },
                /* /Had a mission that failed to start cus of these. Disable for now. Prob enough others not to care
                new MarkerLocation()
                {
                    EditorID = "se_PatrolStartMarkerLocRef01",
                    QuestID = 0x000277A4,
                    AliasID = 16,
                },
                new MarkerLocation()
                {
                    EditorID = "se_PatrolStartMarkerLocRef02",
                    QuestID = 0x00127FA8,
                    AliasID = 9,
                },
                new MarkerLocation()
                {
                    EditorID = "se_PatrolStartMarkerLocRef03",
                    QuestID = 0x00127FA8,
                    AliasID = 10,
                },*/

            };

            Random random = new Random();
            var Choosen = Markers[random.Next(Markers.Count)];
            var quest = gen_quest_main._StarfieldMod.Quests[new FormKey(gen_quest_main.StarfieldModKey, Choosen.QuestID)];
            var alias = quest.Aliases[Choosen.AliasID];
            var blah = ((IQuestReferenceAliasGetter)alias).Conditions[0];

            return blah.DeepCopy();
        }
    }
}
