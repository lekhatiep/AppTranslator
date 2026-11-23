using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AppTranslator.Capture.Windows;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Core.Settings;
using AppTranslator.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App;

public partial class MainViewModel : ObservableObject
{
    private readonly IProfileStore _profileStore;
    private readonly JsonSettingsStore _settingsStore;
    private readonly AppPaths _paths;
    private readonly WindowsGraphicsCapture _graphicsCapture;
    private readonly DesktopDuplicationCapture _desktopDuplicationCapture;
    private readonly ILogger<MainViewModel> _logger;
    private AppSettings _settings;

    public MainViewModel(
        IProfileStore profileStore,
        JsonSettingsStore settingsStore,
        AppSettings settings,
        AppPaths paths,
        WindowsGraphicsCapture graphicsCapture,
        DesktopDuplicationCapture desktopDuplicationCapture,
        ILogger<MainViewModel> logger)
    {
        _profileStore = profileStore;
        _settingsStore = settingsStore;
        _settings = settings;
        _paths = paths;
        _graphicsCapture = graphicsCapture;
        _desktopDuplicationCapture = desktopDuplicationCapture;
        _logger = logger;

        Profiles = new ObservableCollection<GameProfile>(profileStore.LoadAll());
        _selectedCaptureEngine = settings.CaptureEngine;
        RefreshWindows();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == settings.ActiveProfileId) ?? Profiles.FirstOrDefault();
    }

    public ObservableCollection<GameProfile> Profiles { get; }

    public ObservableCollection<WindowInfo> Windows { get; } = [];

    public IReadOnlyList<CaptureEngine> CaptureEngines { get; } = Enum.GetValues<CaptureEngine>();

    [ObservableProperty]
    private GameProfile? _selectedProfile;

    [ObservableProperty]
    private WindowInfo? _selectedWindow;

    [ObservableProperty]
    private CaptureEngine _selectedCaptureEngine;

    [ObservableProperty]
    private string _status = string.Empty;

    private IScreenCapture CurrentCapture
    {
        get
        {
            if (SelectedCaptureEngine == CaptureEngine.DesktopDuplication)
                return _desktopDuplicationCapture;

            return _graphicsCapture;
        }
    }

    partial void OnSelectedProfileChanged(GameProfile? value)
    {
        if (value is null)
            return;

        SaveSettings(_settings with { ActiveProfileId = value.Id });

        var matchingWindow = WindowFinder.FindByTitleHint(Windows, value.WindowTitleHint);
        if (matchingWindow is not null)
            SelectedWindow = matchingWindow;
    }

    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        // Bỏ viền vàng khỏi cửa sổ cũ; lần chụp sau tự mở phiên mới cho cửa sổ mới.
        _graphicsCapture.Stop();

        if (value is null || SelectedProfile is null)
            return;

        // FR-01: nhớ cửa sổ theo hồ sơ. Gợi ý cũ vẫn khớp (vd "Chiaki") thì giữ, để tiêu đề thay đổi nhẹ vẫn tìm ra.
        string? hint = SelectedProfile.WindowTitleHint;
        bool hintStillMatches = !string.IsNullOrWhiteSpace(hint) && value.Title.Contains(hint, StringComparison.OrdinalIgnoreCase);
        if (!hintStillMatches)
            SaveProfile(SelectedProfile with { WindowTitleHint = value.Title });
    }

    partial void OnSelectedCaptureEngineChanged(CaptureEngine value)
    {
        // Phiên WGC còn chạy thì viền vàng vẫn hiện và lọt vào ảnh chụp của Desktop Duplication.
        _graphicsCapture.Stop();
        SaveSettings(_settings with { CaptureEngine = value });
    }

    [RelayCommand]
    private void RefreshWindows()
    {
        nint previousHandle = SelectedWindow?.Handle ?? 0;
        Windows.Clear();
        foreach (var window in WindowFinder.GetCapturableWindows())
            Windows.Add(window);

        SelectedWindow = Windows.FirstOrDefault(w => w.Handle == previousHandle)
            ?? WindowFinder.FindByTitleHint(Windows, SelectedProfile?.WindowTitleHint);
    }

    [RelayCommand]
    private async Task SelectRegionsAsync()
    {
        var frame = await CaptureSelectedWindowAsync();
        if (frame is null || SelectedProfile is null)
            return;

        var selector = new RegionSelectorWindow(FrameImage.ToBitmapSource(frame), SelectedProfile)
        {
            Owner = Application.Current.MainWindow,
        };
        if (selector.ShowDialog() != true)
            return;

        SaveProfile(selector.Result!);
        Status = "Đã lưu vùng chữ vào hồ sơ.";
    }

    [RelayCommand]
    private async Task SaveDebugImagesAsync()
    {
        var frame = await CaptureSelectedWindowAsync();
        if (frame is null || SelectedProfile is null)
            return;

        Directory.CreateDirectory(_paths.DebugDirectory);
        string prefix = $"{SelectedProfile.Id}-{DateTime.Now:yyyyMMdd-HHmmss}";
        var image = FrameImage.ToBitmapSource(frame);
        FrameImage.SavePng(image, Path.Combine(_paths.DebugDirectory, $"{prefix}-full.png"));

        foreach (var region in SelectedProfile.Regions)
        {
            var area = region.ToPixelRect(frame.Width, frame.Height);
            if (area.Width == 0 || area.Height == 0)
                continue;

            string fileName = $"{prefix}-{region.Type.ToString().ToLowerInvariant()}.png";
            FrameImage.SavePng(FrameImage.Crop(image, area), Path.Combine(_paths.DebugDirectory, fileName));
        }

        Status = $"Đã lưu ảnh {frame.Width}x{frame.Height} vào {_paths.DebugDirectory}";
    }

    private async Task<Frame?> CaptureSelectedWindowAsync()
    {
        if (SelectedWindow is null)
        {
            Status = "Chưa chọn cửa sổ nguồn.";
            return null;
        }

        try
        {
            var frame = await CurrentCapture.CaptureAsync(SelectedWindow.Handle);
            if (frame is null)
                Status = "Không chụp được: cửa sổ đã đóng, đang thu nhỏ hoặc chưa có khung hình.";

            return frame;
        }
        catch (Exception exception)
        {
            // Lỗi từ D3D/WinRT (driver, quyền chụp...): báo người dùng, ghi log, không làm sập app.
            _logger.LogError(exception, "Chụp cửa sổ {Title} bằng {Engine} thất bại", SelectedWindow.Title, SelectedCaptureEngine);
            Status = $"Lỗi chụp: {exception.Message}";
            return null;
        }
    }

    private void SaveProfile(GameProfile profile)
    {
        _profileStore.Save(profile);

        int index = Profiles.IndexOf(Profiles.First(p => p.Id == profile.Id));
        Profiles[index] = profile;
        SelectedProfile = profile;
    }

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        _settingsStore.Save(settings);
    }
}
