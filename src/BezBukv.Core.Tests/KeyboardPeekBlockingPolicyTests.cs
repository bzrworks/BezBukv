using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Tests;

public sealed class KeyboardPeekBlockingPolicyTests
{
    private static readonly PhysicalKey[] LetterAndSymbolKeys =
    [
        PhysicalKey.KeyA,
        PhysicalKey.KeyB,
        PhysicalKey.KeyC,
        PhysicalKey.KeyD,
        PhysicalKey.KeyE,
        PhysicalKey.KeyF,
        PhysicalKey.KeyG,
        PhysicalKey.KeyH,
        PhysicalKey.KeyI,
        PhysicalKey.KeyJ,
        PhysicalKey.KeyK,
        PhysicalKey.KeyL,
        PhysicalKey.KeyM,
        PhysicalKey.KeyN,
        PhysicalKey.KeyO,
        PhysicalKey.KeyP,
        PhysicalKey.KeyQ,
        PhysicalKey.KeyR,
        PhysicalKey.KeyS,
        PhysicalKey.KeyT,
        PhysicalKey.KeyU,
        PhysicalKey.KeyV,
        PhysicalKey.KeyW,
        PhysicalKey.KeyX,
        PhysicalKey.KeyY,
        PhysicalKey.KeyZ,
        PhysicalKey.BracketLeft,
        PhysicalKey.BracketRight,
        PhysicalKey.Backslash,
        PhysicalKey.Semicolon,
        PhysicalKey.Quote,
        PhysicalKey.Comma,
        PhysicalKey.Period,
        PhysicalKey.Slash,
        PhysicalKey.Backquote,
    ];

    private static readonly PhysicalKey[] NumberRowKeys =
    [
        PhysicalKey.Digit0,
        PhysicalKey.Digit1,
        PhysicalKey.Digit2,
        PhysicalKey.Digit3,
        PhysicalKey.Digit4,
        PhysicalKey.Digit5,
        PhysicalKey.Digit6,
        PhysicalKey.Digit7,
        PhysicalKey.Digit8,
        PhysicalKey.Digit9,
        PhysicalKey.Minus,
        PhysicalKey.Equal,
    ];

    private static readonly PhysicalKey[] ServiceKeys =
    [
        PhysicalKey.F1,
        PhysicalKey.F2,
        PhysicalKey.F3,
        PhysicalKey.F4,
        PhysicalKey.F5,
        PhysicalKey.F6,
        PhysicalKey.F7,
        PhysicalKey.F8,
        PhysicalKey.F9,
        PhysicalKey.F10,
        PhysicalKey.F11,
        PhysicalKey.F12,
        PhysicalKey.F13,
        PhysicalKey.F14,
        PhysicalKey.F15,
        PhysicalKey.F16,
        PhysicalKey.F17,
        PhysicalKey.F18,
        PhysicalKey.F19,
        PhysicalKey.F20,
        PhysicalKey.F21,
        PhysicalKey.F22,
        PhysicalKey.F23,
        PhysicalKey.F24,
        PhysicalKey.PrintScreen,
        PhysicalKey.ScrollLock,
        PhysicalKey.Pause,
        PhysicalKey.Backspace,
        PhysicalKey.Tab,
        PhysicalKey.CapsLock,
        PhysicalKey.Enter,
        PhysicalKey.Space,
        PhysicalKey.ShiftLeft,
        PhysicalKey.ShiftRight,
        PhysicalKey.ControlLeft,
        PhysicalKey.ControlRight,
        PhysicalKey.MetaLeft,
        PhysicalKey.MetaRight,
        PhysicalKey.AltLeft,
        PhysicalKey.AltRight,
        PhysicalKey.ContextMenu,
        PhysicalKey.Insert,
        PhysicalKey.Home,
        PhysicalKey.PageUp,
        PhysicalKey.Delete,
        PhysicalKey.End,
        PhysicalKey.PageDown,
        PhysicalKey.ArrowRight,
        PhysicalKey.ArrowLeft,
        PhysicalKey.ArrowDown,
        PhysicalKey.ArrowUp,
        PhysicalKey.NumLock,
        PhysicalKey.NumpadEnter,
    ];

