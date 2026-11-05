using BezBukv.Core.Conversion;
using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;

namespace BezBukv.Core.Tests;

public sealed class LayoutCorrectionScenarioTests
{
    [Fact]
    public async Task EditableNonTerminalWithoutKeyboardLayoutServicePastesConvertedText()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("ghbdtn"),
        };
        var input = new FakeInputSimulationService();
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal("привет", result.ConvertedText);
        Assert.Equal(["привет"], clipboard.WrittenTexts);
        Assert.Equal(1, input.SendPasteCallCount);
    }

    [Theory]
    [InlineData("ghbdtn", KeyboardInputLanguage.Russian)]
    [InlineData("привет", KeyboardInputLanguage.English)]
    public async Task AutoReplacementSwitchesToLanguageFromResultDirection(
        string sourceText,
        KeyboardInputLanguage expectedLanguage)
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText(sourceText),
        };
        var keyboardLayout = new FakeKeyboardLayoutService();
        var scenario = CreateScenario(
            clipboard,
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal([expectedLanguage], keyboardLayout.TargetLanguages);
    }

    [Fact]
    public async Task ExplicitDirectionRejectsTextWithoutLettersBeforeConversion()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("12345"),
        };
        var input = new FakeInputSimulationService();
        var keyboardLayout = new FakeKeyboardLayoutService();
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            direction: LayoutConversionDirection.EnglishToRussian,
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.NothingToConvert, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Empty(clipboard.WrittenTexts);
        Assert.Equal(0, input.SendPasteCallCount);
        Assert.Empty(keyboardLayout.TargetLanguages);
    }

    [Theory]
    [InlineData(KeyboardLayoutSwitchOutcome.TargetLanguageUnavailable)]
    [InlineData(KeyboardLayoutSwitchOutcome.NoTargetWindow)]
    [InlineData(KeyboardLayoutSwitchOutcome.Failed)]
    public async Task KeyboardLayoutFailureDoesNotChangeOutcomeOrPreventRestore(
        KeyboardLayoutSwitchOutcome switchOutcome)
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        var input = new FakeInputSimulationService
        {
            Calls = calls,
        };
        var keyboardLayout = new FakeKeyboardLayoutService
        {
            Calls = calls,
            SwitchOutcome = switchOutcome,
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1),
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(
            [
                "CaptureSnapshot",
                "SendCopy",
                "SendPaste",
                "SwitchKeyboardLayout",
                "RestoreSnapshot",
                "DisposeSnapshot",
            ],
            calls);
        Assert.Same(snapshot, Assert.Single(clipboard.RestoredSnapshots));
    }

    [Fact]
    public async Task KeyboardLayoutExceptionDoesNotChangeOutcomeOrPreventRestore()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        var input = new FakeInputSimulationService
        {
            Calls = calls,
        };
        var keyboardLayout = new FakeKeyboardLayoutService
        {
            Calls = calls,
            SwitchException = new InvalidOperationException("Switch failed."),
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1),
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(
            [
                "CaptureSnapshot",
                "SendCopy",
                "SendPaste",
                "SwitchKeyboardLayout",
                "RestoreSnapshot",
                "DisposeSnapshot",
            ],
            calls);
        Assert.Same(snapshot, Assert.Single(clipboard.RestoredSnapshots));
    }

    [Fact]
    public async Task CapturesBeforeCopyThenRestoresAfterSuccessfulPasteAndDisposesSnapshot()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        var input = new FakeInputSimulationService
        {
            Calls = calls,
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1));

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(
            ["CaptureSnapshot", "SendCopy", "SendPaste", "RestoreSnapshot", "DisposeSnapshot"],
            calls);
        Assert.Same(snapshot, Assert.Single(clipboard.RestoredSnapshots));
        Assert.Equal(1, snapshot.DisposeCallCount);
    }

    [Fact]
    public async Task ResultWindowRequiredDoesNotRestoreAndDisposesSnapshot()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        var input = new FakeInputSimulationService
        {
            Calls = calls,
        };
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.ReadOnly,
        };
        var keyboardLayout = new FakeKeyboardLayoutService();
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1),
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal(["CaptureSnapshot", "SendCopy", "DisposeSnapshot"], calls);
        Assert.Empty(clipboard.RestoredSnapshots);
        Assert.Equal(1, snapshot.DisposeCallCount);
        Assert.Empty(keyboardLayout.TargetLanguages);
    }

    [Theory]
    [InlineData(LayoutCorrectionOutcome.NoSelection)]
    [InlineData(LayoutCorrectionOutcome.NotPlainText)]
    [InlineData(LayoutCorrectionOutcome.ExceedsLimit)]
    [InlineData(LayoutCorrectionOutcome.NothingToConvert)]
    [InlineData(LayoutCorrectionOutcome.ClipboardUnavailable)]
    [InlineData(LayoutCorrectionOutcome.InputError)]
    public async Task NonSuccessfulOutcomeDoesNotRestoreAndDisposesSnapshot(
        LayoutCorrectionOutcome expectedOutcome)
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = new FakeClipboardService
        {
            Calls = calls,
            CaptureResult = CreateCaptureResult(snapshot),
            HasChangedResult = expectedOutcome != LayoutCorrectionOutcome.NoSelection,
            ReadResult = expectedOutcome switch
            {
                LayoutCorrectionOutcome.NotPlainText =>
                    ClipboardTextReadResult.NoPlainText,
                LayoutCorrectionOutcome.ExceedsLimit =>
                    ClipboardTextReadResult.ExceedsLimit,
                LayoutCorrectionOutcome.NothingToConvert =>
                    ClipboardTextReadResult.FromText("12345"),
                LayoutCorrectionOutcome.ClipboardUnavailable =>
                    ClipboardTextReadResult.ClipboardUnavailable,
                _ => ClipboardTextReadResult.FromText("ghbdtn"),
            },
        };
        var input = new FakeInputSimulationService
        {
            Calls = calls,
            SendCopyResult = expectedOutcome == LayoutCorrectionOutcome.InputError
                ? InputSimulationResult.FromPlatformError(5)
                : InputSimulationResult.Success,
        };
        var timeout = expectedOutcome is LayoutCorrectionOutcome.NoSelection or
            LayoutCorrectionOutcome.ClipboardUnavailable
                ? TimeSpan.FromMilliseconds(1)
                : (TimeSpan?)null;
        var keyboardLayout = new FakeKeyboardLayoutService();
        var scenario = CreateScenario(
            clipboard,
            timeout,
            input,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1),
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.DoesNotContain("RestoreSnapshot", calls);
        Assert.Empty(clipboard.RestoredSnapshots);
        Assert.Equal(1, snapshot.DisposeCallCount);
        Assert.Equal(0, input.SendPasteCallCount);
        Assert.Empty(keyboardLayout.TargetLanguages);
    }

    [Fact]
    public async Task UnavailableSnapshotDoesNotChangeAutoReplacementOutcome()
    {
        List<string> calls = [];
        var clipboard = CreateConvertibleClipboard();
        clipboard.Calls = calls;
        var input = new FakeInputSimulationService
        {
            Calls = calls,
        };
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(["CaptureSnapshot", "SendCopy", "SendPaste"], calls);
        Assert.Empty(clipboard.RestoredSnapshots);
    }

    [Fact]
    public async Task CaptureExceptionDoesNotChangeAutoReplacementOutcome()
    {
        var clipboard = CreateConvertibleClipboard();
        clipboard.CaptureException = new InvalidOperationException("Capture failed.");
        var scenario = CreateScenario(clipboard);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(1, clipboard.CaptureSnapshotCallCount);
        Assert.Empty(clipboard.RestoredSnapshots);
    }

    [Theory]
    [InlineData(ClipboardSnapshotRestoreOutcome.FormatsSkipped)]
    [InlineData(ClipboardSnapshotRestoreOutcome.ClipboardUnavailable)]
    public async Task RestoreFailureResultDoesNotChangeAutoReplacementOutcome(
        ClipboardSnapshotRestoreOutcome restoreOutcome)
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var restoreResult = restoreOutcome == ClipboardSnapshotRestoreOutcome.FormatsSkipped
            ? ClipboardSnapshotRestoreResult.FromCompletedRestore(
                ClipboardContentKinds.Text,
                ClipboardContentKinds.Html,
                restoredFormatCount: 1,
                skippedFormatCount: 1)
            : ClipboardSnapshotRestoreResult.FromClipboardUnavailable(
                ClipboardContentKinds.Text,
                skippedFormatCount: 1);
        var clipboard = CreateCapturedConvertibleClipboard(
            snapshot,
            calls,
            restoreResult);
        var scenario = CreateScenario(
            clipboard,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1));

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Same(snapshot, Assert.Single(clipboard.RestoredSnapshots));
        Assert.Equal(1, snapshot.DisposeCallCount);
    }

    [Fact]
    public async Task RestoreExceptionDoesNotChangeAutoReplacementOutcome()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        clipboard.RestoreException = new InvalidOperationException("Restore failed.");
        var scenario = CreateScenario(
            clipboard,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1));

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(1, clipboard.RestoreSnapshotCallCount);
        Assert.Equal(1, snapshot.DisposeCallCount);
    }

    [Fact]
    public async Task OperationCanceledExceptionAfterCaptureDisposesSnapshotWithoutRestore()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        clipboard.ReadException = new OperationCanceledException();
        var scenario = CreateScenario(clipboard);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await scenario.RunAsync());

        Assert.Empty(clipboard.RestoredSnapshots);
        Assert.Equal(1, snapshot.DisposeCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveClipboardRestoreDelayIsRejected(int delayMilliseconds)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateScenario(
                new FakeClipboardService(),
                clipboardRestoreDelay: TimeSpan.FromMilliseconds(delayMilliseconds)));

        Assert.Equal("clipboardRestoreDelay", exception.ParamName);
    }

    [Fact]
    public async Task EditableTerminalRequiresResultWindowWithoutPasting()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService();
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.Editable,
        };
        var activeApplication = new FakeActiveApplicationService
        {
            ActiveApplication = ActiveApplicationInfo.FromProcessName("WindowsTerminal"),
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement,
            activeApplicationService: activeApplication);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal("привет", result.ConvertedText);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task ResultWindowRequiredReturnsConvertedTextWrittenToClipboard()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("ghbdtn rfr ltkf"),
        };
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.ReadOnly,
        };
        var scenario = CreateScenario(
            clipboard,
            focusedElementService: focusedElement);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal("привет как дела", result.ConvertedText);
        Assert.Equal(["привет как дела"], clipboard.WrittenTexts);
    }

    [Theory]
    [InlineData(
        "ghbdtn",
        LayoutConversionDirection.EnglishToRussian,
        KeyboardInputLanguage.Russian)]
    [InlineData(
        "привет",
        LayoutConversionDirection.RussianToEnglish,
        KeyboardInputLanguage.English)]
    public async Task ResultWindowRequiredCarriesResultDirectionAndTargetLanguage(
        string sourceText,
        LayoutConversionDirection expectedDirection,
        KeyboardInputLanguage expectedLanguage)
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText(sourceText),
        };
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.ReadOnly,
        };
        var scenario = CreateScenario(
            clipboard,
            focusedElementService: focusedElement);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal(expectedDirection, result.ResultLayoutDirection);
        Assert.Equal(expectedLanguage, result.TargetInputLanguage);
    }

    [Fact]
    public async Task ReadOnlyElementRequiresResultWindowWithoutPasting()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService();
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.ReadOnly,
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal(0, input.SendPasteCallCount);
        Assert.Equal(1, focusedElement.GetEditabilityCallCount);
        Assert.Equal(0, focusedElement.GetEditabilityWithDiagnosticsCallCount);
    }

    [Fact]
    public async Task UnknownElementWithDefaultFlagRequiresResultWindowWithoutPasting()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService();
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.Unknown,
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task UnknownElementWithEnabledFlagAndNonTerminalPastesConvertedText()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService();
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.Unknown,
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement,
            pasteIntoUnknown: true);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal("привет", result.ConvertedText);
        Assert.Equal(1, input.SendPasteCallCount);
    }

    [Fact]
    public async Task UnknownElementWithEnabledFlagAndTerminalRequiresResultWindowWithoutPasting()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService();
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.Unknown,
        };
        var activeApplication = new FakeActiveApplicationService
        {
            ActiveApplication = ActiveApplicationInfo.FromProcessName("cmd"),
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement,
            activeApplicationService: activeApplication,
            pasteIntoUnknown: true);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task UnavailableActiveApplicationRequiresResultWindowWithoutPasting()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService();
        var activeApplication = new FakeActiveApplicationService
        {
            ActiveApplication = ActiveApplicationInfo.Unavailable,
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            activeApplicationService: activeApplication);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task FailedPasteReturnsInputErrorWithoutReportingAutoReplacement()
    {
        var clipboard = CreateConvertibleClipboard();
        var input = new FakeInputSimulationService
        {
            SendPasteResult = InputSimulationResult.FromPlatformError(5),
        };
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.InputError, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Equal(1, input.SendPasteCallCount);
    }

    [Fact]
    public async Task UnavailableClipboardWriteReturnsClipboardUnavailableWithoutPasting()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("ghbdtn"),
            WriteResult = ClipboardTextWriteResult.ClipboardUnavailable,
        };
        var input = new FakeInputSimulationService();
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ClipboardUnavailable, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task UnchangedClipboardUntilTimeoutReturnsNoSelection()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = false,
        };
        var scenario = CreateScenario(clipboard, TimeSpan.FromMilliseconds(1));

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.NoSelection, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Empty(clipboard.WrittenTexts);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("\u00A0\u2003")]
    public async Task EmptyOrWhitespaceOnlyTextReturnsNoSelectionWithoutWritingOrPasting(
        string text)
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText(text),
        };
        var input = new FakeInputSimulationService();
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.NoSelection, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Empty(clipboard.WrittenTexts);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task NoPlainTextReturnsNotPlainText()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.NoPlainText,
        };
        var scenario = CreateScenario(clipboard);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.NotPlainText, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Empty(clipboard.WrittenTexts);
    }

    [Fact]
    public async Task TextExceedingLimitReturnsExceedsLimit()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.ExceedsLimit,
        };
        var scenario = CreateScenario(clipboard);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ExceedsLimit, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Empty(clipboard.WrittenTexts);
    }

    [Theory]
    [InlineData(LayoutConversionDirection.Auto)]
    [InlineData(LayoutConversionDirection.EnglishToRussian)]
    [InlineData(LayoutConversionDirection.RussianToEnglish)]
    public async Task TextWithoutLatinOrCyrillicLettersReturnsNothingToConvertInEveryDirection(
        LayoutConversionDirection direction)
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText(
                "123 @&? 🙂 漢字 العربية"),
        };
        var input = new FakeInputSimulationService();
        var keyboardLayout = new FakeKeyboardLayoutService();
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            direction: direction,
            keyboardLayoutService: keyboardLayout);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.NothingToConvert, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Empty(clipboard.WrittenTexts);
        Assert.Equal(0, input.SendPasteCallCount);
        Assert.Empty(keyboardLayout.TargetLanguages);
    }

    [Fact]
    public async Task TransientUnavailableReadIsRetriedAndCanStillSucceed()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResults =
            [
                ClipboardTextReadResult.ClipboardUnavailable,
                ClipboardTextReadResult.FromText("ghbdtn"),
            ],
        };
        var input = new FakeInputSimulationService();
        var scenario = CreateScenario(
            clipboard,
            timeout: TimeSpan.FromSeconds(1),
            inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal("привет", result.ConvertedText);
        Assert.Equal(2, clipboard.ReadUnicodeTextCallCount);
        Assert.Equal(["привет"], clipboard.WrittenTexts);
        Assert.Equal(1, input.SendPasteCallCount);
    }

    [Fact]
    public async Task UnavailableReadUntilTimeoutReturnsClipboardUnavailable()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.ClipboardUnavailable,
        };
        var input = new FakeInputSimulationService();
        var scenario = CreateScenario(
            clipboard,
            timeout: TimeSpan.FromMilliseconds(25),
            inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.ClipboardUnavailable, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.True(clipboard.ReadUnicodeTextCallCount > 0);
        Assert.Empty(clipboard.WrittenTexts);
        Assert.Equal(0, input.SendPasteCallCount);
    }

    [Fact]
    public async Task EqualLatinAndCyrillicCountsAreFlippedWrittenAndPasted()
    {
        var clipboard = new FakeClipboardService
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("aб"),
        };
        var input = new FakeInputSimulationService();
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal("ф,", result.ConvertedText);
        Assert.Equal(["ф,"], clipboard.WrittenTexts);
        Assert.Equal(1, input.SendPasteCallCount);
    }

    [Fact]
    public async Task FailedSendCopyReturnsInputError()
    {
        var clipboard = new FakeClipboardService();
        var input = new FakeInputSimulationService
        {
            SendCopyResult = InputSimulationResult.FromPlatformError(5),
        };
        var scenario = CreateScenario(clipboard, inputSimulationService: input);

        var result = await scenario.RunAsync();

        Assert.Equal(LayoutCorrectionOutcome.InputError, result.Outcome);
        Assert.Null(result.ConvertedText);
        Assert.Equal(0, clipboard.HasChangedCallCount);
        Assert.Empty(clipboard.WrittenTexts);
    }

    [Fact]
    public async Task DiagnosticsEmitCorrelatedAutoPastePipelineWithoutRawText()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        var input = new FakeInputSimulationService
        {
            Calls = calls,
            SendPasteResult = InputSimulationResult.FromSuccessfulSend(
                physicalControlDown: false,
                plannedInputCount: 4,
                sentInputCount: 4),
        };
        var activeApplication = new FakeActiveApplicationService
        {
            ActiveApplication = ActiveApplicationInfo.FromProcessName(
                "notepad",
                windowHandle: 123),
        };
        var keyboardLayout = new FakeKeyboardLayoutService
        {
            Calls = calls,
        };
        var diagnosticLogger = new FakeDiagnosticLogger();
        var diagnosticRun = diagnosticLogger.StartRun();
        var focusedElement = new FakeFocusedElementService
        {
            DiagnosticResult = new FocusedElementEditabilityDiagnosticResult(
                FocusedElementEditability.Editable,
                "ControlType.Edit",
                FocusedElementEditabilityDiagnosticValues.ValuePatternBranch,
                ValuePatternAvailable: true,
                TextPatternAvailable: null),
        };
        var scenario = CreateScenario(
            clipboard,
            inputSimulationService: input,
            focusedElementService: focusedElement,
            activeApplicationService: activeApplication,
            clipboardRestoreDelay: TimeSpan.FromMilliseconds(1),
            keyboardLayoutService: keyboardLayout,
            diagnosticLogger: diagnosticLogger);

        var result = await scenario.RunAsync(diagnosticRun);

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal(
            [
                "clipboard_snapshot",
                "clipboard_copy",
                "clipboard_read",
                "clipboard_converted_payload",
                "clipboard_write",
                "active_application",
                "editability",
                "send_paste",
                "route_decision",
                "layout_switch",
                "clipboard_restore",
            ],
            diagnosticLogger.Events.Select(entry => entry.EventName));
        Assert.All(
            diagnosticLogger.Events,
            entry => Assert.Equal(diagnosticRun.RunId, entry.Run.RunId));

        var payloadEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "clipboard_converted_payload");
        var payloadMetrics = Assert.IsType<DiagnosticPayloadMetrics>(
            payloadEvent.Fields["payloadMetrics"]);
        Assert.Equal(6, payloadMetrics.CharCount);
        Assert.NotNull(payloadMetrics.Fingerprint);
        Assert.DoesNotContain(
            "ghbdtn",
            string.Join('|', payloadEvent.Fields.Values));
        Assert.DoesNotContain(
            "привет",
            string.Join('|', payloadEvent.Fields.Values));

        var editabilityEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "editability");
        Assert.Equal("editable", editabilityEvent.Fields["outcome"]);
        Assert.Equal("ControlType.Edit", editabilityEvent.Fields["controlType"]);
        Assert.Equal("value_pattern", editabilityEvent.Fields["decisionBranch"]);
        Assert.Equal(true, editabilityEvent.Fields["valuePatternAvailable"]);
        Assert.Null(editabilityEvent.Fields["textPatternAvailable"]);
        Assert.Equal(0, focusedElement.GetEditabilityCallCount);
        Assert.Equal(1, focusedElement.GetEditabilityWithDiagnosticsCallCount);

        var pasteEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "send_paste");
        Assert.Equal("auto", pasteEvent.Fields["source"]);
        Assert.Null(pasteEvent.Fields["clickId"]);
        Assert.Equal(123L, pasteEvent.Fields["foregroundWindowHandleBefore"]);
        Assert.Equal(123L, pasteEvent.Fields["foregroundWindowHandleAfter"]);
        Assert.Equal(false, pasteEvent.Fields["physicalCtrlDown"]);
        Assert.Equal(4, pasteEvent.Fields["plannedInputCount"]);
        Assert.Equal(4, pasteEvent.Fields["sentInputCount"]);

        var routeEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "route_decision");
        Assert.Equal("auto", routeEvent.Fields["route"]);
        Assert.Equal("editable", routeEvent.Fields["reason"]);
    }

    [Fact]
    public async Task DiagnosticLoggerFailureDoesNotChangeScenarioOutcome()
    {
        var clipboard = CreateConvertibleClipboard();
        var diagnosticLogger = new ThrowingDiagnosticLogger();
        var scenario = CreateScenario(
            clipboard,
            diagnosticLogger: diagnosticLogger);

        var result = await scenario.RunAsync(diagnosticLogger.StartRun());

        Assert.Equal(LayoutCorrectionOutcome.AutoReplacementCompleted, result.Outcome);
        Assert.Equal("привет", result.ConvertedText);
    }

    [Fact]
    public async Task DiagnosticsDescribeBlockedUnknownRouteAndSkippedFollowUpActions()
    {
        List<string> calls = [];
        var snapshot = new FakeClipboardSnapshot(calls);
        var clipboard = CreateCapturedConvertibleClipboard(snapshot, calls);
        var focusedElement = new FakeFocusedElementService
        {
            Editability = FocusedElementEditability.Unknown,
        };
        var diagnosticLogger = new FakeDiagnosticLogger();
        var diagnosticRun = diagnosticLogger.StartRun();
        var scenario = CreateScenario(
            clipboard,
            focusedElementService: focusedElement,
            diagnosticLogger: diagnosticLogger);

        var result = await scenario.RunAsync(diagnosticRun);

        Assert.Equal(LayoutCorrectionOutcome.ResultWindowRequired, result.Outcome);

        var routeEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "route_decision");
        Assert.Equal("unknown", routeEvent.Fields["editability"]);
        Assert.Equal("result", routeEvent.Fields["route"]);
        Assert.Equal("unknown_blocked", routeEvent.Fields["reason"]);

        Assert.DoesNotContain(
            diagnosticLogger.Events,
            entry => entry.EventName == "send_paste");

        var layoutEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "layout_switch");
        Assert.Equal(false, layoutEvent.Fields["requested"]);
        Assert.Equal("route_result", layoutEvent.Fields["reason"]);

        var restoreEvent = Assert.Single(
            diagnosticLogger.Events,
            entry => entry.EventName == "clipboard_restore");
        Assert.Equal(false, restoreEvent.Fields["scheduled"]);
        Assert.Equal("not_scheduled", restoreEvent.Fields["outcome"]);
        Assert.Equal("outcome_not_auto", restoreEvent.Fields["reason"]);
    }

    private static LayoutCorrectionScenario CreateScenario(
        FakeClipboardService clipboardService,
        TimeSpan? timeout = null,
        FakeInputSimulationService? inputSimulationService = null,
        FakeFocusedElementService? focusedElementService = null,
        FakeActiveApplicationService? activeApplicationService = null,
        bool pasteIntoUnknown = false,
        TimeSpan? clipboardRestoreDelay = null,
        LayoutConversionDirection direction = LayoutConversionDirection.Auto,
        FakeKeyboardLayoutService? keyboardLayoutService = null,
        IDiagnosticLogger? diagnosticLogger = null) =>
        new(
            clipboardService,
            inputSimulationService ?? new FakeInputSimulationService(),
            focusedElementService ?? new FakeFocusedElementService(),
            activeApplicationService ?? new FakeActiveApplicationService(),
            timeout,
            direction,
            pasteIntoUnknown: pasteIntoUnknown,
            clipboardRestoreDelay: clipboardRestoreDelay,
            keyboardLayoutService: keyboardLayoutService,
            diagnosticLogger: diagnosticLogger);

    private static FakeClipboardService CreateConvertibleClipboard() =>
        new()
        {
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("ghbdtn"),
        };

    private static FakeClipboardService CreateCapturedConvertibleClipboard(
        FakeClipboardSnapshot snapshot,
        List<string> calls,
        ClipboardSnapshotRestoreResult? restoreResult = null) =>
        new()
        {
            Calls = calls,
            CaptureResult = CreateCaptureResult(snapshot),
            RestoreResult = restoreResult ??
                ClipboardSnapshotRestoreResult.FromCompletedRestore(
                    ClipboardContentKinds.Text,
                    ClipboardContentKinds.None,
                    restoredFormatCount: 1,
                    skippedFormatCount: 0),
            HasChangedResult = true,
            ReadResult = ClipboardTextReadResult.FromText("ghbdtn"),
        };

    private static ClipboardSnapshotCaptureResult CreateCaptureResult(
        IClipboardSnapshot snapshot) =>
        ClipboardSnapshotCaptureResult.FromSnapshot(
            snapshot,
            ClipboardContentKinds.Text,
            ClipboardContentKinds.None,
            capturedFormatCount: 1,
            nonCapturedFormatCount: 0);

    private sealed class FakeClipboardService : IClipboardService
    {
        public List<string> Calls { get; set; } = [];

        public ClipboardSnapshotCaptureResult CaptureResult { get; init; } =
            ClipboardSnapshotCaptureResult.ClipboardUnavailable;

        public ClipboardSnapshotRestoreResult RestoreResult { get; init; } =
            ClipboardSnapshotRestoreResult.FromCompletedRestore(
                ClipboardContentKinds.None,
                ClipboardContentKinds.None,
                restoredFormatCount: 0,
                skippedFormatCount: 0);

        public Exception? CaptureException { get; set; }

        public Exception? RestoreException { get; set; }

        public Exception? ReadException { get; set; }

        public bool HasChangedResult { get; init; }

        public ClipboardTextReadResult ReadResult { get; init; } =
            ClipboardTextReadResult.FromText(string.Empty);

        public IReadOnlyList<ClipboardTextReadResult> ReadResults { get; init; } = [];

        public ClipboardTextWriteResult WriteResult { get; init; } =
            ClipboardTextWriteResult.Success;

        public List<string> WrittenTexts { get; } = [];

        public List<IClipboardSnapshot> RestoredSnapshots { get; } = [];

        public int CaptureSnapshotCallCount { get; private set; }

        public int RestoreSnapshotCallCount { get; private set; }

        public int HasChangedCallCount { get; private set; }

        public int ReadUnicodeTextCallCount { get; private set; }

        private int _readResultIndex;

        public ClipboardStateToken CaptureStateToken() => new(1);

        public bool HasChanged(ClipboardStateToken stateToken)
        {
            Assert.Equal(new ClipboardStateToken(1), stateToken);
            HasChangedCallCount++;
            return HasChangedResult;
        }

        public ValueTask<ClipboardSnapshotCaptureResult> CaptureSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add("CaptureSnapshot");
            CaptureSnapshotCallCount++;

            if (CaptureException is not null)
            {
                throw CaptureException;
            }

            return ValueTask.FromResult(CaptureResult);
        }

        public ValueTask<ClipboardSnapshotRestoreResult> RestoreSnapshotAsync(
            IClipboardSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("RestoreSnapshot");
            RestoreSnapshotCallCount++;

            if (RestoreException is not null)
            {
                throw RestoreException;
            }

            RestoredSnapshots.Add(snapshot);
            return ValueTask.FromResult(RestoreResult);
        }

        public ValueTask<ClipboardTextReadResult> ReadUnicodeTextAsync(
            CancellationToken cancellationToken = default)
        {
            ReadUnicodeTextCallCount++;

            if (ReadException is not null)
            {
                throw ReadException;
            }

            if (ReadResults.Count == 0)
            {
                return ValueTask.FromResult(ReadResult);
            }

            var result = ReadResults[
                Math.Min(_readResultIndex, ReadResults.Count - 1)];
            _readResultIndex++;
            return ValueTask.FromResult(result);
        }

        public ValueTask<ClipboardTextWriteResult> SetUnicodeTextAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            WrittenTexts.Add(text);
            return ValueTask.FromResult(WriteResult);
        }
    }

    private sealed class FakeInputSimulationService : IInputSimulationService
    {
        public InputSimulationResult SendCopyResult { get; init; } =
            InputSimulationResult.Success;

        public InputSimulationResult SendPasteResult { get; init; } =
            InputSimulationResult.Success;

        public List<string> Calls { get; init; } = [];

        public int SendPasteCallCount { get; private set; }

        public InputSimulationResult SendCopy()
        {
            Calls.Add("SendCopy");
            return SendCopyResult;
        }

        public InputSimulationResult SendPaste()
        {
            Calls.Add("SendPaste");
            SendPasteCallCount++;
            return SendPasteResult;
        }
    }

    private sealed class FakeKeyboardLayoutService : IKeyboardLayoutService
    {
        public KeyboardLayoutSwitchOutcome SwitchOutcome { get; init; } =
            KeyboardLayoutSwitchOutcome.Success;

        public Exception? SwitchException { get; init; }

        public List<string> Calls { get; init; } = [];

        public List<KeyboardInputLanguage> TargetLanguages { get; } = [];

        public KeyboardLayoutSwitchOutcome SwitchActiveInputLanguage(
            KeyboardInputLanguage targetLanguage)
        {
            Calls.Add("SwitchKeyboardLayout");
            TargetLanguages.Add(targetLanguage);

            if (SwitchException is not null)
            {
                throw SwitchException;
            }

            return SwitchOutcome;
        }
    }

    private sealed class FakeClipboardSnapshot(List<string> calls) : IClipboardSnapshot
    {
        public int DisposeCallCount { get; private set; }

        public void Dispose()
        {
            calls.Add("DisposeSnapshot");
            DisposeCallCount++;
        }
    }

    private sealed class FakeFocusedElementService : IFocusedElementService
    {
        public FocusedElementEditability Editability { get; init; } =
            FocusedElementEditability.Editable;

        public FocusedElementEditabilityDiagnosticResult? DiagnosticResult { get; init; }

        public int GetEditabilityCallCount { get; private set; }

        public int GetEditabilityWithDiagnosticsCallCount { get; private set; }

        public FocusedElementEditability GetEditability()
        {
            GetEditabilityCallCount++;
            return Editability;
        }

        public FocusedElementEditabilityDiagnosticResult GetEditabilityWithDiagnostics()
        {
            GetEditabilityWithDiagnosticsCallCount++;
            return DiagnosticResult ?? new FocusedElementEditabilityDiagnosticResult(
                Editability,
                FocusedElementEditabilityDiagnosticValues.UnavailableControlType,
                FocusedElementEditabilityDiagnosticValues.ExceptionBranch,
                ValuePatternAvailable: null,
                TextPatternAvailable: null);
        }
    }

    private sealed class FakeActiveApplicationService : IActiveApplicationService
    {
        public ActiveApplicationInfo ActiveApplication { get; init; } =
            ActiveApplicationInfo.FromProcessName("notepad");

        public ActiveApplicationInfo GetActiveApplication() => ActiveApplication;
    }

    private sealed class FakeDiagnosticLogger : IDiagnosticLogger
    {
        public List<DiagnosticEvent> Events { get; } = [];

        public DiagnosticRunContext StartRun() => DiagnosticRunContext.Start();

        public void Log(
            DiagnosticRunContext run,
            string eventName,
            IReadOnlyDictionary<string, object?>? fields = null)
        {
            Events.Add(
                new DiagnosticEvent(
                    run,
                    eventName,
                    fields ?? new Dictionary<string, object?>()));
        }
    }

    private sealed class ThrowingDiagnosticLogger : IDiagnosticLogger
    {
        public DiagnosticRunContext StartRun() => DiagnosticRunContext.Start();

        public void Log(
            DiagnosticRunContext run,
            string eventName,
            IReadOnlyDictionary<string, object?>? fields = null) =>
            throw new InvalidOperationException("Diagnostic sink failed.");
    }

    private sealed record DiagnosticEvent(
        DiagnosticRunContext Run,
        string EventName,
        IReadOnlyDictionary<string, object?> Fields);
}
