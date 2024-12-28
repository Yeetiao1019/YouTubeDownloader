using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using YouTubeDownloader.Helpers.Converters;
using YouTubeDownloader.Interfaces;
using YouTubeDownloader.Services;
using YouTubeDownloader.ViewModels;
using YouTubeDownloader.Views;

namespace YouTubeDownloader
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            Resources.Add("NullToVisibilityConverter", new NullToVisibilityConverter());
            Resources.Add("InverseBooleanConverter", new InverseBooleanConverter());
            Resources.Add("StatusToColorConverter", new StatusToColorConverter());
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 註冊服務
            services.AddSingleton<IYouTubeDownloadService, YouTubeDownloadService>();

            // 註冊 ViewModel
            services.AddSingleton<MainViewModel>();

            // 註冊 View
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetService<MainViewModel>();
            mainWindow.Show();
        }
    }
}