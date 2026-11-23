using AppTranslator.Core.Settings;

namespace AppTranslator.Core.Tests;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void TryParse_DefaultToggleHotkey_ReturnsCtrlAltT()
    {
        var gesture = HotkeyGesture.TryParse("Ctrl+Alt+T");

        Assert.Equal(new HotkeyGesture(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'T'), gesture);
    }

    [Theory]
    [InlineData("ctrl + shift + r", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 'R')]
    [InlineData("Alt+5", HotkeyModifiers.Alt, '5')]
    [InlineData("Win+F1", HotkeyModifiers.Win, 0x70)]
    [InlineData("Ctrl+F12", HotkeyModifiers.Ctrl, 0x7B)]
    public void TryParse_ValidGestures_IgnoresCaseAndSpaces(string text, HotkeyModifiers modifiers, int virtualKey)
    {
        var gesture = HotkeyGesture.TryParse(text);

        Assert.Equal(new HotkeyGesture(modifiers, virtualKey), gesture);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("T")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Space")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+F0")]
    [InlineData("Hyper+T")]
    [InlineData("None+T")]
    [InlineData("Ctrl+Alt")]
    public void TryParse_InvalidGestures_ReturnsNull(string? text)
    {
        Assert.Null(HotkeyGesture.TryParse(text));
    }
}
