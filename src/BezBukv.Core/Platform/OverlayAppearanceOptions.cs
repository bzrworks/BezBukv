namespace BezBukv.Core.Platform;

/// <summary>
/// Defines platform-independent appearance options for the keyboard peek overlay.
/// </summary>
public sealed record OverlayAppearanceOptions
{
    public const double DefaultOpacity = 1.0;
    public const double DefaultScale = 1.0;
    public const bool DefaultAnimationEnabled = false;
    public const int DefaultAnimationDurationMs = 300;

    public OverlayAppearanceOptions(
        double opacity = DefaultOpacity,
        double scale = DefaultScale,
        bool animationEnabled = DefaultAnimationEnabled,
        int animationDurationMs = DefaultAnimationDurationMs)
    {
        if (!double.IsFinite(opacity) || opacity <= 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(opacity),
                opacity,
                "Opacity must be finite and greater than zero and at most one.");
        }

        if (!double.IsFinite(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                "Scale must be finite and greater than zero.");
        }

        if (animationDurationMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(animationDurationMs),
                animationDurationMs,
                "Animation duration must be greater than zero.");
        }

        Opacity = opacity;
        Scale = scale;
        AnimationEnabled = animationEnabled;
        AnimationDurationMs = animationDurationMs;
    }

    public double Opacity { get; }

    public double Scale { get; }

    public bool AnimationEnabled { get; }

    public int AnimationDurationMs { get; }
}
