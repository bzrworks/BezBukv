namespace BezBukv.Core.Keyboard;

/// <summary>
/// The base and shifted symbols associated with a physical key in Latin and Cyrillic layouts.
/// </summary>
public readonly record struct KeyboardKeySymbols(
    string LatinBaseSymbol,
    string LatinShiftSymbol,
    string CyrillicBaseSymbol,
    string CyrillicShiftSymbol)
{
    /// <summary>
    /// Returns non-empty symbols in Latin-base, Latin-Shift, Cyrillic-base, Cyrillic-Shift order,
    /// preserving the first occurrence when symbols differ only by case.
    /// </summary>
    public IReadOnlyList<string> GetDistinctSymbols()
    {
        var distinctSymbols = new List<string>(4);
        var knownSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfDistinct(LatinBaseSymbol, distinctSymbols, knownSymbols);
        AddIfDistinct(LatinShiftSymbol, distinctSymbols, knownSymbols);
        AddIfDistinct(CyrillicBaseSymbol, distinctSymbols, knownSymbols);
        AddIfDistinct(CyrillicShiftSymbol, distinctSymbols, knownSymbols);

        return distinctSymbols.AsReadOnly();
    }

    private static void AddIfDistinct(
        string? symbol,
        ICollection<string> distinctSymbols,
        ISet<string> knownSymbols)
    {
        if (!string.IsNullOrEmpty(symbol) && knownSymbols.Add(symbol))
        {
            distinctSymbols.Add(symbol);
        }
    }
}
