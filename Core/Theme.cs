using System.Drawing.Drawing2D;

namespace RestaurantManagementSystem.Core;

public static class Theme
{
    public static readonly Color Cream = ColorTranslator.FromHtml("#f2f3ae");
    public static readonly Color Gold = ColorTranslator.FromHtml("#edd382");
    public static readonly Color Orange = ColorTranslator.FromHtml("#fc9e4f");
    public static readonly Color Red = ColorTranslator.FromHtml("#f4442e");
    public static readonly Color Navy = ColorTranslator.FromHtml("#020122");
    public static readonly Color Canvas = Color.FromArgb(250, 249, 242);
    public static readonly Color White = Color.White;
    public static readonly Color Muted = Color.FromArgb(102, 105, 123);
    public static readonly Color Line = Color.FromArgb(227, 226, 217);
    public static readonly Color Green = Color.FromArgb(28, 130, 87);
    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Heading = new("Segoe UI Semibold", 21f);
    public static readonly Font Section = new("Segoe UI Semibold", 13f);
    public static readonly Font Metric = new("Segoe UI Semibold", 24f);
}

public static class Ui
{
    public static Label Label(string text, float size = 10, bool bold = false,
        Color? color = null, ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        return new Label
        {
            Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? Theme.Navy, TextAlign = alignment,
            AutoEllipsis = true, Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
    }

    public static Button Button(string text, Color? color = null, Color? textColor = null,
        int width = 112, int height = 40)
    {
        var btn = new Button
        {
            Text = text, BackColor = color ?? Theme.Orange, ForeColor = textColor ?? Theme.Navy,
            Font = new Font("Segoe UI Semibold", 9.5f),
            FlatStyle = FlatStyle.Flat, Width = width, Height = height,
            Cursor = Cursors.Hand, Margin = new Padding(5, 0, 5, 0),
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    public static TextBox Text(string? placeholder = null)
    {
        return new TextBox
        {
            Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = placeholder ?? "", Height = 34, Dock = DockStyle.Top,
            Margin = new Padding(0, 5, 0, 5)
        };
    }

    public static NumericUpDown Number(decimal min = 0, decimal max = 100000000, int decimals = 0)
    {
        return new NumericUpDown
        {
            Minimum = min, Maximum = max, DecimalPlaces = decimals,
            ThousandsSeparator = true, Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle, Height = 33,
            Dock = DockStyle.Top, Margin = new Padding(0, 5, 0, 5)
        };
    }

    public static ComboBox Combo(IEnumerable<string> options)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Body, Height = 35,
            Dock = DockStyle.Top, Margin = new Padding(0, 5, 0, 5)
        };
        combo.Items.AddRange(options.Select(x => (object)x).ToArray());
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, BackgroundColor = Theme.White, BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false, ColumnHeadersHeight = 46,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Theme.Navy, ForeColor = Theme.White,
                Font = new Font("Segoe UI Semibold", 10f), Padding = new Padding(10, 0, 4, 0),
                SelectionBackColor = Theme.Navy, SelectionForeColor = Theme.White
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Theme.White, ForeColor = Theme.Navy,
                SelectionBackColor = Theme.Cream, SelectionForeColor = Theme.Navy,
                Font = Theme.Body, Padding = new Padding(9, 2, 4, 2)
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(255, 253, 247)
            },
            GridColor = Theme.Line, RowHeadersVisible = false,
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 43 }
        };
        return grid;
    }

    public static Panel Card(int padding = 17)
    {
        return new Panel
        {
            BackColor = Theme.White, BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(padding)
        };
    }

    public static FlowLayoutPanel Toolbar()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 62, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, BackColor = Theme.Canvas,
            Padding = new Padding(0, 10, 0, 9)
        };
    }

    public static void Alert(string message, string title = "Restaurant Management System") =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(string message, string title = "Please confirm") =>
        MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    public static void Error(Exception ex) =>
        MessageBox.Show(ex.Message, "Operation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static long SelectedId(DataGridView grid)
    {
        if (grid.CurrentRow is null || !grid.Columns.Contains("Id")) return 0;
        return Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value ?? 0);
    }

    public static void HideColumns(DataGridView grid, params string[] names)
    {
        foreach (var name in names)
            if (grid.Columns.Contains(name)) grid.Columns[name]!.Visible = false;
    }

    public static void CurrencyColumn(DataGridView grid, params string[] names)
    {
        foreach (var name in names)
            if (grid.Columns.Contains(name))
                grid.Columns[name]!.DefaultCellStyle.Format = "N0";
    }

    public static void WithWaitCursor(Control owner, Action action)
    {
        try
        {
            owner.Cursor = Cursors.WaitCursor;
            action();
        }
        catch (Exception ex) { Error(ex); }
        finally { owner.Cursor = Cursors.Default; }
    }
}

