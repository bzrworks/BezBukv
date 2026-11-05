using System.Runtime.InteropServices;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    private const uint CfBitmap = 2;
    private const uint CfDib = 8;
    private const uint CfUnicodeText = 13;
    private const uint CfHdrop = 15;
    private const uint CfDibV5 = 17;
    private const uint ImageBitmap = 0;
    private const uint LrCreatedibsection = 0x00002000;
    private const long MaxCapturedFormatBytes = 128L * 1024 * 1024;
    private const int DefaultReadAttemptCount = 5;
    private static readonly TimeSpan DefaultReadRetryDelay = TimeSpan.FromMilliseconds(10);
    private static readonly uint RichTextFormat = RegisterClipboardFormat("Rich Text Format");
    private static readonly uint HtmlFormat = RegisterClipboardFormat("HTML Format");
    private static readonly uint PngFormat = RegisterClipboardFormat("PNG");
    private static readonly IReadOnlyList<ClipboardFormatSpec> SnapshotFormats =
    [
        new(CfUnicodeText, ClipboardContentKinds.Text, ClipboardStorage.Global),
        new(CfDib, ClipboardContentKinds.Image, ClipboardStorage.Global),
        new(CfDibV5, ClipboardContentKinds.Image, ClipboardStorage.Global),
        new(CfBitmap, ClipboardContentKinds.Image, ClipboardStorage.Bitmap),
        new(PngFormat, ClipboardContentKinds.Image, ClipboardStorage.Global),
        new(HtmlFormat, ClipboardContentKinds.Html, ClipboardStorage.Global),
        new(RichTextFormat, ClipboardContentKinds.RichText, ClipboardStorage.Global),
        new(CfHdrop, ClipboardContentKinds.Files, ClipboardStorage.Global),
    ];

    private int _maxTextSizeInBytes;
    private readonly int _readAttemptCount;
    private readonly TimeSpan _readRetryDelay;

    public WindowsClipboardService(
        int maxTextSizeInBytes = ClipboardTextLimit.DefaultMaxBytes,
        int readAttemptCount = DefaultReadAttemptCount,
        TimeSpan? readRetryDelay = null)
    {
        if (maxTextSizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTextSizeInBytes),
                "Clipboard text limit must be positive.");
        }

        if (readAttemptCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readAttemptCount),
                "Clipboard read attempt count must be positive.");
        }

        var effectiveReadRetryDelay = readRetryDelay ?? DefaultReadRetryDelay;
        if (effectiveReadRetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readRetryDelay),
                "Clipboard read retry delay cannot be negative.");
        }

        _maxTextSizeInBytes = maxTextSizeInBytes;
        _readAttemptCount = readAttemptCount;
        _readRetryDelay = effectiveReadRetryDelay;
    }

    public void UpdateMaxTextSizeInBytes(int maxTextSizeInBytes)
    {
        if (maxTextSizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTextSizeInBytes),
                "Clipboard text limit must be positive.");
        }

        Volatile.Write(ref _maxTextSizeInBytes, maxTextSizeInBytes);
    }

    public ClipboardStateToken CaptureStateToken()
    {
        return new ClipboardStateToken(GetClipboardSequenceNumber());
    }

    public bool HasChanged(ClipboardStateToken stateToken)
    {
        return CaptureStateToken() != stateToken;
    }

    public async ValueTask<ClipboardSnapshotCaptureResult> CaptureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= _readAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = TryCaptureSnapshot();
            if (result.Outcome != ClipboardSnapshotCaptureOutcome.ClipboardUnavailable ||
                attempt == _readAttemptCount)
            {
                return result;
            }

            await Task.Delay(_readRetryDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Clipboard snapshot retry loop completed unexpectedly.");
    }

    public async ValueTask<ClipboardSnapshotRestoreResult> RestoreSnapshotAsync(
        IClipboardSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot is not WindowsClipboardSnapshot windowsSnapshot)
        {
            throw new ArgumentException(
                "The snapshot was not created by WindowsClipboardService.",
                nameof(snapshot));
        }

        for (var attempt = 1; attempt <= _readAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = TryRestoreSnapshot(windowsSnapshot);
            if (result.Outcome != ClipboardSnapshotRestoreOutcome.ClipboardUnavailable ||
                attempt == _readAttemptCount)
            {
                return result;
            }

            await Task.Delay(_readRetryDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Clipboard restore retry loop completed unexpectedly.");
    }

    public async ValueTask<ClipboardTextReadResult> ReadUnicodeTextAsync(
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= _readAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = TryReadUnicodeText();
            if (result.Outcome != ClipboardTextReadOutcome.ClipboardUnavailable ||
                attempt == _readAttemptCount)
            {
                return result;
            }

            await Task.Delay(_readRetryDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Clipboard read retry loop completed unexpectedly.");
    }

    public async ValueTask<ClipboardTextWriteResult> SetUnicodeTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        for (var attempt = 1; attempt <= _readAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = TrySetUnicodeText(text);
            if (result.Outcome != ClipboardTextWriteOutcome.ClipboardUnavailable ||
                attempt == _readAttemptCount)
            {
                return result;
            }

            await Task.Delay(_readRetryDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Clipboard write retry loop completed unexpectedly.");
    }

    private ClipboardTextReadResult TryReadUnicodeText()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return ClipboardTextReadResult.ClipboardUnavailable;
        }

        try
        {
            if (!IsClipboardFormatAvailable(CfUnicodeText))
            {
                return ClipboardTextReadResult.NoPlainText;
            }

            var clipboardData = GetClipboardData(CfUnicodeText);
            if (clipboardData == IntPtr.Zero)
            {
                return ClipboardTextReadResult.ClipboardUnavailable;
            }

            var textPointer = GlobalLock(clipboardData);
            if (textPointer == IntPtr.Zero)
            {
                return ClipboardTextReadResult.ClipboardUnavailable;
            }

            try
            {
                var text = Marshal.PtrToStringUni(textPointer) ?? string.Empty;

                return ClipboardTextLimit.EvaluateUtf16(
                    text,
                    Volatile.Read(ref _maxTextSizeInBytes)) ==
                    ClipboardTextLimitOutcome.ExceedsLimit
                    ? ClipboardTextReadResult.ExceedsLimit
                    : ClipboardTextReadResult.FromText(text);
            }
            finally
            {
                GlobalUnlock(clipboardData);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private ClipboardTextWriteResult TrySetUnicodeText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return ClipboardTextWriteResult.ClipboardUnavailable;
        }

        var clipboardMemory = IntPtr.Zero;
        var ownsClipboardMemory = true;

        try
        {
            var terminatorOffset = checked(text.Length * sizeof(char));
            var allocationSize = checked((nuint)(terminatorOffset + sizeof(char)));

            clipboardMemory = GlobalAlloc(GlobalMemoryFlags.Moveable, allocationSize);
            if (clipboardMemory == IntPtr.Zero)
            {
                return ClipboardTextWriteResult.ClipboardUnavailable;
            }

            var textPointer = GlobalLock(clipboardMemory);
            if (textPointer == IntPtr.Zero)
            {
                return ClipboardTextWriteResult.ClipboardUnavailable;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, textPointer, text.Length);
                Marshal.WriteInt16(textPointer, terminatorOffset, 0);
            }
            finally
            {
                GlobalUnlock(clipboardMemory);
            }

            if (!EmptyClipboard() ||
                SetClipboardData(CfUnicodeText, clipboardMemory) == IntPtr.Zero)
            {
                return ClipboardTextWriteResult.ClipboardUnavailable;
            }

            ownsClipboardMemory = false;
            return ClipboardTextWriteResult.Success;
        }
        finally
        {
            if (ownsClipboardMemory && clipboardMemory != IntPtr.Zero)
            {
                GlobalFree(clipboardMemory);
            }

            CloseClipboard();
        }
    }

    private static ClipboardSnapshotCaptureResult TryCaptureSnapshot()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return ClipboardSnapshotCaptureResult.ClipboardUnavailable;
        }

        var entries = new List<ClipboardSnapshotEntry>();
        var capturedContent = ClipboardContentKinds.None;
        var nonCapturedContent = ClipboardContentKinds.None;
        var nonCapturedFormatCount = 0;

        try
        {
            foreach (var format in SnapshotFormats)
            {
                if (format.Id == 0 || !IsClipboardFormatAvailable(format.Id))
                {
                    continue;
                }

                var clipboardData = GetClipboardData(format.Id);
                var entry = clipboardData == IntPtr.Zero
                    ? null
                    : ClipboardSnapshotEntry.TryCapture(format, clipboardData);

                if (entry is null)
                {
                    nonCapturedContent |= format.ContentKind;
                    nonCapturedFormatCount++;
                    continue;
                }

                entries.Add(entry);
                capturedContent |= format.ContentKind;
            }
        }
        finally
        {
            CloseClipboard();
        }

        var snapshot = new WindowsClipboardSnapshot(entries, capturedContent);
        return ClipboardSnapshotCaptureResult.FromSnapshot(
            snapshot,
            capturedContent,
            nonCapturedContent,
            entries.Count,
            nonCapturedFormatCount);
    }

    private static ClipboardSnapshotRestoreResult TryRestoreSnapshot(
        WindowsClipboardSnapshot snapshot)
    {
        lock (snapshot.SyncRoot)
        {
            snapshot.ThrowIfDisposed();

            if (!OpenClipboard(IntPtr.Zero))
            {
                return ClipboardSnapshotRestoreResult.FromClipboardUnavailable(
                    snapshot.CapturedContent,
                    snapshot.Entries.Count);
            }

            try
            {
                if (!EmptyClipboard())
                {
                    return ClipboardSnapshotRestoreResult.FromClipboardUnavailable(
                        snapshot.CapturedContent,
                        snapshot.Entries.Count);
                }

                var restoredContent = ClipboardContentKinds.None;
                var skippedContent = ClipboardContentKinds.None;
                var restoredFormatCount = 0;
                var skippedFormatCount = 0;

                foreach (var entry in snapshot.Entries.OrderBy(entry => entry.Format.Id))
                {
                    var transferHandle = entry.CreateTransferHandle();
                    if (transferHandle == IntPtr.Zero ||
                        SetClipboardData(entry.Format.Id, transferHandle) == IntPtr.Zero)
                    {
                        if (transferHandle != IntPtr.Zero)
                        {
                            entry.ReleaseUntransferredHandle(transferHandle);
                        }

                        skippedContent |= entry.Format.ContentKind;
                        skippedFormatCount++;
                        continue;
                    }

                    restoredContent |= entry.Format.ContentKind;
                    restoredFormatCount++;
                }

                return ClipboardSnapshotRestoreResult.FromCompletedRestore(
                    restoredContent,
                    skippedContent,
                    restoredFormatCount,
                    skippedFormatCount);
            }
            finally
            {
                CloseClipboard();
            }
        }
    }

    private sealed class WindowsClipboardSnapshot : IClipboardSnapshot
    {
        private bool _disposed;

        internal WindowsClipboardSnapshot(
            List<ClipboardSnapshotEntry> entries,
            ClipboardContentKinds capturedContent)
        {
            Entries = entries;
            CapturedContent = capturedContent;
        }

        internal object SyncRoot { get; } = new();

        internal List<ClipboardSnapshotEntry> Entries { get; }

        internal ClipboardContentKinds CapturedContent { get; }

        internal void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                foreach (var entry in Entries)
                {
                    entry.Dispose();
                }

                Entries.Clear();
            }
        }
    }

    private sealed class ClipboardSnapshotEntry : IDisposable
    {
        private readonly byte[]? _bytes;
        private IntPtr _bitmap;
        private bool _disposed;

        private ClipboardSnapshotEntry(
            ClipboardFormatSpec format,
            byte[]? bytes,
            IntPtr bitmap)
        {
            Format = format;
            _bytes = bytes;
            _bitmap = bitmap;
        }

        internal ClipboardFormatSpec Format { get; }

        internal static ClipboardSnapshotEntry? TryCapture(
            ClipboardFormatSpec format,
            IntPtr clipboardData)
        {
            return format.Storage == ClipboardStorage.Bitmap
                ? TryCaptureBitmap(format, clipboardData)
                : TryCaptureGlobal(format, clipboardData);
        }

        internal IntPtr CreateTransferHandle()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_bytes is null)
            {
                return CopyBitmap(_bitmap);
            }

            var memory = GlobalAlloc(GlobalMemoryFlags.Moveable, (nuint)_bytes.Length);
            if (memory == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var pointer = GlobalLock(memory);
            if (pointer == IntPtr.Zero)
            {
                GlobalFree(memory);
                return IntPtr.Zero;
            }

            try
            {
                Marshal.Copy(_bytes, 0, pointer, _bytes.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            return memory;
        }

        internal void ReleaseUntransferredHandle(IntPtr handle)
        {
            if (Format.Storage == ClipboardStorage.Bitmap)
            {
                DeleteObject(handle);
            }
            else
            {
                GlobalFree(handle);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_bytes is not null)
            {
                Array.Clear(_bytes);
            }

            if (_bitmap != IntPtr.Zero)
            {
                DeleteObject(_bitmap);
                _bitmap = IntPtr.Zero;
            }
        }

        private static ClipboardSnapshotEntry? TryCaptureGlobal(
            ClipboardFormatSpec format,
            IntPtr clipboardData)
        {
            var size = GlobalSize(clipboardData);
            if (size == 0 ||
                size > (nuint)MaxCapturedFormatBytes ||
                size > int.MaxValue)
            {
                return null;
            }

            var pointer = GlobalLock(clipboardData);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var bytes = new byte[(int)size];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                return new ClipboardSnapshotEntry(format, bytes, IntPtr.Zero);
            }
            finally
            {
                GlobalUnlock(clipboardData);
            }
        }

        private static ClipboardSnapshotEntry? TryCaptureBitmap(
            ClipboardFormatSpec format,
            IntPtr clipboardData)
        {
            var bitmap = CopyBitmap(clipboardData);
            return bitmap == IntPtr.Zero
                ? null
                : new ClipboardSnapshotEntry(format, null, bitmap);
        }

        private static IntPtr CopyBitmap(IntPtr bitmap)
        {
            return CopyImage(bitmap, ImageBitmap, 0, 0, LrCreatedibsection);
        }
    }

    private sealed record ClipboardFormatSpec(
        uint Id,
        ClipboardContentKinds ContentKind,
        ClipboardStorage Storage);

    private enum ClipboardStorage
    {
        Global,
        Bitmap,
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(GlobalMemoryFlags flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint GlobalSize(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CopyImage(
        IntPtr image,
        uint type,
        int desiredWidth,
        int desiredHeight,
        uint flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [Flags]
    private enum GlobalMemoryFlags : uint
    {
        None = 0,
        Moveable = 0x0002,
    }
}
