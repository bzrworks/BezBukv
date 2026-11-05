namespace BezBukv.Core.Platform;

public static class ClipboardTextLimit
{
    public const int Utf16BytesPerCodeUnit = sizeof(char);
    public const int DefaultMaxCharacters = 32_000;
    public const int DefaultMaxBytes = DefaultMaxCharacters * Utf16BytesPerCodeUnit;

    public static ClipboardTextLimitOutcome EvaluateUtf16(
        string text,
        int maxBytes = DefaultMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes),
                "Clipboard text limit must be positive.");
        }

        var textSizeInBytes = (long)text.Length * Utf16BytesPerCodeUnit;

        return textSizeInBytes <= maxBytes
            ? ClipboardTextLimitOutcome.WithinLimit
            : ClipboardTextLimitOutcome.ExceedsLimit;
    }
}

public enum ClipboardTextLimitOutcome
{
    WithinLimit,
    ExceedsLimit,
}
