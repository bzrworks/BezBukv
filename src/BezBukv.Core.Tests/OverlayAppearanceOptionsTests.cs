using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class OverlayAppearanceOptionsTests
{
    [Fact]
    public void DefaultsPreserveCurrentOverlayAppearance()
    {
        var options = new OverlayAppearanceOptions();

        Assert.Equal(1.0, options.Opacity);
        Assert.Equal(1.0, options.Scale);
        Assert.False(options.AnimationEnabled);
        Assert.Equal(300, options.AnimationDurationMs);
    }

    [Fact]
    public void ValidCustomValuesArePreserved()
    {
        var options = new OverlayAppearanceOptions(
            opacity: 0.75,
            scale: 1.25,
            animationEnabled: true,
            animationDurationMs: 280);

        Assert.Equal(0.75, options.Opacity);
        Assert.Equal(1.25, options.Scale);
        Assert.True(options.AnimationEnabled);
        Assert.Equal(280, options.AnimationDurationMs);
    }

    [Theory]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(1.01)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void InvalidOpacityIsRejected(double opacity)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OverlayAppearanceOptions(opacity: opacity));

        Assert.Equal("opacity", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void InvalidScaleIsRejected(double scale)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OverlayAppearanceOptions(scale: scale));

        Assert.Equal("scale", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidAnimationDurationIsRejected(int animationDurationMs)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new OverlayAppearanceOptions(
                animationDurationMs: animationDurationMs));

        Assert.Equal("animationDurationMs", exception.ParamName);
    }
}
