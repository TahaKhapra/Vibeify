using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VibeMP.Models;
using VibeMP.ViewModels;

namespace VibeMP.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState =
                WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (
                e.Data.GetDataPresent(DataFormats.FileDrop)
                && DataContext is MainViewModel viewModel
            )
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                await viewModel.ImportPathsAsync(paths);

                ShowToastNotification(
                    "Library Import Successful!",
                    "Tracks added and categorized."
                );
            }
        }

        private async void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Music Folder",
                Multiselect = false,
            };

            if (dialog.ShowDialog() == true && DataContext is MainViewModel viewModel)
            {
                await viewModel.ImportPathsAsync(new[] { dialog.FolderName });

                ShowToastNotification(
                    "Library Import Successful!",
                    "Tracks added and categorized."
                );
            }
        }

        private void ClearPaths_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is VibeMP.ViewModels.MainViewModel vm)
            {
                vm.PendingImportPaths.Clear();
                vm.HasPendingImports = false;
            }
        }

        private void TrackRow_MouseClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is Track selectedTrack)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    if (viewModel.PlaySpecificTrackCommand.CanExecute(selectedTrack))
                    {
                        viewModel.PlaySpecificTrackCommand.Execute(selectedTrack);
                    }
                }
            }
        }

        private void ShowToastNotification(string title, string message)
        {
            var toast = new Border
            {
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2D2D2D")
                ),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FF4B4B")
                ),
                BorderThickness = new Thickness(1, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Width = 280,
                Height = 65,
                Padding = new Thickness(12, 8, 12, 8),
                RenderTransform = new TranslateTransform(300, 0),
            };

            var stack = new StackPanel();
            stack.Children.Add(
                new TextBlock
                {
                    Text = title,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                }
            );
            stack.Children.Add(
                new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.DarkGray,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0),
                }
            );
            toast.Child = stack;

            ToastContainer.Children.Clear();
            ToastContainer.Children.Add(toast);

            var anim = new DoubleAnimation(300, 0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            toast.RenderTransform.BeginAnimation(TranslateTransform.XProperty, anim);

            Task.Delay(3500)
                .ContinueWith(_ =>
                    Dispatcher.Invoke(() =>
                    {
                        var hideAnim = new DoubleAnimation(0, 300, TimeSpan.FromMilliseconds(300))
                        {
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                        };
                        hideAnim.Completed += (s, e) => ToastContainer.Children.Remove(toast);
                        toast.RenderTransform.BeginAnimation(
                            TranslateTransform.XProperty,
                            hideAnim
                        );
                    })
                );
        }

        private void PreviewMouseWheel_BubblingFix(
            object sender,
            System.Windows.Input.MouseWheelEventArgs e
        )
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(
                    e.MouseDevice,
                    e.Timestamp,
                    e.Delta
                )
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender,
                };

                if (sender is FrameworkElement frameworkElement)
                {
                    var parent = frameworkElement.Parent as UIElement;
                    parent?.RaiseEvent(eventArg);
                }
            }
        }

        public bool IsDraggingProgress { get; private set; } = false;

        private void ProgressBar_PreviewMouseLeftButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e
        )
        {
            IsDraggingProgress = true;
        }

        private void ProgressBar_PreviewMouseLeftButtonUp(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e
        )
        {
            if (sender is Slider slider && DataContext is MainViewModel viewModel)
            {
                viewModel.SeekToPosition(slider.Value);
            }

            IsDraggingProgress = false;
        }
    }
}