public abstract class PageBase : UserControl
{
    protected readonly Panel Body;
    protected PageBase(string title, string subtitle)
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        Padding = new Padding(27, 22, 27, 24);
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            BackColor = Theme.Canvas, Margin = new Padding(0)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 83));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var head = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        head.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        head.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        head.Controls.Add(Ui.Label(title, 23, true), 0, 0);
        head.Controls.Add(Ui.Label(subtitle, 9.5f, false, Theme.Muted), 0, 1);
        Body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
        outer.Controls.Add(head, 0, 0);
        outer.Controls.Add(Body, 0, 1);
        Controls.Add(outer);
    }

    public virtual void Reload() { }
}

public sealed class SalesChart : Control
{
    public List<(string Label, decimal Value)> Points { get; set; } = [];

    public SalesChart()
    {
        DoubleBuffered = true; BackColor = Theme.White;
        MinimumSize = new Size(240, 160);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var rect = ClientRectangle;
        if (rect.Width < 150 || rect.Height < 100) return;
        int left = 44, right = 15, top = 20, bottom = 38;
        var plotHeight = Math.Max(30, rect.Height - top - bottom);
        var plotWidth = Math.Max(30, rect.Width - left - right);
        using var axis = new Pen(Theme.Line, 1f);
        using var gridFont = new Font("Segoe UI", 8f);
        for (int i = 0; i <= 3; i++)
        {
            var y = top + (int)(plotHeight * i / 3.0);
            g.DrawLine(axis, left, y, rect.Width - right, y);
        }
        if (Points.Count == 0) return;
        var max = Math.Max(1m, Points.Max(x => x.Value));
        var count = Points.Count;
        float step = (float)plotWidth / count;
        float barWidth = Math.Min(56, Math.Max(8, step * .52f));
        using var brush = new SolidBrush(Theme.Orange);
        using var labelBrush = new SolidBrush(Theme.Muted);
        using var titleBrush = new SolidBrush(Theme.Navy);
        for (var i = 0; i < count; i++)
        {
            var (label, value) = Points[i];
            float bh = (float)(value / max) * (plotHeight - 15);
            float x = left + step * i + (step - barWidth) / 2;
            float y = top + plotHeight - bh;
            g.FillRoundedRectangle(brush, new RectangleF(x, y, barWidth, Math.Max(2, bh)), 5f);
            using var format = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(label, gridFont, labelBrush, new RectangleF(left + step * i,
                top + plotHeight + 7, step, 22), format);
            if (step >= 45)
                g.DrawString(value >= 1000 ? $"{value / 1000:N0}k" : $"{value:N0}",
                    gridFont, titleBrush, new RectangleF(left + step * i,
                    Math.Max(0, y - 19), step, 20), format);
        }
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush,
        RectangleF r, float radius)
    {
        if (r.Width < radius * 2 || r.Height < radius * 2)
        {
            graphics.FillRectangle(brush, r); return;
        }
        using var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
