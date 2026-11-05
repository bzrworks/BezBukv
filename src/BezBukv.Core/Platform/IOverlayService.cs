using System.Drawing;
using BezBukv.Core.Keyboard;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Platform;

/// <summary>
/// Shows and hides the platform overlay for a physical keyboard key.
/// </summary>
public interface IOverlayService : IDisposable
{
    /// <summary>
    /// Updates the appearance options used the next time the overlay is shown.
    /// </summary>
    void UpdateAppearance(OverlayAppearanceOptions options);

    /// <summary>
    /// Updates the keyboard profile context used the next time the overlay is shown.
    /// </summary>
    void UpdateKeyboardProfile(
        KeyboardProfile keyboardProfile,
        IReadOnlyList<string> selectedLayoutKlids,
        CustomKeyboardLayout? customKeyboardLayout);

    /// <summary>
    /// Shows a keyboard peek preview centered in the specified screen area.
    /// The preview is independent from the regular caret-positioned overlay.
    /// </summary>
    void ShowPreview(Rectangle placementArea, OverlayAppearanceOptions options);

    /// <summary>
    /// Updates the appearance of the currently shown preview in place.
    /// </summary>
    void UpdatePreviewAppearance(OverlayAppearanceOptions options);

    /// <summary>
    /// Hides the preview without affecting the regular overlay.
    /// </summary>
    void HidePreview();

    /// <summary>
    /// Shows the overlay centered on the specified physical key.
    /// Repeated calls for the currently shown key are idempotent.
    /// </summary>
    void Show(PhysicalKey centerKey);

    /// <summary>
    /// Shows the Custom-profile overlay centered on the specified scan-code identity.
    /// Repeated calls for the currently shown identity are idempotent.
    /// </summary>
    void Show(KeyboardScanCodeIdentity centerIdentity);

    /// <summary>
    /// Hides the overlay only when it is centered on the specified physical key.
    /// </summary>
    void Hide(PhysicalKey centerKey);

    /// <summary>
    /// Hides the overlay only when it is centered on the specified scan-code identity.
    /// </summary>
    void Hide(KeyboardScanCodeIdentity centerIdentity);

    /// <summary>
    /// Hides the overlay when it is shown.
    /// </summary>
    void Hide();
}
