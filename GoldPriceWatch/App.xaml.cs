using GoldPriceWatch.Core;
using GoldPriceWatch.Core.Background;
using GoldPriceWatch.Model;
using GoldPriceWatch.ModelView;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;

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

        /// <summary>
        /// 是否正在退出。为 true 时各窗口不再拦截关闭（用于托盘菜单退出）。
        /// </summary>
        public bool IsShuttingDown { get; set; }

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
                    services.AddSingleton<GoldPriceWatch.Core.HoldingStore>(GoldPriceWatch.Core.HoldingStore.Load());
                    services.AddSingleton<GoldModelView>();
                    services.AddSingleton<INotificationHandler<GoldResult>>(provide => provide.GetRequiredService<GoldModelView>());
                    services.AddSingleton<INotificationHandler<FetchStatusChanged>>(provide => provide.GetRequiredService<GoldModelView>());
                    // 持仓监控（复用同一取价管道）
                    services.AddSingleton<HoldingViewModel>();
                    services.AddSingleton<INotificationHandler<GoldResult>>(provide => provide.GetRequiredService<HoldingViewModel>());
                    // 注册 WPF 窗口
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<HoldingWindow>();
                    // 托盘图标
                    services.AddSingleton<TrayIconService>();

                    // 注册后台服务：同时注册具体类型，供“立即刷新”通过 DI 直接调用
                    services.AddSingleton<LoopFetcherService>();
                    services.AddHostedService(sp => sp.GetRequiredService<LoopFetcherService>());
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

                // 显示悬浮框
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                // 启动即显示托盘图标
                _host.Services.GetRequiredService<TrayIconService>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}");
                Shutdown();
            }
        }
    }

}
