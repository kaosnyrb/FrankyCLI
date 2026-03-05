using Retrograde.AI.Utils;
using Retrograde.Interfaces;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns.Gangs
{
    // A gang is a collection of npcs that fight the player.
    // This will create a formlist of the NPCs that can be used by scripts to spawn them.
    public class StreetGang : IGang
    {
        public StarfieldMod myMod;
        public string interal_gangName;
        public Mutagen.Bethesda.Starfield.FormList gangList;

        public StreetGang()
        {
            myMod = RetrogradeContext.Current.TargetMod;
            interal_gangName = GetGangName();

            gangList = GenerateGang();
        }

        public string gangName { get => interal_gangName; set => interal_gangName = value; }
        Mutagen.Bethesda.Starfield.FormList IGang.gangList { get => gangList; set => gangList = value; }

        public static string GetGangName()
        {
            Random random = RandomProvider.Random;
            return GangSeedData.StreetGangPrefixes[random.Next(GangSeedData.StreetGangPrefixes.Count)] + " " +
                   GangSeedData.StreetGangSuffixes[random.Next(GangSeedData.StreetGangSuffixes.Count)];
        }

        public static string GetGangJobRole()
        {
            Random r = RandomProvider.Random;
            return GangSeedData.StreetGangRoles[r.Next(GangSeedData.StreetGangRoles.Count)];
        }


        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = RandomProvider.Random;

            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();

            //Generate a new NPC

            var outfit = NPCTools.GetRandomOutfit(true);

            int gangmembers = 2 + random.Next(5);

            for (int i = 0; i < gangmembers; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }

                var NPC = NPCTools.FindTemplateNpc(isfemale);
                Npc npc = NPCTools.CloneNPC(myMod, NPC);
                string Gender = "Male";
                if (isfemale) Gender = "Female";

                npc.Name = gangName + " " + GetGangJobRole();

                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
                Random wrand = RandomProvider.Random;
                npc.Weight = new NpcWeight()
                {
                    Fat = (float)wrand.NextDouble(),
                    Muscular = (float)wrand.NextDouble(),
                    Thin = (float)wrand.NextDouble()
                };
                var lev = new PcLevelMult();
                lev.LevelMult = 0.25f + (float)random.NextDouble();
                npc.Level = lev;
                npc.SpaceOutfit = outfit;
                npc.EyeColor = NPCTools.GetEyeColour();
                npc.HairColor = NPCTools.GetHairColour();
                npc.SkinToneIndex = (byte)wrand.Next(8);
                npc.HeadParts.Add(NPCTools.GetHaircut(isfemale));
                npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } }
                };

                myMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
            }
            //Save the list
            Mutagen.Bethesda.Starfield.FormList formList = new Mutagen.Bethesda.Starfield.FormList(myMod)
            {
                EditorID = "frmlist_" + Guid.NewGuid().ToString().Substring(0, 8),
                Items = list,
            };

            myMod.FormLists.Add(formList);

            return formList;
        }
    }
}
