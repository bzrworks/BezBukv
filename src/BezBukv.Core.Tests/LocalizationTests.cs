using System.Globalization;
using BezBukv.Core.Localization;
using BezBukv.Core.Resources;

namespace BezBukv.Core.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void UnsupportedCultureFallsBackToBaseEnglishCoreResource()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            var unsupportedCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = unsupportedCulture;

            var fallbackValue = CoreStrings.ResourceManager.GetString(
                nameof(CoreStrings.Error_NoSelection),
                unsupportedCulture);
            var englishValue = CoreStrings.ResourceManager.GetString(
                nameof(CoreStrings.Error_NoSelection),
                CultureInfo.GetCultureInfo("en"));

            Assert.False(string.IsNullOrWhiteSpace(fallbackValue));
            Assert.Equal(englishValue, fallbackValue);
            Assert.Equal(fallbackValue, CoreStrings.Error_NoSelection);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void CoreStringResolvesFromBaseEnglishResource()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            var englishValue = CoreStrings.ResourceManager.GetString(
                nameof(CoreStrings.Error_NoSelection),
                CultureInfo.CurrentUICulture);
            var russianValue = CoreStrings.ResourceManager.GetString(
                nameof(CoreStrings.Error_NoSelection),
                CultureInfo.GetCultureInfo("ru"));

            Assert.False(string.IsNullOrWhiteSpace(englishValue));
            Assert.False(string.IsNullOrWhiteSpace(russianValue));
            Assert.NotEqual(englishValue, russianValue);
            Assert.Equal(englishValue, CoreStrings.Error_NoSelection);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(AppLanguage.English, "en")]
    [InlineData(AppLanguage.Russian, "ru")]
    public void SetUpdatesCurrentLanguageCultureAndRaisesEvent(
        AppLanguage language,
        string expectedCultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            var service = new LanguageService();
            var languageChangedCount = 0;
            service.LanguageChanged += (_, _) => languageChangedCount++;

            service.Set(language);

            Assert.Equal(language, service.Current);
            Assert.Equal(expectedCultureName, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(1, languageChangedCount);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void SetSystemRestoresInstalledUiCulture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            var service = new LanguageService();
            service.Set(AppLanguage.English);

            service.Set(AppLanguage.System);

            Assert.Equal(AppLanguage.System, service.Current);
            Assert.Equal(CultureInfo.InstalledUICulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void RussianCultureResolvesRussianCoreResource()
    {
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            var service = new LanguageService();
            service.Set(AppLanguage.Russian);

            var russianValue = CoreStrings.ResourceManager.GetString(
                nameof(CoreStrings.Error_NoSelection),
                CultureInfo.CurrentUICulture);
            var englishValue = CoreStrings.ResourceManager.GetString(
                nameof(CoreStrings.Error_NoSelection),
                CultureInfo.GetCultureInfo("en"));

            Assert.False(string.IsNullOrWhiteSpace(englishValue));
            Assert.False(string.IsNullOrWhiteSpace(russianValue));
            Assert.NotEqual(englishValue, russianValue);
            Assert.Equal(russianValue, CoreStrings.Error_NoSelection);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
