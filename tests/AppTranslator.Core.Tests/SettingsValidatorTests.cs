using AppTranslator.Core.Settings;

namespace AppTranslator.Core.Tests;

public class SettingsValidatorTests
{
    [Fact]
    public void Defaults_Valid()
    {
        Assert.Empty(SettingsValidator.Validate(new AppSettings()));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(100)]
    public void FontSizeOutOfRange_Error(double fontSize)
    {
        var settings = new AppSettings { Overlay = new OverlaySettings { FontSize = fontSize } };

        Assert.Contains(SettingsValidator.Validate(settings), error => error.Contains("Cỡ chữ"));
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("#00000")]
    [InlineData("#GG0000")]
    [InlineData("black")]
    public void BadColor_Error(string color)
    {
        var settings = new AppSettings { Overlay = new OverlaySettings { BackgroundColor = color } };

        Assert.Contains(SettingsValidator.Validate(settings), error => error.Contains("Màu nền"));
    }

    [Fact]
    public void OpacityTooLow_Error()
    {
        var settings = new AppSettings { Overlay = new OverlaySettings { BackgroundOpacity = 0.1 } };

        Assert.Contains(SettingsValidator.Validate(settings), error => error.Contains("Độ mờ"));
    }

    [Fact]
    public void BadHotkey_Error()
    {
        var settings = new AppSettings { Hotkeys = new HotkeySettings { TranslateArea = "Q" } };

        Assert.Contains(SettingsValidator.Validate(settings), error => error.Contains("Khoanh vùng dịch"));
    }

    [Fact]
    public void DuplicateHotkeys_Error_EvenWithDifferentSpelling()
    {
        var settings = new AppSettings { Hotkeys = new HotkeySettings { ToggleTranslation = "Ctrl+Alt+T", SelectRegion = "alt + ctrl + t" } };

        Assert.Contains(SettingsValidator.Validate(settings), error => error.Contains("trùng"));
    }

    [Fact]
    public void EmptyModel_Error()
    {
        Assert.Contains(SettingsValidator.Validate(new AppSettings { GeminiModel = " " }), error => error.Contains("model"));
    }
}
