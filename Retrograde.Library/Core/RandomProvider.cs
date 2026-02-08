using System;

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
}
