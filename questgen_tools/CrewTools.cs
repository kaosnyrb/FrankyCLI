using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class CrewTools
    {
        public static IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction,string ShipName)
        {
            var frmlst = new FormList(gen_quest.myMod)
            {
                EditorID = ShipName + "_crewlist",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };


            frmlst.Items.Add(gen_quest._StarfieldMod.Npcs[new FormKey(gen_quest.StarfieldModKey, 0x0025E1A6)]);//Loot_Corpse_SpacerF_Fresh01 "Spacer" [NPC_:0025E1A6]
            frmlst.Items.Add(gen_quest._StarfieldMod.Npcs[new FormKey(gen_quest.StarfieldModKey, 0x000C66A8)]);//Loot_Corpse_SpacerF_Fresh02 "Spacer" [NPC_:000C66A8]
            frmlst.Items.Add(gen_quest._StarfieldMod.Npcs[new FormKey(gen_quest.StarfieldModKey, 0x0025E1A5)]);//Loot_Corpse_SpacerM_Fresh01 "Spacer" [NPC_:0025E1A5]
            

            gen_quest.myMod.FormLists.Add(frmlst);

            return gen_quest.myMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>();
        }
    }
}
