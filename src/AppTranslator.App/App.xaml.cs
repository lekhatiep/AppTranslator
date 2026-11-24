using System.IO;
using System.Net.Http;
using System.Windows;
using AppTranslator.App.Overlay;
using AppTranslator.App.Pipeline;
using AppTranslator.Capture.Windows;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Settings;
using AppTranslator.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AppTranslator.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.Default;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(paths.LogsDirectory, "app-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Lỗi chưa xử lý trên UI thread");
            args.Handled = true;
        };

        var profileStore = new JsonProfileStore(paths);
        profileStore.SeedMissing(Path.Combine(AppContext.BaseDirectory, "profiles"));
        var settingsStore = new JsonSettingsStore(paths);

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddSingleton(paths);
                services.AddSingleton<IProfileStore>(profileStore);
                services.AddSingleton(settingsStore);
                services.AddSingleton(_ => settingsStore.Load());
                services.AddSingleton<WindowsGraphicsCapture>();
                services.AddSingleton<DesktopDuplicationCapture>();
                services.AddSingleton<OverlayController>();
                services.AddSingleton<HotkeyService>();
                services.AddSingleton<SubtitleReader>();
                services.AddSingleton<ChoiceReader>();
                services.AddSingleton<SecretStore>();
                services.AddSingleton(_ => new SqliteTranslationCache(paths.DatabaseFile));
                services.AddSingleton(_ => new SqliteHistoryStore(paths.DatabaseFile));
                // Một HttpClient dùng suốt đời app (tạo mới mỗi lần gọi sẽ cạn socket).
                services.AddSingleton(_ => new HttpClient());
                services.AddSingleton<SubtitleTranslator>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();
        Log.Information("AppTranslator khởi động, dữ liệu tại {Root}", paths.Root);

        // Overlay cũng là một Window và được tạo trước, nên WPF tự coi nó là MainWindow; phải chỉ rõ cửa sổ chính
        // (ShutdownMode=OnMainWindowClose đặt trong App.xaml).
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Chưa có hosted service nên chỉ cần Dispose, không phải chờ StopAsync.
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
