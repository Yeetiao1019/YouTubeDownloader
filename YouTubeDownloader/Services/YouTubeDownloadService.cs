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
using FFMpegCore;
using FFMpegCore.Enums;
using System.Diagnostics;

namespace YouTubeDownloader.Services;

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
            System.Diagnostics.Debug.WriteLine($"視訊串流數量: {videoStreams.Count}");
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

                // 篩選出 MP4 容器的音訊串流 (更適合 iPhone)
                var mp4AudioStreams = audioStreams
                    .Where(s => s.Container.ToString().Equals("Mp4", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.Bitrate)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"MP4 容器音訊串流數量: {mp4AudioStreams.Count}");

                if (mp4AudioStreams.Any())
                {
                    // 如果有 MP4 音訊，優先使用
                    streamInfo = mp4AudioStreams.First();
                    fileName = $"{video.Title}.m4a";  // 使用 m4a 副檔名
                    System.Diagnostics.Debug.WriteLine("選擇 iPhone 相容的 m4a 音訊");
                }
                else
                {
                    // 如果沒有 MP4 音訊，使用其他格式
                    streamInfo = audioStreams
                        .OrderByDescending(s => s.Bitrate)
                        .First();
                    fileName = $"{video.Title}.mp3";
                    System.Diagnostics.Debug.WriteLine("找不到 iPhone 相容的音訊，使用標準 mp3");
                }

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
                        .OrderByDescending(s => s.Bitrate)
                        .First();
                    System.Diagnostics.Debug.WriteLine($"\n使用混合串流:");
                }
                else if (videoStreams.Any())
                {
                    streamInfo = videoStreams
                        .OrderByDescending(s => s.Bitrate)
                        .First();
                    System.Diagnostics.Debug.WriteLine($"\n使用視訊串流:");
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

            if (isAudioOnly && (fileName.EndsWith(".m4a") || fileName.EndsWith(".mp3")))
            {
                var tempFilePath = filePath + ".temp";
                File.Move(filePath, tempFilePath);

                await ProcessAudioForIOS(tempFilePath, filePath);

                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }

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

    private async Task ProcessAudioForIOS(string inputFile, string outputFile)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"開始處理音頻: {inputFile} -> {outputFile}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputFile}\" -c:a aac -b:a 128k -ar 44100 -vsync 0 -af \"aresample=44100\" -movflags faststart \"{outputFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            var exitCode = process.ExitCode;
            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            System.Diagnostics.Debug.WriteLine($"FFmpeg 處理完成，退出碼: {exitCode}");

            if (exitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg 錯誤: {error}");
                throw new Exception($"FFmpeg 處理失敗，退出碼: {exitCode}");
            }

            // 確認輸出文件存在
            if (!File.Exists(outputFile))
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg 未能創建輸出文件: {outputFile}");
                throw new Exception("FFmpeg 未能創建輸出文件");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"處理音頻時發生錯誤: {ex.Message}");
            throw; // 重新拋出異常以便上層處理
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
