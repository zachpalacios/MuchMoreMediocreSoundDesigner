using System.ComponentModel;

namespace MediocreSoundDesigner;

// A flat, modern slider: rounded track, accent-colored fill, round thumb that grows on hover.
// Raises Scroll only when the USER changes it (dragging, clicking, mouse wheel).
// Properties are set in code, not in the form designer, so they're marked Hidden (required by .NET 9's WFO1000 check)
public class FlatSlider : Control
{
    private int min = 0, max = 100, val = 0;
    private bool dragging, hovering;
    private const int PAD = 10;   // space at each end so the thumb isn't cut off

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(79, 195, 247);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = Color.FromArgb(70, 70, 74);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ThumbColor { get; set; } = Color.FromArgb(224, 224, 224);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int WheelStep { get; set; } = 5;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? Mark { get; set; }   // optional small tick (e.g. 100% on the volume slider)

    public event EventHandler Scroll;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => min;
        set { min = value; if (max < min) max = min; Value = val; Invalidate(); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => max;
        set { max = value; if (min > max) min = max; Value = val; Invalidate(); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => val;
        set
        {
            int v = Math.Clamp(value, min, max);
            if (v != val) { val = v; Invalidate(); }
        }
    }

    public FlatSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
        Height = 30;
    }

    private int XForValue(int value)
    {
        double t = (value - min) / (double)Math.Max(1, max - min);
        return PAD + (int)Math.Round((Width - 2 * PAD) * t);
    }

    private void SetFromMouse(int x)
    {
        double t = (x - PAD) / (double)Math.Max(1, Width - 2 * PAD);
        int v = Math.Clamp(min + (int)Math.Round(t * (max - min)), min, max);
        if (v != val)
        {
            Value = v;
            Scroll?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);   // raise MouseDown first (used to save an undo step BEFORE the value changes)
        if (!Enabled || e.Button != MouseButtons.Left) return;
        dragging = true;
        Capture = true;
        SetFromMouse(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging) SetFromMouse(e.X);
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(e);     // raise MouseUp after the final value is set
    }
    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Enabled) return;
        int before = val;
        Value += Math.Sign(e.Delta) * WheelStep;
        if (val != before) Scroll?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor == Color.Transparent ? (Parent?.BackColor ?? Color.Black) : BackColor);

        int cy = Height / 2;
        int thumbX = XForValue(val);
        Color accent = Enabled ? AccentColor : Color.FromArgb(90, 90, 90);

        // track (full width), then the filled part up to the thumb
        using (Pen track = new(TrackColor, 4) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
        {
            g.DrawLine(track, PAD, cy, Width - PAD, cy);
        }
        if (thumbX > PAD)
        {
            using Pen fill = new(accent, 4) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(fill, PAD, cy, thumbX, cy);
        }

        // optional mark (e.g. 100%)
        if (Mark is int mark && mark > min && mark < max)
        {
            int mx = XForValue(mark);
            using Pen markPen = new(Color.FromArgb(120, ThumbColor), 2);
            g.DrawLine(markPen, mx, cy + 7, mx, cy + 11);
        }

        // thumb: bigger while hovering or dragging
        int r = (hovering || dragging) && Enabled ? 8 : 6;
        using (SolidBrush thumb = new(Enabled ? ThumbColor : Color.FromArgb(110, 110, 110)))
        {
            g.FillEllipse(thumb, thumbX - r, cy - r, r * 2, r * 2);
        }
        using (Pen ring = new(accent, 2))
        {
            g.DrawEllipse(ring, thumbX - r, cy - r, r * 2, r * 2);
        }
    }
}