using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Lore;
using Retrograde.Nouns;
using Retrograde.Utils;

namespace Retrograde.Lore;

/// <summary>
/// Creates a named Mutagen NPC record from a NpcLore definition and registers it
/// in LoreRegistry so quest templates can resolve it by lore id.
///
/// Follows the same pattern as OutlawNpc.GenerateNPC() but with lore-specified
/// name, gender, and voice type instead of randomised values.
/// </summary>
public class NpcLoreNoun
{
    /// <summary>Ready-to-use OutlawNpc with the lore NPC's record as .instance.</summary>
    public OutlawNpc Result { get; }

    public NpcLoreNoun(NpcLore lore)
    {
        bool female = lore.Gender.Equals("female", StringComparison.OrdinalIgnoreCase);
        var targetMod = RetrogradeContext.Current.TargetMod;

        // Build OutlawNpc shell — constructor randomises gender/name/voice, all overwritten below
        var outlawNpc = new OutlawNpc(targetMod, false);
        outlawNpc.female = female;
        outlawNpc.gender = female ? "female" : "male";
        outlawNpc.name   = lore.Name;
        outlawNpc.ElevenLabsVoiceId   = lore.ElevenLabsVoiceId ?? "";
        outlawNpc.ElevenLabsVoiceName = lore.VoiceType;  // human-readable stand-in

        // Clone NPC from template
        var templateNpc = NPCTools.FindTemplateNpc(female);
        var npc = NPCTools.CloneNPC(targetMod, templateNpc);
        npc.Name     = lore.Name;
        npc.EditorID = "npc_lore_" + lore.Id;

        // Voice type by EditorID — lore specifies this explicitly (e.g. "MaleOutlaw")
        var sfMod = RetrogradeContext.Current.StarfieldMod;
        var vt = sfMod.VoiceTypes.FirstOrDefault(v => v.EditorID == lore.VoiceType);
        if (vt != null)
            npc.Voice.SetTo(vt.FormKey);
        outlawNpc.VoiceEditorId = lore.VoiceType;

        // Randomise appearance (same as OutlawNpc.GenerateNPC())
        var rng = RandomProvider.Random;
        foreach (var facemorph in npc.FaceMorphs)
            foreach (var inner in facemorph.MorphGroups)
                inner.BlendIntensity = (float)rng.NextDouble();

        npc.Weight = new NpcWeight
        {
            Fat      = (float)rng.NextDouble(),
            Muscular = (float)rng.NextDouble(),
            Thin     = (float)rng.NextDouble(),
        };
        npc.SpaceOutfit = NPCTools.GetRandomOutfit(false);
        npc.EyeColor    = NPCTools.GetEyeColour();
        npc.HairColor   = NPCTools.GetHairColour();
        npc.SkinToneIndex = (byte)rng.Next(8);
        npc.HeadParts.Add(NPCTools.GetHaircut(female));
        npc.Level = new PcLevelMult { LevelMult = 0.25f + (float)rng.NextDouble() };
        npc.Items = new ExtendedList<ContainerEntry>
        {
            new() { Item = new ContainerItem { Item = NPCTools.GetRandomGear(), Count = 1 } },
        };

        // Death-items FormList (same pattern as OutlawNpc)
        var frmlst = new FormList(targetMod)
        {
            EditorID = npc.EditorID + "_deathitems",
            Items    = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
        };
        targetMod.FormLists.Add(frmlst);
        outlawNpc.deathItems = frmlst.FormKey;

        targetMod.Npcs.Add(npc);
        outlawNpc.instance = npc;

        LoreRegistry.RegisterNpc(lore.Id, npc.FormKey);
        Console.WriteLine($"Lore NPC created: {lore.Name} ({lore.Id})");

        Result = outlawNpc;
    }
}
