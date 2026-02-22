using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Places hydroponic lab decorations inside each science building pod.
///
/// Must run after <see cref="ScienceBuildingPass"/>, which populates
/// <see cref="WorldspaceState.BuildingPodPositions"/>.
///
/// Three categories of items are placed per pod:
///   - Counter-top items (Z+0.80): planters, hydroponic crops, shelf dressings observed
///     on science counters in SC_CounterScience01a and OESF003World.
///   - Freestanding floor equipment (Z+0): hydroponics samplers observed in OESF003World.
///   - Lab equipment PackIns (Z+0): computers and server racks from the SC_ kit
///     (SC_ComputerSingle_A*, SC_Servers*) placed as freestanding stations.
///
/// Placement uses random offsets within the 6-unit pod interior. Wall-hugging
/// placement (e.g. cabinets) is a planned future refinement.
/// </summary>
public class BuildingDecoratorPass : IWorldspacePass
{
    // ── Counter-top clutter (placed at Z + CounterTopZ) ─────────────────────────
    //
    // Pre-built desktop clutter PackIns — designed to sit on a science bench surface.
    // Source: gen_inspect found DesktopClutter_Science_A* as dedicated bench-clutter kits.
    // These contain internally positioned small props (vials, papers, equipment) so
    // placing one PackIn gives a realistic grouping without individual item management.
    private static readonly (uint formId, string editorId)[] DesktopClutterPackIns =
    [
        (0x0C11D7, "DesktopClutter_Science_A01"),  // tight cluster ~0.5×0.8
        (0x17B715, "DesktopClutter_Science_A02"),  // medium spread ~1.2×1.1
        (0x1EF554, "DesktopClutter_Science_A03"),  // wide spread  ~0.8×1.6
    ];

    // Vial tray PackIns — placed flat on a surface.
    private static readonly (uint formId, string editorId)[] VialTrayPackIns =
    [
        (0x1D4CD0, "PI_ScienceGlass_VialTray01_Full01"),    // all-same vials
        (0x3F6678, "ScienceGlass_VialTray01_MixedVials01"), // mixed vials A
        (0x3F6685, "ScienceGlass_VialTray01_MixedVials02"), // mixed vials B
    ];

    // Individual small science props for countertop scatter.
    private static readonly (uint formId, string editorId)[] SmallScienceProps =
    [
        (0x265AD5, "ScienceGlass_VialTray01"),     // vial tray static
        (0x0317D9, "SpecimenContainer01"),          // horizontal specimen container
        (0x0378AC, "SpecimenContainer02"),          // vertical specimen container
        (0x037803, "MagnifyingGlassDesktop01"),     // magnifying glass
        (0x208CE6, "Monitor_MedicalSm01"),          // small medical monitor
        (0x257AB7, "Ind_ShelfKitA02"),              // industrial shelf kit (SC_CounterScience01a)
        (0x06AE79, "PlanterLargeShort01"),          // planter (OESF003World)
        (0x0D9817, "HydroSage01"),                  // hydroponic sage
        (0x0D9818, "HydroRosemary01"),              // hydroponic rosemary
        (0x0D981A, "HydroCabbage01"),               // hydroponic cabbage
    ];

    // ── Floor-standing equipment (placed at Z + 0) ───────────────────────────────
    //
    // Science tables — the primary work surfaces in a science lab.
    // Clutter PackIns (above) are placed on top of these.
    private static readonly (uint formId, string editorId)[] ScienceTables =
    [
        (0x0713B9, "TableScienceSm1Hex01"),     // hexagonal lit table (OESF003World)
        (0x070A73, "TableScienceSm1Square01"),  // square lit table
        (0x2502C0, "TableScienceSm1Square01_off"),  // square unlit
        (0x25033D, "TableScienceSm1Hex01_table_off"), // hex unlit
    ];

    // Hydroponics sampler units (freestanding, from OESF003World).
    private static readonly (uint formId, string editorId)[] HydroFloorItems =
    [
        (0x06D438, "HydroponicSampler01"),
        (0x078815, "HydroponicSampler02"),
        (0x078818, "HydroponicSampler03"),
        (0x07881C, "HydroponicSampler_Plant01"),
        (0x07881D, "HydroponicSampler_Plant02"),
        (0x07881A, "HydroponicSampler_Plant03"),
    ];

    // Larger lab containers and specimen equipment.
    private static readonly (uint formId, string editorId)[] LabStorageItems =
    [
        (0x126B03, "ContainmentTank_Specimen_Sm01"),
        (0x1E4FAD, "ScienceLiquidsStorage01A"),
        (0x1E4FAC, "ScienceLiquidsStorage01B"),
        (0x0B696D, "ScienceLiquidsStorage02A"),
        (0x257E67, "MobileTerminal_01"),
    ];

    // SC_ lab equipment PackIns (computers, server racks).
    private static readonly (uint formId, string editorId)[] LabEquipPackIns =
    [
        (0x012DDD, "SC_ComputerSingle_A03"),
        (0x0114E4, "SC_ComputerSingle_A04"),
        (0x0155DA, "SC_ComputerSingle_A05"),
        (0x01157D, "SC_ComputerSingle_A07"),
        (0x01132F, "SC_Servers02"),
        (0x0154F1, "SC_Servers03"),
        (0x011479, "SC_Servers05"),
    ];

    // Counter-top height above the pod floor.
    // Confirmed: SC_CounterScience01a places shelf at Z=0.8; science table tops ~0.8.
    private const float CounterTopZ = 0.80f;

    // Maximum lateral scatter from pod centre (pod is 6 units wide, radius ~3).
    private const float MaxOffset = 2.0f;

