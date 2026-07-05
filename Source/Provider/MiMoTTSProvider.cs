using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;

namespace RimTalk.TTS.Provider
{
    public class MiMoTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                return await MiMoTTSClient.GenerateSpeechAsync(request, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return null;
            }
            catch (System.Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] MiMoTTSProvider: {ex.Message}");
                return null;
            }
        }

        public void Shutdown()
        {
        }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrEmpty(apiKey);
        }
    }
}
