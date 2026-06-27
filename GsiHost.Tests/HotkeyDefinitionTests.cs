using FluentAssertions;
using GsiHost.Services;

namespace GsiHost.Tests;

/// <summary>
/// Unit tests for the pure hotkey-string parser <see cref="HotkeyDefinition.TryParse"/>.
/// Covers the valid default MVP bindings plus invalid inputs. These do not exercise
/// the Win32 RegisterHotKey message loop.
/// </summary>
public class HotkeyDefinitionTests
{
    [Fact]
    public void TryParse_DefaultMvpPauseBinding_ParsesCtrlAltP()
    {
        var parsed = HotkeyDefinition.TryParse("Ctrl+Alt+P");

        parsed.Should().NotBeNull();
        parsed!.Value.Modifiers.Should().Be(HotkeyDefinition.ModControl | HotkeyDefinition.ModAlt);
        parsed.Value.VirtualKey.Should().Be('P');
    }

    [Fact]
    public void TryParse_DefaultMvpResumeBinding_ParsesCtrlAltR()
    {
        var parsed = HotkeyDefinition.TryParse("Ctrl+Alt+R");

        parsed.Should().NotBeNull();
        parsed!.Value.Modifiers.Should().Be(HotkeyDefinition.ModControl | HotkeyDefinition.ModAlt);
        parsed.Value.VirtualKey.Should().Be('R');
    }

    [Fact]
    public void TryParse_DefaultMvpMuteBinding_ParsesCtrlAltM()
    {
        var parsed = HotkeyDefinition.TryParse("Ctrl+Alt+M");

        parsed.Should().NotBeNull();
        parsed!.Value.Modifiers.Should().Be(HotkeyDefinition.ModControl | HotkeyDefinition.ModAlt);
        parsed.Value.VirtualKey.Should().Be('M');
    }

    [Fact]
    public void TryParse_FunctionKeyWithShift_ParsesShiftF9()
    {
        var parsed = HotkeyDefinition.TryParse("Shift+F9");

        parsed.Should().NotBeNull();
        parsed!.Value.Modifiers.Should().Be(HotkeyDefinition.ModShift);
        // VK_F9 = 0x78 (0x70 base for F1 + 8).
        parsed.Value.VirtualKey.Should().Be(0x70 + 9 - 1);
    }

    [Fact]
    public void TryParse_IsCaseInsensitiveAndAcceptsControlAlias()
    {
        var lower = HotkeyDefinition.TryParse("ctrl+alt+p");
        var alias = HotkeyDefinition.TryParse("Control+Alt+P");

        lower.Should().NotBeNull();
        lower!.Value.Modifiers.Should().Be(HotkeyDefinition.ModControl | HotkeyDefinition.ModAlt);
        lower.Value.VirtualKey.Should().Be('P');
        alias.Should().NotBeNull();
        alias!.Value.Should().Be(lower.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Alt+Shift+Win")]
    [InlineData("Ctrl+F99")]
    [InlineData("garbage")]
    [InlineData("Ctrl+Nonsense")]
    public void TryParse_InvalidInput_ReturnsNull(string? text)
    {
        var parsed = HotkeyDefinition.TryParse(text);

        parsed.Should().BeNull();
    }
}
