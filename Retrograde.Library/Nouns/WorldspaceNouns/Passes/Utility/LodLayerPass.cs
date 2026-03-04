using Mutagen.Bethesda.Starfield;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that creates a Layer record with <see cref="Layer.LodBehaviorType.Aggregate"/>
/// and stores its FormKey in <see cref="WorldspaceState.LodLayer"/>.
/// Run this early in ContentPasses so subsequent passes can assign objects to the layer.
/// </summary>
public class LodLayerPass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        var layer = new Layer(targetMod)
        {
            EditorID = state.Worldspace.EditorID + "_LodLayer",
            LodBehavior = Layer.LodBehaviorType.Aggregate,
        };
        targetMod.Layers.Add(layer);

        state.LodLayer = layer.FormKey;

        Console.WriteLine($"[LodLayerPass] Created LOD layer {layer.EditorID} ({layer.FormKey})");
    }
}
