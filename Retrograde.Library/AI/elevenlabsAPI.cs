using NAudio.Wave;
using RestSharp;
using Retrograde.AI.Utils;
using System.Text.Json.Serialization;

namespace Retrograde.AI
{
    public static class ElevenLabsAPI
    {
        private static readonly string _apiKey;
        private static readonly RestClient _client;

        public const string DefaultModel = "eleven_v3";
        public const string DefaultOutputFormat = "mp3_44100_128";

        static ElevenLabsAPI()
        {
            _apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY")
                      ?? throw new InvalidOperationException("ELEVENLABS_API_KEY environment variable is not set.");

            _client = new RestClient("https://api.elevenlabs.io");
        }

        /// <summary>
        /// Calls ElevenLabs TTS, downloads the MP3, converts it to WAV via NAudio,
        /// and writes the result to <paramref name="outputWavPath"/>.
        /// </summary>
        public static async Task GenerateSpeechAsync(
            string text,
            string voiceId,
            string outputWavPath,
            string modelId = DefaultModel)
        {
            var request = new RestRequest($"/v1/text-to-speech/{voiceId}", Method.Post);
            request.AddQueryParameter("output_format", DefaultOutputFormat);
            request.AddHeader("xi-api-key", _apiKey);
            request.AddJsonBody(new
            {
                text,
                model_id = modelId
            });

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.RawBytes is null || response.RawBytes.Length == 0)
                throw new InvalidOperationException(
                    $"ElevenLabs API error {(int)response.StatusCode}: {response.Content}");

            // Write MP3 to a temp file, then convert to WAV.
            var tempMp3 = Path.ChangeExtension(Path.GetTempFileName(), ".mp3");
            try
            {
                await File.WriteAllBytesAsync(tempMp3, response.RawBytes);

                var dir = Path.GetDirectoryName(outputWavPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using var reader = new Mp3FileReader(tempMp3);
                WaveFileWriter.CreateWaveFile(outputWavPath, reader);
            }
            finally
            {
                if (File.Exists(tempMp3))
                    File.Delete(tempMp3);
            }
        }

        /// <summary>
        /// Synchronous wrapper around <see cref="GenerateSpeechAsync"/>.
        /// </summary>
        public static void GenerateSpeech(
            string text,
            string voiceId,
            string outputWavPath,
            string modelId = DefaultModel)
            => GenerateSpeechAsync(text, voiceId, outputWavPath, modelId).GetAwaiter().GetResult();

        // ── Voice listing ────────────────────────────────────────────────────

        private record VoiceDto(
            [property: JsonPropertyName("voice_id")] string VoiceId,
            [property: JsonPropertyName("name")]     string Name);

        private record VoiceListResponse(
            [property: JsonPropertyName("voices")]           List<VoiceDto> Voices,
            [property: JsonPropertyName("has_more")]         bool HasMore,
            [property: JsonPropertyName("next_page_token")] string? NextPageToken);

        /// <summary>
        /// Returns all voices of the given type from your ElevenLabs account,
        /// paging automatically. Default is "personal" (your own voices).
        /// Pass "default" for ElevenLabs stock voices, or omit for all.
        /// </summary>
        public static async Task<List<ElevenLabsVoice>> ListVoicesAsync(string voiceType = "personal")
        {
            var all = new List<ElevenLabsVoice>();
            string? nextToken = null;

            do
            {
                var req = new RestRequest("/v2/voices", Method.Get);
                req.AddHeader("xi-api-key", _apiKey);
                req.AddQueryParameter("voice_type", voiceType);
                req.AddQueryParameter("page_size", "100");
                if (nextToken != null)
                    req.AddQueryParameter("next_page_token", nextToken);

                var resp = await _client.ExecuteAsync<VoiceListResponse>(req);
                if (!resp.IsSuccessful || resp.Data is null)
                    throw new InvalidOperationException(
                        $"ElevenLabs voices error {(int)resp.StatusCode}: {resp.Content}");

                foreach (var v in resp.Data.Voices)
                    all.Add(new ElevenLabsVoice(v.Name, v.VoiceId));

                nextToken = resp.Data.HasMore ? resp.Data.NextPageToken : null;
            }
            while (nextToken != null);

            return all;
        }

        /// <summary>Synchronous wrapper around <see cref="ListVoicesAsync"/>.</summary>
        public static List<ElevenLabsVoice> ListVoices(string voiceType = "personal")
            => ListVoicesAsync(voiceType).GetAwaiter().GetResult();

        /// <summary>
        /// Prints ready-to-paste SeedManager entries for all voices of the given type.
        /// Example output:  new("Rachel", "21m00Tcm4TlvDq8ikWAM"),
        /// </summary>
        public static void PrintVoices(string voiceType = "personal")
        {
            foreach (var v in ListVoices(voiceType))
                Console.WriteLine($"  new(\"{v.Name}\", \"{v.Id}\"),");
        }
    }
}
