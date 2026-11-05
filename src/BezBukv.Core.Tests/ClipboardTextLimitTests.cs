using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class ClipboardTextLimitTests
{
    private const int DefaultMaxUtf16CodeUnits =
        ClipboardTextLimit.DefaultMaxBytes / ClipboardTextLimit.Utf16BytesPerCodeUnit;

    [Fact]
    public void TextBelowDefaultLimitIsWithinLimit()
    {
        var text = new string('a', DefaultMaxUtf16CodeUnits - 1);

        var outcome = ClipboardTextLimit.EvaluateUtf16(text);

        Assert.Equal(ClipboardTextLimitOutcome.WithinLimit, outcome);
    }

    [Fact]
    public void TextExactlyAtDefaultLimitIsWithinLimit()
    {
        var text = new string('a', DefaultMaxUtf16CodeUnits);

        var outcome = ClipboardTextLimit.EvaluateUtf16(text);

        Assert.Equal(ClipboardTextLimitOutcome.WithinLimit, outcome);
    }

    [Fact]
    public void TextAboveDefaultLimitExceedsLimit()
    {
        var text = new string('a', DefaultMaxUtf16CodeUnits + 1);

        var outcome = ClipboardTextLimit.EvaluateUtf16(text);

        Assert.Equal(ClipboardTextLimitOutcome.ExceedsLimit, outcome);
    }
}
