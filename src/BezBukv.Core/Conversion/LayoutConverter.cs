namespace BezBukv.Core.Conversion;

public enum LayoutConversionDirection
{
    EnglishToRussian,
    RussianToEnglish,
    Auto,
}

public enum LayoutConversionOutcome
{
    Converted,
    NoOp,
}

public sealed record LayoutConversionResult
{
    private LayoutConversionResult(
        LayoutConversionOutcome outcome,
        string originalText,
        LayoutConversionDirection? appliedDirection,
        LayoutConversionDirection? resultLayoutDirection,
        string? convertedText)
    {
        Outcome = outcome;
        OriginalText = originalText;
        AppliedDirection = appliedDirection;
        ResultLayoutDirection = resultLayoutDirection;
        ConvertedText = convertedText;
    }

    public LayoutConversionOutcome Outcome { get; }

    public string OriginalText { get; }

    public LayoutConversionDirection? AppliedDirection { get; }

    public LayoutConversionDirection? ResultLayoutDirection { get; }

    public string? ConvertedText { get; }

    internal static LayoutConversionResult Converted(
        string originalText,
        LayoutConversionDirection? appliedDirection,
        LayoutConversionDirection? resultLayoutDirection,
        string convertedText) =>
        new(
            LayoutConversionOutcome.Converted,
            originalText,
            appliedDirection,
            resultLayoutDirection,
            convertedText);

    internal static LayoutConversionResult NoOp(string originalText) =>
        new(
            LayoutConversionOutcome.NoOp,
            originalText,
            null,
            null,
            null);
}

public static class LayoutConverter
{
    private static readonly IReadOnlyDictionary<char, char> EnglishToRussianMap = BuildMap(
        ("`qwertyuiop[]\\asdfghjkl;'zxcvbnm,./", "ёйцукенгшщзхъ\\фывапролджэячсмитьбю."),
        ("~QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?", "ЁЙЦУКЕНГШЩЗХЪ/ФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,"),
        ("1234567890-=", "1234567890-="),
        ("!@#$%^&*()_+", "!\"№;%:?*()_+"));

    private static readonly IReadOnlyDictionary<char, char> RussianToEnglishMap = BuildMap(
        ("ёйцукенгшщзхъ\\фывапролджэячсмитьбю.", "`qwertyuiop[]\\asdfghjkl;'zxcvbnm,./"),
        ("ЁЙЦУКЕНГШЩЗХЪ/ФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,", "~QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?"),
        ("1234567890-=", "1234567890-="),
        ("!\"№;%:?*()_+", "!@#$%^&*()_+"));

    public static LayoutConversionResult ConvertLayout(
        string input,
        LayoutConversionDirection direction)
    {
        ArgumentNullException.ThrowIfNull(input);

        return direction switch
        {
            LayoutConversionDirection.EnglishToRussian => ConvertExplicit(
                input,
                direction,
                EnglishToRussianMap),
            LayoutConversionDirection.RussianToEnglish => ConvertExplicit(
                input,
                direction,
                RussianToEnglishMap),
            LayoutConversionDirection.Auto => ConvertAuto(input),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };
    }

    internal static bool ContainsLatinOrCyrillicLetter(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        foreach (var character in input)
        {
            if (IsLatinLetter(character) || IsCyrillicLetter(character))
            {
                return true;
            }
        }

        return false;
    }

    private static LayoutConversionResult ConvertAuto(string input)
    {
        var latinLetterCount = 0;
        var cyrillicLetterCount = 0;

        foreach (var character in input)
        {
            if (IsLatinLetter(character))
            {
                latinLetterCount++;
            }
            else if (IsCyrillicLetter(character))
            {
                cyrillicLetterCount++;
            }
        }

        var conversion = ConvertAutoText(input);

        if (latinLetterCount == 0 &&
            cyrillicLetterCount == 0 &&
            conversion.ConvertedText == input)
        {
            return LayoutConversionResult.NoOp(input);
        }

        LayoutConversionDirection? appliedDirection = latinLetterCount == 0 &&
                                                      cyrillicLetterCount == 0
            ? null
            : cyrillicLetterCount == 0
                ? LayoutConversionDirection.EnglishToRussian
                : latinLetterCount == 0
                    ? LayoutConversionDirection.RussianToEnglish
                    : null;

        return LayoutConversionResult.Converted(
            input,
            appliedDirection,
            conversion.ResultLayoutDirection,
            conversion.ConvertedText);
    }

