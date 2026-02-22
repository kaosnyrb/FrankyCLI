using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Registers the generated worldspace with the Planet Content Manager (PCM) quest tree.
/// These entries mark locations eligible to host quests on planets.
///
/// Structure mirrors <see cref="PlanetContentManagerPass"/> but parents the top Branch Node
/// to PCM_LocationRequest_General [PCBN:000F35E4] instead of the block-creation root.
///
///   1. A <see cref="PlanetContentManagerBranchNode"/> of type <c>BranchNode</c>  — shared
///      grouping node parented to PCM_LocationRequest_General. Found or created by EditorID.
///
///   2. A <see cref="PlanetContentManagerBranchNode"/> of type <c>ContentNode</c> — shared
///      grouping node with conditions. Found or created by EditorID, parented to (1).
///
///   3. A <see cref="PlanetContentManagerContentNode"/> — leaf record linking the worldspace,
///      always created fresh, parented to (2).
/// </summary>
public class PlanetQuestPass(
    string branchNodeEditorId,
    string contentBranchEditorId) : IWorldspacePass
{
    // PCM_LocationRequest_General [PCBN:000F35E4] — the Starfield.esm root node for quest
    // location requests (locations chosen to host quests on planets).
    private static readonly uint LocationRequestGeneralFormId = 0x000F35E4;

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;

        // Step 1: Find or create the shared Branch Node (type = BranchNode).
        var branchNode = targetMod.PlanetContentManagerBranchNodes
            .FirstOrDefault(n => n.EditorID == branchNodeEditorId);

        if (branchNode == null)
        {
            branchNode = new PlanetContentManagerBranchNode(targetMod)
            {
                EditorID = branchNodeEditorId,
                NodeType = PlanetContentManagerBranchNode.NodeTypeOption.BranchNode,
            };
            branchNode.ParentNode = new FormKey(starfieldEsm, LocationRequestGeneralFormId)
                .ToNullableLink<IPlanetParentNodeGetter>();
            targetMod.PlanetContentManagerBranchNodes.Add(branchNode);
            Console.WriteLine($"[PCMQuest] Created Branch Node: {branchNodeEditorId}");
        }
        else
        {
            Console.WriteLine($"[PCMQuest] Found existing Branch Node: {branchNodeEditorId}");
        }

        // Step 2: Find or create the Content Branch Node (type = ContentNode).
        var contentBranch = targetMod.PlanetContentManagerBranchNodes
            .FirstOrDefault(n => n.EditorID == contentBranchEditorId);

        if (contentBranch == null)
        {
            contentBranch = new PlanetContentManagerBranchNode(targetMod)
            {
                EditorID = contentBranchEditorId,
                NodeType = PlanetContentManagerBranchNode.NodeTypeOption.ContentNode,
            };
            contentBranch.ParentNode = branchNode.FormKey.ToNullableLink<IPlanetParentNodeGetter>();

            contentBranch.Components.Add(new PlanetContentManagerContentPropertiesComponent
            {
                ZNAM = 0,
                YNAM = 1,
                XNAM = 0,
                WNAM = 0,
                VNAM = 0,
                UNAM = 0,
                NAM1 = 0f,
                NAM3 = 0,
                NAM4 = new byte[] { 0x00, 0xFF, 0x00, 0x00 },
                NAM5 = 0,
                NAM6 = 0,
                NAM7 = 0,
                NAM8 = 0,
                NAM9 = 1,
            });

            targetMod.PlanetContentManagerBranchNodes.Add(contentBranch);
            Console.WriteLine($"[PCMQuest] Created Content Branch: {contentBranchEditorId}");
        }
        else
        {
            Console.WriteLine($"[PCMQuest] Found existing Content Branch: {contentBranchEditorId}");
        }

        // Step 3: Create the Content Node that links to the worldspace.
        string contentNodeEditorId = "ps_quest_" + state.Worldspace.EditorID;
        var contentNode = new PlanetContentManagerContentNode(targetMod)
        {
            EditorID = contentNodeEditorId,
        };
        contentNode.Content = state.Worldspace.FormKey.ToNullableLink<IPlanetContentTargetGetter>();
        contentNode.ParentNode = contentBranch.FormKey.ToNullableLink<IPlanetContentManagerBranchNodeGetter>();
        targetMod.PlanetContentManagerContentNodes.Add(contentNode);

        Console.WriteLine($"[PCMQuest] Tree: {branchNodeEditorId} → {contentBranchEditorId} → {contentNodeEditorId}");
    }
}
