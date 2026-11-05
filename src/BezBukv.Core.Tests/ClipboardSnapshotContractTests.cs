using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class ClipboardSnapshotContractTests
{
    [Fact]
    public async Task CaptureResultCarriesOpaqueDisposableSnapshotAndFormatSummary()
    {
        var snapshot = new FakeClipboardSnapshot();
        var captureResult = ClipboardSnapshotCaptureResult.FromSnapshot(
            snapshot,
            ClipboardContentKinds.Text | ClipboardContentKinds.Image,
            ClipboardContentKinds.Html,
            capturedFormatCount: 3,
            nonCapturedFormatCount: 1);
        var service = new FakeClipboardService(captureResult);

        var result = await service.CaptureSnapshotAsync();

        Assert.Equal(ClipboardSnapshotCaptureOutcome.Captured, result.Outcome);
        Assert.Same(snapshot, result.Snapshot);
        Assert.Equal(
            ClipboardContentKinds.Text | ClipboardContentKinds.Image,
            result.CapturedContent);
        Assert.Equal(ClipboardContentKinds.Html, result.NonCapturedContent);
        Assert.Equal(3, result.CapturedFormatCount);
        Assert.Equal(1, result.NonCapturedFormatCount);

        result.Snapshot!.Dispose();
        result.Snapshot.Dispose();

        Assert.Equal(1, snapshot.DisposeCount);
    }

    [Fact]
    public async Task CaptureUnavailableDoesNotExposeSnapshotOrContent()
    {
        var service = new FakeClipboardService(
            ClipboardSnapshotCaptureResult.ClipboardUnavailable);

        var result = await service.CaptureSnapshotAsync();

        Assert.Equal(ClipboardSnapshotCaptureOutcome.ClipboardUnavailable, result.Outcome);
        Assert.Null(result.Snapshot);
        Assert.Equal(ClipboardContentKinds.None, result.CapturedContent);
        Assert.Equal(ClipboardContentKinds.None, result.NonCapturedContent);
        Assert.Equal(0, result.CapturedFormatCount);
        Assert.Equal(0, result.NonCapturedFormatCount);
    }

    [Fact]
    public async Task RestoreResultDistinguishesRestoredCriticalContentFromSkippedBestEffortContent()
    {
        var snapshot = new FakeClipboardSnapshot();
        var restoreResult = ClipboardSnapshotRestoreResult.FromCompletedRestore(
            ClipboardContentKinds.Text | ClipboardContentKinds.Image,
            ClipboardContentKinds.Html | ClipboardContentKinds.RichText,
            restoredFormatCount: 5,
            skippedFormatCount: 2);
        var service = new FakeClipboardService(
            ClipboardSnapshotCaptureResult.ClipboardUnavailable,
            restoreResult);

        var result = await service.RestoreSnapshotAsync(snapshot);

        Assert.Equal(ClipboardSnapshotRestoreOutcome.FormatsSkipped, result.Outcome);
        Assert.Equal(
            ClipboardContentKinds.Text | ClipboardContentKinds.Image,
            result.RestoredContent);
        Assert.Equal(
            ClipboardContentKinds.Html | ClipboardContentKinds.RichText,
            result.SkippedContent);
        Assert.Equal(5, result.RestoredFormatCount);
        Assert.Equal(2, result.SkippedFormatCount);
        Assert.Same(snapshot, service.LastRestoredSnapshot);
    }

    [Fact]
    public void EmptySnapshotRestoreIsSuccessful()
    {
        var result = ClipboardSnapshotRestoreResult.FromCompletedRestore(
            ClipboardContentKinds.None,
            ClipboardContentKinds.None,
            restoredFormatCount: 0,
            skippedFormatCount: 0);

        Assert.Equal(ClipboardSnapshotRestoreOutcome.Success, result.Outcome);
        Assert.Equal(0, result.RestoredFormatCount);
        Assert.Equal(0, result.SkippedFormatCount);
    }

    [Fact]
    public void ClipboardUnavailableReportsAllSnapshotContentAsSkipped()
    {
        var result = ClipboardSnapshotRestoreResult.FromClipboardUnavailable(
            ClipboardContentKinds.Text | ClipboardContentKinds.Files,
            skippedFormatCount: 2);

        Assert.Equal(ClipboardSnapshotRestoreOutcome.ClipboardUnavailable, result.Outcome);
        Assert.Equal(ClipboardContentKinds.None, result.RestoredContent);
        Assert.Equal(
            ClipboardContentKinds.Text | ClipboardContentKinds.Files,
            result.SkippedContent);
        Assert.Equal(0, result.RestoredFormatCount);
        Assert.Equal(2, result.SkippedFormatCount);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        private readonly ClipboardSnapshotCaptureResult _captureResult;
        private readonly ClipboardSnapshotRestoreResult _restoreResult;

        internal FakeClipboardService(
            ClipboardSnapshotCaptureResult captureResult,
            ClipboardSnapshotRestoreResult? restoreResult = null)
        {
            _captureResult = captureResult;
            _restoreResult = restoreResult ??
                ClipboardSnapshotRestoreResult.FromCompletedRestore(
                    ClipboardContentKinds.None,
                    ClipboardContentKinds.None,
                    restoredFormatCount: 0,
                    skippedFormatCount: 0);
        }

        internal IClipboardSnapshot? LastRestoredSnapshot { get; private set; }

        public ClipboardStateToken CaptureStateToken() => new(0);

        public bool HasChanged(ClipboardStateToken stateToken) => false;

        public ValueTask<ClipboardSnapshotCaptureResult> CaptureSnapshotAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_captureResult);

        public ValueTask<ClipboardSnapshotRestoreResult> RestoreSnapshotAsync(
            IClipboardSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            LastRestoredSnapshot = snapshot;
            return ValueTask.FromResult(_restoreResult);
        }

        public ValueTask<ClipboardTextReadResult> ReadUnicodeTextAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ClipboardTextReadResult.NoPlainText);

        public ValueTask<ClipboardTextWriteResult> SetUnicodeTextAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ClipboardTextWriteResult.Success);
    }

    private sealed class FakeClipboardSnapshot : IClipboardSnapshot
    {
        private bool _disposed;

        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeCount++;
        }
    }
}