    private readonly float _workstationChance; // chance per pod of table + desktop clutter
    private readonly float _hydroFloorChance;  // chance per pod of a hydro sampler unit
    private readonly float _labEquipChance;    // chance per pod of a computer/server PackIn

    public BuildingDecoratorPass(
        float workstationChance = 0.60f,
        float hydroFloorChance  = 0.40f,
        float labEquipChance    = 0.35f)
    {
        _workstationChance = workstationChance;
        _hydroFloorChance  = hydroFloorChance;
        _labEquipChance    = labEquipChance;
    }

    public void RunPass(WorldspaceState state)
    {
        if (state.BuildingPodPositions == null || state.BuildingPodPositions.Count == 0)
        {
            if (!RetrogradeContext.Quiet)
                Console.WriteLine("[BuildingDecoratorPass] No pod positions — skipping (run after ScienceBuildingPass)");
            return;
        }

        var targetMod = RetrogradeContext.Current.TargetMod;
        var sfEsm     = RetrogradeContext.Current.StarfieldModKey;
        var rand      = state.Rng;
        int placed    = 0;

        foreach (var pod in state.BuildingPodPositions)
        {
            int cellX = (int)Math.Floor(pod.X / 100f);
            int cellY = (int)Math.Floor(pod.Y / 100f);
            if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                continue;

            // ── Workstation: science table + surface clutter ──────────────────
            // Place a work surface then dress it with 1–2 clutter items.
            if (rand.NextDouble() < _workstationChance)
            {
                float ox  = (float)((rand.NextDouble() * 2 - 1) * MaxOffset);
                float oy  = (float)((rand.NextDouble() * 2 - 1) * MaxOffset);
                float yaw = (float)(rand.NextDouble() * Math.PI * 2);

                // The table itself.
                var (tableId, _) = ScienceTables[rand.Next(ScienceTables.Length)];
                Place(targetMod, sfEsm, tableId, pod.X + ox, pod.Y + oy, pod.Z, yaw, cell, state);
                placed++;

                // Desktop clutter on top — primary clutter PackIn or individual prop.
                bool useClutterPackIn = rand.NextDouble() < 0.6;
                if (useClutterPackIn)
                {
                    // DesktopClutter PackIn: place at same XY but on table surface.
                    var (clutterPackId, _) = DesktopClutterPackIns[rand.Next(DesktopClutterPackIns.Length)];
                    Place(targetMod, sfEsm, clutterPackId, pod.X + ox, pod.Y + oy, pod.Z + CounterTopZ, yaw, cell, state);
                    placed++;
                }
                else
                {
                    // Vial tray or individual small prop.
                    bool useVialTray = rand.NextDouble() < 0.4;
                    var pool = useVialTray ? VialTrayPackIns : SmallScienceProps;
                    var (propId, _) = pool[rand.Next(pool.Length)];
                    Place(targetMod, sfEsm, propId, pod.X + ox, pod.Y + oy, pod.Z + CounterTopZ, yaw, cell, state);
                    placed++;
                }

                // Second individual prop on the same surface (~50% chance).
                if (rand.NextDouble() < 0.5)
                {
                    float ox2  = ox + (float)((rand.NextDouble() * 2 - 1) * 0.5f);
                    float oy2  = oy + (float)((rand.NextDouble() * 2 - 1) * 0.5f);
                    var (propId2, _) = SmallScienceProps[rand.Next(SmallScienceProps.Length)];
                    Place(targetMod, sfEsm, propId2, pod.X + ox2, pod.Y + oy2, pod.Z + CounterTopZ, yaw, cell, state);
                    placed++;
                }
            }

            // ── Hydroponic floor unit ─────────────────────────────────────────
            if (rand.NextDouble() < _hydroFloorChance)
            {
                float ox  = (float)((rand.NextDouble() * 2 - 1) * MaxOffset);
                float oy  = (float)((rand.NextDouble() * 2 - 1) * MaxOffset);
                float yaw = (float)(rand.NextDouble() * Math.PI * 2);
                // Prefer a sampler, occasionally use a lab storage item.
                bool useStorage = rand.NextDouble() < 0.25;
                var pool = useStorage ? LabStorageItems : HydroFloorItems;
                var (formId, _) = pool[rand.Next(pool.Length)];
                Place(targetMod, sfEsm, formId, pod.X + ox, pod.Y + oy, pod.Z, yaw, cell, state);
                placed++;
            }

            // ── Lab equipment station (computer or server PackIn) ─────────────
            if (rand.NextDouble() < _labEquipChance)
            {
                float ox  = (float)((rand.NextDouble() * 2 - 1) * MaxOffset);
                float oy  = (float)((rand.NextDouble() * 2 - 1) * MaxOffset);
                float yaw = (float)(rand.NextDouble() * Math.PI * 2);
                var (formId, _) = LabEquipPackIns[rand.Next(LabEquipPackIns.Length)];
                Place(targetMod, sfEsm, formId, pod.X + ox, pod.Y + oy, pod.Z, yaw, cell, state);
                placed++;
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[BuildingDecoratorPass] Placed {placed} decorations across {state.BuildingPodPositions.Count} pods");
    }

    private static void Place(
        StarfieldMod targetMod, ModKey sfEsm,
        uint formId, float wx, float wy, float wz, float yawZ,
        Cell cell, WorldspaceState state)
    {
        var placed = new PlacedObject(targetMod)
        {
            Base     = new FormKey(sfEsm, formId).ToNullableLink<IPlaceableObjectGetter>(),
            Position = new P3Float(wx, wy, wz),
            Rotation = new P3Float(0f, 0f, yawZ),
        };
        if (state.LodLayer.HasValue)
            placed.Layer = state.LodLayer.Value.ToNullableLink<ILayerGetter>();
        state.PlacementUtil.AddToTemporary(cell, placed);
    }
}
