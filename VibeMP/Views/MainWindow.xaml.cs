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

            // gt = 78, detected = 78
            string testPath1 = @"Alfredo 2 - Freddie Gibbs\01 - Freddie Gibbs - 1995.flac";

            // gt = 144, detected = 144
            string testPath2 = @"Alfredo 2 - Freddie Gibbs\03 - Freddie Gibbs - Lemon Pepper Steppers.flac";

            // gt = 140, detected = 124
            string testPath3 = @"DBR Deluxe\04-drugs-you-should-try-it.flac";

            // gt = 116, detected = 78
            string testPath4 = @"RAYE - WHERE IS MY HUSBAND! (Official Music Video).mp3";

            // gt = 181, detected = 91
            string testPath5 = @"Nujabes - Feather (feat. Cise Starr & Akin from CYNE) [Official Audio].mp3";
            
            // gt = 134, detected = 139
            string testPath6 = @"Pink Floyd - Echoes (Official Audio).mp3";
            
            // gt = 150, detected = 150
            string testPath7 = @"forrest nolan - thank you i guess (official music video).mp3";

            // gt = 140, detected = 95
            string testPath8 = @"Olivia Dean - So Easy (To Fall In Love).mp3";

            // gt = 58, detected = 126
            string testPath9 = @"Mac Miller - Congratulations (feat. Bilal).mp3";

            string test = testPath9;

            if (System.IO.File.Exists(test))
            {
                _engine.Load(test);
                _engine.Play();
            }
        }
    }
}