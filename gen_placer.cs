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

                // Cell contents -------------------------------
                //LvlCrimsonFleet_Assault [NPC_:00054327]
                var spawner = immutableLoadOrderLinkCache.Resolve("dv_nt_spawnmarker");
                //var spacespawner = immutableLoadOrderLinkCache.Resolve("du_ct_spacespawnmarker");

                Console.WriteLine("Placing Pirates");
                foreach (var cell in env.LoadOrder[0].Mod.EnumerateMajorRecordContexts<ICell, ICellGetter>(env.LinkCache))
                {
                    //cell.TryGetParent<IWorldspaceGetter>(out var _) || 
                    if (cell.Record.EditorID != null)
                    {
                        if (cell.Record.EditorID.Contains("PackInENVHazardPK"))
                        {
                            var cellOverride = cell.GetOrAddAsOverride(myMod);
                            cellOverride.Temporary.Clear();
                            cellOverride.Persistent.Clear();
                            cellOverride.NavigationMeshes.Clear();
                            cellOverride.Temporary.Add(new PlacedObject(myMod)
                            {
                                EditorID = "dv_nt_marker",
                                Base = spawner.ToLink<IPlaceableObjectGetter>(),
                                Position = new P3Float(0, 0, 0),
                                Rotation = new P3Float(0, 0, 0),
                            });
                        }
                    }

                    /*
                    if (cell.Record.EditorID != null)
                    {
                        if (cell.Record.EditorID.Contains("scGen"))
                        {
                            var cellOverride = cell.GetOrAddAsOverride(myMod);
                            cellOverride.Temporary.Clear();
                            cellOverride.Persistent.Clear();
                            cellOverride.NavigationMeshes.Clear();
                            cellOverride.Temporary.Add(new PlacedObject(myMod)
                            {
                                EditorID = "du_ct_space_placedmarker",
                                Base = spacespawner.ToLink<IPlaceableObjectGetter>(),
                                Position = new P3Float(0, 0, 0),
                                Rotation = new P3Float(0, 0, 0),
                            });
                        }
                    }*/
                }

                //Parallel
                /*
                foreach (var worldspace in env.LoadOrder[0].Mod.Worldspaces)
                {
                    var newworldspace = worldspace.DeepCopy();
                    Parallel.ForEach(newworldspace.SubCells, wsblock =>
                    {
                        Parallel.ForEach(wsblock.Items, wssblock =>
                        {
                            Parallel.ForEach(wssblock.Items, cell =>
                            {
//                                cell.Clear();
                                Console.WriteLine("Cell:" + worldspace.FormKey + " " + cell.EditorID);
                                cell.Temporary.Clear();
                                cell.Persistent.Clear();
                                cell.NavigationMeshes.Clear();
//                                cell.Components.Clear();
//                                cell.Grid.Clear();
                                cell.Temporary.Add(new PlacedObject(myMod)
                                {
                                    EditorID = "du_ct_placedmarker",
                                    Base = spawner.ToLink<IPlaceableObjectGetter>(),
                                    Position = new P3Float(0, 0, 0),
                                    Rotation = new P3Float(0, 0, 0),
                                });
                            });
                        });
                    });
                    myMod.Worldspaces.Add(newworldspace);
                }*/
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            return 0;
        }
    }
}
