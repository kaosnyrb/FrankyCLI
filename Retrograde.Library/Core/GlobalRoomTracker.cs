using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Retrograde;

/// <summary>
/// Tracks room prefab usage counts across multiple station generations.
/// Persists to a JSON file so counts survive between CLI invocations.
/// Used by room selection to bias towards underused prefabs.
/// </summary>
public static class GlobalRoomTracker
{
    private static Dictionary<string, int> _usageCounts = new(StringComparer.OrdinalIgnoreCase);
    private static string _filePath;
    private static int _minimumUsage = 10;

    /// <summary>
    /// Minimum number of times each room should be used across all stations.
    /// Rooms below this threshold are strongly preferred during selection.
    /// </summary>
    public static int MinimumUsage
    {
        get => _minimumUsage;
        set => _minimumUsage = Math.Max(0, value);
    }

    /// <summary>
    /// Whether the tracker has been loaded from a file.
    /// </summary>
    public static bool IsLoaded => _filePath != null;

    /// <summary>
    /// Loads usage counts from the specified JSON file. Creates empty state if file doesn't exist.
    /// </summary>
    public static void Load(string filePath)
    {
        _filePath = filePath;
        _usageCounts.Clear();

        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        _usageCounts[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GlobalRoomTracker] Warning: Could not load '{filePath}': {ex.Message}. Starting fresh.");
                _usageCounts.Clear();
            }
        }
    }

    /// <summary>
    /// Saves current usage counts to the file specified during Load().
    /// </summary>
    public static void Save()
    {
        if (_filePath == null)
            return;

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_usageCounts.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value), options);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GlobalRoomTracker] Warning: Could not save '{_filePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the global usage count for a prefab.
    /// </summary>
    public static int GetUsageCount(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId))
            return 0;
        return _usageCounts.TryGetValue(prefabId, out var count) ? count : 0;
    }

    /// <summary>
    /// Records that a prefab was placed, incrementing its usage count.
    /// </summary>
    public static void RecordUsage(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId))
            return;
        _usageCounts.TryGetValue(prefabId, out var count);
        _usageCounts[prefabId] = count + 1;
    }

    /// <summary>
    /// Chooses a prefab from the candidate list, biased towards rooms that are below MinimumUsage.
    /// Rooms below the threshold get higher weight; rooms at or above get weight 1.
    /// </summary>
    public static string ChooseWeighted(List<string> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // Compute weights: rooms below minimum get a boost
        var weights = new double[candidates.Count];
        double totalWeight = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            int usage = GetUsageCount(candidates[i]);
            // Rooms below minimum get weight proportional to how far below they are
            // e.g., if minimum=10 and usage=0, weight=11; if usage=9, weight=2; if usage>=10, weight=1
            weights[i] = usage < _minimumUsage ? (_minimumUsage - usage + 1) : 1;
            totalWeight += weights[i];
        }

        // Weighted random selection
        double roll = RandomProvider.Random.NextDouble() * totalWeight;
        double cumulative = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return candidates[i];
        }

        // Fallback (shouldn't happen due to floating-point, but just in case)
        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// Resets all tracked usage counts.
    /// </summary>
    public static void Reset()
    {
        _usageCounts.Clear();
        _filePath = null;
    }
}
