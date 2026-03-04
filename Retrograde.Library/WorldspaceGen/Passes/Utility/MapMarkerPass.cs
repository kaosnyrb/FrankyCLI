using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that places a MapMarker_MediumLongRange [STAT:0031FB3E] in the
/// worldspace's persistent cell, plus a linked XMarker as the fast-travel destination.
/// Matches the setup seen in OEAF026World (TopCell 029EAD).
///
/// Position priority:
///   1. <see cref="WorldspaceState.MarkerPosition"/> — explicit override
///   2. <see cref="WorldspaceState.PoiCenterX"/>/<see cref="WorldspaceState.PoiCenterY"/> — set by topology pass
///   3. Worldspace origin (0, 0) — final fallback
/// </summary>
public class MapMarkerPass(MapMarkerPass.MarkerType markerType = MapMarkerPass.MarkerType.Settlement) : IWorldspacePass
{
    /// <summary>
    /// Map marker icon type, as seen in Starfield.esm worldspace TopCells.
    /// Values verified by surveying vanilla and mod worldspaces.
    /// </summary>
    public enum MarkerType : sbyte
    {
        City          = 2,
        Cave          = 10,
        Outpost       = 11,
        SmallStructure = 12,
        ResearchBase  = 13,
        Landmark      = 14,
        CrashedShip   = 19,
        Farm          = 20,
        Industrial    = 21,  // pipelines, processing facilities, bunkers
        MilitaryBase  = 22,  // military compounds, observation towers
        Settlement    = 35,  // colonies, encampments, inhabited outposts
        Town          = 48,
    }

    /// <summary>
    /// VNAM field on the map marker sub-record.
    /// Only Structure (2) observed across all surveyed worldspaces.
    /// </summary>
    public enum MarkerVnam : ushort
    {
        Structure = 2,
    }

    // MapMarker_MediumLongRange [STAT:0031FB3E]
    private static readonly uint MapMarkerStaticFormId = 0x0031FB3E;

    // XMarker [STAT:00003B] — fast-travel destination linked from the map marker
    private static readonly uint XMarkerFormId = 0x00003B;

    // MapMarkerRefType [LCRT:0002271F]
    private static readonly uint MapMarkerLocRefFormId = 0x0002271F;

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;

        float z = state.TerrainHeight + 4.0f;
        var position = state.MarkerPosition
            ?? (state.PoiCenterX.HasValue && state.PoiCenterY.HasValue
                ? new P3Float(state.PoiCenterX.Value, state.PoiCenterY.Value, z)
                : new P3Float(0f, 0f, z));

        const StarfieldMajorRecord.StarfieldMajorRecordFlag PersistentFlag =
            (StarfieldMajorRecord.StarfieldMajorRecordFlag)PlacedObject.DefaultMajorFlag.Persistent;

        // XMarker is the fast-travel spawn point, linked from the map marker
        var travelMarker = new PlacedObject(targetMod)
        {
            StarfieldMajorRecordFlags = PersistentFlag,
            Base = new FormKey(starfieldEsm, XMarkerFormId).ToNullableLink<IPlaceableObject>(),
            Position = position,
        };

        var mapMarker = new PlacedObject(targetMod)
        {
            StarfieldMajorRecordFlags = PersistentFlag,
            Base = new FormKey(starfieldEsm, MapMarkerStaticFormId).ToNullableLink<IPlaceableObject>(),
            Position = position,
            MapMarker = new PlacedObjectMapMarker
            {
                // 25 = Visible(1) | UseLocationName(8) | 0x10 — as seen in OEAF026World
                Flags = PlacedObjectMapMarker.Flag.Visible
                      | PlacedObjectMapMarker.Flag.UseLocationName
                      | (PlacedObjectMapMarker.Flag)16,
                Name = "",
                Type = (sbyte)markerType,
                Unknown = 0,
                UNAM = "",
                VNAM = (ushort)MarkerVnam.Structure,
                VISI = 0,
            },
        };

        mapMarker.LinkedReferences.Add(new LinkedReferences
        {
            KeywordOrReference = FormKey.Null.ToLink<IKeywordLinkedReferenceGetter>(),
            Reference = travelMarker.FormKey.ToLink<IPlacedGetter>(),
        });

        state.PlacementUtil.AddToPersistent(travelMarker);
        state.PlacementUtil.AddToPersistent(mapMarker);

        // Wire the map marker into the Location's MasterSpecialReferences.
        // The Location field takes the SubCell that spatially contains the marker position.
        int cellX = (int)MathF.Floor(position.X / 100f);
        int cellY = (int)MathF.Floor(position.Y / 100f);
        if (state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var markerCell))
        {
            state.Location.MasterSpecialReferences ??= new ExtendedList<LocationCellStaticReference>();
            state.Location.MasterSpecialReferences.Add(new LocationCellStaticReference
            {
                LocationRefType = new FormKey(starfieldEsm, MapMarkerLocRefFormId).ToLink<ILocationReferenceTypeGetter>(),
                Marker          = mapMarker.FormKey.ToLink<IPlacedGetter>(),
                Location        = markerCell.FormKey.ToLink<IComplexLocationGetter>(),
                Grid            = new P2Int16((short)cellX, (short)cellY),
            });
        }
        else
        {
            Console.WriteLine($"[MapMarkerPass] WARNING: no SubCell at ({cellX},{cellY}) for map marker LocRef — skipping");
        }
    }
}
