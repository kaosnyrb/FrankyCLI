using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using System;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Clones the non-Static Temporary objects from the vanilla source cell
/// (patrol markers, ship spawn markers, etc.) into the new space cell.
/// Each gets a fresh FormKey; all other fields are copied verbatim.
/// </summary>
public class SpaceMarkersPass : ISpaceCellPass
{
    public void RunPass(SpaceCellState state)
    {
        if (state.MarkerTemplates == null || state.MarkerTemplates.Count == 0)
        {
            Console.WriteLine("[SpaceMarkersPass] No marker templates — skipping.");
            return;
        }

        var targetMod = RetrogradeContext.Current.TargetMod;

        foreach (var src in state.MarkerTemplates)
        {
            var po = new PlacedObject(targetMod)
            {
                Action                    = src.Action,
                AttachRef                 = src.AttachRef,
                Base                      = src.Base,
                BlueprintPartOrigin       = src.BlueprintPartOrigin,
                BOLV                      = src.BOLV,
                Collision                 = src.Collision,
                Comments                  = src.Comments,
                Components                = src.Components,
                ConstrainedDecal          = src.ConstrainedDecal,
                Count                     = src.Count,
                CurrentZoneCell           = src.CurrentZoneCell,
                DebugText                 = src.DebugText,
                EditorID                  = src.EditorID != null
                                               ? src.EditorID + RandomProvider.Random.Next(10000)
                                               : null,
                EnableParent              = src.EnableParent,
                ExternalEmittance         = src.ExternalEmittance,
                FactionRank               = src.FactionRank,
                GeometryDirtinessScale    = src.GeometryDirtinessScale,
                GroupedPackIn             = src.GroupedPackIn,
                HeadTrackingWeight        = src.HeadTrackingWeight,
                HealthPercent             = src.HealthPercent,
                IsActivationPoint         = src.IsActivationPoint,
                IsIgnoredBySandbox        = src.IsIgnoredBySandbox,
                IsLinkedRefTransient      = src.IsLinkedRefTransient,
                LayeredMaterialSwaps      = src.LayeredMaterialSwaps,
                LevelModifier             = src.LevelModifier,
                LightArea                 = src.LightArea,
                LightBarndoorData         = src.LightBarndoorData,
                LightColors               = src.LightColors,
                LightFlicker              = src.LightFlicker,
                GoboAnimatedProperties    = src.GoboAnimatedProperties,
                Lighting                  = src.Lighting,
                LightLayerData            = src.LightLayerData,
                LightRoundedness          = src.LightRoundedness,
                LightStaticShadowMap      = src.LightStaticShadowMap,
                LightVolumetricData       = src.LightVolumetricData,
                LinkedReferences          = src.LinkedReferences,
                LocationRefTypes          = src.LocationRefTypes,
                Lock                      = src.Lock,
                MapMarker                 = src.MapMarker,
                NavigationDoorLink        = src.NavigationDoorLink,
                NumTraversalFluffBytes    = src.NumTraversalFluffBytes,
                OpenByDefault             = src.OpenByDefault,
                Ownership                 = src.Ownership,
                Patrol                    = src.Patrol,
                Position                  = src.Position,
                PowerLinks                = src.PowerLinks,
                Primitive                 = src.Primitive,
                ProjectedDecal            = src.ProjectedDecal,
                ProjectedDecalReferences  = src.ProjectedDecalReferences,
                Radius                    = src.Radius,
                RagdollBipedRotation      = src.RagdollBipedRotation,
                Properties                = src.Properties,
                RagdollData               = src.RagdollData,
                StarfieldMajorRecordFlags = src.StarfieldMajorRecordFlags,
                Rotation                  = src.Rotation,
                Scale                     = src.Scale,
                ShipArrival               = src.ShipArrival,
                SnapLinks                 = src.SnapLinks,
                TeleportDestination       = src.TeleportDestination,
                Spline                    = src.Spline,
                TimeOfDay                 = src.TimeOfDay,
                Traversals                = src.Traversals,
                VolumeData                = src.VolumeData,
                VirtualMachineAdapter     = src.VirtualMachineAdapter,
                XALG                      = src.XALG,
                PlacedObjectXCZRXCZA      = src.PlacedObjectXCZRXCZA,
                XFLG                      = src.XFLG,
                XNSE                      = src.XNSE,
                XPCK                      = src.XPCK,
            };
            // FormLink fields after construction.
            if (!src.Emittance.IsNull)
                po.Emittance = src.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();
            if (!src.Layer.IsNull)
                po.Layer = src.Layer.FormKey.ToNullableLink<ILayerGetter>();
            if (!src.PersistentLocation.IsNull)
                po.PersistentLocation = src.PersistentLocation.FormKey.ToNullableLink<ILocationGetter>();
            if (!src.ReferenceGroup.IsNull)
                po.ReferenceGroup = src.ReferenceGroup.FormKey.ToNullableLink<IReferenceGroupGetter>();
            if (!src.SourcePackIn.IsNull)
                po.SourcePackIn = src.SourcePackIn.FormKey.ToNullableLink<IPackInGetter>();
            if (!src.EncounterZone.IsNull)
                po.EncounterZone = src.EncounterZone;

            state.Cell.Temporary.Add(po);
        }

        Console.WriteLine($"[SpaceMarkersPass] Placed {state.MarkerTemplates.Count} markers.");
    }
}
