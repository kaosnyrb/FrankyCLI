using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Registers the generated worldspace with the Planet Content Manager (PCM) tree.
///
/// The PCM tree is the mechanism Starfield uses to decide which POI worldspaces
/// are eligible for placement on planets. Three records are involved:
///
///   1. A <see cref="PlanetContentManagerBranchNode"/> of type <c>BranchNode</c>  — the
///      top-level grouping node, shared across multiple worldspaces.  The pass finds an
///      existing record in the target mod by EditorID, or creates one if absent.
///
///   2. A <see cref="PlanetContentManagerBranchNode"/> of type <c>ContentNode</c> — a
///      grouping node (conditions live here) shared across worldspaces. Found by EditorID
///      or created if absent, parented to (1).
///
///   3. A <see cref="PlanetContentManagerContentNode"/> — the leaf record that links
///      the worldspace itself, always created fresh, parented to (2).
///
/// Equivalent to the records in du_takeover.esm:
///   du_takeover_blockbranch → du_takeover_blockcontent → WorldspaceContent_PCM_BlockCreationRequest097
/// </summary>
public class PlanetContentManagerPass(
    string branchNodeEditorId,
    string contentBranchEditorId,
    string contentNodeEditorId) : IWorldspacePass
{
    // PCM_BlockCreation_PrimaryContent [PCBN:00225373] — the Starfield.esm root node
    // that all mod-added block branch nodes must parent to in order to hook into
    // the planet generation system.
    private static readonly uint PrimaryContentFormId = 0x00225373;

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;

        // Step 1: Find or create the shared Branch Node (type = BranchNode).
        // Multiple worldspaces in the same mod run may share this node.
        var branchNode = targetMod.PlanetContentManagerBranchNodes
            .FirstOrDefault(n => n.EditorID == branchNodeEditorId);

        if (branchNode == null)
        {
            branchNode = new PlanetContentManagerBranchNode(targetMod)
            {
                EditorID = branchNodeEditorId,
                NodeType = PlanetContentManagerBranchNode.NodeTypeOption.BranchNode,
            };
            // Parent to PCM_BlockCreation_PrimaryContent in Starfield.esm — the root
            // hook for all planet content block branches.
            branchNode.ParentNode = new FormKey(starfieldEsm, PrimaryContentFormId)
                .ToNullableLink<IPlanetParentNodeGetter>();
            targetMod.PlanetContentManagerBranchNodes.Add(branchNode);
            Console.WriteLine($"[PCM] Created Branch Node: {branchNodeEditorId}");
        }
        else
        {
            Console.WriteLine($"[PCM] Found existing Branch Node: {branchNodeEditorId}");
        }

        // Step 2: Find or create the Content Branch Node (type = ContentNode).
        // This is a shared grouping node; conditions are set on it externally.
        var contentBranch = targetMod.PlanetContentManagerBranchNodes
            .FirstOrDefault(n => n.EditorID == contentBranchEditorId);

        if (contentBranch == null)
        {
            contentBranch = new PlanetContentManagerBranchNode(targetMod)
            {
                EditorID = contentBranchEditorId,
                NodeType = PlanetContentManagerBranchNode.NodeTypeOption.ContentNode,
            };
            // Set FormLink properties after construction per Mutagen nullable FormLink rules.
            contentBranch.ParentNode = branchNode.FormKey.ToNullableLink<IPlanetParentNodeGetter>();
            targetMod.PlanetContentManagerBranchNodes.Add(contentBranch);

            // Wire content branch as a child of the branch node.
            branchNode.Nodes.Add(contentBranch.FormKey.ToLink<IPlanetNodeGetter>());
            Console.WriteLine($"[PCM] Created Content Branch: {contentBranchEditorId}");
        }
        else
        {
            Console.WriteLine($"[PCM] Found existing Content Branch: {contentBranchEditorId}");
        }

        // Step 3: Create the Content Node that links to the worldspace.
        var contentNode = new PlanetContentManagerContentNode(targetMod)
        {
            EditorID = contentNodeEditorId,
        };
        // Worldspace implements IPlanetContentTargetGetter.
        contentNode.Content = state.Worldspace.FormKey.ToNullableLink<IPlanetContentTargetGetter>();
        contentNode.ParentNode = contentBranch.FormKey.ToNullableLink<IPlanetContentManagerBranchNodeGetter>();
        targetMod.PlanetContentManagerContentNodes.Add(contentNode);

        // Wire content node as a child of the content branch.
        contentBranch.Nodes.Add(contentNode.FormKey.ToLink<IPlanetNodeGetter>());

        Console.WriteLine($"[PCM] Tree: {branchNodeEditorId} → {contentBranchEditorId} → {contentNodeEditorId}");
    }
}
