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
                });
            };

            // gt = 78, detected = 150
            string testPath1 = @"Alfredo 2 - Freddie Gibbs\01 - Freddie Gibbs - 1995.flac";

            // gt = 144, detected = 130
            string testPath2 = @"Alfredo 2 - Freddie Gibbs\03 - Freddie Gibbs - Lemon Pepper Steppers.flac";

            // gt = 140, detected = 144
            string testPath3 = @"DBR Deluxe\04-drugs-you-should-try-it.flac";

            // gt = 116, detected = 153
            string testPath4 = @"RAYE - WHERE IS MY HUSBAND! (Official Music Video).mp3";

            // gt = 181, detected = 0
            string testPath5 = @"Nujabes - Feather (feat. Cise Starr & Akin from CYNE) [Official Audio].mp3";
            
            // gt = 134, detected = 152
            string testPath6 = @"Pink Floyd - Echoes (Official Audio).mp3";
            
            // gt = 150, detected = 89
            string testPath7 = @"forrest nolan - thank you i guess (official music video).mp3";

            string test = testPath5;

            if (System.IO.File.Exists(test))
            {
                _engine.Load(test);
                _engine.Play();
            }
        }
    }
}