using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VibeMP.Services
{
    public class BpmAnalyzer
    {
        public float Analyze(
            string filePath,
            Action<int>? progressCallback = null,
            bool isRetry = false
        )
        {
            try
            {
                double totalSeconds = 0;

                using (var reader = new AudioFileReader(filePath))
                {
                    totalSeconds = reader.TotalTime.TotalSeconds;
                }

                var votes = new List<float>();

                CollectVotes(filePath, totalSeconds, 0.20, votes, progressCallback, 0);

                CollectVotes(filePath, totalSeconds, 0.50, votes, progressCallback, 50);

                progressCallback?.Invoke(100);

                return FinalizeBpm(votes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BPM CRASH] Core failure on {System.IO.Path.GetFileName(filePath)}: {ex}"
                );
                return 0;
            }
        }

        private void CollectVotes(
            string filePath,
            double totalSeconds,
            double skipFraction,
            List<float> votes,
            Action<int>? progressCallback,
            double baseProgress
        )
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    var monoProvider = new StereoToMonoSampleProvider(reader);
                    int sampleRate = monoProvider.WaveFormat.SampleRate;

                    var lowPassDetect = new SoundTouch.BpmDetect(1, sampleRate);
                    var highPassDetect = new SoundTouch.BpmDetect(1, sampleRate);

                    int skipSamples = (int)(sampleRate * (totalSeconds * skipFraction));
                    float[] skipBuffer = new float[8192];
                    int skipped = 0;
                    while (skipped < skipSamples)
                    {
                        int toRead = Math.Min(skipBuffer.Length, skipSamples - skipped);
                        int read = monoProvider.Read(skipBuffer, 0, toRead);
                        if (read <= 0)
                            break;
                        skipped += read;
                    }

                    int totalSamples = sampleRate * 45;
                    int samplesRead = 0;
                    float[] buffer = new float[8192];
                    float[] lowBuffer = new float[8192];
                    float[] highBuffer = new float[8192];

                    float lpFiltered = 0,
                        lpPrev = 0,
                        hpPrev = 0,
                        lpAlpha = 0.15f;

                    while (samplesRead < totalSamples)
                    {
                        int read = monoProvider.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                            break;

                        for (int j = 0; j < read; j++)
                        {
                            float raw = buffer[j];

                            lpFiltered = lpFiltered + lpAlpha * (raw - lpFiltered);
                            float lpFlux = Math.Max(0, Math.Abs(lpFiltered) - lpPrev);
                            lowBuffer[j] = Math.Clamp(lpFlux * 30f, 0, 1.0f);
                            lpPrev = Math.Abs(lpFiltered);

                            float hpSignal = raw - lpFiltered;
                            float hpFlux = Math.Max(0, Math.Abs(hpSignal) - hpPrev);
                            highBuffer[j] = Math.Clamp(hpFlux * 40f, 0, 1.0f);
                            hpPrev = Math.Abs(hpSignal);
                        }

                        lowPassDetect.InputSamples(lowBuffer, read);
                        highPassDetect.InputSamples(highBuffer, read);
                        samplesRead += read;

                        if (progressCallback != null)
                        {
                            double currentPassCompletion = (double)samplesRead / totalSamples;
                            int overallPercentage = Math.Clamp(
                                (int)(baseProgress + (currentPassCompletion * 50.0)),
                                0,
                                99
                            );
                            progressCallback.Invoke(overallPercentage);
                        }
                    }

                    float lpBpm = lowPassDetect.GetBpm();
                    float hpBpm = highPassDetect.GetBpm();

                    if (lpBpm > 0 && hpBpm > 0)
                    {
                        if (Math.Abs((lpBpm * 2) - hpBpm) <= 6)
                            lpBpm *= 2;
                        else if (Math.Abs((hpBpm * 2) - lpBpm) <= 6)
                            hpBpm *= 2;
                        else if (Math.Abs((lpBpm * 1.5f) - hpBpm) <= 6)
                            lpBpm *= 1.5f;
                        else if (Math.Abs((hpBpm * 1.5f) - lpBpm) <= 6)
                            hpBpm *= 1.5f;
                    }

                    if (lpBpm > 0)
                        votes.Add(lpBpm);
                    if (hpBpm > 0)
                        votes.Add(hpBpm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BPM VOTE ERROR]: {ex.Message}");
            }
        }

        private float FinalizeBpm(List<float> votes)
        {
            if (!votes.Any())
                return 0;

            var grouped = votes
                .GroupBy(v => Math.Round(v / 3.0) * 3.0)
                .OrderByDescending(g => g.Count())
                .First();

            float consensus = grouped.Average();

            while (consensus > 0 && consensus < 70)
                consensus *= 2;
            while (consensus > 190)
                consensus /= 2;

            return (float)Math.Round(consensus, 2);
        }
    }
}
