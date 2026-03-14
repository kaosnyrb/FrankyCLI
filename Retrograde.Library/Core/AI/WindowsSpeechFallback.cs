using System.Speech.AudioFormat;
using System.Speech.Synthesis;

namespace Retrograde.AI;

/// <summary>
/// Fallback TTS using Windows SAPI when ElevenLabs is unavailable.
/// Quality is robotic but functional for placeholder audio.
/// </summary>
public static class WindowsSpeechFallback
{
    /// <summary>
    /// Synthesises <paramref name="text"/> to a WAV file at <paramref name="outputPath"/>
    /// using the default installed Windows voice.
    /// Output: 44100 Hz, 16-bit, mono — matches ElevenLabs WAV format.
    /// </summary>
    public static void GenerateWav(string text, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var synth = new SpeechSynthesizer();
        var fmt = new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
        synth.SetOutputToWaveFile(outputPath, fmt);
        synth.Speak(text);
        synth.SetOutputToDefaultAudioDevice(); // release file handle
    }
}
