using Cs2Simulator.Runtime;
using FluentAssertions;

namespace Cs2Simulator.Tests;

public sealed class SpeedTests
{
    [Theory]
    [InlineData("max")]
    [InlineData("MAX")]
    [InlineData("Max")]
    public void Parse_MaxToken_IsMax(string text)
    {
        var speed = Speed.Parse(text);

        speed.IsMax.Should().BeTrue();
    }

    [Theory]
    [InlineData("1", 1d)]
    [InlineData("2", 2d)]
    [InlineData("5", 5d)]
    [InlineData("2x", 2d)]
    [InlineData("5X", 5d)]
    public void Parse_NumericOrXSuffix_UsesMultiplier(string text, double expected)
    {
        var speed = Speed.Parse(text);

        speed.IsMax.Should().BeFalse();
        speed.Multiplier.Should().Be(expected);
    }

    [Fact]
    public void Parse_UnknownValue_Throws()
    {
        var act = () => Speed.Parse("ma");

        act.Should().Throw<FormatException>().WithMessage("*ma*");
    }
}
