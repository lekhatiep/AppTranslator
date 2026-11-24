using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AppTranslator.App.Overlay;
using AppTranslator.App.Pipeline;
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
    private readonly OverlayController _overlay;
    private readonly HotkeyService _hotkeys;
    private readonly SubtitleReader _subtitleReader;
    private readonly ILogger<MainViewModel> _logger;
    private AppSettings _settings;
    private CancellationTokenSource? _readerCancellation;

    // Stage 2 chưa có OCR/dịch: overlay hiện chữ giả để kiểm tra vị trí, độ dài và cách xuống dòng.
    private const string SampleSubtitle = "[Thử overlay] Thử khắp thành phố. Hắn là một trong bốn trùm cai quản thế giới ngầm của thành phố này.";
    private const string SampleChoices = "▶ 1. [Thử] Ta mơ thấy Kaer Morhen...\n2. [Thử] Thật lòng không muốn nhắc đến.";

    public MainViewModel(
        IProfileStore profileStore,
        JsonSettingsStore settingsStore,
        AppSettings settings,
        AppPaths paths,
        WindowsGraphicsCapture graphicsCapture,
        DesktopDuplicationCapture desktopDuplicationCapture,
        OverlayController overlay,
        HotkeyService hotkeys,
        SubtitleReader subtitleReader,
        ILogger<MainViewModel> logger)
    {
        _profileStore = profileStore;
        _settingsStore = settingsStore;
        _settings = settings;
        _paths = paths;
        _graphicsCapture = graphicsCapture;
        _desktopDuplicationCapture = desktopDuplicationCapture;
        _overlay = overlay;
        _hotkeys = hotkeys;
        _subtitleReader = subtitleReader;
        _logger = logger;
        _subtitleReader.SentenceRead += OnSentenceRead;
        _subtitleReader.SubtitleCleared += OnSubtitleCleared;

        Profiles = new ObservableCollection<GameProfile>(profileStore.LoadAll());
        _selectedCaptureEngine = settings.CaptureEngine;
        _overlay.SetCaptureEngine(settings.CaptureEngine);
        RefreshWindows();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == settings.ActiveProfileId) ?? Profiles.FirstOrDefault();
        RegisterHotkeys();
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

    [ObservableProperty]
    private bool _isTranslationEnabled;

    [ObservableProperty]
    private string _lastSubtitle = string.Empty;

    [ObservableProperty]
    private string _lastSubtitleTiming = string.Empty;

    public string HotkeyHint => $"Phím tắt: {_settings.Hotkeys.ToggleTranslation} bật/tắt dịch, {_settings.Hotkeys.SelectRegion} chọn vùng chữ.";

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
        ShowSampleOverlayTexts(value);
        RestartSubtitleReader();

        var matchingWindow = WindowFinder.FindByTitleHint(Windows, value.WindowTitleHint);
        if (matchingWindow is not null)
            SelectedWindow = matchingWindow;
    }

    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        // Bỏ viền vàng khỏi cửa sổ cũ; lần chụp sau tự mở phiên mới cho cửa sổ mới.
        _graphicsCapture.Stop();
        _overlay.SetTarget(value?.Handle ?? 0);
        RestartSubtitleReader();

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
        _overlay.SetCaptureEngine(value);
        SaveSettings(_settings with { CaptureEngine = value });
        RestartSubtitleReader();
    }

    partial void OnIsTranslationEnabledChanged(bool value)
    {
        _overlay.SetEnabled(value);
        Status = value ? "Đã bật dịch (overlay hiện khi cửa sổ nguồn đang được dùng)." : "Đã tắt dịch.";
        RestartSubtitleReader();
    }

    // Stage 4: chỉ đọc phụ đề tiếng Anh và hiện trên cửa sổ chính; dịch và đè lên overlay ở Stage 5–6.
    private void RestartSubtitleReader()
    {
        _readerCancellation?.Cancel();
        _readerCancellation?.Dispose();
        _readerCancellation = null;

        if (!IsTranslationEnabled || SelectedWindow is null || SelectedProfile is null)
            return;

        var region = SelectedProfile.Regions.FirstOrDefault(r => r.Type == RegionType.Subtitle);
        if (region is null || region.W <= 0 || region.H <= 0)
        {
            Status = "Hồ sơ chưa có vùng phụ đề: bấm \"Chọn vùng chữ\".";
            return;
        }

        _readerCancellation = new CancellationTokenSource();
        _ = RunSubtitleReaderAsync(SelectedWindow, region, _readerCancellation.Token);
    }

    private async Task RunSubtitleReaderAsync(WindowInfo window, Region region, CancellationToken cancellationToken)
    {
        try
        {
            await _subtitleReader.RunAsync(token => CurrentCapture.CaptureAsync(window.Handle, token), region, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Tắt dịch hoặc đổi hồ sơ/cửa sổ/engine: vòng mới (nếu có) đã được khởi động.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vòng đọc phụ đề dừng vì lỗi");
            Status = $"Đọc phụ đề dừng vì lỗi: {exception.Message}";
        }
    }

    private void OnSentenceRead(SubtitleRead read)
    {
        LastSubtitle = read.Text;
        if (read.IsCorrection)
        {
            LastSubtitleTiming = $"Đã sửa lại sau khi đọc thêm ({DateTime.Now:HH:mm:ss})";
            return;
        }

        LastSubtitleTiming = $"OCR {read.OcrDuration.TotalMilliseconds:F0} ms, trễ từ lúc câu hiện {read.Latency.TotalMilliseconds:F0} ms ({DateTime.Now:HH:mm:ss})";
    }

    private void OnSubtitleCleared()
    {
        LastSubtitleTiming = $"Phụ đề đã tắt ({DateTime.Now:HH:mm:ss})";
    }

    private void ShowSampleOverlayTexts(GameProfile profile)
    {
        var texts = new List<(Region Region, string Text)>();
        foreach (var region in profile.Regions)
        {
            string text = region.Type == RegionType.Subtitle ? SampleSubtitle : SampleChoices;
            texts.Add((region, text));
        }

        _overlay.ShowTexts(texts, _settings.Overlay);
    }

    private void RegisterHotkeys()
    {
        var failures = new List<string>();
        RegisterHotkey(_settings.Hotkeys.ToggleTranslation, () => IsTranslationEnabled = !IsTranslationEnabled, failures);
        RegisterHotkey(_settings.Hotkeys.SelectRegion, () => SelectRegionsCommand.Execute(null), failures);

        if (failures.Count > 0)
            Status = "Không đăng ký được phím tắt: " + string.Join(", ", failures) + ". Sửa trong settings.json rồi mở lại app.";
    }

    private void RegisterHotkey(string text, Action action, List<string> failures)
    {
        var gesture = HotkeyGesture.TryParse(text);
        if (gesture is null)
        {
            _logger.LogWarning("Phím tắt {Hotkey} sai định dạng", text);
            failures.Add($"{text} (sai định dạng)");
            return;
        }

        int error = _hotkeys.Register(gesture, action);
        if (error != 0)
        {
            _logger.LogWarning("Đăng ký phím tắt {Hotkey} thất bại, mã lỗi Win32 {Error}", text, error);
            failures.Add($"{text} (đã bị app khác dùng)");
            return;
        }

        _logger.LogInformation("Đã đăng ký phím tắt {Hotkey}", text);
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
