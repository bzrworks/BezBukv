namespace BezBukv.Core.Platform;

public sealed record GlobalHotkey(
    HotkeyKey Key,
    IReadOnlyCollection<HotkeyModifier> Modifiers)
{
    private readonly IReadOnlyCollection<HotkeyModifier> _modifiers =
        NormalizeModifiers(Modifiers);

    public IReadOnlyCollection<HotkeyModifier> Modifiers
    {
        get => _modifiers;
        init => _modifiers = NormalizeModifiers(value);
    }

    public bool Equals(GlobalHotkey? other) =>
        ReferenceEquals(this, other)
        || (other is not null
            && Key == other.Key
            && ModifiersEqual(Modifiers, other.Modifiers));

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Key);
        hashCode.Add(Modifiers is not null);

        if (Modifiers is not null)
        {
            foreach (var modifier in Modifiers)
            {
                hashCode.Add(modifier);
            }
        }

        return hashCode.ToHashCode();
    }

    private static bool ModifiersEqual(
        IReadOnlyCollection<HotkeyModifier>? left,
        IReadOnlyCollection<HotkeyModifier>? right) =>
        left is null
            ? right is null
            : right is not null && left.SequenceEqual(right);

    private static IReadOnlyCollection<HotkeyModifier> NormalizeModifiers(
        IReadOnlyCollection<HotkeyModifier>? modifiers) =>
        modifiers is null
            ? null!
            : Array.AsReadOnly(
                modifiers
                    .Distinct()
                    .OrderBy(static modifier => modifier)
                    .ToArray());
}

public enum HotkeyKey
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    F20,
    F21,
    F22,
    F23,
    F24,
}

public enum HotkeyModifier
{
    Primary,
    Control,
    Alt,
    Shift,
    Meta,
}
