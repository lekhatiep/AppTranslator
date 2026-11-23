using AppTranslator.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace AppTranslator.Capture.Windows;

/// <summary>
/// Chụp bằng Windows.Graphics.Capture: chụp đúng cửa sổ kể cả khi bị che.
/// Trên Windows 10, Windows luôn vẽ viền vàng quanh cửa sổ đang bị chụp và không tắt được.
/// Giữ một phiên chụp cho mỗi cửa sổ và luôn giữ khung hình mới nhất; chỉ chép về CPU khi gọi CaptureAsync.
/// </summary>
public sealed class WindowsGraphicsCapture : IScreenCapture, IDisposable
{
    private const int FramePoolBufferCount = 2;
    private const DirectXPixelFormat PixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(2);

    private readonly ILogger<WindowsGraphicsCapture> _logger;
    private readonly object _frameLock = new();
    private ID3D11Device? _device;
    private IDirect3DDevice? _winRtDevice;
    private TextureReader? _reader;

    private nint _windowHandle;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private Direct3D11CaptureFrame? _latestFrame;
    private SizeInt32 _poolSize;
    private TaskCompletionSource _firstFrameArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WindowsGraphicsCapture(ILogger<WindowsGraphicsCapture> logger)
    {
        _logger = logger;
    }

    public async Task<Frame?> CaptureAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        if (!WindowFinder.IsAlive(windowHandle))
        {
            StopSession();
            return null;
        }

        if (windowHandle != _windowHandle || _session is null)
            StartSession(windowHandle);

        var firstFrameTask = _firstFrameArrived.Task;
        var completed = await Task.WhenAny(firstFrameTask, Task.Delay(FirstFrameTimeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (completed != firstFrameTask)
        {
            // Cửa sổ thu nhỏ hoặc chưa vẽ gì thì WGC không gửi khung hình.
            _logger.LogWarning("WGC: chưa nhận được khung hình sau {Timeout}", FirstFrameTimeout);
            return null;
        }

        return ReadLatestFrame();
    }

    /// <summary>Dừng phiên chụp để Windows bỏ viền vàng, ví dụ khi người dùng chuyển sang engine khác hoặc cửa sổ khác.</summary>
    public void Stop()
    {
        StopSession();
    }

    public void Dispose()
    {
        StopSession();
        _reader?.Dispose();
        _winRtDevice?.Dispose();
        _device?.Dispose();
    }

    private void StartSession(nint windowHandle)
    {
        StopSession();
        EnsureDevice();

        _item = Direct3DInterop.CreateCaptureItemForWindow(windowHandle);
        _item.Closed += OnItemClosed;
        _poolSize = _item.Size;
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winRtDevice!, PixelFormat, FramePoolBufferCount, _poolSize);
        _framePool.FrameArrived += OnFrameArrived;

        _session = _framePool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = false;
        _session.StartCapture();

        _windowHandle = windowHandle;
        _logger.LogInformation("WGC: bắt đầu chụp cửa sổ {Handle} ({Width}x{Height})", windowHandle, _poolSize.Width, _poolSize.Height);
    }

    private void StopSession()
    {
        if (_session is null)
            return;

        _framePool!.FrameArrived -= OnFrameArrived;
        _item!.Closed -= OnItemClosed;
        _session.Dispose();
        _framePool.Dispose();

        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }

        _session = null;
        _framePool = null;
        _item = null;
        _windowHandle = 0;
        _firstFrameArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void EnsureDevice()
    {
        if (_device is not null)
            return;

        _device = Direct3DInterop.CreateDevice(null);
        _winRtDevice = Direct3DInterop.CreateWinRtDevice(_device);
        _reader = new TextureReader(_device);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        var frame = sender.TryGetNextFrame();
        if (frame is null)
            return;

        // Cửa sổ đổi kích thước: tạo lại bộ đệm theo kích thước mới cho các khung sau.
        if (frame.ContentSize.Width != _poolSize.Width || frame.ContentSize.Height != _poolSize.Height)
        {
            _poolSize = frame.ContentSize;
            sender.Recreate(_winRtDevice!, PixelFormat, FramePoolBufferCount, _poolSize);
        }

        // Không được chờ khóa ở đây: WGC gọi sự kiện này khi đang giữ khóa của D3D device, còn ReadLatestFrame
        // giữ _frameLock rồi mới gọi D3D, chờ nhau sẽ deadlock. Đang có người đọc thì bỏ khung này, khung sau sẽ thay.
        if (!Monitor.TryEnter(_frameLock))
        {
            frame.Dispose();
            return;
        }

        try
        {
            _latestFrame?.Dispose();
            _latestFrame = frame;
        }
        finally
        {
            Monitor.Exit(_frameLock);
        }

        _firstFrameArrived.TrySetResult();
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        _logger.LogInformation("WGC: cửa sổ nguồn đã đóng");
    }

    private Frame? ReadLatestFrame()
    {
        lock (_frameLock)
        {
            if (_latestFrame is null)
                return null;

            // Bộ đệm có thể lớn hơn nội dung (ngay sau khi đổi kích thước), chỉ lấy phần nội dung.
            var content = _latestFrame.ContentSize;
            using var texture = Direct3DInterop.GetTexture(_latestFrame.Surface);
            var description = texture.Description;
            int width = Math.Min(content.Width, (int)description.Width);
            int height = Math.Min(content.Height, (int)description.Height);
            return _reader!.Read(texture, new PixelRect(0, 0, width, height));
        }
    }
}
