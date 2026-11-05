namespace BezBukv.Core.Platform;

/// <summary>
/// Enumerates keyboard layouts installed in the operating system.
/// </summary>
public interface IInstalledKeyboardLayoutService
{
    /// <summary>
    /// Returns installed layouts identified by stable keyboard layout identifiers (KLIDs).
    /// </summary>
    IReadOnlyList<InstalledKeyboardLayout> GetInstalledKeyboardLayouts();
}

/// <summary>
/// A keyboard layout identified by its stable eight-digit hexadecimal KLID.
/// </summary>
public sealed record InstalledKeyboardLayout(string Klid, string DisplayName);
