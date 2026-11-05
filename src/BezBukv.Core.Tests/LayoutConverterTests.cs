using BezBukv.Core.Conversion;

namespace BezBukv.Core.Tests;

public sealed class LayoutConverterTests
{
    [Theory]
    [InlineData("ghbdtn", "привет")]
    [InlineData("Ghbdtn", "Привет")]
    [InlineData("rfr ltkf?", "как дела,")]
    [InlineData("rfr ltkf&", "как дела?")]
    public void EnglishToRussianPassesCanonicalExamples(string input, string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.EnglishToRussian, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("rfr ltkf?", "как дела,")]
    [InlineData("rfr ltkf&", "как дела?")]
    public void AutoPassesPositionalQuestionMarkCanonicalExamples(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.EnglishToRussian, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("руддщ", "hello")]
    [InlineData("Руддщ", "Hello")]
    public void RussianToEnglishPassesCanonicalExamples(string input, string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.RussianToEnglish);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.RussianToEnglish, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Fact]
    public void AutoReturnsNoOpForEmptyInput()
    {
        var result = LayoutConverter.ConvertLayout(string.Empty, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.NoOp, result.Outcome);
        Assert.Equal(string.Empty, result.OriginalText);
        Assert.Null(result.AppliedDirection);
        Assert.Null(result.ConvertedText);
    }

    [Fact]
    public void AutoReturnsNoOpForInputWithoutLatinOrCyrillicLetters()
    {
        const string input = "123 !?\t\r\n🙂漢";

        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.NoOp, result.Outcome);
        Assert.Equal(input, result.OriginalText);
        Assert.Null(result.AppliedDirection);
        Assert.Null(result.ConvertedText);
    }

    [Theory]
    [InlineData("@", "\"")]
    [InlineData("&", "?")]
    [InlineData("#", "№")]
    [InlineData("[", "х")]
    [InlineData("№", "#")]
    public void AutoConvertsSingleLayoutSymbolsWithoutLetterContext(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Null(result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("ghб;", "пр,$")]
    [InlineData("рудx;", "helчж")]
    public void AutoFlipsEveryLetterAndRoutesClosingPunctuationByNearestFragment(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Null(result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Fact]
    public void AutoRoutesClosingPunctuationToThePreviousFragment()
    {
        var result = LayoutConverter.ConvertLayout("aб;", LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal("aб;", result.OriginalText);
        Assert.Null(result.AppliedDirection);
        Assert.Equal("ф,$", result.ConvertedText);
    }

    [Theory]
    [InlineData("ghbdtn;@,", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("руддщ;№б", LayoutConversionDirection.RussianToEnglish)]
    public void AutoMatchesExplicitDirectionForMonoalphabeticInput(
        string input,
        LayoutConversionDirection direction)
    {
        var autoResult = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);
        var explicitResult = LayoutConverter.ConvertLayout(input, direction);

        Assert.Equal(LayoutConversionOutcome.Converted, autoResult.Outcome);
        Assert.Equal(direction, autoResult.AppliedDirection);
        Assert.Equal(explicitResult.ConvertedText, autoResult.ConvertedText);
    }

    [Theory]
    [InlineData("ghbdtn.,/?;:\"", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("руддщ.,/?;:\"", LayoutConversionDirection.RussianToEnglish)]
    public void AutoRoutesAllContextualSymbolsLikeExplicitDirectionForMonoalphabeticInput(
        string input,
        LayoutConversionDirection direction)
    {
        var autoResult = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);
        var explicitResult = LayoutConverter.ConvertLayout(input, direction);

        Assert.Equal(LayoutConversionOutcome.Converted, autoResult.Outcome);
        Assert.Equal(direction, autoResult.AppliedDirection);
        Assert.Equal(explicitResult.ConvertedText, autoResult.ConvertedText);
    }

    [Fact]
    public void AutoFlipsMixedSentenceCharacterByCharacter()
    {
        const string input = "ghbdtn ns cksifk yjdsq nhtr иуттн";

        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Null(result.AppliedDirection);
        Assert.Equal("привет ты слышал новый трек benny", result.ConvertedText);
    }

    [Fact]
    public void ConversionPreservesLfAndCrLf()
    {
        const string input = "ghbdtn\nGhbdtn\r\n";

        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal("привет\nПривет\r\n", result.ConvertedText);
    }

    [Fact]
    public void ConversionHandlesLargeInputWithoutAnArtificialLimit()
    {
        var input = string.Concat(Enumerable.Repeat("ghbdtn ", 20_000));
        var expected = string.Concat(Enumerable.Repeat("привет ", 20_000));

        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Fact]
    public void ConversionPreservesUnknownCharacters()
    {
        const string input = "gh🙂é漢";

        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal("пр🙂é漢", result.ConvertedText);
    }

    [Fact]
    public void ConversionPreservesMixedLetterCasePositionByPosition()
    {
        var result = LayoutConverter.ConvertLayout(
            "qWeRtY",
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal("йЦуКеН", result.ConvertedText);
    }

    [Theory]
    [InlineData("`~", "ёЁ")]
    [InlineData("[]{}\\|", "хъХЪ\\/")]
    [InlineData(";:'\"", "жЖэЭ")]
    [InlineData(",.<>/?", "бюБЮ.,")]
    [InlineData("1234567890-=!@#$%^&*()_+", "1234567890-=!\"№;%:?*()_+")]
    public void EnglishToRussianMapsPunctuationAndShiftSymbols(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("ёЁ", "`~")]
    [InlineData("хъХЪ\\/", "[]{}\\|")]
    [InlineData("жЖэЭ", ";:'\"")]
    [InlineData("бюБЮ.,?", ",.<>/?&")]
    [InlineData("1234567890-=!\"№;%:?*()_+", "1234567890-=!@#$%^&*()_+")]
    public void RussianToEnglishMapsPunctuationAndShiftSymbols(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.RussianToEnglish);

        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData(".", "ю")]
    [InlineData(",", "б")]
    [InlineData("?", ",")]
    public void EnglishToRussianMapsStandalonePunctuationPositionally(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.EnglishToRussian, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData(".", "/")]
    [InlineData(",", "?")]
    [InlineData("?", "&")]
    public void RussianToEnglishMapsStandalonePunctuationPositionally(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.RussianToEnglish);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.RussianToEnglish, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("ghbdtn.", "приветю")]
    [InlineData("ghbdtn,", "приветб")]
    public void EnglishToRussianTreatsTrailingPeriodAndCommaPositionallyNotAsPunctuation(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.EnglishToRussian, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("руддщ.", "hello/")]
    [InlineData("руддщ,", "hello?")]
    public void RussianToEnglishTreatsTrailingPeriodAndCommaPositionallyNotAsPunctuation(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.RussianToEnglish);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(LayoutConversionDirection.RussianToEnglish, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData(".")]
    [InlineData(",")]
    [InlineData("?")]
    public void AutoReturnsNoOpForStandalonePunctuation(string input)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.NoOp, result.Outcome);
        Assert.Equal(input, result.OriginalText);
        Assert.Null(result.AppliedDirection);
        Assert.Null(result.ConvertedText);
    }

    [Theory]
    [InlineData("ghbdtn.", "приветю", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("ghbdtn,", "приветб", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("ghbdtn?", "привет,", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("руддщ.", "hello/", LayoutConversionDirection.RussianToEnglish)]
    [InlineData("руддщ,", "hello?", LayoutConversionDirection.RussianToEnglish)]
    [InlineData("руддщ?", "hello&", LayoutConversionDirection.RussianToEnglish)]
    public void AutoRoutesTrailingPunctuationByNearestFragment(
        string input,
        string expected,
        LayoutConversionDirection expectedDirection)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal(expectedDirection, result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData(".", "ghbdtn/", "Руддщ Рш ghbdtn/")]
    [InlineData(",", "ghbdtn?", "Руддщ Рш ghbdtn?")]
    public void AutoRoutesContextualPunctuationStablyByNearestFragment(
        string symbol,
        string expectedWithoutDistantWords,
        string expectedWithDistantWords)
    {
        var resultWithoutDistantWords = LayoutConverter.ConvertLayout(
            $"привет{symbol}",
            LayoutConversionDirection.Auto);
        var resultWithDistantWords = LayoutConverter.ConvertLayout(
            $"Hello Hi привет{symbol}",
            LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, resultWithoutDistantWords.Outcome);
        Assert.Equal(
            LayoutConversionDirection.RussianToEnglish,
            resultWithoutDistantWords.AppliedDirection);
        Assert.Equal(expectedWithoutDistantWords, resultWithoutDistantWords.ConvertedText);
        Assert.Equal(LayoutConversionOutcome.Converted, resultWithDistantWords.Outcome);
        Assert.Null(resultWithDistantWords.AppliedDirection);
        Assert.Equal(expectedWithDistantWords, resultWithDistantWords.ConvertedText);
    }

    [Theory]
    [InlineData("@", "ghbdtn\"", "Руддщ Рш ghbdtn\"")]
    [InlineData("&", "ghbdtn?", "Руддщ Рш ghbdtn?")]
    [InlineData("#", "ghbdtn№", "Руддщ Рш ghbdtn№")]
    public void AutoRoutesSingleLayoutSymbolsDeterministically(
        string symbol,
        string expectedWithoutDistantWords,
        string expectedWithDistantWords)
    {
        var resultWithoutDistantWords = LayoutConverter.ConvertLayout(
            $"привет{symbol}",
            LayoutConversionDirection.Auto);
        var resultWithDistantWords = LayoutConverter.ConvertLayout(
            $"Hello Hi привет{symbol}",
            LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, resultWithoutDistantWords.Outcome);
        Assert.Equal(
            LayoutConversionDirection.RussianToEnglish,
            resultWithoutDistantWords.AppliedDirection);
        Assert.Equal(expectedWithoutDistantWords, resultWithoutDistantWords.ConvertedText);
        Assert.Equal(LayoutConversionOutcome.Converted, resultWithDistantWords.Outcome);
        Assert.Null(resultWithDistantWords.AppliedDirection);
        Assert.Equal(expectedWithDistantWords, resultWithDistantWords.ConvertedText);
    }

    [Theory]
    [InlineData("привет. Hello", "ghbdtn/ Руддщ")]
    [InlineData("привет, Hello", "ghbdtn? Руддщ")]
    [InlineData("привет? Hello", "ghbdtn& Руддщ")]
    [InlineData("привет; Hello", "ghbdtn$ Руддщ")]
    [InlineData("привет: Hello", "ghbdtn^ Руддщ")]
    [InlineData("привет\" Hello", "ghbdtn@ Руддщ")]
    [InlineData("привет \"Hello", "ghbdtn ЭРуддщ")]
    [InlineData("привет/Hello", "ghbdtn/Руддщ")]
    public void AutoUsesSyntacticTieBreakBetweenDifferentAlphabets(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Null(result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("тест .BPYM", "ntcn юИЗНЬ")]
    [InlineData("тест ,BPYM", "ntcn бИЗНЬ")]
    [InlineData("тест /BPYM", "ntcn .ИЗНЬ")]
    [InlineData("тест ?BPYM", "ntcn ,ИЗНЬ")]
    [InlineData("тест ;BPYM", "ntcn жИЗНЬ")]
    [InlineData("тест :BPYM", "ntcn ЖИЗНЬ")]
    [InlineData("тест \"BPYM", "ntcn ЭИЗНЬ")]
    [InlineData("jnkbxyfz/   Погода", "отличная.   Gjujlf")]
    public void AutoRoutesContextualSymbolsByNearestDifferentAlphabetFragment(
        string input,
        string expected)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Null(result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData(".", "/")]
    [InlineData(",", "?")]
    [InlineData("/", "/")]
    [InlineData("?", "&")]
    [InlineData(";", "$")]
    [InlineData(":", "^")]
    [InlineData("\"", "@")]
    public void AutoUsesLeftDirectionForDifferentAlphabetDistanceTieExceptSlash(
        string symbol,
        string expectedSymbol)
    {
        var result = LayoutConverter.ConvertLayout(
            $"привет{symbol}Hello",
            LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Null(result.AppliedDirection);
        Assert.Equal($"ghbdtn{expectedSymbol}Руддщ", result.ConvertedText);
    }

    [Theory]
    [InlineData("привет/", "ghbdtn|")]
    [InlineData("/Hello", ".Руддщ")]
    [InlineData("привет/мир", "ghbdtn|vbh")]
    public void AutoRoutesSlashByItsAdjacentFragments(string input, string expected)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.NotNull(result.AppliedDirection);
        Assert.Equal(expected, result.ConvertedText);
    }

    [Fact]
    public void AutoSearchesAcrossSingleLineBreakAndNonLetterChain()
    {
        var result = LayoutConverter.ConvertLayout(
            "привет\n .?",
            LayoutConversionDirection.Auto);

        Assert.Equal("ghbdtn\n /&", result.ConvertedText);
    }

    [Fact]
    public void AutoDoesNotRouteContextualSymbolsAcrossBlankParagraphs()
    {
        const string input = "Hello\n\n?\n\nпривет";

        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal("Руддщ\n\n?\n\nghbdtn", result.ConvertedText);
    }

    [Fact]
    public void ExplicitDirectionConvertsEvenWhenInputHasNoLetters()
    {
        var result = LayoutConverter.ConvertLayout(
            "@?",
            LayoutConversionDirection.EnglishToRussian);

        Assert.Equal(LayoutConversionOutcome.Converted, result.Outcome);
        Assert.Equal("\",", result.ConvertedText);
    }

    [Fact]
    public void LettersRoundTripInBothDirections()
    {
        const string english = "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";

        var russian = LayoutConverter.ConvertLayout(
            english,
            LayoutConversionDirection.EnglishToRussian);
        var roundTrip = LayoutConverter.ConvertLayout(
            Assert.IsType<string>(russian.ConvertedText),
            LayoutConversionDirection.RussianToEnglish);

        Assert.Equal(english, roundTrip.ConvertedText);
    }

    [Theory]
    [InlineData("ghbdtn", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("руддщ", LayoutConversionDirection.RussianToEnglish)]
    public void AutoReportsResultLayoutFromTrailingLetter(
        string input,
        LayoutConversionDirection expectedDirection)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Equal(expectedDirection, result.ResultLayoutDirection);
    }

    [Fact]
    public void AutoReportsResultLayoutFromStandaloneDeterministicSymbol()
    {
        var result = LayoutConverter.ConvertLayout("&", LayoutConversionDirection.Auto);

        Assert.Equal("?", result.ConvertedText);
        Assert.Equal(
            LayoutConversionDirection.EnglishToRussian,
            result.ResultLayoutDirection);
    }

    [Fact]
    public void AutoIgnoresTrailingDigitsIdenticalSymbolsAndWhitespace()
    {
        var result = LayoutConverter.ConvertLayout(
            "&123!%*()_+-= \t\r\n",
            LayoutConversionDirection.Auto);

        Assert.Equal("?123!%*()_+-= \t\r\n", result.ConvertedText);
        Assert.Equal(
            LayoutConversionDirection.EnglishToRussian,
            result.ResultLayoutDirection);
    }

    [Fact]
    public void AutoIgnoresTrailingUnchangedContextualSymbol()
    {
        var result = LayoutConverter.ConvertLayout("&/", LayoutConversionDirection.Auto);

        Assert.Equal("?/", result.ConvertedText);
        Assert.Equal(
            LayoutConversionDirection.EnglishToRussian,
            result.ResultLayoutDirection);
    }

    [Fact]
    public void AutoReportsResolvedDirectionFromChangedTrailingContextualSymbol()
    {
        var result = LayoutConverter.ConvertLayout("ghbdtn?", LayoutConversionDirection.Auto);

        Assert.Equal("привет,", result.ConvertedText);
        Assert.Equal(
            LayoutConversionDirection.EnglishToRussian,
            result.ResultLayoutDirection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123 !%*()_+-= \t\r\n")]
    [InlineData("/")]
    public void AutoReportsNoResultLayoutWithoutLayoutDefiningTokens(string input)
    {
        var result = LayoutConverter.ConvertLayout(input, LayoutConversionDirection.Auto);

        Assert.Null(result.ResultLayoutDirection);
    }

    [Fact]
    public void AutoReportsLastTokenDirectionInsteadOfMajorityDirection()
    {
        var result = LayoutConverter.ConvertLayout(
            "ghbdtn ghbdtn ghbdtn и",
            LayoutConversionDirection.Auto);

        Assert.Equal("привет привет привет b", result.ConvertedText);
        Assert.Null(result.AppliedDirection);
        Assert.Equal(
            LayoutConversionDirection.RussianToEnglish,
            result.ResultLayoutDirection);
    }

    [Theory]
    [InlineData("ghbdtn", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("руддщ", LayoutConversionDirection.RussianToEnglish)]
    [InlineData("&", LayoutConversionDirection.EnglishToRussian)]
    [InlineData("?", LayoutConversionDirection.RussianToEnglish)]
    public void ExplicitConversionReportsItsDirectionForLayoutDefiningInput(
        string input,
        LayoutConversionDirection direction)
    {
        var result = LayoutConverter.ConvertLayout(input, direction);

        Assert.Equal(direction, result.ResultLayoutDirection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123 !%*()_+-= \t\r\n")]
    public void ExplicitConversionReportsNoResultLayoutWithoutLayoutDefiningTokens(string input)
    {
        var englishToRussian = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.EnglishToRussian);
        var russianToEnglish = LayoutConverter.ConvertLayout(
            input,
            LayoutConversionDirection.RussianToEnglish);

        Assert.Null(englishToRussian.ResultLayoutDirection);
        Assert.Null(russianToEnglish.ResultLayoutDirection);
    }
}
