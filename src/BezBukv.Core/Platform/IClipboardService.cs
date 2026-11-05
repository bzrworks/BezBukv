namespace BezBukv.Core.Platform;

public interface IClipboardService
{
    ClipboardStateToken CaptureStateToken();

    bool HasChanged(ClipboardStateToken stateToken);

    ValueTask<ClipboardSnapshotCaptureResult> CaptureSnapshotAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ClipboardSnapshotRestoreResult> RestoreSnapshotAsync(
        IClipboardSnapshot snapshot,
        CancellationToken cancellationToken = default);

    ValueTask<ClipboardTextReadResult> ReadUnicodeTextAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ClipboardTextWriteResult> SetUnicodeTextAsync(
        string text,
        CancellationToken cancellationToken = default);
}

public readonly record struct ClipboardStateToken(ulong Value);

public interface IClipboardSnapshot : IDisposable;

[Flags]
public enum ClipboardContentKinds
{
    None = 0,
    Text = 1 << 0,
    Image = 1 << 1,
    Html = 1 << 2,
    RichText = 1 << 3,
    Files = 1 << 4,
}

public enum ClipboardSnapshotCaptureOutcome
{
    Captured,
    ClipboardUnavailable,
}

public sealed record ClipboardSnapshotCaptureResult
{
    private ClipboardSnapshotCaptureResult(
        ClipboardSnapshotCaptureOutcome outcome,
        IClipboardSnapshot? snapshot,
        ClipboardContentKinds capturedContent,
        ClipboardContentKinds nonCapturedContent,
        int capturedFormatCount,
        int nonCapturedFormatCount)
    {
        Outcome = outcome;
        Snapshot = snapshot;
        CapturedContent = capturedContent;
        NonCapturedContent = nonCapturedContent;
        CapturedFormatCount = capturedFormatCount;
        NonCapturedFormatCount = nonCapturedFormatCount;
    }

    public ClipboardSnapshotCaptureOutcome Outcome { get; }

    public IClipboardSnapshot? Snapshot { get; }

    public ClipboardContentKinds CapturedContent { get; }

    public ClipboardContentKinds NonCapturedContent { get; }

    public int CapturedFormatCount { get; }

    public int NonCapturedFormatCount { get; }

    public static ClipboardSnapshotCaptureResult FromSnapshot(
        IClipboardSnapshot snapshot,
        ClipboardContentKinds capturedContent,
        ClipboardContentKinds nonCapturedContent,
        int capturedFormatCount,
        int nonCapturedFormatCount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(capturedFormatCount);
        ArgumentOutOfRangeException.ThrowIfNegative(nonCapturedFormatCount);

        return new ClipboardSnapshotCaptureResult(
            ClipboardSnapshotCaptureOutcome.Captured,
            snapshot,
            capturedContent,
            nonCapturedContent,
            capturedFormatCount,
            nonCapturedFormatCount);
    }

    public static ClipboardSnapshotCaptureResult ClipboardUnavailable { get; } =
        new(
            ClipboardSnapshotCaptureOutcome.ClipboardUnavailable,
            null,
            ClipboardContentKinds.None,
            ClipboardContentKinds.None,
            0,
            0);
}

public enum ClipboardSnapshotRestoreOutcome
{
    Success,
    FormatsSkipped,
    ClipboardUnavailable,
}

public sealed record ClipboardSnapshotRestoreResult
{
    private ClipboardSnapshotRestoreResult(
        ClipboardSnapshotRestoreOutcome outcome,
        ClipboardContentKinds restoredContent,
        ClipboardContentKinds skippedContent,
        int restoredFormatCount,
        int skippedFormatCount)
    {
        Outcome = outcome;
        RestoredContent = restoredContent;
        SkippedContent = skippedContent;
        RestoredFormatCount = restoredFormatCount;
        SkippedFormatCount = skippedFormatCount;
    }

    public ClipboardSnapshotRestoreOutcome Outcome { get; }

    public ClipboardContentKinds RestoredContent { get; }

    public ClipboardContentKinds SkippedContent { get; }

    public int RestoredFormatCount { get; }

    public int SkippedFormatCount { get; }

    public static ClipboardSnapshotRestoreResult FromCompletedRestore(
        ClipboardContentKinds restoredContent,
        ClipboardContentKinds skippedContent,
        int restoredFormatCount,
        int skippedFormatCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(restoredFormatCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedFormatCount);

        return new ClipboardSnapshotRestoreResult(
            skippedFormatCount == 0
                ? ClipboardSnapshotRestoreOutcome.Success
                : ClipboardSnapshotRestoreOutcome.FormatsSkipped,
            restoredContent,
            skippedContent,
            restoredFormatCount,
            skippedFormatCount);
    }

    public static ClipboardSnapshotRestoreResult FromClipboardUnavailable(
        ClipboardContentKinds skippedContent,
        int skippedFormatCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skippedFormatCount);

        return new ClipboardSnapshotRestoreResult(
            ClipboardSnapshotRestoreOutcome.ClipboardUnavailable,
            ClipboardContentKinds.None,
            skippedContent,
            0,
            skippedFormatCount);
    }
}

public enum ClipboardTextReadOutcome
{
    Text,
    NoPlainText,
    ExceedsLimit,
    ClipboardUnavailable,
}

public sealed record ClipboardTextReadResult
{
    private ClipboardTextReadResult(ClipboardTextReadOutcome outcome, string? text)
    {
        Outcome = outcome;
        Text = text;
    }

    public ClipboardTextReadOutcome Outcome { get; }

    public string? Text { get; }

    public static ClipboardTextReadResult FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new ClipboardTextReadResult(ClipboardTextReadOutcome.Text, text);
    }

    public static ClipboardTextReadResult NoPlainText { get; } =
        new(ClipboardTextReadOutcome.NoPlainText, null);

    public static ClipboardTextReadResult ExceedsLimit { get; } =
        new(ClipboardTextReadOutcome.ExceedsLimit, null);

    public static ClipboardTextReadResult ClipboardUnavailable { get; } =
        new(ClipboardTextReadOutcome.ClipboardUnavailable, null);
}

public enum ClipboardTextWriteOutcome
{
    Success,
    ClipboardUnavailable,
}

public sealed record ClipboardTextWriteResult
{
    private ClipboardTextWriteResult(ClipboardTextWriteOutcome outcome)
    {
        Outcome = outcome;
    }

    public ClipboardTextWriteOutcome Outcome { get; }

    public static ClipboardTextWriteResult Success { get; } =
        new(ClipboardTextWriteOutcome.Success);

    public static ClipboardTextWriteResult ClipboardUnavailable { get; } =
        new(ClipboardTextWriteOutcome.ClipboardUnavailable);
}
