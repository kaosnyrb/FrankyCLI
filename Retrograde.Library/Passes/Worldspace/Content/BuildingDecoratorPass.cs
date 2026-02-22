using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Places interior decorations inside each science building pod.
///
/// Must run after <see cref="ScienceBuildingPass"/>, which populates
/// <see cref="WorldspaceState.BuildingPodPositions"/>.
///
/// Rather than rolling per-category dice, this pass uses five room templates
/// extracted directly from <b>LC179World</b> — a Starfield hydroponic facility
/// that uses the same <c>OphIntPodSmMid01</c> pod statics as our buildings.
/// Each pod is assigned a random template; all items are placed at their
/// original relative positions and rotations from the source worldspace.
///
/// Templates (by LC179World pod index):
/// <list type="bullet">
///   <item>0 — Sparse: a few empty + filled plant pots near one wall</item>
///   <item>1 — PlantDisplay_A: euphorbia + paddle plants on industrial shelves</item>
///   <item>2 — GrowingWalls_A: plant pots + broadleaf shrubs on stacked shelf walls</item>
///   <item>3 — PlantDisplay_B: denser version of PlantDisplay_A with more euphorbia</item>
///   <item>5 — GrowingWalls_B: denser growing walls + herb-storage PackIns</item>
/// </list>
///
/// Rotation note: LC179World pods have Rot Z = π/2; our building pods have Rot Z = 0.
/// Item positions are used as-is (no −π/2 transform). Since the pods are round,
/// the layouts look coherent regardless of orientation.
/// If exact wall-hugging is later required, apply: dx_new = dy, dy_new = −dx, rz_new = rz − π/2.
/// </summary>
public class BuildingDecoratorPass : IWorldspacePass
{
    // ── Template item type ───────────────────────────────────────────────────────
    // (dx, dy, dz) — offset from pod centre in overlay units
    // (rx, ry, rz) — world-space rotation in radians, directly from source worldspace
    // FormId       — Starfield.esm FormID of the Static/PackIn to place
    private readonly record struct PodItem(
        float Dx, float Dy, float Dz,
        float Rx, float Ry, float Rz,
        uint FormId);

    // ── Templates ────────────────────────────────────────────────────────────────

    // Sparse: 13 items from LC179World pod 0
    private static readonly PodItem[] TemplateSparse =
    [
        new(+0.1678f,+2.3650f,+0.5001f, 0.0f,-0.0f,0.0f,     0x20AFE5),  // PlantPotGen_Empty01
        new(-2.0001f,+2.3684f,+0.5000f, 0.0f,0.0f,4.3817f,   0x23753E),
        new(+0.9031f,+1.8074f,-0.0040f, 0.0f,-0.0f,0.0f,     0x21C1DD),
        new(-2.0001f,+1.7500f,+0.5000f, 0.0f,0.0f,0.0f,      0x20AFE5),  // PlantPotGen_Empty01
        new(+0.9031f,+1.8074f,+0.4960f, 0.0f,-0.0f,0.0f,     0x21C1DB),
        new(-0.2538f,+1.7549f,+0.5000f, 0.0f,-0.0f,0.0f,     0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6969f,+2.5074f,+0.4960f, 0.0f,-0.0f,0.0f,     0x20AFE5),  // PlantPotGen_Empty01
        new(+0.9031f,+1.8074f,-0.0112f, 0.0f,0.0f,3.1416f,   0x2CB3B2),
        new(+0.3387f,+0.3407f,+0.0000f, 0.2084f,1.5708f,4.9208f, 0x2024A6),
        new(-0.0758f,+1.2846f,+0.6401f, 1.6248f,-0.9993f,6.2377f, 0x16161B),
        new(+0.6158f,+0.3585f,+0.0000f, 0.0f,0.0f,0.0f,      0x237541),
        new(-2.0550f,+1.0909f,+0.6101f, 1.6248f,-0.9993f,6.2377f, 0x161619),
        new(-1.9303f,+2.3470f,+1.1378f, 0.0f,-0.0f,4.5382f,  0x24DD7B),  // PortableGreenhouse01_CapsChlorophyll01
    ];

