using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BezBukv.Windows;

internal sealed class TrayMenuRenderer : ToolStripRenderer
{
    internal static readonly Size MenuItemSize = new(168, 36);
    internal static readonly Size SeparatorSize = new(168, 9);
    internal const int MenuCornerRadius = 8;
    internal const int MenuPadding = 5;

    private const float DefaultDpi = 96F;
    private const int ItemHorizontalPadding = 10;
    private const int ToggleWidth = 38;
    private const int ToggleHeight = 20;
    private const int ToggleKnobSize = 16;
    private const int ToggleInset = 2;
    private const TextFormatFlags ItemTextFormat =
        TextFormatFlags.VerticalCenter
        | TextFormatFlags.SingleLine
        | TextFormatFlags.NoPrefix
        | TextFormatFlags.NoPadding;

    private TrayMenuPalette _palette;

    public TrayMenuRenderer(TrayMenuPalette palette)
    {
        _palette = palette;
    }

    public void UpdatePalette(TrayMenuPalette palette) =>
        _palette = palette;

    internal static float GetDpiScale(Control control) =>
        GetDpiScale(control.DeviceDpi);

    internal static float GetDpiScale(int dpi) =>
        Math.Max(1F, dpi / DefaultDpi);

    internal static Size GetMenuItemSize(float scale) =>
        new(Scale(MenuItemSize.Width, scale), Scale(MenuItemSize.Height, scale));

    internal static Size GetSeparatorSize(float scale) =>
        new(Scale(SeparatorSize.Width, scale), Scale(SeparatorSize.Height, scale));

    internal static int GetMenuCornerRadius(float scale) =>
        Scale(MenuCornerRadius, scale);

    internal static Padding GetMenuPadding(float scale) =>
        new(Scale(MenuPadding, scale));

    internal static int MeasureRequiredItemWidth(
        Graphics graphics,
        ToolStripMenuItem item,
        float scale)
    {
        var textSize = TextRenderer.MeasureText(
            graphics,
            item.Text,
            item.Font,
            Size.Empty,
            ItemTextFormat);
        var horizontalPadding = Scale(ItemHorizontalPadding, scale);
        var toggleWidth = item is TrayToggleMenuItem
            ? Scale(ToggleWidth, scale) + horizontalPadding
            : 0;

        return textSize.Width
            + (horizontalPadding * 2)
            + toggleWidth
            + Scale(1, scale);
    }

    internal static GraphicsPath CreateRoundedRectangle(
        Rectangle bounds,
        int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(
            radius * 2,
            Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnRenderToolStripBackground(
        ToolStripRenderEventArgs e)
    {
        var scale = GetDpiScale(e.ToolStrip);
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(0, e.ToolStrip.Width),
            Math.Max(0, e.ToolStrip.Height));
        using var path = CreateRoundedRectangle(
            bounds,
            GetMenuCornerRadius(scale));
        using var brush = new SolidBrush(_palette.MenuBackground);

        WithAntialiasing(e.Graphics, () => e.Graphics.FillPath(brush, path));
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var scale = GetDpiScale(e.ToolStrip);
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(0, e.ToolStrip.Width - 1),
            Math.Max(0, e.ToolStrip.Height - 1));
        using var path = CreateRoundedRectangle(
            bounds,
            GetMenuCornerRadius(scale));
        using var pen = new Pen(_palette.Border, Scale(1, scale));

        WithAntialiasing(e.Graphics, () => e.Graphics.DrawPath(pen, path));
    }

