using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Tests;

public sealed class KeyboardCalibrationControllerTests
{
    [Fact]
    public void FirstKeyStartsRowWithoutLeadingCells()
    {
        using var controller = new KeyboardCalibrationController();

        Press(controller, scanCode: 0x3B, extended: false, virtualKey: 0x70);

        var snapshot = controller.Snapshot;
        var cell = Assert.Single(snapshot.CurrentRow.Cells);
        Assert.Equal(1, cell.WidthUnits);
        Assert.Equal(0x3B, cell.Key?.ScanCode);
        Assert.False(cell.Key?.Extended);
        Assert.Equal(0x70, cell.Key?.VirtualKey);
        Assert.Equal(1, snapshot.CursorPosition);
    }

    [Fact]
    public void DifferentKeysAreAppendedInCaptureOrderWithUnitWidth()
    {
        using var controller = new KeyboardCalibrationController();

        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        Press(controller, scanCode: 0x11, extended: false, virtualKey: 0x57);
        Press(controller, scanCode: 0x4B, extended: true, virtualKey: 0x25);

        var cells = controller.Snapshot.CurrentRow.Cells;
        Assert.Equal([0x10, 0x11, 0x4B], cells.Select(cell => cell.Key?.ScanCode));
        Assert.Equal([false, false, true], cells.Select(cell => cell.Key?.Extended));
        Assert.All(cells, cell => Assert.Equal(1, cell.WidthUnits));
    }

