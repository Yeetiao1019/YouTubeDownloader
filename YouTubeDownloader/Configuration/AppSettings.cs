using System;
using System.IO;

namespace YouTubeDownloader.Configuration
{
    public class AppSettings
    {
        public string DefaultDownloadPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "YouTubeDownloads"
        );
        public int MaxConcurrentDownloads { get; set; } = 5;
        public string LastUsedPath { get; set; }
    }
}