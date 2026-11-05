using System.Collections.ObjectModel;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class GlobalHotkeyTests
{
    [Fact]
    public void SameKeyAndModifiersInDifferentOrderAreEqualWithEqualHashes()
    {
        var first = new GlobalHotkey(
            HotkeyKey.F7,
            new[] { HotkeyModifier.Primary, HotkeyModifier.Shift });
        var second = new GlobalHotkey(
            HotkeyKey.F7,
            new[] { HotkeyModifier.Shift, HotkeyModifier.Primary });

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentCollectionTypesWithSameContentsAreEqual()
    {
        IReadOnlyCollection<HotkeyModifier> array =
            new[] { HotkeyModifier.Control, HotkeyModifier.Alt };
        IReadOnlyCollection<HotkeyModifier> readOnlyCollection =
            new ReadOnlyCollection<HotkeyModifier>(
                new List<HotkeyModifier>
                {
                    HotkeyModifier.Alt,
                    HotkeyModifier.Control,
                });

        var first = new GlobalHotkey(HotkeyKey.K, array);
        var second = new GlobalHotkey(HotkeyKey.K, readOnlyCollection);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DuplicateModifiersAreEquivalentToDeduplicatedSet()
    {
        var withDuplicates = new GlobalHotkey(
            HotkeyKey.F8,
            new[]
            {
                HotkeyModifier.Primary,
                HotkeyModifier.Primary,
                HotkeyModifier.Shift,
            });
        var deduplicated = new GlobalHotkey(
            HotkeyKey.F8,
            new[] { HotkeyModifier.Primary, HotkeyModifier.Shift });

        Assert.Equal(deduplicated, withDuplicates);
        Assert.Equal(deduplicated.GetHashCode(), withDuplicates.GetHashCode());
        Assert.Equal(2, withDuplicates.Modifiers.Count);
    }

    [Fact]
    public void DifferentKeyOrModifierSetAreNotEqual()
    {
        var hotkey = new GlobalHotkey(
            HotkeyKey.F7,
            new[] { HotkeyModifier.Primary, HotkeyModifier.Shift });
        var differentKey = new GlobalHotkey(
            HotkeyKey.F8,
            new[] { HotkeyModifier.Primary, HotkeyModifier.Shift });
        var differentModifiers = new GlobalHotkey(
            HotkeyKey.F7,
            new[] { HotkeyModifier.Primary, HotkeyModifier.Alt });

        Assert.NotEqual(hotkey, differentKey);
        Assert.NotEqual(hotkey, differentModifiers);
    }

    [Fact]
    public void EquivalentHotkeysWorkAsHashSetElementsAndDictionaryKeys()
    {
        var registered = new GlobalHotkey(
            HotkeyKey.F9,
            new[] { HotkeyModifier.Primary, HotkeyModifier.Shift });
        var conflict = new GlobalHotkey(
            HotkeyKey.F9,
            new[]
            {
                HotkeyModifier.Shift,
                HotkeyModifier.Primary,
                HotkeyModifier.Primary,
            });
        var hotkeys = new HashSet<GlobalHotkey> { registered };
        var commands = new Dictionary<GlobalHotkey, string>
        {
            [registered] = "Correction",
        };

        Assert.False(hotkeys.Add(conflict));
        Assert.Equal("Correction", commands[conflict]);
    }

    [Fact]
    public void MutatingSourceCollectionDoesNotChangeHotkeyValue()
    {
        var sourceModifiers = new List<HotkeyModifier>
        {
            HotkeyModifier.Primary,
        };
        var hotkey = new GlobalHotkey(HotkeyKey.F7, sourceModifiers);
        var hashCode = hotkey.GetHashCode();

        sourceModifiers.Add(HotkeyModifier.Shift);

        Assert.Equal(new[] { HotkeyModifier.Primary }, hotkey.Modifiers);
        Assert.Equal(hashCode, hotkey.GetHashCode());
    }
}
