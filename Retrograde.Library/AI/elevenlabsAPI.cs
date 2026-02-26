using NAudio.Wave;
using RestSharp;

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
    }
}
