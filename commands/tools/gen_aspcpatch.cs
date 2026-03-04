using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// Patches du_retrograde.esm:
    ///   1. Copies Int_Space_Ship_UC_Small_NoAlarm into the mod as DU_Station_ASPC.
    ///   2. For every placed instance of that ASPC (matched by all known FormKeys,
    ///      including any copy imported into the mod), replaces the base with
    ///      DU_Station_ASPC and expands the PlacedPrimitive to cover the cell bbox.
    /// </summary>
    class gen_aspcpatch
    {
        const string TargetModName      = "du_retrograde";
        const string SourceAspcEditorId = "Int_Space_StarStationA_02_Medium";
        const string NewAspcEditorId    = "DU_Station_ASPC";
        const float  BoundsPadding      = 500f;

        public static int Run()
        {
            string modPath;
            StarfieldMod myMod;
            string tmpPath;

            using (var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build())
            {
                // ── Load target mod ──────────────────────────────────────────
                modPath = Path.Combine(env.DataFolderPath, TargetModName + ".esm");
                tmpPath = Path.Combine(env.DataFolderPath, TargetModName + "_patchtmp.esm");

                Console.WriteLine($"Loading {modPath}...");
                myMod = StarfieldMod.CreateFromBinary(
                    modPath, StarfieldRelease.Starfield,
                    gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                // ── Find vanilla source ASPC ─────────────────────────────────
                var sourceAspc = env.LoadOrder.PriorityOrder
                    .AcousticSpace().WinningOverrides()
                    .FirstOrDefault(a => a.EditorID == SourceAspcEditorId);

                if (sourceAspc == null)
                {
                    Console.WriteLine($"ERROR: '{SourceAspcEditorId}' not found in load order.");
                    return 1;
                }
                Console.WriteLine($"Source ASPC: {sourceAspc.EditorID} [{sourceAspc.FormKey}]");

                // ── Create DU_Station_ASPC ───────────────────────────────────
                var existingDuAspc = myMod.AcousticSpaces
                    .FirstOrDefault(a => a.EditorID == NewAspcEditorId);

                AcousticSpace duAspc;
                if (existingDuAspc != null)
                {
                    duAspc = existingDuAspc;
                    Console.WriteLine($"Reusing existing {NewAspcEditorId} [{duAspc.FormKey}]");
                }
                else
                {
                    duAspc = new AcousticSpace(myMod) { EditorID = NewAspcEditorId };
                    CopyAspcFields(sourceAspc, duAspc);
                    myMod.AcousticSpaces.Add(duAspc);
                    Console.WriteLine($"Created {NewAspcEditorId} [{duAspc.FormKey}]");
                }

                // ── Build search key set ─────────────────────────────────────
                // The gen code may have imported Int_Space_Ship_UC_Small_NoAlarm
                // into du_retrograde as a copy with a different FormKey. Collect
                // ALL FormKeys across the mod's ASPC group that match the source
                // EditorID, plus the vanilla FormKey, so we find them all.
                var targetKeys = new HashSet<FormKey> { sourceAspc.FormKey };
                foreach (var a in myMod.AcousticSpaces)
                {
                    if (a.EditorID == SourceAspcEditorId)
                    {
                        targetKeys.Add(a.FormKey);
                        Console.WriteLine($"  Also targeting imported copy [{a.FormKey}]");
                    }
                }

                var duAspcLink = duAspc.FormKey.ToLink<IPlaceableObjectGetter>();

                // ── Patch all cells ──────────────────────────────────────────
                int cellsPatched   = 0;
                int objectsPatched = 0;

                foreach (var cellBlock in myMod.Cells)
                {
                    foreach (var subBlock in cellBlock.SubBlocks)
                    {
                        foreach (var cell in subBlock.Cells)
                        {
                            var allRefs = (cell.Temporary?.OfType<PlacedObject>() ?? [])
                                .Concat(cell.Persistent?.OfType<PlacedObject>() ?? [])
                                .ToList();

                            var aspcRefs = allRefs
                                .Where(p => targetKeys.Contains(p.Base.FormKey))
                                .ToList();

                            if (aspcRefs.Count == 0) continue;

                            var positions = allRefs.Select(p => p.Position).ToList();

                            if (positions.Count < 2)
                            {
                                Console.WriteLine($"  WARNING: {cell.EditorID ?? cell.FormKey.ToString()} — too few objects for bounding box, skipping.");
                                continue;
                            }

                            float minX = positions.Min(p => p.X), maxX = positions.Max(p => p.X);
                            float minY = positions.Min(p => p.Y), maxY = positions.Max(p => p.Y);
                            float minZ = positions.Min(p => p.Z), maxZ = positions.Max(p => p.Z);

                            foreach (var placed in aspcRefs)
                            {
                                float halfX = Math.Max(Math.Abs(maxX - placed.Position.X),
                                                       Math.Abs(minX - placed.Position.X)) + BoundsPadding;
                                float halfY = Math.Max(Math.Abs(maxY - placed.Position.Y),
                                                       Math.Abs(minY - placed.Position.Y)) + BoundsPadding;
                                float halfZ = Math.Max(Math.Abs(maxZ - placed.Position.Z),
                                                       Math.Abs(minZ - placed.Position.Z)) + BoundsPadding;

                                placed.Base      = duAspcLink;
                                placed.Primitive = new PlacedPrimitive
                                {
                                    Bounds = new P3Float(halfX * 2f, halfY * 2f, halfZ * 2f),
                                    Color  = Color.FromArgb(100, 150, 255),
                                    Type   = PlacedPrimitive.TypeEnum.Box,
                                };

                                Console.WriteLine($"  {cell.EditorID ?? cell.FormKey.ToString()}: bounds ({halfX*2f:F0} x {halfY*2f:F0} x {halfZ*2f:F0})");
                                objectsPatched++;
                            }
                            cellsPatched++;
                        }
                    }
                }

                Console.WriteLine($"\nPatched {objectsPatched} ASPC(s) across {cellsPatched} cell(s).");

                // Write to temp — ModKeyOption.NoCheck bypasses the filename/ModKey
                // validation that would reject a filename that doesn't match the mod key.
                myMod.WriteToBinary(tmpPath, new BinaryWriteParameters
                {
                    MasterFlagsLookup = gen_quest_main.MasterFlagsCache,
                    ModKey            = ModKeyOption.NoCheck,
                });
                Console.WriteLine("Written to temp, releasing load order...");
            } // env disposed here — read handles released

            File.Move(tmpPath, modPath, overwrite: true);
            Console.WriteLine("Saved.");
            return 0;
        }

        static void CopyAspcFields(IAcousticSpaceGetter src, AcousticSpace dst)
        {
            dst.ObjectBounds               = src.ObjectBounds.DeepCopy();
            dst.DirtinessScale             = src.DirtinessScale;
            dst.LoopingSound               = src.LoopingSound?.DeepCopy();
            dst.InteriorSound              = src.InteriorSound?.DeepCopy();
            dst.ExteriorSound              = src.ExteriorSound?.DeepCopy();
            dst.ExteriorWeatherAttenuation = src.ExteriorWeatherAttenuation;
            dst.InteriorExteriorRatio      = src.InteriorExteriorRatio;
            dst.IsInterior                 = src.IsInterior;
            dst.AllowExterior              = src.AllowExterior;
            dst.SoundDetectionLevel        = src.SoundDetectionLevel;
            dst.DisableFlags               = src.DisableFlags;

            if (!src.AmbientSet.IsNull)
                dst.AmbientSet = src.AmbientSet.FormKey.ToNullableLink<IAmbienceSetGetter>();
            if (!src.MusicType.IsNull)
                dst.MusicType = src.MusicType.FormKey.ToNullableLink<IMusicTypeGetter>();
            if (!src.EnvironmentType.IsNull)
                dst.EnvironmentType = src.EnvironmentType.FormKey.ToNullableLink<IReverbParametersGetter>();
        }
    }
}
