using Newtonsoft.Json;
using System;
using System.IO;
using YouTubeDownloader.Configuration;
using YouTubeDownloader.Models;

namespace YouTubeDownloader.Helpers
{
    public static class SettingsHelper
    {
        private static readonly string SettingsPath = "appsettings.json";

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    // 確保即使讀取到設定檔，也要檢查必要的設定是否存在
                    EnsureDownloadFolderExists(settings);
                    return settings ?? CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"讀取設定檔失敗: {ex.Message}");
            }

            return CreateDefaultSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                EnsureDownloadFolderExists(settings);
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"儲存設定檔失敗: {ex.Message}");
            }
        }

        private static AppSettings CreateDefaultSettings()
        {
            var settings = new AppSettings();
            EnsureDownloadFolderExists(settings);
            return settings;
        }

        private static void EnsureDownloadFolderExists(AppSettings settings)
        {
            // 確保預設下載路徑存在
            if (!string.IsNullOrEmpty(settings.DefaultDownloadPath))
            {
                Directory.CreateDirectory(settings.DefaultDownloadPath);
            }

            // 如果有上次使用的路徑，也確保它存在
            if (!string.IsNullOrEmpty(settings.LastUsedPath))
            {
                Directory.CreateDirectory(settings.LastUsedPath);
            }
        }
    }
}