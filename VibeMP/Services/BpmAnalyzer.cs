using NAudio.Wave;

namespace VibeMP.Services.BpmAnalysis
{
    public class BpmAnalyzer
    {
        public float Analyze(string filePath, bool isRetry = false)
        {
            using (var reader = new AudioFileReader(filePath))
            {
                var sampleRate = reader.WaveFormat.SampleRate;
                var channels = reader.WaveFormat.Channels;
                var bpmVotes = new List<float>();

                var lowPassDetect = new SoundTouch.BpmDetect(channels, sampleRate);
                var highPassDetect = new SoundTouch.BpmDetect(channels, sampleRate);

                reader.Position = reader.Length / 5;
                int totalSamples = sampleRate * channels * 45;
                int samplesRead = 0;

                float[] buffer = new float[8192 * channels];
                float[] highBuffer = new float[8192 * channels];

                float lpFiltered = 0, hpFiltered = 0;
                float lpPrev = 0, hpPrev = 0;
                float lpAlpha = 0.15f;
                float hpAlpha = 0.65f;

                while (samplesRead < totalSamples)
                {
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    for (int j = 0; j < read; j++)
                    {
                        float rawSample = buffer[j];

                        // Pass 1: Low End (Bass/Kick)
                        lpFiltered = lpFiltered + lpAlpha * (rawSample - lpFiltered);
                        float lpFlux = Math.Max(0, Math.Abs(lpFiltered) - lpPrev);
                        buffer[j] = Math.Clamp(lpFlux * 30f, 0, 1.0f);
                        lpPrev = Math.Abs(lpFiltered);

                        // Pass 2: High End (Snare/Hats/Jazz Piano)
                        hpFiltered = hpFiltered + hpAlpha * (rawSample - hpFiltered);
                        float hpFlux = Math.Max(0, Math.Abs(hpFiltered) - hpPrev);
                        highBuffer[j] = Math.Clamp(hpFlux * 40f, 0, 1.0f);
                        hpPrev = Math.Abs(hpFiltered);
                    }

                    lowPassDetect.InputSamples(buffer, read / channels);
                    highPassDetect.InputSamples(highBuffer, read / channels);
                    samplesRead += read;
                }

                float lpBpm = lowPassDetect.GetBpm();
                float hpBpm = highPassDetect.GetBpm();

                System.Diagnostics.Debug.WriteLine($"Low-Pass: {lpBpm} | High-Pass: {hpBpm}");

                var votes = new List<float>();
                if (lpBpm > 0) votes.Add(lpBpm);
                if (hpBpm > 0) votes.Add(hpBpm);

                return FinalizeBpm(votes);
            }
        }

        private float FinalizeBpm(List<float> votes)
        {
            if (!votes.Any()) return 0;

            var grouped = votes.GroupBy(v => Math.Round(v / 3.0) * 3.0)
                               .OrderByDescending(g => g.Count())
                               .First();

            float consensus = grouped.Average();

            while (consensus < 85)
            {
                consensus *= 2;
            }

            while (consensus > 155)
            {
                consensus /= 2;
            }

            return (float)Math.Round(consensus, 2);
        }
    }
}