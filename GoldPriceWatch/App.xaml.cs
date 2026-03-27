using GoldPriceWatch.Core.Background;
using GoldPriceWatch.Model;
using GoldPriceWatch.ModelView;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Navigation;

namespace GoldPriceWatch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        /// <summary>
        /// 获取当前 App 实例
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// 提供全局服务访问
        /// </summary>
        public IServiceProvider Services => _host.Services;

        public App()
        {
            InitializeComponent();


            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 注册日志
                    services.AddLogging(logging =>
                        logging.AddConsole().SetMinimumLevel(LogLevel.Debug));

                    // 注册 MediatR
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(App).Assembly));
                    // 注册 Config
                    services.AddSingleton<GoldInvestmentConfig>(new GoldInvestmentConfig());
                    services.AddSingleton<GoldModelView>();
                    services.AddSingleton<INotificationHandler<GoldResult>>(provide => provide.GetRequiredService<GoldModelView>());
                    services.AddSingleton<INotificationHandler<FetchStatusChanged>>(provide => provide.GetRequiredService<GoldModelView>());
                    // 注册 WPF 主窗口
                    services.AddSingleton<MainWindow>();

                    // 注册后台服务
                    services.AddHostedService<LoopFetcherService>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 启动主机，运行所有托管服务
                await _host.StartAsync();

                // 显示主窗口
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}");
                Shutdown();
            }
        }
    }

}
