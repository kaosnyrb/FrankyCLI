using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog.StructuredStrings.CSharp;

namespace FrankyCLI
{
    class gen_placer
    {

        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string form = args[4];

            string datapath = "";
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
                //Find the modkey 
                ModKey newMod = new ModKey(modname, ModType.Master);
                myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (!env.LoadOrder.ModExists(newMod))
                {
                    myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                }
                else
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {
                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield);

                        }
                    }
                }

                List<string> editorIds = new List<string>()
                {
                    "Loot_Storage_MedKit_Common",
                    "Loot_Storage_AmmoCase_Medium_Throwables_Common",
                    "Loot_Storage_Miscbox02_Common",
                    "Loot_Storage_Miscbox02_Rare",
                    "Loot_Storage_CreaturePile01_Common",
                    "Loot_Storage_BossChest_Creature_Rare",
                    "Loot_Storage_BossChest_Industrial_Rare",
                    "Loot_Storage_BossChest_Science_Rare",
                    "Loot_Display_HelmetCase_Common",
                    "REOverlayContainerNatureBarren",
                    "Locker01",
                    "ClutterPI_ElectricalGenerator02",
                    "DesktopClutter_A01"
                };


                // Cell contents -------------------------------
                IFormLink<INpcGetter> pirate = new FormKey(env.LoadOrder[0].ModKey, 0x00010B3A).ToLink<INpcGetter>();
                Console.WriteLine("Placing Pirates");
                int totalcount = 0;


                foreach (var worldspace in env.LoadOrder[0].Mod.Worldspaces)
                {
                    try
                    {
                        var newworldspace = worldspace.DeepCopy();
                        myMod.Worldspaces.Add(newworldspace);
                        for (int targetsubcell = 0; targetsubcell < myMod.Worldspaces[worldspace.FormKey].SubCells.Count(); targetsubcell++)
                        {
                            var subcell = myMod.Worldspaces[worldspace.FormKey].SubCells[targetsubcell];                            
                            for (int subblocks = 0; subblocks < subcell.Items.Count(); subblocks++)
                            {
                                int placedinblock = 0;
                                for (int mysbcell = 0; mysbcell < subcell.Items[subblocks].Items.Count(); mysbcell++)
                                {
                                    var cell = myMod.Worldspaces[worldspace.FormKey].SubCells[targetsubcell].Items[subblocks].Items[mysbcell];
                                    Console.WriteLine("Cell:" + worldspace.FormKey + " " + targetsubcell + " " + subblocks + " " + mysbcell);
                                    List<P3Float> positions = new List<P3Float>();
                                    foreach (var placed in cell.Temporary)
                                    {
                                        try
                                        {
                                            var placedcopy = placed.DeepCopy();
                                            var baseobj = ((PlacedObject)placedcopy).Base;
                                            var resolvedbase = immutableLoadOrderLinkCache.Resolve(baseobj.FormKey);
                                            if (editorIds.Contains(resolvedbase.EditorID))
                                            {
                                                positions.Add(((PlacedObject)placedcopy).Position);
                                            }
                                        }
                                        catch (Exception ex)
                                        {

                                        }
                                    }
                                    cell.Persistent.Clear();
                                    cell.Temporary.Clear();
                                    cell.NavigationMeshes.Clear();
                                    if (positions.Count == 0)
                                    {
                                        myMod.Worldspaces[worldspace.FormKey].SubCells[targetsubcell].Items[subblocks].Items.Remove(cell);
                                    }
                                    foreach (var pos in positions)
                                    {
                                        cell.Temporary.Add(new PlacedNpc(myMod)
                                        {
                                            EditorID = "DU_CT_enc",
                                            Base = pirate,
                                            LevelModifier = Level.Easy,
                                            Position = pos,
                                            Rotation = new P3Float(0, 0, 0)
                                        });
                                        totalcount++;
                                        placedinblock++;
                                    }
                                }
                                subcell.Items.Remove(subcell.Items[subblocks]);
                                subblocks--;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    Console.WriteLine("Pirates Placed: " + totalcount);
                }

            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            return 0;
        }
    }
}
