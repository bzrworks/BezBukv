namespace BezBukv.Core.Platform;

public interface IActiveWindowFocusService
{
    ActiveWindowFocusToken? CaptureActiveWindow();

    bool TryRestoreFocus(ActiveWindowFocusToken? token);
}

public abstract class ActiveWindowFocusToken
{
    protected ActiveWindowFocusToken()
    {
    }
}
