using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RdpLauncher;

/// <summary>Lightweight native dark controls. No image assets, animation loop or UI package.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(11, 16, 32);
    public static readonly Color Panel = Color.FromArgb(18, 27, 44);
    public static readonly Color Raised = Color.FromArgb(25, 38, 58);
    public static readonly Color Border = Color.FromArgb(41, 57, 82);
    public static readonly Color Text = Color.FromArgb(234, 240, 250);
    public static readonly Color Muted = Color.FromArgb(157, 175, 198);
    public static readonly Color Accent = Color.FromArgb(112, 226, 191);
    public static readonly Color Purple = Color.FromArgb(183, 166, 255);
    public static readonly Color Danger = Color.FromArgb(255, 129, 151);

    public static System.Windows.Forms.Panel Card() => new RoundedCard();
    public static System.Windows.Forms.Button Button(string text, bool primary = false, bool danger = false) =>
        new RoundedButton { Text = text, Primary = primary, Danger = danger };

    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null) => new()
    {
        Text = text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color ?? Text, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8)
    };

    public static void Apply(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10);
        Style(form);
        void DarkTitle(object? sender, EventArgs args)
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                var enabled = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { }
        }
        form.HandleCreated += DarkTitle;
        if (form.IsHandleCreated) DarkTitle(form, EventArgs.Empty);
    }

    static void Style(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case RoundedButton: break;
                case System.Windows.Forms.Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = Raised;
                    button.BackColor = Panel;
                    button.ForeColor = Text;
                    break;
                case LinkLabel link:
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = Purple;
                    link.VisitedLinkColor = Purple;
                    link.DisabledLinkColor = Muted;
                    link.BackColor = Color.Transparent;
                    break;
                case TextBox text:
                    text.BackColor = text.Multiline && text.ReadOnly ? Bg : Raised;
                    text.ForeColor = Text;
                    text.BorderStyle = BorderStyle.None;
                    break;
                case NumericUpDown number:
                    number.BackColor = Raised;
                    number.ForeColor = Text;
                    number.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox combo:
                    combo.BackColor = Raised;
                    combo.ForeColor = Text;
                    combo.FlatStyle = FlatStyle.Flat;
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.ItemHeight = 26;
                    combo.DrawItem -= DrawComboItem;
                    combo.DrawItem += DrawComboItem;
                    break;
                case RoundedCard: break;
                case Label label:
                    label.BackColor = Color.Transparent;
                    break;
                case System.Windows.Forms.Panel:
                    if (control.BackColor == SystemColors.Control) control.BackColor = Color.Transparent;
                    break;
            }
            Style(control);
        }
    }

    static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? Border : Raised);
        e.Graphics.FillRectangle(background, e.Bounds);
        var label = e.Index >= 0 && e.Index < combo.Items.Count ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
        TextRenderer.DrawText(e.Graphics, label, combo.Font, Rectangle.Inflate(e.Bounds, -8, 0), Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }

    internal static Color ParentBackground(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent.BackColor.A == 255) return parent.BackColor;
        return Bg;
    }

    internal static GraphicsPath Round(RectangleF r, float radius)
    {
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        var path = new GraphicsPath(); var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

public sealed class RoundedCard : System.Windows.Forms.Panel
{
    public int CornerRadius { get; set; } = 16;
    public Color BorderColor { get; set; } = Theme.Border;
    public RoundedCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Padding = new Padding(20);
        Margin = new Padding(0);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.ParentBackground(this));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width < 2 || Height < 2) return;
        using var shape = Theme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), CornerRadius);
        using var brush = new SolidBrush(BackColor);
        using var pen = new Pen(BorderColor);
        e.Graphics.FillPath(brush, shape);
        e.Graphics.DrawPath(pen, shape);
    }
}

public sealed class RoundedButton : System.Windows.Forms.Button
{
    bool hover;
    bool pressed;
    public bool Primary { get; set; }
    public bool Danger { get; set; }
    bool selected;
    public bool Selected { get => selected; set { if (selected == value) return; selected = value; Invalidate(); } }
    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Raised;
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
        Padding = new Padding(14, 8, 14, 8);
        MinimumSize = new Size(0, 38);
        Height = 42;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        UseVisualStyleBackColor = false;
        AccessibleRole = AccessibleRole.PushButton;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = false; Invalidate(); } base.OnKeyUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.ParentBackground(this));
        if (Width < 3 || Height < 3) return;
        var fill = Primary ? Theme.Accent : Danger ? Color.FromArgb(63, 30, 47) : Selected ? Color.FromArgb(48, 43, 79) : BackColor;
        var ink = Primary ? Theme.Bg : Danger ? Theme.Danger : Selected ? Theme.Purple : ForeColor;
        if (!Enabled) { fill = Color.FromArgb(25, 34, 49); ink = Color.FromArgb(111, 128, 151); }
        else if (pressed) fill = Mix(fill, Theme.Bg, .20f);
        else if (hover) fill = Mix(fill, Primary ? Color.White : Theme.Purple, Primary ? .10f : .13f);
        using var shape = Theme.Round(new RectangleF(1, 1, Width - 2, Height - 2), 9);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(Primary ? fill : Selected ? Theme.Purple : Theme.Border);
        g.FillPath(brush, shape); g.DrawPath(pen, shape);
        var content = Rectangle.Inflate(ClientRectangle, -10, -3);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(g, Text, Font, content, ink, flags);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -5, -5), ink, fill);
    }
    static Color Mix(Color a, Color b, float t) => Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}
