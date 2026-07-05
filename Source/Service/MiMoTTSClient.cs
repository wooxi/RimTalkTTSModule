using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Service
{
    public static class MiMoTTSClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string BaseUrl = "https://api.xiaomimimo.com/v1/chat/completions";

        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                string apiKey = request.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    TTSLog.Error("[RimTalk.TTS] MiMo TTS: API key is not set.");
                    return null;
                }

                string model = request.Model ?? "mimo-v2.5-tts";
                string voice = request.Voice ?? "mimo_default";
                string text = request.Input ?? "";

                string jsonBody = BuildRequestBody(model, text, voice);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
                httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                httpRequest.Headers.TryAddWithoutValidation("api-key", apiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    TTSLog.Error($"[RimTalk.TTS] MiMo TTS: HTTP {(int)response.StatusCode} - {errorBody}");
                    return null;
                }

                string responseText = await response.Content.ReadAsStringAsync();

                byte[] audioData = ExtractAudioData(responseText);
                if (audioData == null)
                {
                    TTSLog.Error("[RimTalk.TTS] MiMo TTS: No audio data in response.");
                    return null;
                }

                TTSLog.Message($"[RimTalk.TTS] MiMo TTS: Received {audioData.Length} bytes");
                return audioData;
            }
            catch (OperationCanceledException)
            {
                TTSLog.Warning("[RimTalk.TTS] MiMo TTS: Request cancelled or timed out");
                throw;
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.Append($"{ex.GetType().Name}: {ex.Message}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.Append($" --> {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }
                TTSLog.Error($"[RimTalk.TTS] MiMo TTS: {sb}");
                return null;
            }
        }

        private static string BuildRequestBody(string model, string text, string voice)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append($"\"model\":{JsonEscape(model)}");
            sb.Append(",\"messages\":[");
            sb.Append("{\"role\":\"user\",\"content\":\"\"},");
            sb.Append("{\"role\":\"assistant\",\"content\":");
            sb.Append(JsonEscape(text));
            sb.Append('}');
            sb.Append("],\"audio\":{");
            sb.Append("\"format\":\"wav\",");
            sb.Append("\"voice\":");
            sb.Append(JsonEscape(voice));
            sb.Append("}}");
            return sb.ToString();
        }

        private static byte[] ExtractAudioData(string responseText)
        {
            try
            {
                string searchKey = "\"audio\":{";
                int audioIdx = responseText.IndexOf(searchKey);
                if (audioIdx < 0) return null;

                string dataKey = "\"data\":\"";
                int dataIdx = responseText.IndexOf(dataKey, audioIdx);
                if (dataIdx < 0) return null;

                dataIdx += dataKey.Length;
                int endIdx = responseText.IndexOf('"', dataIdx);
                if (endIdx < 0) return null;

                string base64Data = responseText.Substring(dataIdx, endIdx - dataIdx);

                return Convert.FromBase64String(base64Data);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] MiMo TTS: Failed to parse response - {ex.Message}");
                return null;
            }
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return "\"\"";
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
