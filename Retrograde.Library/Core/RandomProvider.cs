using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;

namespace Retrograde;

/// <summary>
/// Provides a shared Random instance for the Retrograde library.
/// Can be replaced by the host application if needed.
/// </summary>
public static class RandomProvider
{
    private static Random _random = new Random();

    /// <summary>
    /// The shared Random instance used throughout the library.
    /// </summary>
    public static Random Random
    {
        get => _random;
        set => _random = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Resets to a new Random instance with a random seed.
    /// </summary>
    public static void Reset()
    {
        _random = new Random();
    }

    /// <summary>
    /// Resets to a new Random instance with the specified seed.
    /// </summary>
    public static void Reset(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Gets a random marker record matching the specified name, searching TargetMod then TemplateMods.
    /// </summary>
    public static IMajorRecordGetter GetRandomMarker(string name)
    {
        var ctx = RetrogradeContext.Current;
        var rec = new List<IMajorRecordGetter>();

        foreach (var record in ctx.TargetMod.EnumerateMajorRecords())
            if (record.EditorID?.Contains(name) == true)
                rec.Add(record);

        if (rec.Count == 0)
        {
            foreach (var mod in ctx.TemplateMods)
                foreach (var record in mod.EnumerateMajorRecords())
                    if (record.EditorID?.Contains(name) == true)
                        rec.Add(record);
        }

        if (rec.Count == 0)
            throw new InvalidOperationException($"GetRandomMarker: no records found matching '{name}'");

        return rec[Random.Next(rec.Count)];
    }

    /// <summary>
    /// Gets a random synonym for log/journal entries.
    /// </summary>
    public static string GetLogSynonym()
    {
        var synonyms = new List<string>
        {
            "Ship Notes", "Crew Notes", "Duty Records", "Mission Notes",
            "Mission Records", "Daily Entries", "Service Entries", "Personal Records",
            "Crew Journals", "Field Notes", "Status Reports", "Shift Reports",
            "Voyage Notes", "Travel Records", "Operations Logs", "Observation Notes",
            "Shipboard Records", "Work Entries", "Duty Journals", "Activity Reports",
            "Notes", "Ledger", "Recordings", "Memoranda", "Transcript",
            "Summary", "Documentation", "Overview", "Statement", "Recount", "Diary"
        };
        return synonyms[Random.Next(synonyms.Count)];
    }
}
