using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VibeMP.Services.BpmAnalysis
{
    /// <summary>
    /// Analyzes audio files to estimate their Beats Per Minute (BPM).
    /// Uses frequency splitting, onset flux detection, and multi-position sampling.
    /// </summary>
    public class BpmAnalyzer
    {
        /// <summary>
        /// Analyzes the specified audio file and returns the estimated BPM.
        /// </summary>
        /// <param name="filePath">The full disk path to the audio file to analyze.</param>
        /// <param name="isRetry">An optional flag indicating if this is a retry attempt (currently unused).</param>
        /// <returns>The estimated BPM as a float rounded to two decimal places, or 0 if analysis fails.</returns>
        public float Analyze(string filePath, bool isRetry = false)
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    double totalSeconds = reader.TotalTime.TotalSeconds;

                    // Open a single vote list to pool results from different parts of the song
                    var votes = new List<float>();

                    // Vote 1: Analyze 45 seconds of audio starting at 20% into the song
                    CollectVotes(filePath, totalSeconds, 0.20, votes);

                    // Vote 2: Analyze 45 seconds of audio starting at 50% into the song (midpoint)
                    CollectVotes(filePath, totalSeconds, 0.50, votes);

                    // Aggregate the votes and calculate the final BPM consensus
                    return FinalizeBpm(votes);
                }
            }
            catch (Exception ex)
            {
                // Log the exception to the debug console and fail gracefully
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Collects BPM estimates from a specific section of the audio file and adds them to the votes list.
        /// </summary>
        /// <param name="filePath">The full disk path to the audio file.</param>
        /// <param name="totalSeconds">The total duration of the track in seconds.</param>
        /// <param name="skipFraction">The percentage of the track to skip before analyzing (e.g., 0.20 for 20%).</param>
        /// <param name="votes">The mutable list where detected BPM values will be stored.</param>
        private void CollectVotes(string filePath, double totalSeconds, double skipFraction, List<float> votes)
        {
            using (var reader = new AudioFileReader(filePath))
            {
                // Converts stereo to mono
                var monoProvider = new StereoToMonoSampleProvider(reader);
                int sampleRate = monoProvider.WaveFormat.SampleRate;

                // Detectors for low-frequency (bass) and high-frequency (snare/hi-hat) transients
                var lowPassDetect = new SoundTouch.BpmDetect(1, sampleRate);
                var highPassDetect = new SoundTouch.BpmDetect(1, sampleRate);

                // Manually consume samples to fast-forward to the target skip fraction
                int skipSamples = (int)(sampleRate * (totalSeconds * skipFraction));
                float[] skipBuffer = new float[8192];
                int skipped = 0;
                while (skipped < skipSamples)
                {
                    int toRead = Math.Min(skipBuffer.Length, skipSamples - skipped);
                    int read = monoProvider.Read(skipBuffer, 0, toRead);
                    if (read <= 0) break;
                    skipped += read;
                }

                int totalSamples = sampleRate * 45;
                int samplesRead = 0;
                float[] buffer = new float[8192];
                float[] lowBuffer = new float[8192];
                float[] highBuffer = new float[8192];

                float lpFiltered = 0;
                float lpPrev = 0, hpPrev = 0;
                float lpAlpha = 0.15f;

                while (samplesRead < totalSamples)
                {
                    int read = monoProvider.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    for (int j = 0; j < read; j++)
                    {
                        float raw = buffer[j];

                        lpFiltered = lpFiltered + lpAlpha * (raw - lpFiltered);

                        float lpFlux = Math.Max(0, Math.Abs(lpFiltered) - lpPrev);
                        lowBuffer[j] = Math.Clamp(lpFlux * 30f, 0, 1.0f); // Amplify and clamp for SoundTouch
                        lpPrev = Math.Abs(lpFiltered);

                        float hpSignal = raw - lpFiltered;

                        float hpFlux = Math.Max(0, Math.Abs(hpSignal) - hpPrev);
                        highBuffer[j] = Math.Clamp(hpFlux * 40f, 0, 1.0f); // Amplify and clamp for SoundTouch
                        hpPrev = Math.Abs(hpSignal);
                    }

                    lowPassDetect.InputSamples(lowBuffer, read);
                    highPassDetect.InputSamples(highBuffer, read);
                    samplesRead += read;
                }

                float lpBpm = lowPassDetect.GetBpm();
                float hpBpm = highPassDetect.GetBpm();


                if (lpBpm > 0 && hpBpm > 0)
                {
                    if (Math.Abs((lpBpm * 2) - hpBpm) <= 6) lpBpm *= 2;
                    else if (Math.Abs((hpBpm * 2) - lpBpm) <= 6) hpBpm *= 2;
                    else if (Math.Abs((lpBpm * 1.5f) - hpBpm) <= 6) lpBpm *= 1.5f;
                    else if (Math.Abs((hpBpm * 1.5f) - lpBpm) <= 6) hpBpm *= 1.5f;
                }

                if (lpBpm > 0) votes.Add(lpBpm);
                if (hpBpm > 0) votes.Add(hpBpm);
            }
        }

        /// <summary>
        /// Resolves all collected BPM votes into a singular clamped tempo value.
        /// </summary>
        /// <param name="votes">A list of all calculated BPM values from the analysis phases.</param>
        /// <returns>A finalized BPM float value rounded to 2 decimals.</returns>
        private float FinalizeBpm(List<float> votes)
        {
            if (!votes.Any()) return 0;

            // Group votes into 3-BPM ranges to find where the numbers cluster most densely
            var grouped = votes.GroupBy(v => Math.Round(v / 3.0) * 3.0)
                               .OrderByDescending(g => g.Count())
                               .First();

            // Average the values inside the winning cluster
            float consensus = grouped.Average();

            // If the consensus tempo is unrealistically slow, double it
            while (consensus > 0 && consensus < 70)
            {
                consensus *= 2;
            }

            // If the consensus tempo is unrealistically fast, halve it
            while (consensus > 190)
            {
                consensus /= 2;
            }

            return (float)Math.Round(consensus, 2);
        }
    }
}