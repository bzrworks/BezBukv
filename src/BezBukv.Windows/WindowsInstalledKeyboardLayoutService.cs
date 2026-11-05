using System.Globalization;
using System.Runtime.InteropServices;
using BezBukv.Core.Platform;
using Microsoft.Win32;

namespace BezBukv.Windows;

public sealed class WindowsInstalledKeyboardLayoutService : IInstalledKeyboardLayoutService
{
    private const string KeyboardLayoutsRegistryPath =
        @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";
    private const string LayoutIdValueName = "Layout Id";
    private const string LayoutTextValueName = "Layout Text";
    private const ushort SpecialLayoutDeviceMask = 0xF000;
    private const ushort LayoutIdMask = 0x0FFF;

    public IReadOnlyList<InstalledKeyboardLayout> GetInstalledKeyboardLayouts()
    {
        try
        {
            var layoutCount = GetKeyboardLayoutList(0, null);
            if (layoutCount <= 0)
            {
                return [];
            }

            var keyboardLayouts = new IntPtr[layoutCount];
            var copiedLayoutCount = GetKeyboardLayoutList(
                keyboardLayouts.Length,
                keyboardLayouts);
            if (copiedLayoutCount <= 0)
            {
                return [];
            }

            var registryLayouts = ReadRegistryLayouts();
            if (registryLayouts is null)
            {
                return [];
            }

            var rawLayouts = new List<InstalledKeyboardLayout>();
            foreach (var keyboardLayout in keyboardLayouts.Take(
                         Math.Min(copiedLayoutCount, keyboardLayouts.Length)))
            {
                if (TryResolveRegistryLayout(
                        keyboardLayout,
                        registryLayouts,
                        out var registryLayout))
                {
                    rawLayouts.Add(
                        new InstalledKeyboardLayout(
                            registryLayout.Klid,
                            registryLayout.DisplayName));
                }
            }

            return InstalledKeyboardLayoutCatalog.Normalize(rawLayouts);
        }
        catch (Exception)
        {
            return [];
        }
    }

    internal static bool TryGetInstalledKeyboardLayout(
        string? klid,
        out IntPtr keyboardLayout)
    {
        keyboardLayout = IntPtr.Zero;

        try
        {
            if (!InstalledKeyboardLayoutCatalog.TryNormalizeKlid(
                    klid,
                    out var normalizedKlid))
            {
                return false;
            }

            var layoutCount = GetKeyboardLayoutList(0, null);
            if (layoutCount <= 0)
            {
                return false;
            }

            var keyboardLayouts = new IntPtr[layoutCount];
            var copiedLayoutCount = GetKeyboardLayoutList(
                keyboardLayouts.Length,
                keyboardLayouts);
            if (copiedLayoutCount <= 0)
            {
                return false;
            }

            var registryLayouts = ReadRegistryLayouts();
            if (registryLayouts is null)
            {
                return false;
            }

            foreach (var candidate in keyboardLayouts.Take(
                         Math.Min(copiedLayoutCount, keyboardLayouts.Length)))
            {
                if (TryResolveRegistryLayout(
                        candidate,
                        registryLayouts,
                        out var registryLayout) &&
                    string.Equals(
                        registryLayout.Klid,
                        normalizedKlid,
                        StringComparison.Ordinal))
                {
                    keyboardLayout = candidate;
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static RegistryLayoutCatalog? ReadRegistryLayouts()
    {
        using var localMachine = RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine,
            RegistryView.Default);
        using var keyboardLayoutsKey = localMachine.OpenSubKey(
            KeyboardLayoutsRegistryPath,
            writable: false);

        if (keyboardLayoutsKey is null)
        {
            return null;
        }

        var layoutsByKlid = new Dictionary<string, RegistryLayout>(StringComparer.Ordinal);
        var layoutsByLayoutId = new Dictionary<ushort, RegistryLayout>();

        foreach (var subKeyName in keyboardLayoutsKey
                     .GetSubKeyNames()
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!InstalledKeyboardLayoutCatalog.TryNormalizeKlid(
                    subKeyName,
                    out var normalizedKlid))
            {
                continue;
            }

            using var layoutKey = keyboardLayoutsKey.OpenSubKey(
                subKeyName,
                writable: false);
            if (layoutKey is null)
            {
                continue;
            }

            var displayName = layoutKey.GetValue(
                    LayoutTextValueName,
                    string.Empty,
                    RegistryValueOptions.DoNotExpandEnvironmentNames) as string
                ?? string.Empty;
            var registryLayout = new RegistryLayout(
                normalizedKlid,
                FormatDisplayName(normalizedKlid, displayName));

            layoutsByKlid.TryAdd(normalizedKlid, registryLayout);

            if (TryReadLayoutId(layoutKey, out var layoutId))
            {
                layoutsByLayoutId.TryAdd(layoutId, registryLayout);
            }
        }

        return new RegistryLayoutCatalog(layoutsByKlid, layoutsByLayoutId);
    }

    private static string FormatDisplayName(string klid, string layoutText)
    {
        var layoutName = layoutText.Trim();
        if (layoutName.Length == 0
            || !uint.TryParse(
                klid,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var numericKlid))
        {
            return layoutName;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(unchecked((ushort)numericKlid));
            var languageCulture = culture.Parent == CultureInfo.InvariantCulture
                ? culture
                : culture.Parent;
            var languageName = languageCulture.EnglishName;

            if (string.IsNullOrWhiteSpace(languageName)
                || string.Equals(
                    languageName,
                    layoutName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return layoutName;
            }

            return $"{languageName} ({layoutName})";
        }
        catch (CultureNotFoundException)
        {
            return layoutName;
        }
    }

    private static bool TryReadLayoutId(RegistryKey layoutKey, out ushort layoutId)
    {
        layoutId = 0;

        var rawLayoutId = layoutKey.GetValue(
            LayoutIdValueName,
            null,
            RegistryValueOptions.DoNotExpandEnvironmentNames) as string;

        return !string.IsNullOrWhiteSpace(rawLayoutId) &&
            ushort.TryParse(
                rawLayoutId.Trim(),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out layoutId);
    }

    private static bool TryResolveRegistryLayout(
        IntPtr keyboardLayout,
        RegistryLayoutCatalog registryLayouts,
        out RegistryLayout registryLayout)
    {
        var hklValue = unchecked((uint)keyboardLayout.ToInt64());
        var languageId = unchecked((ushort)hklValue);
        var deviceId = unchecked((ushort)(hklValue >> 16));

        if ((deviceId & SpecialLayoutDeviceMask) == SpecialLayoutDeviceMask &&
            registryLayouts.ByLayoutId.TryGetValue(
                (ushort)(deviceId & LayoutIdMask),
                out registryLayout!))
        {
            return true;
        }

        var layoutIdentifier = deviceId == 0
            ? languageId
            : deviceId;
        var defaultKlid = layoutIdentifier.ToString(
            "x8",
            CultureInfo.InvariantCulture);

        if (registryLayouts.ByKlid.TryGetValue(defaultKlid, out registryLayout!))
        {
            return true;
        }

        var exactKlid = hklValue.ToString("x8", CultureInfo.InvariantCulture);

        return registryLayouts.ByKlid.TryGetValue(exactKlid, out registryLayout!);
    }

    private sealed record RegistryLayout(string Klid, string DisplayName);

    private sealed record RegistryLayoutCatalog(
        IReadOnlyDictionary<string, RegistryLayout> ByKlid,
        IReadOnlyDictionary<ushort, RegistryLayout> ByLayoutId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetKeyboardLayoutList(
        int count,
        [Out] IntPtr[]? layouts);
}
