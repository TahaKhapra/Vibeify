using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using VibeMP.Models;
using VibeMP.ViewModels;

namespace VibeMP.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            WeakReferenceMessenger.Default.Register<NotificationToast>(
                this,
                (recipient, message) =>
                {
                    ShowToastNotification(message.Title, message.Message);
                }
            );
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
            e.Handled = true;

            if (
                e.Data.GetDataPresent(DataFormats.FileDrop)
                && DataContext is MainViewModel viewModel
            )
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                await viewModel.ImportPathsAsync(paths);
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

        private async void ShowToastNotification(string title, string message)
        {
            var notificationData = new NotificationToast { Title = title, Message = message };

            var toastControl = new ContentControl
            {
                Content = notificationData,
                RenderTransform = new TranslateTransform(300, 0),
            };

            ToastContainer.Children.Clear();
            ToastContainer.Children.Add(toastControl);

            var anim = new DoubleAnimation(300, 0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            toastControl.RenderTransform.BeginAnimation(TranslateTransform.XProperty, anim);

            try
            {
                await Task.Delay(3500);

                if (Application.Current == null || Dispatcher.HasShutdownStarted)
                    return;

                var hideAnim = new DoubleAnimation(0, 300, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                };
                hideAnim.Completed += (s, e) => ToastContainer.Children.Remove(toastControl);
                toastControl.RenderTransform.BeginAnimation(TranslateTransform.XProperty, hideAnim);
            }
            catch
            {
                // Silently handle exceptions
            }
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
