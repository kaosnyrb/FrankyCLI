using Retrograde.Abstractions;
using Retrograde.Nouns;
using System.Collections.Generic;

namespace Retrograde;

/// <summary>
/// Static accessor for the mod context. Must be initialized before using Retrograde.
/// This enables utilities that don't receive DungeonState to access the mod context.
/// </summary>
public static class RetrogradeContext
{
    private static IModContext? _current;

    /// <summary>
    /// Gets or sets the current mod context. Throws if accessed before initialization.
    /// </summary>
    public static IModContext Current
    {
        get => _current ?? throw new InvalidOperationException(
            "RetrogradeContext.Current not initialized. Call RetrogradeContext.Initialize() first.");
        set => _current = value;
    }

    /// <summary>
    /// Returns true if the context has been initialized.
    /// </summary>
    public static bool IsInitialized => _current != null;

    /// <summary>
    /// When true, suppresses score and plan output to the console.
    /// </summary>
    public static bool Quiet { get; set; } = false;

    /// <summary>
    /// When false, AI calls are skipped for fast generation.
    /// </summary>
    public static bool AIMode { get; set; } = false;

    /// <summary>
    /// When false, WAV/WEM generation is skipped.
    /// </summary>
    public static bool GenerateWavs { get; set; } = false;

    /// <summary>
    /// All Nouns registered during this generation run, in creation order.
    /// Each Noun appends itself here on construction.
    /// </summary>
    public static List<INoun> NounRegistry { get; } = new();

    /// <summary>
    /// Initializes the Retrograde context with the provided mod context.
    /// Should be called once at startup before any Retrograde operations.
    /// </summary>
    public static void Initialize(IModContext context)
    {
        _current = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Resets the context. Call this when done generating to clean up.
    /// </summary>
    public static void Reset()
    {
        _current = null;
        NounRegistry.Clear();
        AIMode = false;
        GenerateWavs = false;
    }
}