    protected override void OnRenderMenuItemBackground(
        ToolStripItemRenderEventArgs e)
    {
        var scale = GetItemScale(e.Item);
        if (e.Item.Selected)
        {
            var inset = Scale(1, scale);
            var bounds = new Rectangle(
                inset,
                inset,
                Math.Max(0, e.Item.Width - (inset * 2)),
                Math.Max(0, e.Item.Height - (inset * 2)));
            using var path = CreateRoundedRectangle(bounds, Scale(6, scale));
            using var brush = new SolidBrush(_palette.HoverBackground);

            WithAntialiasing(e.Graphics, () => e.Graphics.FillPath(brush, path));
        }

        if (e.Item is TrayToggleMenuItem toggleItem)
        {
            DrawToggle(e.Graphics, toggleItem, scale);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var scale = GetItemScale(e.Item);
        var horizontalPadding = Scale(ItemHorizontalPadding, scale);
        var toggleWidth = e.Item is TrayToggleMenuItem
            ? Scale(ToggleWidth, scale) + horizontalPadding
            : 0;
        var textRectangle = new Rectangle(
            horizontalPadding,
            0,
            Math.Max(
                0,
                e.Item.Width - (horizontalPadding * 2) - toggleWidth),
            e.Item.Height);

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont,
            textRectangle,
            _palette.Text,
            ItemTextFormat);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var scale = GetItemScale(e.Item);
        var inset = Scale(8, scale);
        var y = e.Item.Height / 2;
        using var pen = new Pen(_palette.Separator, Scale(1, scale));
        e.Graphics.DrawLine(pen, inset, y, e.Item.Width - inset, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
    }

    private void DrawToggle(
        Graphics graphics,
        TrayToggleMenuItem item,
        float scale)
    {
        var width = Scale(ToggleWidth, scale);
        var height = Scale(ToggleHeight, scale);
        var knobSize = Scale(ToggleKnobSize, scale);
        var inset = Scale(ToggleInset, scale);
        var x = item.Width - Scale(ItemHorizontalPadding, scale) - width;
        var y = (item.Height - height) / 2;
        var trackBounds = new Rectangle(x, y, width, height);
        var knobX = item.IsOn
            ? trackBounds.Right - inset - knobSize
            : trackBounds.Left + inset;
        var knobBounds = new Rectangle(
            knobX,
            trackBounds.Top + inset,
            knobSize,
            knobSize);

        using var trackPath = CreateRoundedRectangle(trackBounds, height / 2);
        using var trackBrush = new SolidBrush(
            item.IsOn ? _palette.Accent : _palette.ToggleTrack);
        using var knobShadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
        using var knobBrush = new SolidBrush(_palette.ToggleKnob);

        WithAntialiasing(graphics, () =>
        {
            graphics.FillPath(trackBrush, trackPath);
            graphics.FillEllipse(
                knobShadowBrush,
                knobBounds.X,
                knobBounds.Y + Scale(1, scale),
                knobBounds.Width,
                knobBounds.Height);
            graphics.FillEllipse(knobBrush, knobBounds);
        });
    }

    private static float GetItemScale(ToolStripItem item) =>
        item.Owner is not null
            ? GetDpiScale(item.Owner)
            : Math.Max(1F, item.Height / (float)MenuItemSize.Height);

    private static int Scale(int value, float scale) =>
        Math.Max(1, (int)Math.Round(value * scale));

    private static void WithAntialiasing(Graphics graphics, Action draw)
    {
        var previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        try
        {
            draw();
        }
        finally
        {
            graphics.SmoothingMode = previousSmoothingMode;
        }
    }
}

internal sealed class TrayToggleMenuItem : ToolStripMenuItem
{
    private bool _isOn;

    public TrayToggleMenuItem(string text)
        : base(text)
    {
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
            {
                return;
            }

            _isOn = value;
            Invalidate();
        }
    }
}

internal readonly record struct TrayMenuPalette(
    Color MenuBackground,
    Color Text,
    Color Border,
    Color Separator,
    Color HoverBackground,
    Color Accent,
    Color ToggleTrack,
    Color ToggleKnob)
{
    public static TrayMenuPalette Light { get; } = new(
        MenuBackground: Color.FromArgb(255, 255, 255),
        Text: Color.FromArgb(26, 29, 33),
        Border: Color.FromArgb(214, 218, 225),
        Separator: Color.FromArgb(194, 200, 210),
        HoverBackground: Color.FromArgb(238, 242, 251),
        Accent: Color.FromArgb(0, 72, 204),
        ToggleTrack: Color.FromArgb(199, 204, 212),
        ToggleKnob: Color.White);

    public static TrayMenuPalette Dark { get; } = new(
        MenuBackground: Color.FromArgb(42, 47, 55),
        Text: Color.FromArgb(242, 244, 247),
        Border: Color.FromArgb(51, 58, 68),
        Separator: Color.FromArgb(94, 104, 120),
        HoverBackground: Color.FromArgb(51, 59, 70),
        Accent: Color.FromArgb(0, 108, 255),
        ToggleTrack: Color.FromArgb(58, 66, 80),
        ToggleKnob: Color.White);
}
