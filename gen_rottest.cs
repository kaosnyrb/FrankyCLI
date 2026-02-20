using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// Rotation test: unpacks the prefab "rg_sts_trk_scom_001" four times (0°, 90°, 180°, 270°)
    /// into a single interior cell, placed side by side in a row along X.
    /// Prints every placed object's EditorID, position, and rotation to console for inspection.
    /// Load the resulting ESM in the Creation Kit and verify visually that rotations are correct.
    ///
    /// Usage:  FrankyCLI gen_rottest [modname]
    /// Bat:    test_rotations.bat
    /// </summary>
    public class gen_rottest
    {
        private const string PrefabEditorId = "rg_sts_trk_scom_001";
        private const float Spacing = 10000f; // units between each rotation group along X

        private static readonly string[] Prefixes = { "Rot000", "Rot090", "Rot180", "Rot270" };
        private static readonly float[] RotationsRad =
        {
            0f,
            MathF.PI / 2f,
            MathF.PI,
            3f * MathF.PI / 2f,
        };

        public static int Generate(string[] args)
        {
            string modname = args[0];
            string datapath;

            Console.WriteLine($"[gen_rottest] modname={modname}  prefab={PrefabEditorId}");

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            datapath = env.DataFolderPath;
            gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
            gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod;

            var newModKey = new ModKey(modname, ModType.Master);
            gen_quest_main.myMod = new StarfieldMod(newModKey, StarfieldRelease.Starfield);

            // Collect template mods (same logic as other gen_ scripts)
            ModContextImpl.TemplateModsList = new List<IStarfieldModGetter>();
            for (int i = 0; i < env.LoadOrder.Count; i++)
            {
                var listing = env.LoadOrder[i];
                var fileName = listing.FileName.ToString();
                if (listing.Mod != null &&
                    (fileName.Contains("template", StringComparison.OrdinalIgnoreCase)
                     || fileName.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
                    && fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
                {
                    ModContextImpl.TemplateModsList.Add(listing.Mod);
                    Console.WriteLine($"  Template mod: {listing.FileName}");
                }
            }

            gen_quest_main.BuildReadParams(env.LoadOrder);
            RetrogradeContext.Current = new ModContextImpl();

            // Find the PackIn FormKey
            FormKey? packinFormKey = null;
            foreach (var tm in ModContextImpl.TemplateModsList)
            {
                var pk = tm.PackIns.FirstOrDefault(p => p.EditorID == PrefabEditorId);
                if (pk != null)
                {
                    packinFormKey = pk.FormKey;
                    Console.WriteLine($"  PackIn '{PrefabEditorId}' → {packinFormKey}");
                    break;
                }
            }

            if (packinFormKey == null)
            {
                Console.WriteLine($"ERROR: PackIn '{PrefabEditorId}' not found in any template mod.");
                return 1;
            }

            // Build the interior cell
            var imageSpace = new FormKey(gen_quest_main.StarfieldModKey, 0x0006AD68)
                .ToNullableLink<IImageSpaceGetter>();
            var cell = new Cell(gen_quest_main.myMod)
            {
                EditorID = "RotTest_Cell",
                Temporary = new ExtendedList<IPlaced>(),
                Flags = Cell.Flag.IsInteriorCell,
                Lighting = new CellLighting()
                {
                    DirectionalFade = 1,
                    FogPower = 1,
                    FogMax = 1,
                    NearHeightRange = 10000,
                    Unknown1 = 1951,
                },
                WaterHeight = 0,
                XILS = 1.0f,
                ImageSpace = imageSpace,
            };

            // Unpack each rotation separately so we can tag objects per group
            for (int i = 0; i < 4; i++)
            {
                int countBefore = cell.Temporary.Count;

                var util = new PlacementUtil();
                var pivot = new PlacedObject(gen_quest_main.myMod)
                {
                    Base = packinFormKey.Value.ToNullableLink<IPlaceableObjectGetter>(),
                    Position = new P3Float(i * Spacing, 0f, 0f),
                    Rotation = new P3Float(0f, 0f, RotationsRad[i]),
                };
                util.AddToTemporary(cell, pivot);
                util.Finalise();

                int countAfter = cell.Temporary.Count;
                int added = countAfter - countBefore;

                // Tag newly placed objects with rotation prefix
                for (int j = countBefore; j < countAfter; j++)
                {
                    if (cell.Temporary[j] is PlacedObject po)
                    {
                        string baseName = string.IsNullOrEmpty(po.EditorID)
                            ? $"obj{j - countBefore:D4}"
                            : po.EditorID;
                        po.EditorID = $"{Prefixes[i]}__{baseName}";
                    }
                }

                // Print this rotation group
                Console.WriteLine($"\n--- {Prefixes[i]} ({i * 90}°) — {added} objects ---");
                for (int j = countBefore; j < countAfter; j++)
                {
                    if (cell.Temporary[j] is PlacedObject po)
                    {
                        Console.WriteLine(
                            $"  {po.EditorID,-55} " +
                            $"pos=({po.Position.X,8:F1},{po.Position.Y,8:F1},{po.Position.Z,8:F1})  " +
                            $"rot=({po.Rotation.X:F4},{po.Rotation.Y:F4},{po.Rotation.Z:F4})");
                    }
                }
            }

            // Place cell into proper block/subblock hierarchy
            var cellId = cell.FormKey.ID;
            var idStr = cellId.ToString();
            int blockNum = int.Parse(idStr.Substring(idStr.Length - 1));
            int subBlockNum = int.Parse(idStr.Substring(idStr.Length - 2, 1));

            var cellBlock = new CellBlock
            {
                BlockNumber = blockNum,
                GroupType = GroupTypeEnum.InteriorCellBlock,
                SubBlocks = new ExtendedList<CellSubBlock>(),
            };
            var subBlock = new CellSubBlock
            {
                BlockNumber = subBlockNum,
                GroupType = GroupTypeEnum.InteriorCellSubBlock,
                Cells = new ExtendedList<Cell>(),
            };
            subBlock.Cells.Add(cell);
            cellBlock.SubBlocks.Add(subBlock);
            gen_quest_main.myMod.Cells.Add(cellBlock);

            foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            gen_quest_main.myMod.WriteToBinary(
                datapath + "\\" + modname + ".esm",
                gen_quest_main.BuildWriteParams());

            Console.WriteLine($"\nWritten: {datapath}\\{modname}.esm");
            return 0;
        }
    }
}
