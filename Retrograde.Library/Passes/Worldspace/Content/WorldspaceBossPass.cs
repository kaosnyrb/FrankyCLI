using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that places a LvlHumanHostile_Boss [NPC_:00375AA5] in the
/// worldspace's persistent TopCell at the worldspace origin (0, 0) and
/// wires it into the Location's MasterSpecialReferences as
/// LocDungeonBossLocRef [LCRT:00003956].
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

        float worldZ = state.TerrainHeight;
        if (state.BtdFile != null)
        {
            // SampleHeightAtWorld takes BTD-internal coords; origin is always 0,0 for Starfield BTDs
            worldZ = state.BtdFile.SampleHeightAtWorld(0f, 0f) / 8f;
        }

        const StarfieldMajorRecord.StarfieldMajorRecordFlag PersistentFlag =
            (StarfieldMajorRecord.StarfieldMajorRecordFlag)PlacedObject.DefaultMajorFlag.Persistent;

        var boss = new PlacedNpc(targetMod)
        {
            StarfieldMajorRecordFlags = PersistentFlag,
            Position = new P3Float(0f, 0f, worldZ),
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

        // Cell at worldspace origin (0, 0) maps to grid (0, 0)
        if (state.CellLookup.TryGetValue(new P2Int(0, 0), out var bossCell))
        {
            state.Location.MasterSpecialReferences ??= new ExtendedList<LocationCellStaticReference>();
            state.Location.MasterSpecialReferences.Add(new LocationCellStaticReference
            {
                LocationRefType = new FormKey(starfieldEsm, BossLocRefFormId).ToLink<ILocationReferenceTypeGetter>(),
                Marker          = boss.FormKey.ToLink<IPlacedGetter>(),
                Location        = bossCell.FormKey.ToLink<IComplexLocationGetter>(),
                Grid            = new P2Int16(0, 0),
            });
        }
        else
        {
            Console.WriteLine("[WorldspaceBossPass] WARNING: no SubCell at (0,0) — LocDungeonBossLocRef not wired");
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[WorldspaceBossPass] Placed boss {boss.FormKey} at (0, 0, {worldZ:F1})");
    }
}