    [Fact]
    public void RepeatedPressOfLastKeyIncreasesItsWidth()
    {
        using var controller = new KeyboardCalibrationController();

        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);
        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);
        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);

        var snapshot = controller.Snapshot;
        var cell = Assert.Single(snapshot.CurrentRow.Cells);
        Assert.Equal(3, cell.WidthUnits);
        Assert.Equal(3, snapshot.CursorPosition);
    }

    [Fact]
    public void AutoRepeatDownEventsDoNotAddCellsOrIncreaseWidth()
    {
        using var controller = new KeyboardCalibrationController();

        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Down);
        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Down);
        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Down);

        var heldSnapshot = controller.Snapshot;
        Assert.Equal(1, Assert.Single(heldSnapshot.CurrentRow.Cells).WidthUnits);

        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Up);
        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Down);
        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Down);

        var pressedAgainSnapshot = controller.Snapshot;
        Assert.Equal(2, Assert.Single(pressedAgainSnapshot.CurrentRow.Cells).WidthUnits);
    }

    [Fact]
    public void AddKeyWithEscapeCodesRecordsKeyAndFinishIncludesIt()
    {
        using var controller = new KeyboardCalibrationController();

        controller.AddKey(scanCode: 0x01, extended: false, virtualKey: 0x1B);

        var snapshotKey = Assert.Single(controller.Snapshot.CurrentRow.Cells).Key;
        Assert.Equal(0x01, snapshotKey?.ScanCode);
        Assert.False(snapshotKey?.Extended);
        Assert.Equal(0x1B, snapshotKey?.VirtualKey);

        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.Succeeded, result.Status);
        var layoutKey = Assert.Single(Assert.Single(result.Layout!.Rows).Cells).Key;
        Assert.Equal(0x01, layoutKey?.ScanCode);
        Assert.False(layoutKey?.Extended);
        Assert.Equal(0x1B, layoutKey?.VirtualKey);
    }

    [Fact]
    public void AddKeyAppendsUnitCellAndAdvancesCursor()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddEmptySlot();

        controller.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);

        var snapshot = controller.Snapshot;
        Assert.Equal(2, snapshot.CurrentRow.Cells.Count);
        Assert.Null(snapshot.CurrentRow.Cells[0].Key);
        var addedCell = snapshot.CurrentRow.Cells[1];
        Assert.Equal(1, addedCell.WidthUnits);
        Assert.Equal(0x10, addedCell.Key?.ScanCode);
        Assert.Equal(2, snapshot.CursorPosition);
    }

    [Fact]
    public void AddKeyNeverIncreasesPreviousMatchingCellWidth()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddKey(scanCode: 0x39, extended: false, virtualKey: 0x20);

        controller.AddKey(scanCode: 0x39, extended: false, virtualKey: 0x20);

        var cells = controller.Snapshot.CurrentRow.Cells;
        Assert.Equal(2, cells.Count);
        Assert.All(cells, cell => Assert.Equal(1, cell.WidthUnits));
    }

    [Fact]
    public void UndoRemovesKeyAddedByCommand()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);

        controller.Undo();

        Assert.Empty(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(0, controller.Snapshot.CursorPosition);
    }

    [Fact]
    public void AddKeyDoesNothingWhenSessionIsNotActive()
    {
        using var controller = new KeyboardCalibrationController();
        controller.Cancel();

        controller.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);

        Assert.Equal(KeyboardCalibrationStatus.Cancelled, controller.Status);
        Assert.Empty(controller.Snapshot.CurrentRow.Cells);
    }

    [Fact]
    public void AddKeyDoesNotMarkPhysicalKeyAsPressed()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddKey(scanCode: 0x39, extended: false, virtualKey: 0x20);

        controller.ProcessKeyEvent(0x39, false, 0x20, KeyboardKeyState.Down);

        var cell = Assert.Single(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(2, cell.WidthUnits);
    }

    [Fact]
    public void EmptySlotsCanBeAddedAtBeginningMiddleAndEnd()
    {
        using var controller = new KeyboardCalibrationController();

        controller.AddEmptySlot();
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        controller.AddEmptySlot();
        Press(controller, scanCode: 0x11, extended: false, virtualKey: 0x57);
        controller.AddEmptySlot();

        var cells = controller.Snapshot.CurrentRow.Cells;
        Assert.Equal(5, cells.Count);
        Assert.Null(cells[0].Key);
        Assert.Equal(0x10, cells[1].Key?.ScanCode);
        Assert.Null(cells[2].Key);
        Assert.Equal(0x11, cells[3].Key?.ScanCode);
        Assert.Null(cells[4].Key);
        Assert.All(cells, cell => Assert.Equal(1, cell.WidthUnits));
    }

    [Fact]
    public void UndoRevertsWidthGrowthAndThenAddedCell()
    {
        using var controller = new KeyboardCalibrationController();
        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);
        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);

        controller.Undo();

        Assert.Equal(1, Assert.Single(controller.Snapshot.CurrentRow.Cells).WidthUnits);

        controller.Undo();

        Assert.Empty(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(0, controller.Snapshot.CursorPosition);
    }

    [Fact]
    public void UndoRemovesAddedEmptySlot()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddEmptySlot();

        controller.Undo();

        Assert.Empty(controller.Snapshot.CurrentRow.Cells);
    }

    [Fact]
    public void ResetRowClearsCellsCursorAndUndoHistory()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddEmptySlot();
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);

        controller.ResetRow();
        controller.Undo();

        Assert.Empty(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(0, controller.Snapshot.CursorPosition);
    }

    [Fact]
    public void NextRowCommitsCurrentRowAndStartsNewEmptyRow()
    {
        using var controller = new KeyboardCalibrationController();
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);

        controller.NextRow();

        var afterFirstRow = controller.Snapshot;
        Assert.Single(afterFirstRow.CompletedRows);
        Assert.Empty(afterFirstRow.CurrentRow.Cells);
        Assert.Equal(0, afterFirstRow.CursorPosition);

        controller.AddEmptySlot();
        Press(controller, scanCode: 0x1E, extended: false, virtualKey: 0x41);

        var afterSecondRow = controller.Snapshot;
        Assert.Single(afterSecondRow.CompletedRows);
        Assert.Equal(2, afterSecondRow.CurrentRow.Cells.Count);
    }

    [Fact]
    public void SamePhysicalKeyCanAppearInTwoRows()
    {
        using var controller = new KeyboardCalibrationController();
        Press(controller, scanCode: 0x1C, extended: false, virtualKey: 0x0D);
        controller.NextRow();

        Press(controller, scanCode: 0x1C, extended: false, virtualKey: 0x0D);
        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Layout?.Rows.Count);
        Assert.All(
            result.Layout!.Rows,
            row => Assert.Equal(0x1C, Assert.Single(row.Cells).Key?.ScanCode));
    }

    [Fact]
    public void FinishReturnsCompleteCustomLayoutWithRawCodesWidthsAndEmptySlots()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddEmptySlot();
        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);
        Press(controller, scanCode: 0x39, extended: false, virtualKey: 0x20);
        controller.NextRow();
        Press(controller, scanCode: 0x4B, extended: true, virtualKey: 0x25);

        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.Succeeded, result.Status);
        Assert.Equal(KeyboardCalibrationStatus.Finished, controller.Status);
        Assert.NotNull(result.Layout);
        Assert.Equal(2, result.Layout.Rows.Count);
        Assert.Null(result.Layout.Rows[0].Cells[0].Key);
        Assert.Equal(2, result.Layout.Rows[0].Cells[1].WidthUnits);
        Assert.Equal(0x39, result.Layout.Rows[0].Cells[1].Key?.ScanCode);
        Assert.False(result.Layout.Rows[0].Cells[1].Key?.Extended);
        Assert.Equal(0x20, result.Layout.Rows[0].Cells[1].Key?.VirtualKey);
        Assert.Equal(0x4B, result.Layout.Rows[1].Cells[0].Key?.ScanCode);
        Assert.True(result.Layout.Rows[1].Cells[0].Key?.Extended);
        Assert.Equal(0x25, result.Layout.Rows[1].Cells[0].Key?.VirtualKey);
    }

    [Fact]
    public void FinishRejectsEmptyGeometryAndKeepsSessionActive()
    {
        using var controller = new KeyboardCalibrationController();

        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.InvalidGeometry, result.Status);
        Assert.Null(result.Layout);
        Assert.Equal(KeyboardCalibrationStatus.Active, controller.Status);
    }

    [Fact]
    public void FinishRejectsGeometryContainingOnlyEmptySlots()
    {
        using var controller = new KeyboardCalibrationController();
        controller.AddEmptySlot();
        controller.NextRow();
        controller.AddEmptySlot();

        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.InvalidGeometry, result.Status);
        Assert.Null(result.Layout);
        Assert.Equal(KeyboardCalibrationStatus.Active, controller.Status);
    }

    [Fact]
    public void CancelEndsSessionWithoutResultAndStopsFurtherMutation()
    {
        using var controller = new KeyboardCalibrationController();
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);

        controller.Cancel();
        controller.AddEmptySlot();
        controller.ProcessKeyEvent(0x11, false, 0x57, KeyboardKeyState.Down);
        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationStatus.Cancelled, controller.Status);
        Assert.Single(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(KeyboardCalibrationFinishStatus.SessionNotActive, result.Status);
        Assert.Null(result.Layout);
    }

    [Fact]
    public void EscapeEventsAreNeverAddedToGeometry()
    {
        using var controller = new KeyboardCalibrationController();

        controller.ProcessKeyEvent(
            new KeyboardCaptureEventArgs(
                PhysicalKey.Escape,
                KeyboardKeyState.Down,
                scanCode: 0x01,
                extended: false,
                virtualKey: 0x1B));
        controller.ProcessKeyEvent(0x01, false, 0, KeyboardKeyState.Down);
        controller.ProcessKeyEvent(0x2A, false, 0x1B, KeyboardKeyState.Down);

        Assert.Empty(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(KeyboardCalibrationStatus.Active, controller.Status);
    }

    [Fact]
    public void RawEventOverloadCapturesUnknownPhysicalKey()
    {
        using var controller = new KeyboardCalibrationController();

        controller.ProcessKeyEvent(
            new KeyboardCaptureEventArgs(
                PhysicalKey.Unknown,
                KeyboardKeyState.Down,
                scanCode: 0x56,
                extended: false,
                virtualKey: 0xE2));

        var key = Assert.Single(controller.Snapshot.CurrentRow.Cells).Key;
        Assert.Equal(0x56, key?.ScanCode);
        Assert.False(key?.Extended);
        Assert.Equal(0xE2, key?.VirtualKey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaptureEventTransfersAltGrMarkerToCurrentRowCell(bool isAltGr)
    {
        using var controller = new KeyboardCalibrationController();

        controller.ProcessKeyEvent(
            new KeyboardCaptureEventArgs(
                PhysicalKey.AltRight,
                KeyboardKeyState.Down,
                scanCode: 0x38,
                extended: true,
                virtualKey: 0xA5,
                isAltGr: isAltGr));

        var key = Assert.Single(controller.Snapshot.CurrentRow.Cells).Key;
        Assert.NotNull(key);
        Assert.Equal(isAltGr, key.IsAltGr);
    }

    [Fact]
    public void PauseSuspendsSessionPreservesProgressAndStopsTimer()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        controller.NextRow();
        controller.AddEmptySlot();
        var beforePause = controller.Snapshot;

        controller.Pause();
        var afterPause = controller.Snapshot;

        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
        Assert.Equal(KeyboardCalibrationStatus.Suspended, afterPause.Status);
        AssertGeometryEqual(beforePause, afterPause);
        Assert.Equal(0, timeProvider.ActiveTimerCount);

        timeProvider.Advance(timeout + timeout);

        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
        AssertGeometryEqual(afterPause, controller.Snapshot);
    }

    [Fact]
    public void PauseOutsideActiveStateDoesNothing()
    {
        var statusChangedCount = 0;
        using var suspendedController = new KeyboardCalibrationController();
        suspendedController.StatusChanged += (_, _) => statusChangedCount++;
        suspendedController.Pause();
        var suspendedSnapshot = suspendedController.Snapshot;
        Assert.Equal(1, statusChangedCount);

        suspendedController.Pause();

        Assert.Equal(KeyboardCalibrationStatus.Suspended, suspendedController.Status);
        AssertGeometryEqual(suspendedSnapshot, suspendedController.Snapshot);
        Assert.Equal(1, statusChangedCount);

        suspendedController.Cancel();
        Assert.Equal(2, statusChangedCount);
        suspendedController.Pause();
        Assert.Equal(KeyboardCalibrationStatus.Cancelled, suspendedController.Status);
        Assert.Equal(2, statusChangedCount);

        using var finishedController = new KeyboardCalibrationController();
        finishedController.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);
        Assert.Equal(
            KeyboardCalibrationFinishStatus.Succeeded,
            finishedController.Finish().Status);

        finishedController.Pause();

        Assert.Equal(KeyboardCalibrationStatus.Finished, finishedController.Status);
    }

    [Fact]
    public void StatusChangedReportsManualPauseResumeAndTimeoutSuspension()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        var statuses = new List<KeyboardCalibrationStatus>();
        controller.StatusChanged += (_, _) => statuses.Add(controller.Status);

        controller.Pause();
        controller.Resume();
        timeProvider.Advance(timeout);

        Assert.Equal(
            [
                KeyboardCalibrationStatus.Suspended,
                KeyboardCalibrationStatus.Active,
                KeyboardCalibrationStatus.Suspended,
            ],
            statuses);
    }

    [Fact]
    public void TimeoutSuspendsSessionAndPreservesProgress()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        controller.NextRow();
        controller.AddEmptySlot();
        Press(controller, scanCode: 0x11, extended: false, virtualKey: 0x57);
        var beforeTimeout = controller.Snapshot;

        timeProvider.Advance(timeout);
        var afterTimeout = controller.Snapshot;

        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
        Assert.Equal(KeyboardCalibrationStatus.Suspended, afterTimeout.Status);
        AssertGeometryEqual(beforeTimeout, afterTimeout);
        Assert.Equal(0, timeProvider.ActiveTimerCount);

        controller.Resume();
        controller.Undo();

        var afterUndo = controller.Snapshot;
        Assert.Single(afterUndo.CompletedRows);
        Assert.Equal(2, Assert.Single(afterUndo.CompletedRows[0].Cells).WidthUnits);
        Assert.Single(afterUndo.CurrentRow.Cells);
        Assert.Null(afterUndo.CurrentRow.Cells[0].Key);
        Assert.Equal(1, afterUndo.CursorPosition);
    }

    [Fact]
    public void KeyEventsAndGeometryCommandsDoNothingWhileSuspended()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        controller.AddEmptySlot();
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        var beforeTimeout = controller.Snapshot;

        timeProvider.Advance(timeout);
        controller.ProcessKeyEvent(0x11, false, 0x57, KeyboardKeyState.Down);
        controller.ProcessKeyEvent(
            new KeyboardCaptureEventArgs(
                PhysicalKey.Unknown,
                KeyboardKeyState.Down,
                scanCode: 0x12,
                extended: false,
                virtualKey: 0x45));
        controller.AddEmptySlot();
        controller.AddKey(scanCode: 0x13, extended: false, virtualKey: 0x52);
        controller.Undo();
        controller.ResetRow();
        controller.NextRow();

        var afterCommands = controller.Snapshot;
        Assert.Equal(KeyboardCalibrationStatus.Suspended, afterCommands.Status);
        AssertGeometryEqual(beforeTimeout, afterCommands);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void ResumeRestartsInactivityTimeout()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);

        timeProvider.Advance(timeout);
        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
        Assert.Equal(0, timeProvider.ActiveTimerCount);

        controller.Resume();

        Assert.Equal(KeyboardCalibrationStatus.Active, controller.Status);
        Assert.Equal(1, timeProvider.ActiveTimerCount);

        timeProvider.Advance(timeout);

        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
        Assert.Single(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void ResumeOutsideSuspendedStateDoesNothing()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var activeController = new KeyboardCalibrationController(timeout, timeProvider);

        timeProvider.Advance(TimeSpan.FromSeconds(9));
        activeController.Resume();
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(KeyboardCalibrationStatus.Suspended, activeController.Status);

        using var cancelledController = new KeyboardCalibrationController();
        cancelledController.Cancel();
        cancelledController.Resume();
        Assert.Equal(KeyboardCalibrationStatus.Cancelled, cancelledController.Status);

        using var finishedController = new KeyboardCalibrationController();
        finishedController.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);
        Assert.Equal(
            KeyboardCalibrationFinishStatus.Succeeded,
            finishedController.Finish().Status);
        finishedController.Resume();
        Assert.Equal(KeyboardCalibrationStatus.Finished, finishedController.Status);
    }

    [Fact]
    public void CancelFromSuspendedStateEndsSession()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        controller.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);
        timeProvider.Advance(timeout);

        controller.Cancel();

        Assert.Equal(KeyboardCalibrationStatus.Cancelled, controller.Status);
        Assert.Single(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void FinishFromSuspendedStateSucceedsWithPreservedGeometry()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        controller.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51);
        timeProvider.Advance(timeout);

        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.Succeeded, result.Status);
        Assert.NotNull(result.Layout);
        Assert.Equal(0x10, Assert.Single(Assert.Single(result.Layout.Rows).Cells).Key?.ScanCode);
        Assert.Equal(KeyboardCalibrationStatus.Finished, controller.Status);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void FinishFromSuspendedStateRejectsInvalidGeometryAndKeepsSessionSuspended()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);
        controller.AddEmptySlot();
        timeProvider.Advance(timeout);

        var result = controller.Finish();

        Assert.Equal(KeyboardCalibrationFinishStatus.InvalidGeometry, result.Status);
        Assert.Null(result.Layout);
        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
        Assert.Single(controller.Snapshot.CurrentRow.Cells);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void KeyEventsAndCommandsResetInactivityTimeout()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardCalibrationController(timeout, timeProvider);

        timeProvider.Advance(TimeSpan.FromSeconds(9));
        controller.ProcessKeyEvent(0x10, false, 0x51, KeyboardKeyState.Down);
        timeProvider.Advance(TimeSpan.FromSeconds(9));
        controller.ProcessKeyEvent(0x10, false, 0x51, KeyboardKeyState.Up);
        timeProvider.Advance(TimeSpan.FromSeconds(9));
        controller.AddEmptySlot();
        timeProvider.Advance(TimeSpan.FromSeconds(9));
        controller.AddKey(scanCode: 0x11, extended: false, virtualKey: 0x57);
        timeProvider.Advance(TimeSpan.FromSeconds(9));

        Assert.Equal(KeyboardCalibrationStatus.Active, controller.Status);

        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(KeyboardCalibrationStatus.Suspended, controller.Status);
    }

    [Fact]
    public void AddKeyAfterDisposeThrows()
    {
        var controller = new KeyboardCalibrationController();
        controller.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => controller.AddKey(scanCode: 0x10, extended: false, virtualKey: 0x51));
    }

    [Fact]
    public void SnapshotIsDetachedFromLaterMutations()
    {
        using var controller = new KeyboardCalibrationController();
        Press(controller, scanCode: 0x10, extended: false, virtualKey: 0x51);
        var snapshot = controller.Snapshot;

        Press(controller, scanCode: 0x11, extended: false, virtualKey: 0x57);

        Assert.Single(snapshot.CurrentRow.Cells);
        Assert.Equal(2, controller.Snapshot.CurrentRow.Cells.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveInactivityTimeoutIsRejected(int timeoutMilliseconds)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new KeyboardCalibrationController(
                TimeSpan.FromMilliseconds(timeoutMilliseconds)));

        Assert.Equal("inactivityTimeout", exception.ParamName);
    }

    private static void Press(
        KeyboardCalibrationController controller,
        int scanCode,
        bool extended,
        int virtualKey)
    {
        controller.ProcessKeyEvent(
            scanCode,
            extended,
            virtualKey,
            KeyboardKeyState.Down);
        controller.ProcessKeyEvent(
            scanCode,
            extended,
            virtualKey,
            KeyboardKeyState.Up);
    }

    private static void AssertGeometryEqual(
        KeyboardCalibrationSnapshot expected,
        KeyboardCalibrationSnapshot actual)
    {
        Assert.Equal(expected.CursorPosition, actual.CursorPosition);
        Assert.Equal(expected.CompletedRows.Count, actual.CompletedRows.Count);

        for (var index = 0; index < expected.CompletedRows.Count; index++)
        {
            AssertRowEqual(expected.CompletedRows[index], actual.CompletedRows[index]);
        }

        AssertRowEqual(expected.CurrentRow, actual.CurrentRow);
    }

    private static void AssertRowEqual(CustomKeyboardRow expected, CustomKeyboardRow actual)
    {
        Assert.Equal(expected.Cells.Count, actual.Cells.Count);

        for (var index = 0; index < expected.Cells.Count; index++)
        {
            var expectedCell = expected.Cells[index];
            var actualCell = actual.Cells[index];
            Assert.Equal(expectedCell.WidthUnits, actualCell.WidthUnits);
            Assert.Equal(expectedCell.Key?.ScanCode, actualCell.Key?.ScanCode);
            Assert.Equal(expectedCell.Key?.Extended, actualCell.Key?.Extended);
            Assert.Equal(expectedCell.Key?.VirtualKey, actualCell.Key?.VirtualKey);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public int ActiveTimerCount => _timers.Count(timer => timer.IsActive);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            foreach (var timer in _timers.ToArray())
            {
                timer.Advance(elapsed);
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private TimeSpan _remaining;
            private TimeSpan _period;
            private bool _disposed;

            public ManualTimer(
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _callback = callback;
                _state = state;
                _remaining = dueTime;
                _period = period;
            }

            public bool IsActive =>
                !_disposed && _remaining != Timeout.InfiniteTimeSpan;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed)
                {
                    return false;
                }

                _remaining = dueTime;
                _period = period;
                return true;
            }

            public void Advance(TimeSpan elapsed)
            {
                if (!IsActive)
                {
                    return;
                }

                _remaining -= elapsed;
                if (_remaining > TimeSpan.Zero)
                {
                    return;
                }

                _callback(_state);
                if (_disposed)
                {
                    return;
                }

                _remaining = _period;
            }

            public void Dispose()
            {
                _disposed = true;
                _remaining = Timeout.InfiniteTimeSpan;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
