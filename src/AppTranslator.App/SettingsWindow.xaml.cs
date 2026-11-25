using System.Globalization;
using System.Windows;
using AppTranslator.Core.Settings;

namespace AppTranslator.App;

/// <summary>FR-11: sửa cỡ chữ, màu/độ mờ nền overlay, phím tắt và model Gemini. Kiểm tra bằng <see cref="SettingsValidator"/>.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _original = settings;

        FontSizeBox.Text = settings.Overlay.FontSize.ToString(CultureInfo.InvariantCulture);
        BackgroundColorBox.Text = settings.Overlay.BackgroundColor;
        OpacitySlider.Value = settings.Overlay.BackgroundOpacity;
        ToggleHotkeyBox.Text = settings.Hotkeys.ToggleTranslation;
        SelectRegionHotkeyBox.Text = settings.Hotkeys.SelectRegion;
        TranslateAreaHotkeyBox.Text = settings.Hotkeys.TranslateArea;
        GeminiModelBox.Text = settings.GeminiModel;
        OpacityText.Text = settings.Overlay.BackgroundOpacity.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Cài đặt mới; chỉ có giá trị khi ShowDialog trả về true.</summary>
    public AppSettings? Result { get; private set; }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Gọi cả lúc InitializeComponent, trước khi OpacityText được tạo.
        if (OpacityText is not null)
            OpacityText.Text = e.NewValue.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();
        // Chấp nhận cả dấu phẩy thập phân kiểu Việt ("28,5").
        string fontText = FontSizeBox.Text.Trim().Replace(',', '.');
        if (!double.TryParse(fontText, NumberStyles.Float, CultureInfo.InvariantCulture, out double fontSize))
        {
            errors.Add("Cỡ chữ phải là số.");
            fontSize = _original.Overlay.FontSize;
        }

        var settings = _original with
        {
            GeminiModel = GeminiModelBox.Text.Trim(),
            Overlay = _original.Overlay with
            {
                FontSize = fontSize,
                BackgroundColor = BackgroundColorBox.Text.Trim(),
                BackgroundOpacity = Math.Round(OpacitySlider.Value, 2),
            },
            Hotkeys = _original.Hotkeys with
            {
                ToggleTranslation = ToggleHotkeyBox.Text.Trim(),
                SelectRegion = SelectRegionHotkeyBox.Text.Trim(),
                TranslateArea = TranslateAreaHotkeyBox.Text.Trim(),
            },
        };

        errors.AddRange(SettingsValidator.Validate(settings));
        if (errors.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, errors);
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = settings;
        DialogResult = true;
    }
}
