using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using VibeMP.Models;
using VibeMP.ViewModels;

namespace VibeMP.Services
{
    public partial class MusicImportService : ObservableObject
    {
        private readonly LibraryManager _libraryManager;
        private int _activeImportCount = 0;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private int _scanProgressValue = 0;

        [ObservableProperty]
        private int _scanProgressMax = 100;

        [ObservableProperty]
        private bool _hasPendingImports;

        public ObservableCollection<PendingItem> PendingImportPaths { get; } = new();

        public MusicImportService(LibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public async Task ImportPathsAsync(IEnumerable<string> paths, Action onBatchFinished)
        {
            try
            {
                _activeImportCount++;
                IsProcessing = _activeImportCount > 0;
                ScanProgressValue = 0;

                foreach (var p in paths)
                {
                    if (Path.GetFileName(p).StartsWith("._"))
                        continue;

                    if (File.Exists(p) && !PendingImportPaths.Any(item => item.FullPath == p))
                    {
                        PendingImportPaths.Add(
                            new PendingItem
                            {
                                FullPath = p,
                                DisplayName = Path.GetFileName(p),
                                IsFolder = false,
                            }
                        );
                    }
                    else if (
                        Directory.Exists(p) && !PendingImportPaths.Any(item => item.FullPath == p)
                    )
                    {
                        PendingImportPaths.Add(
                            new PendingItem
                            {
                                FullPath = p,
                                DisplayName = Path.GetFileName(p),
                                IsFolder = true,
                            }
                        );
                    }
                }

                await _libraryManager.ImportPathsAsync(
                    paths,
                    (filePath, current, total) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string friendlyName = Path.GetFileName(filePath);
                            StatusText =
                                $"Importing & Analyzing song {current} of {total}: {friendlyName}";
                            ScanProgressMax = total;
                            ScanProgressValue = current;

                            var matchingUiItem = PendingImportPaths.FirstOrDefault(item =>
                                filePath.StartsWith(
                                    item.FullPath,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );

                            if (matchingUiItem != null && matchingUiItem.IsFolder)
                            {
                                matchingUiItem.ProgressPercent = (int)(
                                    ((double)current / total) * 100
                                );
                            }
                        });
                    },
                    (filePath, trackPercentage) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var matchingUiItem = PendingImportPaths.FirstOrDefault(item =>
                                item.FullPath == filePath
                            );
                            if (matchingUiItem != null && !matchingUiItem.IsFolder)
                            {
                                matchingUiItem.ProgressPercent = trackPercentage;
                            }
                        });
                    }
                );

                HasPendingImports = PendingImportPaths.Count > 0;
                StatusText = "All songs successfully added and analyzed.";
                onBatchFinished?.Invoke();
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
            finally
            {
                _activeImportCount--;
                IsProcessing = _activeImportCount > 0;

                if (_activeImportCount == 0 && PendingImportPaths.Count > 0)
                {
                    WeakReferenceMessenger.Default.Send(
                        new NotificationToast
                        {
                            Title = "Track(s) Analyzed",
                            Message = "All tracks have been successfully analyzed.",
                        }
                    );
                }
            }
        }
    }
}
