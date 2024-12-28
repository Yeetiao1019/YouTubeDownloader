using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YouTubeDownloader.Models;

namespace YouTubeDownloader.Interfaces
{
    /// <summary>
    /// 定義下載服務的介面
    /// </summary>
    public interface IYouTubeDownloadService
    {
        Task<VideoInfo> GetVideoInfoAsync(string url);
        Task<string> DownloadVideoAsync(string url, string outputPath, bool isAudioOnly, IProgress<double> progress);
    }
}
