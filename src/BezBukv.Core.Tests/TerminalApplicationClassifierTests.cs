using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class TerminalApplicationClassifierTests
{
    [Theory]
    [InlineData("cmd")]
    [InlineData("conhost")]
    [InlineData("OpenConsole")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("WindowsTerminal")]
    [InlineData("wt")]
    [InlineData("CMD.EXE")]
    [InlineData("windowsterminal.exe")]
    public void KnownTerminalProcessNameIsTerminal(string processName)
    {
        var isTerminal = TerminalApplicationClassifier.IsTerminalProcessName(processName);

        Assert.True(isTerminal);
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("Telegram")]
    [InlineData("powershell_ise")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherOrMissingProcessNameIsNotTerminal(string? processName)
    {
        var isTerminal = TerminalApplicationClassifier.IsTerminalProcessName(processName);

        Assert.False(isTerminal);
    }

    [Fact]
    public void FromProcessNameCreatesDetectedSnapshot()
    {
        var application = ActiveApplicationInfo.FromProcessName(" WindowsTerminal ");

        Assert.Equal(ActiveApplicationDetectionOutcome.Detected, application.Outcome);
        Assert.Equal("WindowsTerminal", application.ProcessName);
        Assert.True(application.IsTerminal);
    }

    [Fact]
    public void UnavailableSnapshotHasNoIdentityAndIsNotTerminal()
    {
        var application = ActiveApplicationInfo.Unavailable;

        Assert.Equal(ActiveApplicationDetectionOutcome.Unavailable, application.Outcome);
        Assert.Null(application.ProcessName);
        Assert.False(application.IsTerminal);
    }
}
