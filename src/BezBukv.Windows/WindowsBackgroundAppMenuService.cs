using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsBackgroundAppMenuService : IBackgroundAppMenuService
{
    private const string ApplicationIconFileName = "app.ico";
    private const uint MonitorDefaultToNearest = 0x00000002;

    private readonly ISystemThemeProvider _systemThemeProvider;
    private readonly NotifyIcon _notifyIcon = new();
    private readonly Font _menuFont = new("Segoe UI", 9.75F);
    private ContextMenuStrip? _contextMenu;
    private TrayMenuRenderer? _renderer;
    private TrayToggleMenuItem? _pauseMenuItem;
    private TrayToggleMenuItem? _autoPeekMenuItem;
    private ToolStripMenuItem? _settingsMenuItem;
    private ToolStripMenuItem? _exitMenuItem;
    private Icon? _applicationIcon;
    private Icon? _pausedApplicationIcon;
    private bool _isPaused;
    private bool _isAutoPeekEnabled;
    private bool _disposed;

    public WindowsBackgroundAppMenuService(ISystemThemeProvider systemThemeProvider)
    {
        ArgumentNullException.ThrowIfNull(systemThemeProvider);

        _systemThemeProvider = systemThemeProvider;
    }

    public event EventHandler? PauseToggleRequested;

    public event EventHandler? AutoPeekToggleRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void Show(
        string pauseMenuText,
        string autoPeekMenuText,
        string settingsMenuText,
        string exitMenuText)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pauseMenuText);
        ArgumentException.ThrowIfNullOrWhiteSpace(autoPeekMenuText);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsMenuText);
        ArgumentException.ThrowIfNullOrWhiteSpace(exitMenuText);

        if (_contextMenu is null)
        {
            CreateContextMenu(
                pauseMenuText,
                autoPeekMenuText,
                settingsMenuText,
                exitMenuText);

            _notifyIcon.MouseClick += OnNotifyIconMouseClick;
            _notifyIcon.ContextMenuStrip = _contextMenu;
            _applicationIcon = LoadApplicationIcon();
            _pausedApplicationIcon = TryCreatePausedApplicationIcon(_applicationIcon);
            _notifyIcon.Icon = _applicationIcon;
        }
        else
        {
            _pauseMenuItem!.Text = pauseMenuText;
            _autoPeekMenuItem!.Text = autoPeekMenuText;
            _settingsMenuItem!.Text = settingsMenuText;
            _exitMenuItem!.Text = exitMenuText;
        }

        _notifyIcon.Visible = true;
    }

    public void UpdateState(
        bool isPaused,
        bool isAutoPeekEnabled,
        string tooltipText)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooltipText);

        if (_contextMenu is null)
        {
            throw new InvalidOperationException(
                "The background app menu must be shown before it can be updated.");
        }

        var stateChanged = _isPaused != isPaused;
        _isPaused = isPaused;
        _isAutoPeekEnabled = isAutoPeekEnabled;
        ApplyToggleStates();
        if (stateChanged)
        {
            _notifyIcon.Icon = isPaused
                ? _pausedApplicationIcon ?? _applicationIcon
                : _applicationIcon;
        }

        _notifyIcon.Text = tooltipText;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip = null;
        _notifyIcon.Icon = null;
        _notifyIcon.MouseClick -= OnNotifyIconMouseClick;

        if (_contextMenu is not null)
        {
            _contextMenu.Opening -= OnContextMenuOpening;
            _contextMenu.SizeChanged -= OnContextMenuSizeChanged;
        }

        if (_pauseMenuItem is not null)
        {
            _pauseMenuItem.Click -= OnPauseMenuItemClick;
        }

        if (_autoPeekMenuItem is not null)
        {
            _autoPeekMenuItem.Click -= OnAutoPeekMenuItemClick;
        }

        if (_settingsMenuItem is not null)
        {
            _settingsMenuItem.Click -= OnSettingsMenuItemClick;
        }

        if (_exitMenuItem is not null)
        {
            _exitMenuItem.Click -= OnExitMenuItemClick;
        }

        _contextMenu?.Dispose();
        _notifyIcon.Dispose();
        _pausedApplicationIcon?.Dispose();
        _applicationIcon?.Dispose();
        _menuFont.Dispose();
        _pausedApplicationIcon = null;
        _applicationIcon = null;
        PauseToggleRequested = null;
        AutoPeekToggleRequested = null;
        SettingsRequested = null;
        ExitRequested = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void CreateContextMenu(
        string pauseMenuText,
        string autoPeekMenuText,
        string settingsMenuText,
        string exitMenuText)
    {
        var pauseMenuItem = CreateToggleMenuItem(pauseMenuText);
        pauseMenuItem.Click += OnPauseMenuItemClick;

        var autoPeekMenuItem = CreateToggleMenuItem(autoPeekMenuText);
        autoPeekMenuItem.Click += OnAutoPeekMenuItemClick;

        var separator = new ToolStripSeparator
        {
            AutoSize = false,
            Size = TrayMenuRenderer.SeparatorSize,
            Margin = Padding.Empty,
        };

        var settingsMenuItem = CreateTextMenuItem(settingsMenuText);
        settingsMenuItem.Click += OnSettingsMenuItemClick;

        var exitMenuItem = CreateTextMenuItem(exitMenuText);
        exitMenuItem.Click += OnExitMenuItemClick;

        var palette = ResolveSystemPalette();
        var renderer = new TrayMenuRenderer(palette);
        var contextMenu = new ContextMenuStrip
        {
            AutoSize = true,
            BackColor = palette.MenuBackground,
            DropShadowEnabled = true,
            Font = _menuFont,
            ForeColor = palette.Text,
            Padding = new Padding(TrayMenuRenderer.MenuPadding),
            Renderer = renderer,
            ShowCheckMargin = false,
            ShowImageMargin = false,
        };
        contextMenu.Items.AddRange(
            [pauseMenuItem, autoPeekMenuItem, separator, settingsMenuItem, exitMenuItem]);
        ApplyContextMenuMetrics(contextMenu);
        contextMenu.Opening += OnContextMenuOpening;
        contextMenu.SizeChanged += OnContextMenuSizeChanged;

        _contextMenu = contextMenu;
        _renderer = renderer;
        _pauseMenuItem = pauseMenuItem;
        _autoPeekMenuItem = autoPeekMenuItem;
        _settingsMenuItem = settingsMenuItem;
        _exitMenuItem = exitMenuItem;
        ApplyToggleStates();
    }

    private static TrayToggleMenuItem CreateToggleMenuItem(string text) =>
        ConfigureMenuItem(new TrayToggleMenuItem(text));

    private static ToolStripMenuItem CreateTextMenuItem(string text) =>
        ConfigureMenuItem(new ToolStripMenuItem(text));

    private static T ConfigureMenuItem<T>(T item)
        where T : ToolStripMenuItem
    {
        item.AutoSize = false;
        item.Size = TrayMenuRenderer.MenuItemSize;
        item.Margin = Padding.Empty;
        item.Padding = Padding.Empty;
        item.Image = null;
        return item;
    }

    private static Icon LoadApplicationIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, ApplicationIconFileName);
        return new Icon(iconPath, SystemInformation.SmallIconSize);
    }

    private static Icon? TryCreatePausedApplicationIcon(Icon applicationIcon)
    {
        try
        {
            return CreatePausedApplicationIcon(applicationIcon);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Icon CreatePausedApplicationIcon(Icon applicationIcon)
    {
        using var sourceBitmap = applicationIcon.ToBitmap();
        using var pausedBitmap = new Bitmap(
            applicationIcon.Width,
            applicationIcon.Height,
            PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(pausedBitmap))
        using (var imageAttributes = new ImageAttributes())
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            imageAttributes.SetColorMatrix(CreateGrayscaleColorMatrix());
            graphics.DrawImage(
                sourceBitmap,
                new Rectangle(0, 0, pausedBitmap.Width, pausedBitmap.Height),
                0,
                0,
                sourceBitmap.Width,
                sourceBitmap.Height,
                GraphicsUnit.Pixel,
                imageAttributes);

            DrawPauseBadge(graphics, pausedBitmap.Size);
        }

        var iconHandle = pausedBitmap.GetHicon();
        try
        {
            using var handleIcon = Icon.FromHandle(iconHandle);
            return (Icon)handleIcon.Clone();
        }
        finally
        {
            _ = DestroyIcon(iconHandle);
        }
    }

    private static ColorMatrix CreateGrayscaleColorMatrix() =>
        new(
            [
                [0.299F, 0.299F, 0.299F, 0F, 0F],
                [0.587F, 0.587F, 0.587F, 0F, 0F],
                [0.114F, 0.114F, 0.114F, 0F, 0F],
                [0F, 0F, 0F, 1F, 0F],
                [0F, 0F, 0F, 0F, 1F],
            ]);

    private static void DrawPauseBadge(Graphics graphics, Size iconSize)
    {
        var shortestSide = Math.Min(iconSize.Width, iconSize.Height);
        var badgeDiameter = shortestSide * 0.54F;
        var badgeInset = Math.Max(1F, shortestSide * 0.04F);
        var badgeBounds = new RectangleF(
            iconSize.Width - badgeDiameter - badgeInset,
            iconSize.Height - badgeDiameter - badgeInset,
            badgeDiameter,
            badgeDiameter);

        using var badgeBrush = new SolidBrush(Color.White);
        graphics.FillEllipse(badgeBrush, badgeBounds);

        var barWidth = Math.Max(1, (int)MathF.Round(badgeDiameter * 0.14F));
        var barHeight = Math.Max(1, (int)MathF.Round(badgeDiameter * 0.5F));
        var barGap = Math.Max(1, (int)MathF.Round(badgeDiameter * 0.14F));
        var barsWidth = (2 * barWidth) + barGap;
        var firstBarLeft = (int)MathF.Round(
            badgeBounds.Left + ((badgeDiameter - barsWidth) / 2F));
        var barTop = (int)MathF.Round(
            badgeBounds.Top + ((badgeDiameter - barHeight) / 2F));

        var graphicsState = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            using var pauseBrush = new SolidBrush(Color.FromArgb(32, 32, 32));
            graphics.FillRectangle(
                pauseBrush,
                firstBarLeft,
                barTop,
                barWidth,
                barHeight);
            graphics.FillRectangle(
                pauseBrush,
                firstBarLeft + barWidth + barGap,
                barTop,
                barWidth,
                barHeight);
        }
        finally
        {
            graphics.Restore(graphicsState);
        }
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        var contextMenu = _contextMenu!;
        var displayDpi = GetDisplayDpiAtCursor(contextMenu);
        ApplyToggleStates();
        ApplySystemTheme();
        ApplyContextMenuMetrics(contextMenu, displayDpi);
        contextMenu.PerformLayout();
        ApplyRoundedMenuRegion(TrayMenuRenderer.GetDpiScale(displayDpi));
    }

    private void OnContextMenuSizeChanged(object? sender, EventArgs e) =>
        ApplyRoundedMenuRegion();

    private void ApplyToggleStates()
    {
        if (_pauseMenuItem is not null)
        {
            _pauseMenuItem.IsOn = _isPaused;
        }

        if (_autoPeekMenuItem is not null)
        {
            _autoPeekMenuItem.IsOn = _isAutoPeekEnabled;
        }
    }

    private void ApplySystemTheme()
    {
        var palette = ResolveSystemPalette();
        _renderer!.UpdatePalette(palette);
        _contextMenu!.BackColor = palette.MenuBackground;
        _contextMenu.ForeColor = palette.Text;
        _contextMenu.Invalidate(true);
    }

    private static void ApplyContextMenuMetrics(ContextMenuStrip contextMenu)
    {
        var scale = TrayMenuRenderer.GetDpiScale(contextMenu);
        using var graphics = contextMenu.CreateGraphics();
        ApplyContextMenuMetrics(contextMenu, graphics, scale);
    }

    private static void ApplyContextMenuMetrics(
        ContextMenuStrip contextMenu,
        int dpi)
    {
        var scale = TrayMenuRenderer.GetDpiScale(dpi);
        using var measurementBitmap = new Bitmap(1, 1);
        measurementBitmap.SetResolution(dpi, dpi);
        using var graphics = Graphics.FromImage(measurementBitmap);
        ApplyContextMenuMetrics(contextMenu, graphics, scale);
    }

    private static void ApplyContextMenuMetrics(
        ContextMenuStrip contextMenu,
        Graphics graphics,
        float scale)
    {
        var menuItemSize = TrayMenuRenderer.GetMenuItemSize(scale);
        var separatorSize = TrayMenuRenderer.GetSeparatorSize(scale);
        var menuPadding = TrayMenuRenderer.GetMenuPadding(scale);
        var widestMenuItem = menuItemSize.Width;

        foreach (ToolStripItem item in contextMenu.Items)
        {
            if (item is ToolStripMenuItem menuItem)
            {
                widestMenuItem = Math.Max(
                    widestMenuItem,
                    TrayMenuRenderer.MeasureRequiredItemWidth(
                        graphics,
                        menuItem,
                        scale));
            }
        }

        contextMenu.SuspendLayout();
        contextMenu.Padding = menuPadding;
        foreach (ToolStripItem item in contextMenu.Items)
        {
            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.Size = new Size(widestMenuItem, menuItemSize.Height);
            }
            else if (item is ToolStripSeparator separator)
            {
                separator.Size = new Size(widestMenuItem, separatorSize.Height);
            }
        }

        var requiredWidth = widestMenuItem + contextMenu.Padding.Horizontal;
        contextMenu.MinimumSize = new Size(requiredWidth, 0);
        contextMenu.ResumeLayout(performLayout: false);
    }

    private void ApplyRoundedMenuRegion(float? scale = null)
    {
        var contextMenu = _contextMenu;
        if (contextMenu is null
            || contextMenu.IsDisposed
            || contextMenu.Width <= 0
            || contextMenu.Height <= 0)
        {
            return;
        }

        var bounds = new Rectangle(
            0,
            0,
            contextMenu.Width,
            contextMenu.Height);
        using var path = TrayMenuRenderer.CreateRoundedRectangle(
            bounds,
            TrayMenuRenderer.GetMenuCornerRadius(
                scale ?? TrayMenuRenderer.GetDpiScale(contextMenu)));
        var previousRegion = contextMenu.Region;
        contextMenu.Region = new Region(path);
        previousRegion?.Dispose();
    }

    private static int GetDisplayDpiAtCursor(ContextMenuStrip contextMenu)
    {
        var fallbackDpi = contextMenu.DeviceDpi;

        try
        {
            var monitor = MonitorFromPoint(
                Cursor.Position,
                MonitorDefaultToNearest);
            if (monitor == nint.Zero
                || GetDpiForMonitor(
                    monitor,
                    MonitorDpiType.Effective,
                    out var dpiX,
                    out _) != 0
                || dpiX == 0
                || dpiX > int.MaxValue)
            {
                return fallbackDpi;
            }

            return (int)dpiX;
        }
        catch (DllNotFoundException)
        {
            return fallbackDpi;
        }
        catch (EntryPointNotFoundException)
        {
            return fallbackDpi;
        }
    }

    private TrayMenuPalette ResolveSystemPalette() =>
        _systemThemeProvider.IsDarkTheme
            ? TrayMenuPalette.Dark
            : TrayMenuPalette.Light;

    private void OnPauseMenuItemClick(object? sender, EventArgs e)
    {
        PauseToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            PauseToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAutoPeekMenuItemClick(object? sender, EventArgs e)
    {
        AutoPeekToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsMenuItemClick(object? sender, EventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitMenuItemClick(object? sender, EventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint iconHandle);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(
        Point point,
        uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    private enum MonitorDpiType
    {
        Effective = 0,
    }
}