    // PlantDisplay_A: 30 items from LC179World pod 1
    // Euphorbia succulents + paddle plants arranged on Ind_ShelfKitA01/03 units
    private static readonly PodItem[] TemplatePlantDisplay_A =
    [
        new(+1.6487f,-0.7575f,+0.7536f, 0.0f,0.0f,3.1416f,   0x11B690),  // PlantEuphorbia06
        new(+1.6451f,+0.8981f,+0.7555f, 0.0725f,0.0147f,2.3261f, 0x0EC874),  // PlantPaddle02
        new(+1.2500f,+1.2160f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(+0.6500f,+0.9173f,+1.4154f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.6500f,+0.9173f,+1.4154f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.9000f,-0.7212f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6500f,-0.7225f,+0.7001f, 0.0f,0.0f,3.1416f,   0x11B68B),  // PlantEuphorbia01
        new(+1.2500f,-0.4212f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(+2.9000f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6500f,-0.7212f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.6557f,-0.7345f,+1.7340f, 0.0f,0.0f,2.9416f,   0x11B68F),  // PlantEuphorbia05
        new(+0.7298f,+0.9665f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.2500f,+1.2160f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(+0.7184f,-0.7382f,+1.7253f, 0.0f,-0.0f,2.6416f,  0x11B68C),  // PlantEuphorbia02
        new(+2.9000f,+0.9173f,+1.4142f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.2500f,-0.4212f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(+2.9000f,-0.7225f,+0.7217f, 0.098f,0.0f,0.3416f, 0x11B68F),  // PlantEuphorbia05
        new(+1.6500f,-0.7212f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.9286f,+0.9284f,+1.7931f, 0.0f,0.0f,4.3416f,   0x0EC873),  // PlantPaddle01
        new(+2.9038f,+1.0010f,+0.7621f, 0.0f,0.0f,3.1416f,   0x0EC875),  // PlantPaddle03
        new(+1.6216f,+0.9622f,+1.8430f, 0.024f,-0.0f,4.4416f, 0x0EC876), // PlantPaddle04
        new(+2.9000f,+0.9160f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+0.7091f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.6500f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.9000f,-0.7212f,+1.7292f, 0.0f,0.0f,3.1416f,   0x11B68B),  // PlantEuphorbia01
        new(+1.6500f,+0.9161f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6330f,+0.9921f,+1.7675f, 0.0f,0.0f,3.1416f,   0x0EC875),  // PlantPaddle03
        new(+0.6407f,+1.0061f,+0.7973f, 0.0f,-0.0f,3.7416f,  0x0EC876),  // PlantPaddle04
        new(+0.8377f,+1.0632f,+0.7973f, -0.0f,-0.191f,3.1416f, 0x0EC875), // PlantPaddle03
        new(+2.7010f,-0.0743f,+1.5998f, 0.0f,-0.0f,1.5708f,  0x09B204),  // unknown elevated item
    ];

    // GrowingWalls_A: 32 items from LC179World pod 2
    // PlantPotGen_Filled01 + PlantShrubBroadleaf03 on stacked Ind_ShelfKitA01/03 walls
    private static readonly PodItem[] TemplateGrowingWalls_A =
    [
        new(-0.6727f,+2.9000f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6714f,+0.6500f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6701f,+1.6500f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9675f,+1.7144f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9649f,+2.9644f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,+1.3144f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(+0.9662f,+1.7144f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9662f,+0.7144f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9662f,+2.9644f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9675f,+0.7144f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9662f,+1.7144f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9675f,+2.9644f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6714f,+2.9000f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9662f,+0.7144f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,+1.3144f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.6714f,+1.6500f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9650f,+1.7144f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9662f,+2.9644f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.9714f,+1.2500f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.6714f,+1.6500f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9649f,+0.7144f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6727f,+0.6500f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6714f,+0.6500f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6701f,+0.6500f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.6663f,+1.3144f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(-0.6714f,+2.9000f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6701f,+2.9000f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.9714f,+1.2500f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.9714f,+1.2500f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(-0.6727f,+1.6500f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.0549f,+3.0754f,+1.5033f, 0.0f,-0.0f,3.1416f,  0x09B204),  // unknown elevated item
        new(-0.2346f,-2.0339f,+0.0000f, -3.1416f,-0.0f,4.7124f, 0x1E867E), // unknown floor item
    ];

    // PlantDisplay_B: 55 items from LC179World pod 3
    // Dense euphorbia + paddle plant display on many shelf stacks across the pod
    private static readonly PodItem[] TemplatePlantDisplay_B =
    [
        new(-2.1000f,+0.9160f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.1500f,+0.9160f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-0.5000f,-0.4212f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(+0.0036f,+0.9173f,+1.4147f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.1500f,+0.9173f,+1.4069f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.1500f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-2.5000f,+1.2160f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(-2.0833f,+0.9927f,+0.7720f, -0.0063f,0.0888f,4.6413f, 0x0EC875), // PlantPaddle03
        new(-2.1000f,+0.9173f,+1.4142f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-0.9964f,+0.9173f,+1.4147f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.2103f,-0.7975f,+0.7343f, 0.0f,0.0f,3.1416f,   0x11B68F),  // PlantEuphorbia05
        new(+1.2078f,-0.6964f,+0.7239f, 0.0f,-0.0f,3.6416f,  0x11B68B),  // PlantEuphorbia01
        new(-1.1000f,-0.7212f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-0.9945f,+0.9160f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+2.1291f,+0.9629f,+0.7973f, 0.0f,-0.0f,5.0416f,  0x0EC876),  // PlantPaddle04
        new(+1.1938f,+0.9969f,+0.7718f, 0.0f,0.0f,3.1416f,   0x0EC873),  // PlantPaddle01
        new(-0.9945f,+0.9147f,+0.7141f, 0.0f,0.0f,3.1416f,   0x0EC873),  // PlantPaddle01
        new(+2.1500f,+1.0287f,+1.7666f, 0.0f,0.0f,3.1416f,   0x0EC875),  // PlantPaddle03
        new(+2.1301f,-0.7597f,+1.7283f, 0.0f,-0.0f,4.1416f,  0x11B68B),  // PlantEuphorbia01
        new(-2.5422f,-0.4212f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(-0.1000f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-2.1000f,-0.7212f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.1708f,-0.7926f,+1.7408f, 0.0f,0.0f,3.1416f,   0x11B68C),  // PlantEuphorbia02
        new(+0.0055f,+0.9160f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-2.1000f,-0.7225f,+0.7448f, 0.0f,-0.0f,0.3584f,  0x11B68D),  // PlantEuphorbia03
        new(-0.9081f,+0.9160f,+1.8014f, 0.0f,0.066f,4.7416f, 0x0EC875),  // PlantPaddle03
        new(-0.3945f,+1.2160f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(-0.5000f,-0.4212f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(-0.0109f,+0.9767f,+1.7656f, 0.0f,0.0f,3.1416f,   0x0EC875),  // PlantPaddle03
        new(+0.0055f,+0.9147f,+0.7141f, 0.0f,0.0f,3.1416f,   0x0EC873),  // PlantPaddle01
        new(+2.1920f,-0.7962f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+1.7500f,+1.2160f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(+1.1708f,-0.7593f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-2.1000f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-2.1000f,-0.7212f,+1.7292f, 0.0f,0.0f,3.1416f,   0x11B68B),  // PlantEuphorbia01
        new(+1.2240f,-0.7364f,+0.4000f, 0.0f,-0.0f,3.6416f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-1.1000f,-0.7199f,+1.4082f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(+3.1909f,+0.0942f,-0.0040f, 0.0f,-0.0f,3.1416f,  0x2C1C99),  // Shelves_PalletRacks_Bollard01
        new(-2.0673f,+0.9443f,+1.8272f, 0.0f,-0.056f,1.9416f, 0x0EC876), // PlantPaddle04
        new(-0.0746f,-0.7399f,+0.6964f, 0.0f,0.0f,0.4584f,   0x11B68E),  // PlantEuphorbia04
        new(-1.1347f,-0.7546f,+1.7069f, 0.0f,-0.0f,5.7416f,  0x11B68B),  // PlantEuphorbia01
        new(+1.1849f,+0.9659f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-0.1000f,-0.7212f,+0.4000f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-0.1000f,-0.7674f,+1.7135f, -0.094f,0.0f,3.6416f, 0x11B68B), // PlantEuphorbia01
        new(-0.3945f,+1.2160f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(-2.5422f,-0.4212f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(+1.7500f,+1.2160f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(-2.5000f,+1.2160f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(+1.7500f,-0.4212f,+0.4041f, 0.0f,0.0f,3.1416f,   0x257AB8),  // Ind_ShelfKitA03
        new(-1.1177f,-0.7659f,+0.6968f, -0.041f,0.04f,3.9416f, 0x11B68B), // PlantEuphorbia01
        new(+1.7500f,-0.4212f,+0.0000f, 0.0f,0.0f,3.1416f,   0x257AB6),  // Ind_ShelfKitA01
        new(+1.1635f,+0.8802f,+1.8404f, 0.0f,0.0f,3.1416f,   0x0EC876),  // PlantPaddle04
        new(+2.1500f,+0.9173f,+1.4069f, 0.0f,0.0f,3.1416f,   0x20AFE7),  // PlantPotGen_Filled01
        new(-2.0466f,+0.9363f,+0.7360f, -0.0759f,0.03f,2.4192f, 0x0EC875), // PlantPaddle03
        new(+1.2013f,-0.8148f,+0.7239f, 0.0f,0.0f,0.2584f,   0x11B68F),  // PlantEuphorbia05
    ];

    // GrowingWalls_B: 55 items from LC179World pod 5
    // Dense growing walls with PlantShrubBroadleaf03 + Hydroponics_Storage_Herbs_01 PackIn
    private static readonly PodItem[] TemplateGrowingWalls_B =
    [
        new(-0.6726f,-2.1000f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6726f,-0.6847f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,+2.0779f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.9713f,+2.0758f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(+0.9663f,-2.0356f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6714f,-2.1000f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.9713f,-0.0847f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.6714f,+0.3153f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6713f,-0.6847f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6713f,-0.6847f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,-0.0483f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(+0.6663f,+2.0779f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(+0.6663f,-0.0483f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.6714f,+2.4758f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6713f,+1.4758f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.9713f,-0.0847f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(+0.9663f,-0.6483f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,-2.4356f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.9713f,-2.5000f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.9713f,+2.0758f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.6701f,+2.4758f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9675f,-0.6482f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9650f,-0.6483f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,-0.0483f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.6726f,+2.4758f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9662f,+0.3517f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9662f,+1.4779f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9650f,-2.0356f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6714f,+2.4758f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6701f,-2.1000f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6701f,-0.6847f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6701f,+1.4758f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9663f,-2.0356f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.9713f,-2.5000f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(+0.9663f,-0.6483f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.9713f,-2.5000f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(-0.6714f,+0.3153f,+0.4000f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9675f,+0.3518f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9650f,+1.4779f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.6663f,-2.4356f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(-0.6701f,+0.3153f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6726f,+1.4758f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.9713f,+2.0758f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(+0.6663f,-2.4356f,+1.4196f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(-0.9713f,-0.0847f,+0.4155f, 0.0f,-0.0f,1.5708f,  0x257AB8),  // Ind_ShelfKitA03
        new(+0.9662f,+0.3517f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.9650f,+0.3518f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(-0.6713f,+1.4758f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6714f,-2.1000f,+1.7000f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(+0.6663f,+2.0779f,+0.0000f, 0.0f,-0.0f,1.5708f,  0x257AB6),  // Ind_ShelfKitA01
        new(+0.9675f,-2.0356f,+0.7141f, 0.0f,0.0f,1.5708f,   0x05F178),  // PlantShrubBroadleaf03
        new(-0.6726f,+0.3153f,+1.3859f, 0.0f,-0.0f,1.5708f,  0x20AFE7),  // PlantPotGen_Filled01
        new(+0.9482f,+2.6675f,+0.4036f, 0.0f,0.0f,4.7500f,   0x23BA70),  // Hydroponics_Storage_Herbs_01 (PackIn)
        new(+0.9800f,+2.6190f,+1.4194f, 0.0f,-0.0f,1.6500f,  0x23BA70),  // Hydroponics_Storage_Herbs_01 (PackIn)
        new(+0.9833f,+1.7297f,+0.4036f, 0.0f,0.0f,1.4668f,   0x108701),  // Hydroponics_Storagebin01ClosedFull
    ];

    // ── Template pool ────────────────────────────────────────────────────────────
    private static readonly PodItem[][] Templates =
    [
        TemplateSparse,
        TemplatePlantDisplay_A,
        TemplateGrowingWalls_A,
        TemplatePlantDisplay_B,
        TemplateGrowingWalls_B,
    ];

    // ── IWorldspacePass ──────────────────────────────────────────────────────────

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

            var template = Templates[rand.Next(Templates.Length)];
            foreach (var item in template)
            {
                Place(targetMod, sfEsm, item.FormId,
                      pod.X + item.Dx, pod.Y + item.Dy, pod.Z + item.Dz,
                      item.Rx, item.Ry, item.Rz,
                      cell, state);
                placed++;
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[BuildingDecoratorPass] Placed {placed} decorations across " +
                              $"{state.BuildingPodPositions.Count} pods " +
                              $"({Templates.Length} room templates)");
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    private static void Place(
        StarfieldMod targetMod, ModKey sfEsm,
        uint formId, float wx, float wy, float wz, float rx, float ry, float rz,
        Cell cell, WorldspaceState state)
    {
        var placed = new PlacedObject(targetMod)
        {
            Base     = new FormKey(sfEsm, formId).ToNullableLink<IPlaceableObjectGetter>(),
            Position = new P3Float(wx, wy, wz),
            Rotation = new P3Float(rx, ry, rz),
        };
        if (state.LodLayer.HasValue)
            placed.Layer = state.LodLayer.Value.ToNullableLink<ILayerGetter>();
        state.PlacementUtil.AddToTemporary(cell, placed);
    }
}
