using System.Runtime.InteropServices;
using AppTranslator.Core.Abstractions;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace AppTranslator.Capture.Windows;

/// <summary>
/// Chép một vùng của texture GPU (BGRA) sang <see cref="Frame"/> trên CPU qua một staging texture dùng lại.
/// Dùng chung cho cả hai engine chụp.
/// </summary>
internal sealed class TextureReader : IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private ID3D11Texture2D? _staging;
    private int _stagingWidth;
    private int _stagingHeight;

    public TextureReader(ID3D11Device device)
    {
        _device = device;
        _context = device.ImmediateContext;
    }

    public Frame Read(ID3D11Texture2D source, PixelRect area)
    {
        EnsureStaging(area.Width, area.Height);

        var sourceBox = new Box(area.X, area.Y, 0, area.X + area.Width, area.Y + area.Height, 1);
        _context.CopySubresourceRegion(_staging!, 0, 0, 0, 0, source, 0, sourceBox);

        var mapped = _context.Map(_staging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            return CopyRows(mapped, area.Width, area.Height);
        }
        finally
        {
            _context.Unmap(_staging!, 0);
        }
    }

    public void Dispose()
    {
        _staging?.Dispose();
        _context.Dispose();
    }

    private static Frame CopyRows(MappedSubresource mapped, int width, int height)
    {
        // RowPitch của GPU thường lớn hơn width * 4 nên phải chép từng dòng.
        int stride = width * BytesPerPixel;
        var pixels = new byte[stride * height];
        for (int row = 0; row < height; row++)
        {
            nint rowStart = mapped.DataPointer + row * (nint)mapped.RowPitch;
            Marshal.Copy(rowStart, pixels, row * stride, stride);
        }

        return new Frame(pixels, width, height, stride, DateTimeOffset.Now);
    }

    private void EnsureStaging(int width, int height)
    {
        if (_staging is not null && _stagingWidth == width && _stagingHeight == height)
            return;

        _staging?.Dispose();
        _staging = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
        });
        _stagingWidth = width;
        _stagingHeight = height;
    }
}
