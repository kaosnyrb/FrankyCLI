using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that places a LvlHumanHostile_Boss [NPC_:00375AA5] in the
/// worldspace's persistent TopCell, wired into the Location's
/// MasterSpecialReferences as LocDungeonBossLocRef [LCRT:00003956].
///
/// Position: <see cref="WorldspaceState.PoiCenterX"/>/<see cref="WorldspaceState.PoiCenterY"/>
/// when set by the topology pass; otherwise the worldspace origin (0, 0).
///
/// The placed NPC is stored on <see cref="WorldspaceState.BossPlacedNpc"/>
/// so that downstream passes (quest, bounty) can reference it by FormKey.
/// </summary>
public class WorldspaceBossPass : IWorldspacePass
{
    // LvlHumanHostile_Boss [NPC_:00375AA5]
    private static readonly uint BossNpcFormId = 0x00375AA5;

    // LocDungeonBossLocRef [LCRT:00003956]
    private static readonly uint BossLocRefFormId = 0x00003956;

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;

        float posX = state.PoiCenterX ?? 0f;
        float posY = state.PoiCenterY ?? 0f;

        float worldZ = state.TerrainHeight;
        if (state.BtdFile != null)
        {
            // SampleHeightAtWorld takes BTD-internal coords (overlay → BTD: × 4096/100).
            float btdX = posX * (4096f / 100f);
            float btdY = posY * (4096f / 100f);
            worldZ = state.BtdFile.SampleHeightAtWorld(btdX, btdY) / 8f;
        }

        const StarfieldMajorRecord.StarfieldMajorRecordFlag PersistentFlag =
            (StarfieldMajorRecord.StarfieldMajorRecordFlag)PlacedObject.DefaultMajorFlag.Persistent;

        var boss = new PlacedNpc(targetMod)
        {
            StarfieldMajorRecordFlags = PersistentFlag,
            Position = new P3Float(posX, posY, worldZ),
        };
        boss.Base = new FormKey(starfieldEsm, BossNpcFormId).ToNullableLink<INpcGetter>();
        boss.PersistentLocation = state.Location.FormKey.ToNullableLink<ILocationGetter>();
        boss.Location = state.Location.FormKey.ToNullableLink<ILocationGetter>();
        boss.LocationRefTypes =
        [
            new FormKey(starfieldEsm, BossLocRefFormId).ToLink<ILocationReferenceTypeGetter>(),
        ];

        state.PlacementUtil.AddToPersistent(boss);
        state.BossPlacedNpc = boss;

        int cellX = (int)MathF.Floor(posX / 100f);
        int cellY = (int)MathF.Floor(posY / 100f);
        if (state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var bossCell))
        {
            state.Location.MasterSpecialReferences ??= new ExtendedList<LocationCellStaticReference>();
            state.Location.MasterSpecialReferences.Add(new LocationCellStaticReference
            {
                LocationRefType = new FormKey(starfieldEsm, BossLocRefFormId).ToLink<ILocationReferenceTypeGetter>(),
                Marker          = boss.FormKey.ToLink<IPlacedGetter>(),
                Location        = bossCell.FormKey.ToLink<IComplexLocationGetter>(),
                Grid            = new P2Int16((short)cellX, (short)cellY),
            });
        }
        else
        {
            Console.WriteLine($"[WorldspaceBossPass] WARNING: no SubCell at ({cellX},{cellY}) — LocDungeonBossLocRef not wired");
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[WorldspaceBossPass] Placed boss {boss.FormKey} at ({posX:F1}, {posY:F1}, {worldZ:F1})");
    }
}
