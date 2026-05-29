using System.Windows;

namespace VibeMP.Views
{
    public partial class MainWindow : Window
    {
        private Audio.NAudioEngine _engine;
        public MainWindow()
        {
            InitializeComponent();

            _engine = new Audio.NAudioEngine();

            _engine.BpmDetected += (s, bpm) =>
            {
                Dispatcher.Invoke(() => {
                    System.Diagnostics.Debug.WriteLine($"!!! BPM DETECTED: {bpm} !!!");

                    Dispatcher.Invoke(() => {
                        MessageBox.Show($"Analysis Finished: {bpm} BPM");
                    });
                });
            };

            // gt = 78, detected = 73
            string testPath1 = @"C:\Users\tkhapra\Music\Alfredo 2 - Freddie Gibbs\01 - Freddie Gibbs - 1995.flac";

            // gt = 144, detected = 144
            string testPath2 = @"C:\Users\tkhapra\Music\Alfredo 2 - Freddie Gibbs\03 - Freddie Gibbs - Lemon Pepper Steppers.flac";

            // gt = 140, detected = 141
            string testPath3 = @"C:\Users\tkhapra\Music\DBR Deluxe\04-drugs-you-should-try-it.flac";

            // gt = 116, detected = 116
            string testPath4 = @"C:\Users\tkhapra\Music\RAYE - WHERE IS MY HUSBAND! (Official Music Video).mp3";

            // gt = 181, detected = 179
            string testPath5 = @"C:\Users\tkhapra\Music\Nujabes - Feather (feat. Cise Starr & Akin from CYNE) [Official Audio].mp3";
            
            // gt = 134, detected = 138
            string testPath6 = @"C:\Users\tkhapra\Music\Pink Floyd - Echoes (Official Audio).mp3";
            
            // gt = 150, detected = 153
            string testPath7 = @"C:\Users\tkhapra\Music\forrest nolan - thank you i guess (official music video).mp3";

            // gt = 140, detected = 93
            string testPath8 = @"C:\Users\tkhapra\Music\Olivia Dean - So Easy (To Fall In Love).mp3";

            // gt = 58, detected = 126
            string testPath9 = @"C:\Users\tkhapra\Music\Mac Miller - Congratulations (feat. Bilal).mp3";

            string test = testPath3;

            if (System.IO.File.Exists(test))
            {
                _engine.Load(test);
                _engine.Play();
            }
        }
    }
}