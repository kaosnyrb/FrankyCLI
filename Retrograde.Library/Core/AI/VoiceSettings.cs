namespace Retrograde.Utils;

/// <summary>
/// ElevenLabs voice_settings parameters. Passed through SpeechTools into the API request
/// to match audio delivery to the NPC's personality.
/// </summary>
public record VoiceSettings(
    float Stability,
    float SimilarityBoost,
    float Style,
    float Speed)
{
    // ── Presets ───────────────────────────────────────────────────────────────

    /// <summary>Balanced default for undefined personalities.</summary>
    public static readonly VoiceSettings Default      = new(0.60f, 0.75f, 0.20f, 1.00f);

    /// <summary>Paranoid, evasive, or guarded characters — measured pace, low expressiveness.</summary>
    public static readonly VoiceSettings Cautious     = new(0.75f, 0.75f, 0.10f, 0.90f);

    /// <summary>Blunt, terse, or minimal characters — very consistent, near-flat delivery.</summary>
    public static readonly VoiceSettings Minimal      = new(0.85f, 0.75f, 0.05f, 0.93f);

    /// <summary>Chatty, charming, or entrepreneurial characters — varied, faster, expressive.</summary>
    public static readonly VoiceSettings Casual       = new(0.45f, 0.75f, 0.40f, 1.08f);

    /// <summary>Polished, formal, clinical, or calculating characters — steady and controlled.</summary>
    public static readonly VoiceSettings Professional = new(0.80f, 0.75f, 0.10f, 0.95f);

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Derives a voice preset from an NPC background string by scanning for personality keywords.
    /// Falls back to <see cref="Default"/> when no keywords match.
    /// </summary>
    public static VoiceSettings FromBackground(string background)
    {
        if (string.IsNullOrWhiteSpace(background))
            return Default;

        var b = background.ToLowerInvariant();

        if (b.Contains("paranoid") || b.Contains("cagey") || b.Contains("cautious") ||
            b.Contains("wary")     || b.Contains("evasive") || b.Contains("suspicious") ||
            b.Contains("transactional"))
            return Cautious;

        if (b.Contains("blunt")   || b.Contains("minimal") || b.Contains("terse") ||
            b.Contains("economical") || b.Contains("fewer words") || b.Contains("fewest words"))
            return Minimal;

        if (b.Contains("chatty")  || b.Contains("charming") || b.Contains("entrepreneurial") ||
            b.Contains("observant") || b.Contains("keeps you talking"))
            return Casual;

        if (b.Contains("polished") || b.Contains("formal")     || b.Contains("precise")   ||
            b.Contains("bureaucratic") || b.Contains("calculating") || b.Contains("clinical") ||
            b.Contains("military") || b.Contains("measured"))
            return Professional;

        return Default;
    }
}
