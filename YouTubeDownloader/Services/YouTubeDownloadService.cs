using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YouTubeDownloader.Interfaces;
using YouTubeDownloader.Models;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode;
using System.IO;

namespace YouTubeDownloader.Services
{
    /// <summary>
    /// 實作下載服務，處理與 YouTube 相關的所有操作
    /// </summary>
    public class YouTubeDownloadService : IYouTubeDownloadService
    {
        private readonly YoutubeClient _youtubeClient;

        public YouTubeDownloadService()
        {
            _youtubeClient = new YoutubeClient();
        }

        public async Task<VideoInfo> GetVideoInfoAsync(string url)
        {
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(url);
                var thumbnailUrl = video.Thumbnails
                .OrderByDescending(t => t.Resolution.Height)
                .FirstOrDefault()?.Url;

                return new VideoInfo
                {
                    Title = video.Title,
                    ThumbnailUrl = thumbnailUrl,
                    Duration = video.Duration ?? TimeSpan.Zero,
                    Author = video.Author.ChannelTitle
                };
            }
            catch (Exception ex)
            {
                throw new ApplicationException("無法取得影片資訊", ex);
            }
        }

        public async Task<string> DownloadVideoAsync(string url, string outputPath, bool isAudioOnly, IProgress<double> progress)
        {
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"=== 影片資訊 ===");
                System.Diagnostics.Debug.WriteLine($"標題: {video.Title}");
                System.Diagnostics.Debug.WriteLine($"ID: {video.Id}");
                System.Diagnostics.Debug.WriteLine($"作者: {video.Author}");

                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url);

                // 檢查所有類型的串流
                System.Diagnostics.Debug.WriteLine("\n=== 所有串流類型 ===");

                var muxedStreams = streamManifest.GetMuxedStreams().ToList();
                System.Diagnostics.Debug.WriteLine($"\n混合串流數量: {muxedStreams.Count}");

                var videoStreams = streamManifest.GetVideoStreams().ToList();
                System.Diagnostics.Debug.WriteLine($"視頻串流數量: {videoStreams.Count}");

                var audioStreams = streamManifest.GetAudioOnlyStreams().ToList();
                System.Diagnostics.Debug.WriteLine($"純音頻串流數量: {audioStreams.Count}");

                IStreamInfo streamInfo;
                string fileName;

                if (isAudioOnly)
                {
                    if (!audioStreams.Any())
                    {
                        throw new InvalidOperationException("找不到可用的音訊串流");
                    }

                    streamInfo = audioStreams
                        .OrderByDescending(s => s.Bitrate)
                        .First();
                    fileName = $"{video.Title}.mp3";

                    System.Diagnostics.Debug.WriteLine($"\n選擇的音訊串流:");
                    System.Diagnostics.Debug.WriteLine($"- Bitrate: {streamInfo.Bitrate}");
                    System.Diagnostics.Debug.WriteLine($"- Container: {streamInfo.Container}");
                }
                else
                {
                    // 優先使用混合串流，如果沒有則使用純視頻串流
                    if (muxedStreams.Any())
                    {
                        streamInfo = muxedStreams
                            .OrderByDescending(s => s.Bitrate)  // 使用 Bitrate 替代 VideoQuality
                            .First();
                        System.Diagnostics.Debug.WriteLine($"\n使用混合串流:");
                    }
                    else if (videoStreams.Any())
                    {
                        streamInfo = videoStreams
                            .OrderByDescending(s => s.Bitrate)  // 使用 Bitrate 替代 VideoQuality
                            .First();
                        System.Diagnostics.Debug.WriteLine($"\n使用視頻串流:");
                    }
                    else
                    {
                        throw new InvalidOperationException("找不到任何可用的視訊串流");
                    }

                    fileName = $"{video.Title}.mp4";

                    System.Diagnostics.Debug.WriteLine($"- Bitrate: {streamInfo.Bitrate}");
                    System.Diagnostics.Debug.WriteLine($"- Container: {streamInfo.Container}");
                }

                // 清理檔案名稱中的非法字元
                fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                var filePath = Path.Combine(outputPath, fileName);

                System.Diagnostics.Debug.WriteLine($"\n開始下載到: {filePath}");
                await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);
                System.Diagnostics.Debug.WriteLine("下載完成!");

                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n發生錯誤:");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw new ApplicationException($"下載影片時發生錯誤：{ex.Message}", ex);
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // 移除檔案名稱中的無效字元
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedFileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries))
                .TrimEnd('.');

            // 如果檔案名稱過長，進行截斷
            const int maxFileNameLength = 200; // 設定一個合理的最大長度
            if (sanitizedFileName.Length > maxFileNameLength)
            {
                sanitizedFileName = sanitizedFileName.Substring(0, maxFileNameLength);
            }

            return sanitizedFileName;
        }
    }
}