    private static LayoutConversionResult ConvertExplicit(
        string input,
        LayoutConversionDirection direction,
        IReadOnlyDictionary<char, char> map)
    {
        var conversion = ConvertText(input, direction, map);

        return LayoutConversionResult.Converted(
            input,
            direction,
            conversion.ResultLayoutDirection,
            conversion.ConvertedText);
    }

    private static TextConversion ConvertText(
        string input,
        LayoutConversionDirection direction,
        IReadOnlyDictionary<char, char> map)
    {
        var characters = input.ToCharArray();
        LayoutConversionDirection? resultLayoutDirection = null;

        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            var definesLayout = TryGetLetterDirection(character, out _);

            if (map.TryGetValue(character, out var replacement))
            {
                characters[index] = replacement;
                definesLayout |= replacement != character;
            }
            else if (TryGetDeterministicReplacement(
                         character,
                         out _,
                         out var deterministicDirection))
            {
                definesLayout |= deterministicDirection is not null;
            }

            if (definesLayout)
            {
                resultLayoutDirection = direction;
            }
        }

        return new TextConversion(new string(characters), resultLayoutDirection);
    }

    private static TextConversion ConvertAutoText(string input)
    {
        var characters = input.ToCharArray();
        var paragraphBoundaries = FindParagraphBoundaries(input);
        var leftContexts = FindLeftLetterContexts(input, paragraphBoundaries);
        var rightContexts = FindRightLetterContexts(input, paragraphBoundaries);
        LayoutConversionDirection? resultLayoutDirection = null;

        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];

            if (TryGetLetterDirection(character, out var letterDirection))
            {
                var letterMap = GetMap(letterDirection);

                if (letterMap.TryGetValue(character, out var letterReplacement))
                {
                    characters[index] = letterReplacement;
                }

                resultLayoutDirection = letterDirection;
                continue;
            }

            if (IsContextualSymbol(character))
            {
                var contextualDirection = ResolveContextualDirection(
                    character,
                    index,
                    leftContexts[index],
                    rightContexts[index]);

                if (contextualDirection is not null &&
                    GetMap(contextualDirection.Value).TryGetValue(character, out var replacement))
                {
                    characters[index] = replacement;

                    if (replacement != character)
                    {
                        resultLayoutDirection = contextualDirection;
                    }
                }

                continue;
            }

            if (TryGetDeterministicReplacement(
                    character,
                    out var deterministicReplacement,
                    out var deterministicDirection))
            {
                characters[index] = deterministicReplacement;

                if (deterministicDirection is not null)
                {
                    resultLayoutDirection = deterministicDirection;
                }
            }
        }

        return new TextConversion(new string(characters), resultLayoutDirection);
    }

    private static bool[] FindParagraphBoundaries(string input)
    {
        var boundaries = new bool[input.Length + 1];

        for (var index = 0; index < input.Length; index++)
        {
            var lineBreakLength = GetLineBreakLength(input, index);

            if (lineBreakLength == 0)
            {
                continue;
            }

            var nextLineBreakIndex = index + lineBreakLength;

            while (nextLineBreakIndex < input.Length &&
                   input[nextLineBreakIndex] is ' ' or '\t')
            {
                nextLineBreakIndex++;
            }

            if (GetLineBreakLength(input, nextLineBreakIndex) > 0)
            {
                boundaries[nextLineBreakIndex] = true;
            }

            index += lineBreakLength - 1;
        }

        return boundaries;
    }

    private static LetterContext?[] FindLeftLetterContexts(
        string input,
        IReadOnlyList<bool> paragraphBoundaries)
    {
        var contexts = new LetterContext?[input.Length];
        LetterContext? currentContext = null;

        for (var index = 0; index < input.Length; index++)
        {
            if (paragraphBoundaries[index])
            {
                currentContext = null;
            }

            contexts[index] = currentContext;

            if (TryGetLetterDirection(input[index], out var direction))
            {
                currentContext = new LetterContext(direction, index);
            }
        }

        return contexts;
    }

    private static LetterContext?[] FindRightLetterContexts(
        string input,
        IReadOnlyList<bool> paragraphBoundaries)
    {
        var contexts = new LetterContext?[input.Length];
        LetterContext? currentContext = null;

        for (var index = input.Length - 1; index >= 0; index--)
        {
            if (paragraphBoundaries[index + 1])
            {
                currentContext = null;
            }

            contexts[index] = currentContext;

            if (TryGetLetterDirection(input[index], out var direction))
            {
                currentContext = new LetterContext(direction, index);
            }
        }

        return contexts;
    }

    private static LayoutConversionDirection? ResolveContextualDirection(
        char character,
        int index,
        LetterContext? leftContext,
        LetterContext? rightContext)
    {
        if (leftContext is null)
        {
            return rightContext?.Direction;
        }

        if (rightContext is null || leftContext.Value.Direction == rightContext.Value.Direction)
        {
            return leftContext.Value.Direction;
        }

        var leftDistance = index - leftContext.Value.Index;
        var rightDistance = rightContext.Value.Index - index;

        if (character == '/' && leftDistance == rightDistance)
        {
            return null;
        }

        return rightDistance < leftDistance
            ? rightContext.Value.Direction
            : leftContext.Value.Direction;
    }

    private static bool IsContextualSymbol(char character) =>
        EnglishToRussianMap.TryGetValue(character, out var englishReplacement) &&
        RussianToEnglishMap.TryGetValue(character, out var russianReplacement) &&
        englishReplacement != russianReplacement;

    private static bool TryGetDeterministicReplacement(
        char character,
        out char replacement,
        out LayoutConversionDirection? direction)
    {
        var hasEnglishReplacement = EnglishToRussianMap.TryGetValue(
            character,
            out var englishReplacement);
        var hasRussianReplacement = RussianToEnglishMap.TryGetValue(
            character,
            out var russianReplacement);

        if (hasEnglishReplacement && !hasRussianReplacement)
        {
            replacement = englishReplacement;
            direction = LayoutConversionDirection.EnglishToRussian;
            return true;
        }

        if (hasRussianReplacement && !hasEnglishReplacement)
        {
            replacement = russianReplacement;
            direction = LayoutConversionDirection.RussianToEnglish;
            return true;
        }

        if (hasEnglishReplacement && englishReplacement == russianReplacement)
        {
            replacement = englishReplacement;
            direction = null;
            return true;
        }

        replacement = default;
        direction = null;
        return false;
    }

    private static IReadOnlyDictionary<char, char> GetMap(
        LayoutConversionDirection direction) =>
        direction == LayoutConversionDirection.EnglishToRussian
            ? EnglishToRussianMap
            : RussianToEnglishMap;

    private static int GetLineBreakLength(string input, int index)
    {
        if (index >= input.Length)
        {
            return 0;
        }

        return input[index] switch
        {
            '\r' when index + 1 < input.Length && input[index + 1] == '\n' => 2,
            '\r' or '\n' => 1,
            _ => 0,
        };
    }

    private static bool TryGetLetterDirection(
        char character,
        out LayoutConversionDirection direction)
    {
        if (IsLatinLetter(character))
        {
            direction = LayoutConversionDirection.EnglishToRussian;
            return true;
        }

        if (IsCyrillicLetter(character))
        {
            direction = LayoutConversionDirection.RussianToEnglish;
            return true;
        }

        direction = default;
        return false;
    }

    private static Dictionary<char, char> BuildMap(
        params (string Source, string Target)[] rows)
    {
        var map = new Dictionary<char, char>();

        foreach (var (source, target) in rows)
        {
            if (source.Length != target.Length)
            {
                throw new InvalidOperationException("Layout mapping rows must have equal lengths.");
            }

            for (var index = 0; index < source.Length; index++)
            {
                map.Add(source[index], target[index]);
            }
        }

        return map;
    }

    private static bool IsLatinLetter(char character) =>
        char.IsLetter(character) && character is
            (>= '\u0041' and <= '\u005A') or
            (>= '\u0061' and <= '\u007A') or
            (>= '\u00C0' and <= '\u02AF') or
            (>= '\u1D00' and <= '\u1D7F') or
            (>= '\u1D80' and <= '\u1DBF') or
            (>= '\u1E00' and <= '\u1EFF') or
            (>= '\u2C60' and <= '\u2C7F') or
            (>= '\uA720' and <= '\uA7FF') or
            (>= '\uAB30' and <= '\uAB6F') or
            (>= '\uFF21' and <= '\uFF3A') or
            (>= '\uFF41' and <= '\uFF5A');

    private static bool IsCyrillicLetter(char character) =>
        char.IsLetter(character) && character is
            (>= '\u0400' and <= '\u052F') or
            (>= '\u1C80' and <= '\u1C8F') or
            (>= '\u2DE0' and <= '\u2DFF') or
            (>= '\uA640' and <= '\uA69F');

    private readonly record struct LetterContext(
        LayoutConversionDirection Direction,
        int Index);

    private readonly record struct TextConversion(
        string ConvertedText,
        LayoutConversionDirection? ResultLayoutDirection);
}
