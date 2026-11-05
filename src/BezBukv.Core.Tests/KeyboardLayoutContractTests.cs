using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class KeyboardLayoutContractTests
{
    [Theory]
    [InlineData(KeyboardInputLanguage.English)]
    [InlineData(KeyboardInputLanguage.Russian)]
    public void SwitchForwardsTargetLanguage(KeyboardInputLanguage targetLanguage)
    {
        var service = new FakeKeyboardLayoutService(KeyboardLayoutSwitchOutcome.Success);

        var outcome = service.SwitchActiveInputLanguage(targetLanguage);

        Assert.Equal(KeyboardLayoutSwitchOutcome.Success, outcome);
        Assert.Equal(targetLanguage, service.LastRequestedLanguage);
        Assert.Equal(1, service.RequestCount);
    }

    [Theory]
    [InlineData(KeyboardLayoutSwitchOutcome.Success)]
    [InlineData(KeyboardLayoutSwitchOutcome.TargetLanguageUnavailable)]
    [InlineData(KeyboardLayoutSwitchOutcome.NoTargetWindow)]
    [InlineData(KeyboardLayoutSwitchOutcome.Failed)]
    public void SwitchReflectsImplementationOutcome(KeyboardLayoutSwitchOutcome expectedOutcome)
    {
        var service = new FakeKeyboardLayoutService(expectedOutcome);

        var outcome = service.SwitchActiveInputLanguage(KeyboardInputLanguage.English);

        Assert.Equal(expectedOutcome, outcome);
    }

    private sealed class FakeKeyboardLayoutService(
        KeyboardLayoutSwitchOutcome outcome) : IKeyboardLayoutService
    {
        internal KeyboardInputLanguage? LastRequestedLanguage { get; private set; }

        internal int RequestCount { get; private set; }

        public KeyboardLayoutSwitchOutcome SwitchActiveInputLanguage(
            KeyboardInputLanguage targetLanguage)
        {
            LastRequestedLanguage = targetLanguage;
            RequestCount++;
            return outcome;
        }
    }
}
