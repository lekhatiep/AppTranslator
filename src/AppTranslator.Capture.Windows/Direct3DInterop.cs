using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace AppTranslator.Capture.Windows;

/// <summary>Cầu nối giữa D3D11 (Vortice) và các kiểu WinRT của Windows.Graphics.Capture.</summary>
internal static class Direct3DInterop
{
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, in Guid iid);

        nint CreateForMonitor(nint monitor, in Guid iid);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        nint GetInterface(in Guid iid);
    }

    /// <summary>Tạo D3D11 device hỗ trợ BGRA; adapter null = card mặc định.</summary>
    public static ID3D11Device CreateDevice(IDXGIAdapter? adapter)
    {
        // Có adapter cụ thể thì DriverType phải là Unknown (quy định của D3D11CreateDevice).
        var driverType = adapter is null ? DriverType.Hardware : DriverType.Unknown;
        D3D11.D3D11CreateDevice(adapter, driverType, DeviceCreationFlags.BgraSupport, null, out ID3D11Device? device).CheckError();

        // Khung hình WGC đến trên thread khác với thread đọc ảnh, nên bật khóa đa luồng của D3D11.
        using (var multithread = device!.QueryInterface<ID3D11Multithread>())
        {
            multithread.SetMultithreadProtected(true);
        }

        return device;
    }

    public static IDirect3DDevice CreateWinRtDevice(ID3D11Device device)
    {
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out nint graphicsDevice));
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);
        }
        finally
        {
            Marshal.Release(graphicsDevice);
        }
    }

    public static GraphicsCaptureItem CreateCaptureItemForWindow(nint windowHandle)
    {
        using var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
        try
        {
            nint itemPointer = interop.CreateForWindow(windowHandle, GraphicsCaptureItemIid);
            try
            {
                return GraphicsCaptureItem.FromAbi(itemPointer);
            }
            finally
            {
                Marshal.Release(itemPointer);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(interop);
        }
    }

    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        nint surfacePointer = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(surfacePointer);
        try
        {
            nint texturePointer = access.GetInterface(typeof(ID3D11Texture2D).GUID);
            return new ID3D11Texture2D(texturePointer);
        }
        finally
        {
            Marshal.ReleaseComObject(access);
            Marshal.Release(surfacePointer);
        }
    }
}
