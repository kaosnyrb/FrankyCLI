using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that clusters a number of levelled human hostile NPCs
/// around the POI centre (<see cref="WorldspaceState.PoiCenterX"/>/<see cref="WorldspaceState.PoiCenterY"/>),
/// falling back to (0, 0) if the topology pass has not yet run.
///
/// Each NPC is placed into the sub-cell that owns its XY position and is
/// wired to the worldspace Location via PersistentLocation + Location.
///
/// NPC pool (Starfield.esm):
///   LvlHumanHostile_Assault  [NPC_:00375AA4]
///   LvlHumanHostile_Charger  [NPC_:00375AA6]
///   LvlHumanHostile_Heavy    [NPC_:00375AB9]
///   LvlHumanHostile_Recruit  [NPC_:00375ABA]
///   LvlHumanHostile_Sniper   [NPC_:00375ABB]
///   LvlHumanHostile_Support  [NPC_:00375ABD]
/// </summary>
public class LvlHumanHostilePass : IWorldspacePass
{
    // NPC pool — non-boss LvlHumanHostile_ variants (Starfield.esm)
    private static readonly uint[] NpcFormIds =
    [
        0x00375AA4, // LvlHumanHostile_Assault
        0x00375AA6, // LvlHumanHostile_Charger
        0x00375AB9, // LvlHumanHostile_Heavy
        0x00375ABA, // LvlHumanHostile_Recruit
        0x00375ABB, // LvlHumanHostile_Sniper
        0x00375ABD, // LvlHumanHostile_Support
    ];

    private const float OverlayToBtd = 4096f / 100f;

    private readonly int   _count;
    private readonly float _scatterRadius;

    /// <param name="count">Number of NPCs to place.</param>
    /// <param name="scatterRadius">
    /// Maximum XY scatter offset (overlay units) from the POI centre per NPC.
    /// Defaults to 10 overlay units (~10 m).
    /// </param>
    public LvlHumanHostilePass(int count, float scatterRadius = 10f)
    {
        _count         = count;
        _scatterRadius = scatterRadius;
    }

    public void RunPass(WorldspaceState state)
    {
        var targetMod  = RetrogradeContext.Current.TargetMod;
        var sfEsm      = RetrogradeContext.Current.StarfieldModKey;
        var rand       = state.Rng;

        float centerX = state.PoiCenterX ?? 0f;
        float centerY = state.PoiCenterY ?? 0f;

        const StarfieldMajorRecord.StarfieldMajorRecordFlag PersistentFlag =
            (StarfieldMajorRecord.StarfieldMajorRecordFlag)PlacedObject.DefaultMajorFlag.Persistent;

        int placed = 0;
        for (int i = 0; i < _count; i++)
        {
            // Random offset within a circle of radius _scatterRadius.
            float angle = (float)(rand.NextDouble() * MathF.PI * 2f);
            float dist  = (float)(rand.NextDouble() * _scatterRadius);
            float posX  = centerX + MathF.Cos(angle) * dist;
            float posY  = centerY + MathF.Sin(angle) * dist;

            float posZ = state.TerrainHeight;
            if (state.BtdFile != null)
                posZ = state.BtdFile.SampleHeightAtWorld(posX * OverlayToBtd, posY * OverlayToBtd) / 8f;

            int cellX = (int)MathF.Floor(posX / 100f);
            int cellY = (int)MathF.Floor(posY / 100f);
            if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
            {
                if (!RetrogradeContext.Quiet)
                    Console.WriteLine($"[LvlHumanHostilePass] WARNING: no sub-cell at ({cellX},{cellY}) — skipping NPC {i}");
                continue;
            }

            uint npcFormId = NpcFormIds[rand.Next(NpcFormIds.Length)];

            var npc = new PlacedNpc(targetMod)
            {
                StarfieldMajorRecordFlags = PersistentFlag,
                Position = new P3Float(posX, posY, posZ),
            };
            npc.Base                = new FormKey(sfEsm, npcFormId).ToNullableLink<INpcGetter>();
            npc.PersistentLocation  = state.Location.FormKey.ToNullableLink<ILocationGetter>();
            npc.Location            = state.Location.FormKey.ToNullableLink<ILocationGetter>();

            state.PlacementUtil.AddToTemporary(cell, npc);
            placed++;
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[LvlHumanHostilePass] Placed {placed}/{_count} hostile NPCs around ({centerX:F1}, {centerY:F1})");
    }
}
