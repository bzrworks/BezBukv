namespace BezBukv.Core.Localization;

public interface ILanguageService
{
    AppLanguage Current { get; }

    event EventHandler? LanguageChanged;

    void Set(AppLanguage language);
}
