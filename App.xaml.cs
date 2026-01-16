using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.DependencyInjection;
using MMG.Services;
using MMG.ViewModels;
using MMG.ViewModels.API;
using MMG.ViewModels.Spec;
using MMG.Core.Services;
using MMG.Core.Interfaces;

namespace MMG
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigureServices();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // 서비스 등록 (Singleton)
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<TestDatabaseService>();
            services.AddSingleton<UdpClientService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISpecParserFactory, SpecParserFactory>();
            services.AddSingleton<SpecAdapterService>();
            services.AddSingleton<TestExecutionService>();

            // ViewModels 등록 (Transient)
            services.AddTransient<RequestViewModel>();
            services.AddTransient<ResponseViewModel>();
            services.AddTransient<SavedRequestsViewModel>();
            services.AddTransient<TreeViewViewModel>();
            services.AddTransient<TestsViewModel>();
            services.AddTransient<SpecViewModel>();
            services.AddTransient<DocViewerViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<NavigationViewModel>();

            // CommunityToolkit.Mvvm의 Ioc에 서비스 프로바이더 설정
            Ioc.Default.ConfigureServices(services.BuildServiceProvider());
        }
    }
}
