using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader.Models
{
    /// <summary>
    /// 下載任務的資料模型
    /// </summary>
    public class DownloadTask : INotifyPropertyChanged
    {
        private double _progress;
        private string _status;

        public string Title { get; set; }
        public string OutputPath { get; set; }
        public bool IsAudioOnly { get; set; }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
