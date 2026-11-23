using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;

namespace AppTranslator.App;

/// <summary>FR-02: kéo chuột trên ảnh chụp cửa sổ để khoanh vùng phụ đề và vùng lựa chọn.</summary>
public partial class RegionSelectorWindow : Window
{
    // Vùng nhỏ hơn mức này coi như bấm nhầm, không đổi vùng đang có.
    private const int MinimumRegionSize = 8;
    private const double RegionBorderThickness = 3;

    private readonly GameProfile _profile;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly Dictionary<RegionType, Region> _regions = new();
    private readonly Dictionary<RegionType, Rectangle> _rectangles = new();
    private Point? _dragStart;

    public RegionSelectorWindow(BitmapSource screenshot, GameProfile profile)
    {
        InitializeComponent();
        _profile = profile;
        _imageWidth = screenshot.PixelWidth;
        _imageHeight = screenshot.PixelHeight;

        ScreenshotImage.Source = screenshot;
        ScreenshotImage.Width = _imageWidth;
        ScreenshotImage.Height = _imageHeight;
        SelectionCanvas.Width = _imageWidth;
        SelectionCanvas.Height = _imageHeight;

        RegionTypeBox.ItemsSource = Enum.GetValues<RegionType>();
        RegionTypeBox.SelectedIndex = 0;

        foreach (var region in profile.Regions)
        {
            _regions[region.Type] = region;
            DrawRegion(region.Type, region.ToPixelRect(_imageWidth, _imageHeight));
        }
    }

    /// <summary>Hồ sơ với vùng đã chọn; chỉ có giá trị khi cửa sổ đóng bằng nút Lưu.</summary>
    public GameProfile? Result { get; private set; }

    private RegionType SelectedType => (RegionType)RegionTypeBox.SelectedItem;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(SelectionCanvas);
        SelectionCanvas.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null)
            return;

        DrawRegion(SelectedType, ToPixelRect(_dragStart.Value, e.GetPosition(SelectionCanvas)));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
            return;

        SelectionCanvas.ReleaseMouseCapture();
        var area = ToPixelRect(_dragStart.Value, e.GetPosition(SelectionCanvas));
        _dragStart = null;

        if (area.Width < MinimumRegionSize || area.Height < MinimumRegionSize)
        {
            RestoreRegion(SelectedType);
            return;
        }

        var existing = _regions.GetValueOrDefault(SelectedType) ?? new Region { Type = SelectedType };
        _regions[SelectedType] = existing.WithPixelRect(area, _imageWidth, _imageHeight);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Result = _profile with { Regions = _regions.Values.OrderBy(r => r.Type).ToList() };
        DialogResult = true;
    }

    private PixelRect ToPixelRect(Point start, Point end)
    {
        int left = (int)Math.Clamp(Math.Min(start.X, end.X), 0, _imageWidth);
        int top = (int)Math.Clamp(Math.Min(start.Y, end.Y), 0, _imageHeight);
        int right = (int)Math.Clamp(Math.Max(start.X, end.X), 0, _imageWidth);
        int bottom = (int)Math.Clamp(Math.Max(start.Y, end.Y), 0, _imageHeight);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    private void RestoreRegion(RegionType type)
    {
        if (_regions.TryGetValue(type, out var region))
        {
            DrawRegion(type, region.ToPixelRect(_imageWidth, _imageHeight));
            return;
        }

        if (_rectangles.Remove(type, out var rectangle))
            SelectionCanvas.Children.Remove(rectangle);
    }

    private void DrawRegion(RegionType type, PixelRect area)
    {
        if (!_rectangles.TryGetValue(type, out var rectangle))
        {
            rectangle = new Rectangle
            {
                Stroke = type == RegionType.Subtitle ? Brushes.Cyan : Brushes.Yellow,
                StrokeThickness = RegionBorderThickness,
            };
            _rectangles[type] = rectangle;
            SelectionCanvas.Children.Add(rectangle);
        }

        Canvas.SetLeft(rectangle, area.X);
        Canvas.SetTop(rectangle, area.Y);
        rectangle.Width = area.Width;
        rectangle.Height = area.Height;
    }
}
