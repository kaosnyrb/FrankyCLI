using Mutagen.Bethesda.Plugins;

namespace Retrograde.Lore;

/// <summary>
/// Runtime registry of lore id → FormKey, populated during generation.
/// Generation order: NPCs first → ambient dialogue → quests.
/// </summary>
public static class LoreRegistry
{
    private static readonly Dictionary<string, FormKey> _npcs = new();

    public static void RegisterNpc(string loreId, FormKey formKey) =>
        _npcs[loreId] = formKey;

    public static FormKey ResolveNpc(string loreId) =>
        _npcs.TryGetValue(loreId, out var fk)
            ? fk
            : throw new InvalidOperationException(
                $"NPC '{loreId}' not yet registered. Generate NPC records before quests.");

    public static bool TryResolveNpc(string loreId, out FormKey formKey) =>
        _npcs.TryGetValue(loreId, out formKey);

    public static void Clear() => _npcs.Clear();
}
