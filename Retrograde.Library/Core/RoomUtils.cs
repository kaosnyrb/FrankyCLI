using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde;

public class RoomUtils
{
    public IFormListGetter? roomlist;
    public Dictionary<string, IFormListGetter> roomTemplates;
    private readonly Dictionary<string, Dictionary<string, List<string>>> cachedCandidates = new(StringComparer.OrdinalIgnoreCase);
    public string listName;

    public RoomUtils(string listname)
    {
        var templateMods = RetrogradeContext.Current.TemplateMods;
        roomlist = templateMods
            .SelectMany(m => m.FormLists)
            .FirstOrDefault(fl => fl.EditorID == listname);

        roomTemplates = new Dictionary<string, IFormListGetter>();
        if (roomlist != null)
        {
            foreach (var f in roomlist.Items)
            {
                var list = templateMods
                    .SelectMany(m => m.FormLists)
                    .FirstOrDefault(fl => fl.FormKey == f.FormKey);
                if (list != null)
                {
                    roomTemplates.Add(list.EditorID!, list);
                }
            }
        }
        listName = listname;
        PrebuildCandidateCache();
    }

    public string GetRoom(string theme, string? type = null)
    {
        if (theme.Contains("DUPLICATE"))
        {
            theme = theme.Substring(0,theme.IndexOf("DUPLICATE"));
        }
        var listKey = listName + "_" + theme;
        var typeKey = type ?? string.Empty;


        if (!roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
            throw new Exception($"Room theme list not found or empty: {listKey}");

        if (!cachedCandidates.TryGetValue(listKey, out var typeMap))
        {
            typeMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            cachedCandidates[listKey] = typeMap;
        }

        if (!typeMap.TryGetValue(typeKey, out var candidates))
        {
            candidates = BuildCandidates(listKey, formList, type);
            typeMap[typeKey] = candidates;
        }

        if (candidates.Count == 0)
            throw new Exception($"No PackIns resolved for list: {listKey}");

        // Prefer rooms over blockers
        var rooms = candidates
            .Where(id => id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();

        if (rooms.Count > 0)
            return GlobalRoomTracker.IsLoaded
                ? GlobalRoomTracker.ChooseWeighted(rooms)
                : rooms[RandomProvider.Random.Next(rooms.Count)];

        return GlobalRoomTracker.IsLoaded
            ? GlobalRoomTracker.ChooseWeighted(candidates)
            : candidates[RandomProvider.Random.Next(candidates.Count)];
    }

    private void EnsureConnectorsWithinBounds(string listKey, IPackInGetter packIn)
    {
        var prefab = PrefabCache.GetPrefab(packIn.EditorID!);
        var bounds = packIn.ObjectBounds;

        var minX = Math.Min(bounds.First.X, bounds.Second.X);
        var minY = Math.Min(bounds.First.Y, bounds.Second.Y);
        var minZ = Math.Min(bounds.First.Z, bounds.Second.Z);
        var maxX = Math.Max(bounds.First.X, bounds.Second.X);
        var maxY = Math.Max(bounds.First.Y, bounds.Second.Y);
        var maxZ = Math.Max(bounds.First.Z, bounds.Second.Z);

        const float edgeTolerance = 0.01f;

        foreach (var marker in prefab.Markers)
        {
            if (string.IsNullOrWhiteSpace(marker.MarkerEditorId) ||
                !marker.MarkerEditorId.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                continue;

            var pos = marker.Position;

            if (pos.X < minX - edgeTolerance || pos.X > maxX + edgeTolerance ||
                pos.Y < minY - edgeTolerance || pos.Y > maxY + edgeTolerance ||
                pos.Z < minZ - edgeTolerance || pos.Z > maxZ + edgeTolerance)
            {
                // Connector outside bounds - could log warning here
            }
        }
    }

    private void PrebuildCandidateCache()
    {
        foreach (var kvp in roomTemplates)
        {
            var listKey = kvp.Key;
            var formList = kvp.Value;
            var typeMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            cachedCandidates[listKey] = typeMap;

            var typeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };

            foreach (var item in formList.Items)
            {
                var packIn = FindPackIn(item.FormKey);
                if (packIn == null || string.IsNullOrWhiteSpace(packIn.EditorID))
                    continue;

                var tokens = packIn.EditorID.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    typeKeys.Add(token);
                    var wrapped = $"_{token}_";
                    if (packIn.EditorID.Contains(wrapped, StringComparison.OrdinalIgnoreCase))
                    {
                        typeKeys.Add(wrapped);
                    }
                }
            }

            foreach (var typeKey in typeKeys)
            {
                typeMap[typeKey] = BuildCandidates(listKey, formList, typeKey.Length == 0 ? null : typeKey);
            }
        }
    }

    private List<string> BuildCandidates(string listKey, IFormListGetter formList, string? type)
    {
        var candidates = new List<string>(formList.Items.Count);

        foreach (var item in formList.Items)
        {
            var packIn = FindPackIn(item.FormKey);
            if (packIn?.EditorID == null)
                continue;

            if (type != null)
            {
                if (!packIn.EditorID.Contains(type))
                    continue;
            }

            EnsureConnectorsWithinBounds(listKey, packIn);

            candidates.Add(packIn.EditorID);
        }

        return candidates;
    }

    private static IPackInGetter? FindPackIn(Mutagen.Bethesda.Plugins.FormKey formKey)
    {
        foreach (var mod in RetrogradeContext.Current.TemplateMods)
            if (mod.PackIns.TryGetValue(formKey, out var packIn))
                return packIn;
        return null;
    }
}
