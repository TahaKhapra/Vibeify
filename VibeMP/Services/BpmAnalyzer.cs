using NAudio.Wave;

namespace VibeMP.Services.BpmAnalysis
{
    public class BpmAnalyzer
    {
        public float Analyze(string filePath)
        {
            using (var reader = new AudioFileReader(filePath))
            {
                var sampleRate = reader.WaveFormat.SampleRate;
                var channels = reader.WaveFormat.Channels;

                float[] sampleResults = new float[3];
                long[] scanPositions = {reader.Length / 4, reader.Length / 2, (reader.Length / 4) * 3};

                for (int i = 0; i < scanPositions.Length; i++)
                {
                    var bpmDetect = new SoundTouch.BpmDetect(channels, sampleRate);
                    reader.Position = scanPositions[i];

                    long bytesToRead = (long)(15 * reader.WaveFormat.AverageBytesPerSecond);
                    long bytesRead = 0;
                    float[] buffer = new float[4096 * channels];
                    int read;

                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0 && bytesRead < bytesToRead)
                    {
                        bpmDetect.InputSamples(buffer, read / channels);
                        bytesRead += (read * sizeof(float));
                    }

                    sampleResults[i] = bpmDetect.GetBpm();
                }

                var validResults = sampleResults.Where(b => b > 0).ToList();

                if (!validResults.Any()) return 0;
                float detectedBpm = validResults.Average();

                System.Diagnostics.Debug.WriteLine($"Multi-point Analysis for {filePath}: {detectedBpm} BPM");
                return detectedBpm;
            }
        }
    }
}