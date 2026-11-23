using AppTranslator.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace AppTranslator.Capture.Windows;

/// <summary>
/// Chụp bằng DXGI Desktop Duplication: chụp màn hình chứa cửa sổ rồi cắt đúng khung cửa sổ.
/// Không có viền vàng như WGC, nhưng chụp những gì đang hiện trên màn hình: cửa sổ bị che thì ảnh bị che.
/// Overlay của app phải đặt WDA_EXCLUDEFROMCAPTURE để không lọt vào ảnh (Stage 2).
/// </summary>
public sealed class DesktopDuplicationCapture : IScreenCapture, IDisposable
{
    // Lần đầu sau khi tạo duplication phải chờ ảnh màn hình đầu tiên; các lần sau không chờ,
    // màn hình không đổi thì dùng lại ảnh đã chép trước đó.
    private const uint FirstFrameTimeoutMs = 500;
    private const uint NextFrameTimeoutMs = 0;

    private readonly ILogger<DesktopDuplicationCapture> _logger;
    private nint _monitor;
    private ID3D11Device? _device;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _desktopCopy;
    private TextureReader? _reader;
    private NativeMethods.Rect _outputBounds;

    public DesktopDuplicationCapture(ILogger<DesktopDuplicationCapture> logger)
    {
        _logger = logger;
    }

    public Task<Frame?> CaptureAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Capture(windowHandle));
    }

    public void Dispose()
    {
        StopDuplication();
    }

    private Frame? Capture(nint windowHandle)
    {
        if (!WindowFinder.IsAlive(windowHandle))
            return null;

        var windowBounds = NativeMethods.GetFrameBounds(windowHandle);
        if (windowBounds is null)
            return null;

        nint monitor = NativeMethods.MonitorFromWindow(windowHandle, NativeMethods.MonitorDefaultToNearest);
        if (monitor != _monitor || _duplication is null)
            StartDuplication(monitor);

        if (!UpdateDesktopCopy())
            return null;

        var area = ToOutputArea(windowBounds.Value);
        if (area.Width <= 0 || area.Height <= 0)
            return null;

        return _reader!.Read(_desktopCopy!, area);
    }

    private void StartDuplication(nint monitor)
    {
        StopDuplication();

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter).Success; adapterIndex++)
        {
            using (adapter)
            {
                for (uint outputIndex = 0; adapter!.EnumOutputs(outputIndex, out IDXGIOutput? output).Success; outputIndex++)
                {
                    using (output)
                    {
                        var description = output!.Description;
                        if (description.Monitor != monitor)
                            continue;

                        // Desktop Duplication đòi device tạo trên đúng card đồ họa nối với màn hình đó.
                        _device = Direct3DInterop.CreateDevice(adapter);
                        using var output1 = output.QueryInterface<IDXGIOutput1>();
                        _duplication = output1.DuplicateOutput(_device);
                        _reader = new TextureReader(_device);
                        _outputBounds = new NativeMethods.Rect
                        {
                            Left = description.DesktopCoordinates.Left,
                            Top = description.DesktopCoordinates.Top,
                            Right = description.DesktopCoordinates.Right,
                            Bottom = description.DesktopCoordinates.Bottom,
                        };
                        _monitor = monitor;
                        _logger.LogInformation("DXGI: bắt đầu nhân bản màn hình {Device}", description.DeviceName);
                        return;
                    }
                }
            }
        }

        throw new InvalidOperationException("Không tìm thấy màn hình DXGI chứa cửa sổ nguồn.");
    }

    private void StopDuplication()
    {
        _reader?.Dispose();
        _desktopCopy?.Dispose();
        _duplication?.Dispose();
        _device?.Dispose();
        _reader = null;
        _desktopCopy = null;
        _duplication = null;
        _device = null;
        _monitor = 0;
    }

    /// <summary>Chép ảnh màn hình mới nhất (nếu có) vào texture riêng; false nếu chưa có ảnh nào.</summary>
    private bool UpdateDesktopCopy()
    {
        uint timeout = _desktopCopy is null ? FirstFrameTimeoutMs : NextFrameTimeoutMs;
        var result = _duplication!.AcquireNextFrame(timeout, out _, out IDXGIResource? desktopResource);

        if (result.Code == Vortice.DXGI.ResultCode.WaitTimeout.Code)
            return _desktopCopy is not null;

        if (result.Code == Vortice.DXGI.ResultCode.AccessLost.Code)
        {
            // Đổi độ phân giải, màn hình UAC, game vào fullscreen... phải tạo lại duplication ở lần chụp sau.
            _logger.LogWarning("DXGI: mất quyền nhân bản màn hình, sẽ tạo lại");
            StopDuplication();
            return false;
        }

        result.CheckError();
        try
        {
            using var desktopTexture = desktopResource!.QueryInterface<ID3D11Texture2D>();
            EnsureDesktopCopy(desktopTexture.Description);
            _device!.ImmediateContext.CopyResource(_desktopCopy!, desktopTexture);
            return true;
        }
        finally
        {
            desktopResource?.Dispose();
            _duplication.ReleaseFrame();
        }
    }

    private void EnsureDesktopCopy(Texture2DDescription source)
    {
        if (_desktopCopy is not null && _desktopCopy.Description.Width == source.Width && _desktopCopy.Description.Height == source.Height)
            return;

        _desktopCopy?.Dispose();
        _desktopCopy = _device!.CreateTexture2D(new Texture2DDescription
        {
            Width = source.Width,
            Height = source.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = source.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.None,
        });
    }

    /// <summary>Đổi khung cửa sổ (tọa độ màn hình ảo) sang tọa độ trong ảnh của màn hình đang nhân bản, cắt phần tràn ra ngoài.</summary>
    private PixelRect ToOutputArea(NativeMethods.Rect window)
    {
        int left = Math.Max(window.Left, _outputBounds.Left) - _outputBounds.Left;
        int top = Math.Max(window.Top, _outputBounds.Top) - _outputBounds.Top;
        int right = Math.Min(window.Right, _outputBounds.Right) - _outputBounds.Left;
        int bottom = Math.Min(window.Bottom, _outputBounds.Bottom) - _outputBounds.Top;
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
