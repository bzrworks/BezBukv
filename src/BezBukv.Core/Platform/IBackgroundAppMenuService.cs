namespace BezBukv.Core.Platform;

public interface IBackgroundAppMenuService : IDisposable
{
    event EventHandler? PauseToggleRequested;

    event EventHandler? AutoPeekToggleRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    void Show(
        string pauseMenuText,
        string autoPeekMenuText,
        string settingsMenuText,
        string exitMenuText);

    void UpdateState(
        bool isPaused,
        bool isAutoPeekEnabled,
        string tooltipText);
}
