using System.Runtime.InteropServices;
using System.Text;

namespace BezBukv.Windows;

public static class WindowsPackageIdentity
{
    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoPackage = 15700;

    public static bool IsCurrentProcessPackaged()
    {
        uint packageFullNameLength = 0;
        var result = GetCurrentPackageFullName(
            ref packageFullNameLength,
            packageFullName: null);

        return result switch
        {
            ErrorInsufficientBuffer => true,
            AppModelErrorNoPackage => false,
            _ => false,
        };
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength,
        StringBuilder? packageFullName);
}
