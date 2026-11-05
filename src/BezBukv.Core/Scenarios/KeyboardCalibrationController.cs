using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Scenarios;

/// <summary>
/// Headless state machine that captures a custom keyboard geometry one row at a time.
/// Capture ownership, input blocking, labels, and rendering belong to the application layer.
/// </summary>
public sealed class KeyboardCalibrationController : IDisposable
{
    public static readonly TimeSpan DefaultInactivityTimeout = TimeSpan.FromMinutes(3);

    private const int EscapeScanCode = 0x01;
    private const int EscapeVirtualKey = 0x1B;

    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _inactivityTimeout;
    private readonly List<CustomKeyboardRow> _completedRows = [];
    private readonly List<CustomKeyboardCell> _currentCells = [];
    private readonly Stack<RowMutation> _rowMutations = [];
    private readonly HashSet<CapturedKeyIdentity> _pressedKeys = [];
    private ITimer? _inactivityTimer;
    private KeyboardCalibrationStatus _status = KeyboardCalibrationStatus.Active;
    private bool _disposed;

    public KeyboardCalibrationController(
        TimeSpan? inactivityTimeout = null,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeout = inactivityTimeout ?? DefaultInactivityTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inactivityTimeout),
                "Inactivity timeout must be positive.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _inactivityTimeout = effectiveTimeout;
        _inactivityTimer = CreateInactivityTimer();
    }

    public event EventHandler? StatusChanged;

    public KeyboardCalibrationStatus Status
    {
        get
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _status;
            }
        }
    }

    public KeyboardCalibrationSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return new KeyboardCalibrationSnapshot(
                    _status,
                    FreezeRows(_completedRows),
                    FreezeRow(_currentCells),
                    _currentCells.Sum(cell => cell.WidthUnits));
            }
        }
    }

    public void ProcessKeyEvent(KeyboardCaptureEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        ProcessKeyEventCore(
            eventArgs.ScanCode,
            eventArgs.Extended,
            eventArgs.VirtualKey,
            eventArgs.State,
            eventArgs.Key == PhysicalKey.Escape,
            eventArgs.IsAltGr);
    }

    public void ProcessKeyEvent(
        int scanCode,
        bool extended,
        int virtualKey,
        KeyboardKeyState state) =>
        ProcessKeyEventCore(
            scanCode,
            extended,
            virtualKey,
            state,
            isEscape: false,
            isAltGr: false);

    public void AddKey(int scanCode, bool extended, int virtualKey)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!BeginActivityCore())
            {
                return;
            }

            _currentCells.Add(
                new CustomKeyboardCell
                {
                    Key = new CustomKeyboardKey
                    {
                        ScanCode = scanCode,
                        Extended = extended,
                        VirtualKey = virtualKey,
                    },
                });
            _rowMutations.Push(RowMutation.AddedCell);
        }
    }

    public void AddEmptySlot()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!BeginActivityCore())
            {
                return;
            }

            _currentCells.Add(new CustomKeyboardCell());
            _rowMutations.Push(RowMutation.AddedCell);
        }
    }

    public void Undo()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!BeginActivityCore() || _rowMutations.Count == 0)
            {
                return;
            }

            switch (_rowMutations.Pop())
            {
                case RowMutation.AddedCell:
                    _currentCells.RemoveAt(_currentCells.Count - 1);
                    break;
                case RowMutation.IncreasedWidth:
                    var lastIndex = _currentCells.Count - 1;
                    var lastCell = _currentCells[lastIndex];
                    _currentCells[lastIndex] = lastCell with
                    {
                        WidthUnits = lastCell.WidthUnits - 1,
                    };
                    break;
                default:
                    throw new InvalidOperationException("Unknown row mutation.");
            }
        }
    }

    public void ResetRow()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!BeginActivityCore())
            {
                return;
            }

            _currentCells.Clear();
            _rowMutations.Clear();
        }
    }

    public void NextRow()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!BeginActivityCore())
            {
                return;
            }

            _completedRows.Add(FreezeRow(_currentCells));
            _currentCells.Clear();
            _rowMutations.Clear();
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_status is not KeyboardCalibrationStatus.Active
                and not KeyboardCalibrationStatus.Suspended)
            {
                return;
            }

            _status = KeyboardCalibrationStatus.Cancelled;
            CancelInactivityTimerCore();
        }

        OnStatusChanged();
    }

    public void Pause()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_status != KeyboardCalibrationStatus.Active)
            {
                return;
            }

            _status = KeyboardCalibrationStatus.Suspended;
            _pressedKeys.Clear();
            CancelInactivityTimerCore();
        }

        OnStatusChanged();
    }

    public void Resume()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_status != KeyboardCalibrationStatus.Suspended)
            {
                return;
            }

            _inactivityTimer = CreateInactivityTimer();
            _status = KeyboardCalibrationStatus.Active;
        }

        OnStatusChanged();
    }

    public KeyboardCalibrationFinishResult Finish()
    {
        KeyboardCalibrationFinishResult result;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_status is not KeyboardCalibrationStatus.Active
                and not KeyboardCalibrationStatus.Suspended)
            {
                return new KeyboardCalibrationFinishResult(
                    KeyboardCalibrationFinishStatus.SessionNotActive,
                    Layout: null);
            }

            if (_status == KeyboardCalibrationStatus.Active)
            {
                ResetInactivityTimerCore();
            }

            var layout = CreateLayoutCore();
            if (!IsValidLayout(layout))
            {
                return new KeyboardCalibrationFinishResult(
                    KeyboardCalibrationFinishStatus.InvalidGeometry,
                    Layout: null);
            }

            _status = KeyboardCalibrationStatus.Finished;
            CancelInactivityTimerCore();

            result = new KeyboardCalibrationFinishResult(
                KeyboardCalibrationFinishStatus.Succeeded,
                layout);
        }

        OnStatusChanged();
        return result;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            CancelInactivityTimerCore();
            _pressedKeys.Clear();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private void ProcessKeyEventCore(
        int scanCode,
        bool extended,
        int virtualKey,
        KeyboardKeyState state,
        bool isEscape,
        bool isAltGr)
    {
        if (state is not KeyboardKeyState.Down and not KeyboardKeyState.Up)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!BeginActivityCore())
            {
                return;
            }

            if (isEscape || IsRawEscape(scanCode, extended, virtualKey))
            {
                return;
            }

            var identity = new CapturedKeyIdentity(scanCode, extended);
            if (state == KeyboardKeyState.Up)
            {
                _pressedKeys.Remove(identity);
                return;
            }

            if (!_pressedKeys.Add(identity))
            {
                return;
            }

            if (_currentCells.LastOrDefault() is { Key: { } lastKey } lastCell
                && lastKey.ScanCode == scanCode
                && lastKey.Extended == extended)
            {
                var lastIndex = _currentCells.Count - 1;
                _currentCells[lastIndex] = lastCell with
                {
                    WidthUnits = checked(lastCell.WidthUnits + 1),
                    Key = lastKey with
                    {
                        IsAltGr = lastKey.IsAltGr || isAltGr,
                    },
                };
                _rowMutations.Push(RowMutation.IncreasedWidth);
                return;
            }

            _currentCells.Add(
                new CustomKeyboardCell
                {
                    Key = new CustomKeyboardKey
                    {
                        ScanCode = scanCode,
                        Extended = extended,
                        VirtualKey = virtualKey,
                        IsAltGr = isAltGr,
                    },
                });
            _rowMutations.Push(RowMutation.AddedCell);
        }
    }

    private bool BeginActivityCore()
    {
        if (_status != KeyboardCalibrationStatus.Active)
        {
            return false;
        }

        ResetInactivityTimerCore();
        return true;
    }

    private CustomKeyboardLayout CreateLayoutCore()
    {
        var rows = new List<CustomKeyboardRow>(_completedRows.Count + 1);
        rows.AddRange(_completedRows.Select(FreezeRow));
        rows.Add(FreezeRow(_currentCells));

        return new CustomKeyboardLayout
        {
            Rows = rows.AsReadOnly(),
        };
    }

    private static bool IsValidLayout(CustomKeyboardLayout layout) =>
        layout.Rows.Count > 0
        && layout.Rows.SelectMany(row => row.Cells).Any(cell => cell.Key is not null)
        && layout.Rows.SelectMany(row => row.Cells).All(cell => cell.WidthUnits >= 1);

    private ITimer CreateInactivityTimer() =>
        _timeProvider.CreateTimer(
            static state => ((KeyboardCalibrationController)state!).OnInactivityTimeout(),
            this,
            _inactivityTimeout,
            Timeout.InfiniteTimeSpan);

    private void ResetInactivityTimerCore()
    {
        if (_inactivityTimer is null
            || !_inactivityTimer.Change(_inactivityTimeout, Timeout.InfiniteTimeSpan))
        {
            throw new InvalidOperationException("The inactivity timer could not be reset.");
        }
    }

    private void CancelInactivityTimerCore()
    {
        var timer = _inactivityTimer;
        _inactivityTimer = null;
        timer?.Dispose();
    }

    private void OnInactivityTimeout()
    {
        lock (_sync)
        {
            if (_disposed || _status != KeyboardCalibrationStatus.Active)
            {
                return;
            }

            _status = KeyboardCalibrationStatus.Suspended;
            _pressedKeys.Clear();
            CancelInactivityTimerCore();
        }

        OnStatusChanged();
    }

    private void OnStatusChanged() =>
        StatusChanged?.Invoke(this, EventArgs.Empty);

    private static bool IsRawEscape(int scanCode, bool extended, int virtualKey) =>
        virtualKey == EscapeVirtualKey
        || (scanCode == EscapeScanCode && !extended);

    private static IReadOnlyList<CustomKeyboardRow> FreezeRows(
        IEnumerable<CustomKeyboardRow> rows) =>
        Array.AsReadOnly(rows.Select(FreezeRow).ToArray());

    private static CustomKeyboardRow FreezeRow(CustomKeyboardRow row) =>
        FreezeRow(row.Cells);

    private static CustomKeyboardRow FreezeRow(
        IEnumerable<CustomKeyboardCell> cells) =>
        new()
        {
            Cells = Array.AsReadOnly(cells.Select(CloneCell).ToArray()),
        };

    private static CustomKeyboardCell CloneCell(CustomKeyboardCell cell) =>
        new()
        {
            WidthUnits = cell.WidthUnits,
            Key = cell.Key is null
                ? null
                : new CustomKeyboardKey
                {
                    ScanCode = cell.Key.ScanCode,
                    Extended = cell.Key.Extended,
                    VirtualKey = cell.Key.VirtualKey,
                    IsAltGr = cell.Key.IsAltGr,
                },
        };

    private readonly record struct CapturedKeyIdentity(int ScanCode, bool Extended);

    private enum RowMutation
    {
        AddedCell,
        IncreasedWidth,
    }
}

public enum KeyboardCalibrationStatus
{
    Active,
    Cancelled,
    Suspended,
    Finished,
}

public enum KeyboardCalibrationFinishStatus
{
    Succeeded,
    InvalidGeometry,
    SessionNotActive,
}

public sealed record KeyboardCalibrationFinishResult(
    KeyboardCalibrationFinishStatus Status,
    CustomKeyboardLayout? Layout);

public sealed record KeyboardCalibrationSnapshot(
    KeyboardCalibrationStatus Status,
    IReadOnlyList<CustomKeyboardRow> CompletedRows,
    CustomKeyboardRow CurrentRow,
    int CursorPosition);
