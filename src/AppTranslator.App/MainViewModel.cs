using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AppTranslator.App.Overlay;
using AppTranslator.App.Pipeline;
using AppTranslator.Capture.Windows;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Layout;
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
    private readonly SubtitleTranslator _subtitleTranslator;
    private readonly SecretStore _secrets;
    private readonly ILogger<MainViewModel> _logger;
    private AppSettings _settings;
    private CancellationTokenSource? _readerCancellation;

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
        SubtitleTranslator subtitleTranslator,
        SecretStore secrets,
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
        _subtitleTranslator = subtitleTranslator;
        _secrets = secrets;
        _logger = logger;
        _subtitleReader.SentenceRead += OnSentenceRead;
        _subtitleReader.SubtitleCleared += OnSubtitleCleared;

        Profiles = new ObservableCollection<GameProfile>(profileStore.LoadAll());
        _selectedTranslatorProvider = SubtitleTranslator.Providers.Contains(settings.TranslatorProvider)
            ? settings.TranslatorProvider
            : SubtitleTranslator.Providers[0];
        _subtitleTranslator.UseProvider(_selectedTranslatorProvider, settings.GeminiModel);
        UpdateApiKeyHint();
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

    public IReadOnlyList<string> TranslatorProviders => SubtitleTranslator.Providers;

    [ObservableProperty]
    private string _selectedTranslatorProvider;

    [ObservableProperty]
    private string _apiKeyHint = string.Empty;

    [ObservableProperty]
    private string _lastTranslation = string.Empty;

    [ObservableProperty]
    private string _lastTranslationTiming = string.Empty;

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
        ClearTranslation();
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
        ClearTranslation();
        RestartSubtitleReader();
    }

    partial void OnSelectedTranslatorProviderChanged(string value)
    {
        SaveSettings(_settings with { TranslatorProvider = value });
        _subtitleTranslator.UseProvider(value, _settings.GeminiModel);
        ClearTranslation();
        UpdateApiKeyHint();
    }

    /// <summary>Gọi từ ô nhập key (PasswordBox không bind được). Key rỗng thì xóa key đã lưu.</summary>
    public void SaveApiKey(string apiKey)
    {
        _secrets.Set(SecretStore.GeminiApiKey, apiKey);
        UpdateApiKeyHint();
        Status = string.IsNullOrWhiteSpace(apiKey) ? "Đã xóa key Gemini." : "Đã lưu key Gemini (mã hóa theo tài khoản Windows).";
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        if (SelectedProfile is null)
            return;

        try
        {
            await _subtitleTranslator.ClearCacheAsync(SelectedProfile.Id);
            Status = $"Đã xóa cache bản dịch của {SelectedProfile.GameName}.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Xóa cache thất bại");
            Status = $"Xóa cache thất bại: {exception.Message}";
        }
    }

    private void UpdateApiKeyHint()
    {
        bool hasKey = !string.IsNullOrEmpty(_secrets.Get(SecretStore.GeminiApiKey));
        ApiKeyHint = hasKey ? "Đã có key Gemini. Nhập key mới để thay, để trống rồi Lưu để xóa." : "Chưa có key Gemini.";
    }

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
            LastSubtitleTiming = $"Đã sửa lại sau khi đọc thêm ({DateTime.Now:HH:mm:ss})";
        else
            LastSubtitleTiming = $"OCR {read.OcrDuration.TotalMilliseconds:F0} ms, trễ từ lúc câu hiện {read.Latency.TotalMilliseconds:F0} ms ({DateTime.Now:HH:mm:ss})";

        _ = TranslateSentenceAsync(read);
    }

    private async Task TranslateSentenceAsync(SubtitleRead read)
    {
        var profile = SelectedProfile;
        if (profile is null)
            return;

        try
        {
            var result = await _subtitleTranslator.TranslateAsync(profile, read);
            if (result is null)
                return;

            if (result.Error is not null)
            {
                // FR-13: báo ngắn trên overlay và cửa sổ chính, vòng đọc vẫn chạy tiếp.
                LastTranslation = string.Empty;
                LastTranslationTiming = $"Lỗi: {result.Error.Message}";
                ShowOverlaySubtitle($"⚠ {result.Error.Message}", read.TextBounds);
                return;
            }

            if (result.Translation is null)
            {
                LastTranslation = string.Empty;
                LastTranslationTiming = "Câu đọc ra như chữ rác, không dịch.";
                ShowOverlaySubtitle(null, null);
                return;
            }

            LastTranslation = result.Translation;
            string source = result.FromCache ? "cache" : SelectedTranslatorProvider;
            var total = read.Latency + result.TranslateDuration;
            LastTranslationTiming = read.IsCorrection
                ? $"Dịch lại câu đã sửa ({source}, {result.TranslateDuration.TotalMilliseconds:F0} ms)"
                : $"Dịch ({source}) {result.TranslateDuration.TotalMilliseconds:F0} ms, tổng từ lúc câu hiện {total.TotalMilliseconds:F0} ms";
            ShowOverlaySubtitle(result.Translation, read.TextBounds);
        }
        catch (Exception exception)
        {
            // Lỗi ngoài dự kiến (vd file cache hỏng): ghi log, không làm dừng vòng đọc.
            _logger.LogError(exception, "Dịch câu thất bại");
            LastTranslationTiming = $"Lỗi dịch: {exception.Message}";
        }
    }

    private void OnSubtitleCleared()
    {
        LastSubtitleTiming = $"Phụ đề đã tắt ({DateTime.Now:HH:mm:ss})";
        ClearTranslation();
    }

    private void ClearTranslation()
    {
        _subtitleTranslator.Reset();
        LastTranslation = string.Empty;
        ShowOverlaySubtitle(null, null);
    }

    /// <summary>Hiện chữ ở vùng phụ đề của hồ sơ, đè lên khung chữ gốc (tỉ lệ cửa sổ) nếu biết; text null thì ẩn.</summary>
    private void ShowOverlaySubtitle(string? text, LayoutRect? originalTextBounds)
    {
        var region = SelectedProfile?.Regions.FirstOrDefault(r => r.Type == RegionType.Subtitle);
        if (text is null || region is null)
        {
            _overlay.ShowTexts([], _settings.Overlay);
            return;
        }

        _overlay.ShowTexts([(region, text, originalTextBounds)], _settings.Overlay);
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
