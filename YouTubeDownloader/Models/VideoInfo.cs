using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader.Models
{
    /// <summary>
    /// 影片資訊的資料模型
    /// </summary>
    public class VideoInfo
    {
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public TimeSpan Duration { get; set; }
        public string Author { get; set; }
    }
}