    [Fact]
    public void CustomScanIdentityShowsOnlyWhenExactKeyExistsInLayout()
    {
        var outsideEnumIdentity = new KeyboardScanCodeIdentity(0x56, false);
        var secondOutsideEnumIdentity = new KeyboardScanCodeIdentity(0x7F, true);
        var layout = new CustomKeyboardLayout
        {
            Rows =
            [
                new CustomKeyboardRow
                {
                    Cells =
                    [
                        new CustomKeyboardCell
                        {
                            Key = new CustomKeyboardKey
                            {
                                ScanCode = outsideEnumIdentity.ScanCode,
                                Extended = outsideEnumIdentity.Extended,
                            },
                        },
                        new CustomKeyboardCell
                        {
                            Key = new CustomKeyboardKey
                            {
                                ScanCode = secondOutsideEnumIdentity.ScanCode,
                                Extended = secondOutsideEnumIdentity.Extended,
                            },
                        },
                    ],
                },
            ],
        };

        Assert.False(PhysicalKeyScanCodeMapper.TryGetNamedPhysicalKey(
            outsideEnumIdentity,
            virtualKey: null,
            out _));
        Assert.True(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
            outsideEnumIdentity,
            layout));
        Assert.True(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
            secondOutsideEnumIdentity,
            layout));
        Assert.False(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
            new KeyboardScanCodeIdentity(0x56, true),
            layout));
        Assert.False(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
            new KeyboardScanCodeIdentity(0x55, false),
            layout));
        Assert.False(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
            outsideEnumIdentity,
            customKeyboardLayout: null));
    }

    [Fact]
    public void LettersAndSymbolsAreAlwaysShown()
    {
        foreach (var blockNumberRow in new[] { false, true })
        {
            foreach (var showServiceKeys in new[] { false, true })
            {
                Assert.All(
                    LetterAndSymbolKeys,
                    key =>
                    {
                        Assert.True(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                            key,
                            blockNumberRow,
                            showServiceKeys));
                    });
            }
        }
    }

    [Fact]
    public void NumberRowKeysFollowNumberRowFlag()
    {
        foreach (var blockNumberRow in new[] { false, true })
        {
            foreach (var showServiceKeys in new[] { false, true })
            {
                Assert.All(
                    NumberRowKeys,
                    key =>
                    {
                        Assert.Equal(
                            blockNumberRow,
                            KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                                key,
                                blockNumberRow,
                                showServiceKeys));
                    });
            }
        }
    }

    [Fact]
    public void ServiceKeysIncludingSpaceFollowServiceKeysFlag()
    {
        foreach (var blockNumberRow in new[] { false, true })
        {
            foreach (var showServiceKeys in new[] { false, true })
            {
                Assert.All(
                    ServiceKeys,
                    key =>
                    {
                        Assert.Equal(
                            showServiceKeys,
                            KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                                key,
                                blockNumberRow,
                                showServiceKeys));
                    });
            }
        }
    }

    [Fact]
    public void EveryPhysicalKeyExceptEscapeIsBlockedRegardlessOfShowFlags()
    {
        var blockedKeys = Enum
            .GetValues<PhysicalKey>()
            .Where(key => key != PhysicalKey.Escape);

        foreach (var blockNumberRow in new[] { false, true })
        {
            foreach (var showServiceKeys in new[] { false, true })
            {
                Assert.All(
                    blockedKeys,
                    key => Assert.True(KeyboardPeekBlockingPolicy.ShouldBlock(
                        key,
                        blockNumberRow,
                        showServiceKeys)));
            }
        }
    }

    [Fact]
    public void EscapeAlwaysPassesThroughAndNeverShowsOverlay()
    {
        Assert.False(KeyboardPeekBlockingPolicy.ShouldBlock(
            PhysicalKey.Escape,
            blockNumberRow: true,
            showServiceKeys: true));
        Assert.False(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
            PhysicalKey.Escape,
            blockNumberRow: true,
            showServiceKeys: true));
    }

    [Theory]
    [InlineData(PhysicalKey.ControlLeft)]
    [InlineData(PhysicalKey.ControlRight)]
    [InlineData(PhysicalKey.AltLeft)]
    [InlineData(PhysicalKey.AltRight)]
    [InlineData(PhysicalKey.ShiftLeft)]
    [InlineData(PhysicalKey.ShiftRight)]
    [InlineData(PhysicalKey.MetaLeft)]
    [InlineData(PhysicalKey.MetaRight)]
    public void ActivationChordModifierKeyUpPassesThrough(PhysicalKey modifierKey)
    {
        var activationChordKeys = new HashSet<PhysicalKey>
        {
            PhysicalKey.F9,
            modifierKey,
        };

        Assert.False(KeyboardPeekBlockingPolicy.ShouldBlock(
            modifierKey,
            KeyboardKeyState.Up,
            activationChordKeys,
            blockNumberRow: true,
            showServiceKeys: true));
    }

    [Theory]
    [InlineData(PhysicalKey.ControlRight, KeyboardKeyState.Down)]
    [InlineData(PhysicalKey.F9, KeyboardKeyState.Up)]
    [InlineData(PhysicalKey.AltRight, KeyboardKeyState.Up)]
    [InlineData(PhysicalKey.KeyA, KeyboardKeyState.Up)]
    public void NonExemptEventsRemainBlocked(
        PhysicalKey key,
        KeyboardKeyState state)
    {
        IReadOnlySet<PhysicalKey> activationChordKeys = new HashSet<PhysicalKey>
        {
            PhysicalKey.F9,
            PhysicalKey.ControlLeft,
            PhysicalKey.ControlRight,
        };

        Assert.True(KeyboardPeekBlockingPolicy.ShouldBlock(
            key,
            state,
            activationChordKeys,
            blockNumberRow: false,
            showServiceKeys: false));
    }

    [Fact]
    public void EveryKeyOutsideOverlayClassesDoesNotShowOverlay()
    {
        var handledKeys = LetterAndSymbolKeys
            .Concat(NumberRowKeys)
            .Concat(ServiceKeys)
            .Append(PhysicalKey.Escape)
            .ToHashSet();
        var passThroughKeys = Enum
            .GetValues<PhysicalKey>()
            .Where(key => !handledKeys.Contains(key));

        Assert.All(
            passThroughKeys,
            key =>
            {
                Assert.False(KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                    key,
                    blockNumberRow: true,
                    showServiceKeys: true));
            });
    }
}
