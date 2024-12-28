using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using YouTubeDownloader.Configuration;
using YouTubeDownloader.Helpers;
using YouTubeDownloader.Interfaces;
using YouTubeDownloader.Models;
using YouTubeDownloader.Services;
using MessageBox = System.Windows.MessageBox;

namespace YouTubeDownloader.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IYouTubeDownloadService _downloadService;
        private readonly AppSettings _settings;

        private string _youtubeUrl;
        private VideoInfo _currentVideo;
        private bool _isAudioOnly;
        private string _downloadPath;
        private bool _isLoading;

        public ObservableCollection<DownloadTask> DownloadQueue { get; }

        public MainViewModel(IYouTubeDownloadService downloadService)
        {
            _downloadService = downloadService;
            _settings = SettingsHelper.LoadSettings();
            DownloadQueue = new ObservableCollection<DownloadTask>();

            // 初始化命令時使用 async lambda
            PreviewCommand = new RelayCommand(PreviewVideo, CanPreviewVideo);
            DownloadCommand = new RelayCommand(DownloadVideo, CanDownload);
            SelectPathCommand = new RelayCommand(SelectDownloadPath);

            // 若 LastUsedPath 為空，則使用 DefaultDownloadPath
            // 確保 DownloadPath 一定會有值
            DownloadPath = !string.IsNullOrEmpty(_settings.LastUsedPath)
                ? _settings.LastUsedPath
                : _settings.DefaultDownloadPath;

            // 確保下載路徑存在
            if (!string.IsNullOrEmpty(DownloadPath) && !Directory.Exists(DownloadPath))
            {
                try
                {
                    Directory.CreateDirectory(DownloadPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"無法建立下載資料夾：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 命令屬性
        public ICommand PreviewCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand SelectPathCommand { get; }

        // 資料綁定屬性
        public string YoutubeUrl
        {
            get => _youtubeUrl;
            set
            {
                if (_youtubeUrl != value)
                {
                    _youtubeUrl = value;
                    OnPropertyChanged(nameof(YoutubeUrl));
                    (PreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public VideoInfo CurrentVideo
        {
            get => _currentVideo;
            set
            {
                if (_currentVideo != value)
                {
                    _currentVideo = value;
                    OnPropertyChanged(nameof(CurrentVideo));
                    (DownloadCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsAudioOnly
        {
            get => _isAudioOnly;
            set
            {
                if (_isAudioOnly != value)
                {
                    _isAudioOnly = value;
                    OnPropertyChanged(nameof(IsAudioOnly));
                }
            }
        }

        public string DownloadPath
        {
            get => _downloadPath;
            set
            {
                if (_downloadPath != value)
                {
                    _downloadPath = value;
                    _settings.LastUsedPath = value;
                    SettingsHelper.SaveSettings(_settings);
                    OnPropertyChanged(nameof(DownloadPath));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                    (PreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DownloadCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool CanPreviewVideo()
        {
            return !IsLoading && !string.IsNullOrWhiteSpace(YoutubeUrl);
        }

        private void PreviewVideo()
        {
            _ = PreviewVideoAsync();
        }

        private async Task PreviewVideoAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                CurrentVideo = await _downloadService.GetVideoInfoAsync(YoutubeUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"預覽失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                CurrentVideo = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanDownload()
        {
            return !IsLoading && CurrentVideo != null && !string.IsNullOrEmpty(DownloadPath);
        }

        private void DownloadVideo()
        {
            _ = DownloadVideoAsync();
        }

        private async Task DownloadVideoAsync()
        {
            if (DownloadQueue.Count >= _settings.MaxConcurrentDownloads)
            {
                MessageBox.Show("已達到最大下載數量限制", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var downloadTask = new DownloadTask
            {
                Title = CurrentVideo.Title,
                OutputPath = DownloadPath,
                IsAudioOnly = IsAudioOnly,
                Status = "準備下載..."
            };

            DownloadQueue.Add(downloadTask);

            try
            {
                var progress = new Progress<double>(p =>
                {
                    downloadTask.Progress = p * 100;
                    downloadTask.Status = $"下載中... {p:P0}";
                });

                await _downloadService.DownloadVideoAsync(YoutubeUrl, DownloadPath, IsAudioOnly, progress);
                downloadTask.Status = "完成";
            }
            catch (Exception ex)
            {
                downloadTask.Status = $"失敗：{ex.Message}";
                MessageBox.Show($"下載失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (downloadTask.Status == "完成")
                {
                    // 延遲移除完成的下載項目
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadQueue.Remove(downloadTask);
                    });
                }
            }
        }
        private void SelectDownloadPath()
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "選擇下載位置",
                IsFolderPicker = true,
                InitialDirectory = DownloadPath,
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = DownloadPath,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                DownloadPath = dialog.FileName;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}