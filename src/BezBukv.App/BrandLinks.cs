using System.Globalization;

namespace BezBukv.App;

internal static class BrandLinks
{
    public static string SupportUrl => IsRussianUiCulture
        ? "https://bzrworks.com/ru/support"
        : "https://bzrworks.com/support";

    public const string SourceUrl = "https://github.com/bzrworks/BezBukv";

    public static string ReportUrl => IsRussianUiCulture
        ? "https://bzrworks.com/ru/feedback"
        : "https://bzrworks.com/feedback";

    public static bool IsConfigured(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl)
        && (parsedUrl.Scheme == Uri.UriSchemeHttp
            || parsedUrl.Scheme == Uri.UriSchemeHttps);

    private static bool IsRussianUiCulture =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
}
