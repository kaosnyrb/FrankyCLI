using Retrograde.AI;

namespace FrankyCLI;

/// <summary>
/// Standalone testbed for the ElevenLabs TTS API.
/// No Starfield environment or mod loading required.
///
/// Usage:
///   FrankyCLI gen_elevenlabs_test [outputPath] [voiceId] [text]
///
/// Defaults:
///   outputPath = elevenlabs_test.wav  (written next to the executable)
///   voiceId    = CwhRBWXzGAHq8TQ4Fs17  (male voice)
///   text       = "This is a test of the ElevenLabs voice generation system."
///
/// Requires: ELEVENLABS_API_KEY environment variable set.
/// </summary>
public static class gen_elevenlabs_test
{
    public static int Generate(string[] args)
    {
        string outputPath = args.Length > 1 && !string.IsNullOrEmpty(args[1])
            ? args[1]
            : "elevenlabs_test.wav";

        string voiceId = args.Length > 2 && !string.IsNullOrEmpty(args[2])
            ? args[2]
            : "CwhRBWXzGAHq8TQ4Fs17";

        string text = args.Length > 3 && !string.IsNullOrEmpty(args[3])
            ? args[3]
            : "This is a test of the ElevenLabs voice generation system.";

        Console.WriteLine("[ElevenLabs Test]");
        Console.WriteLine($"  Voice ID  : {voiceId}");
        Console.WriteLine($"  Output    : {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"  Text      : {text}");
        Console.WriteLine();

        try
        {
            ElevenLabsAPI.GenerateSpeech(text, voiceId, outputPath);
            Console.WriteLine($"[ElevenLabs Test] OK — WAV written to: {Path.GetFullPath(outputPath)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ElevenLabs Test] FAILED: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            return 1;
        }
    }
}
