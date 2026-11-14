using System.Runtime.InteropServices;

namespace MediocreSoundDesigner;

// Dark title bar + dark menus/toolbars, using the colors in Settings
public static class DarkTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;   // Windows 10 (2004+) and 11
    private const int DWMWA_CAPTION_COLOR = 35;             // Windows 11 only
    private const int DWMWA_TEXT_COLOR = 36;                // Windows 11 only

    // Makes a window's title bar dark. Safe to call on older Windows (it just does nothing there).
    public static void ApplyTitleBar(Form form)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            int on = 1;
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            int caption = ToColorRef(Settings.Color.WinBg);
            DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            int text = ToColorRef(Settings.Color.WinFg);
            DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        catch
        {
            // very old Windows without dwmapi: keep the normal title bar
        }
    }

    // Windows wants colors as 0x00BBGGRR
    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}

// Draws menus, drop-downs, right-click menus and the toolbar in dark colors
public class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is ToolStripButton { Checked: true })
        {
            e.TextColor = Settings.Color.Accent;                       // e.g. Loop when it's on
        }
        else
        {
            e.TextColor = e.Item.Enabled ? Settings.Color.WinFg : Color.FromArgb(110, 110, 110);
        }
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Settings.Color.WinFg;                         // submenu arrows
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // a simple accent-colored checkmark (the default one is black and disappears on dark)
        Rectangle r = e.ImageRectangle;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using Pen pen = new(Settings.Color.Accent, 2);
        e.Graphics.DrawLines(pen, new[] {
      new Point(r.Left + r.Width / 5, r.Top + r.Height / 2),
      new Point(r.Left + r.Width * 2 / 5, r.Bottom - r.Height / 4),
      new Point(r.Right - r.Width / 6, r.Top + r.Height / 4)
    });
    }
}

public class DarkColorTable : ProfessionalColorTable
{
    private static Color Bar => Settings.Color.WinBg;          // menu bar (matches the title bar)
    private static Color Tool => Settings.Color.Toolbar;       // toolbar
    private static Color Drop => Settings.Color.TrackArea;     // drop-down menus
    private static Color Hover => Color.FromArgb(62, 62, 66);
    private static Color Pressed => Color.FromArgb(75, 75, 80);
    private static Color Border => Color.FromArgb(63, 63, 70);

    public override Color MenuStripGradientBegin => Bar;
    public override Color MenuStripGradientEnd => Bar;
    public override Color ToolStripGradientBegin => Tool;
    public override Color ToolStripGradientMiddle => Tool;
    public override Color ToolStripGradientEnd => Tool;
    public override Color ToolStripBorder => Tool;              // no white line under the toolbar
    public override Color ToolStripDropDownBackground => Drop;
    public override Color ImageMarginGradientBegin => Drop;
    public override Color ImageMarginGradientMiddle => Drop;
    public override Color ImageMarginGradientEnd => Drop;
    public override Color MenuBorder => Border;
    public override Color MenuItemBorder => Hover;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Drop;
    public override Color MenuItemPressedGradientMiddle => Drop;
    public override Color MenuItemPressedGradientEnd => Drop;
    public override Color ButtonSelectedHighlight => Hover;
    public override Color ButtonSelectedGradientBegin => Hover;
    public override Color ButtonSelectedGradientMiddle => Hover;
    public override Color ButtonSelectedGradientEnd => Hover;
    public override Color ButtonSelectedBorder => Hover;
    public override Color ButtonPressedHighlight => Pressed;
    public override Color ButtonPressedGradientBegin => Pressed;
    public override Color ButtonPressedGradientMiddle => Pressed;
    public override Color ButtonPressedGradientEnd => Pressed;
    public override Color ButtonPressedBorder => Pressed;
    public override Color ButtonCheckedHighlight => Pressed;
    public override Color ButtonCheckedGradientBegin => Pressed;
    public override Color ButtonCheckedGradientMiddle => Pressed;
    public override Color ButtonCheckedGradientEnd => Pressed;
    public override Color CheckBackground => Drop;
    public override Color CheckSelectedBackground => Hover;
    public override Color CheckPressedBackground => Pressed;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;
    public override Color GripDark => Tool;
    public override Color GripLight => Tool;
    public override Color OverflowButtonGradientBegin => Tool;
    public override Color OverflowButtonGradientMiddle => Tool;
    public override Color OverflowButtonGradientEnd => Tool;
}