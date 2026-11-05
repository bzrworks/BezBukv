using System.Globalization;

namespace BezBukv.Core.Platform;

/// <summary>
/// Normalizes raw platform layout metadata into the platform-agnostic catalog contract.
/// </summary>
public static class InstalledKeyboardLayoutCatalog
{
    public static IReadOnlyList<InstalledKeyboardLayout> Normalize(
        IEnumerable<InstalledKeyboardLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);

        var displayNamesByKlid = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var layout in layouts)
        {
            if (layout is null || !TryNormalizeKlid(layout.Klid, out var normalizedKlid))
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(layout.DisplayName)
                ? normalizedKlid
                : layout.DisplayName.Trim();

            if (displayNamesByKlid.TryGetValue(normalizedKlid, out var existingDisplayName))
            {
                displayNamesByKlid[normalizedKlid] = SelectDisplayName(
                    normalizedKlid,
                    existingDisplayName,
                    displayName);
            }
            else
            {
                displayNamesByKlid.Add(normalizedKlid, displayName);
            }
        }

        return displayNamesByKlid
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new InstalledKeyboardLayout(pair.Key, pair.Value))
            .ToArray();
    }

    public static bool TryNormalizeKlid(string? klid, out string normalizedKlid)
    {
        normalizedKlid = string.Empty;

        if (string.IsNullOrWhiteSpace(klid))
        {
            return false;
        }

        var trimmedKlid = klid.Trim();
        if (trimmedKlid.Length > 8 ||
            !uint.TryParse(
                trimmedKlid,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var numericKlid))
        {
            return false;
        }

        normalizedKlid = numericKlid.ToString("x8", CultureInfo.InvariantCulture);
        return true;
    }

    private static string SelectDisplayName(
        string normalizedKlid,
        string firstDisplayName,
        string secondDisplayName)
    {
        if (firstDisplayName == normalizedKlid)
        {
            return secondDisplayName;
        }

        if (secondDisplayName == normalizedKlid)
        {
            return firstDisplayName;
        }

        return string.CompareOrdinal(firstDisplayName, secondDisplayName) <= 0
            ? firstDisplayName
            : secondDisplayName;
    }
}
