namespace BezBukv.Core.Platform;

public interface IKeyboardLayoutService
{
    KeyboardLayoutSwitchOutcome SwitchActiveInputLanguage(
        KeyboardInputLanguage targetLanguage);
}

public enum KeyboardInputLanguage
{
    English,
    Russian,
}

public enum KeyboardLayoutSwitchOutcome
{
    Success,
    TargetLanguageUnavailable,
    NoTargetWindow,
    Failed,
}
