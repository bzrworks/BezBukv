using System.Globalization;

namespace BezBukv.Core.Localization;

public sealed class LanguageService : ILanguageService
{
    public AppLanguage Current { get; private set; } = AppLanguage.System;

    public event EventHandler? LanguageChanged;

    public void Set(AppLanguage language)
    {
        var culture = language switch
        {
            AppLanguage.System => CultureInfo.InstalledUICulture,
            AppLanguage.English => CultureInfo.GetCultureInfo("en"),
            AppLanguage.Russian => CultureInfo.GetCultureInfo("ru"),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };

        if (Current == language && CultureInfo.CurrentUICulture.Equals(culture))
        {
            return;
        }

        CultureInfo.CurrentUICulture = culture;
        Current = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
