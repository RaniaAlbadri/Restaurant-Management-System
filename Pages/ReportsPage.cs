using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages;

public sealed class ReportsPage : PageBase
{
    private readonly DateTimePicker _from = new()
    {
        Format = DateTimePickerFormat.Short, Width = 128,
        Value = DateTime.Today.AddDays(-29)
    };
    private readonly DateTimePicker _to = new()
    {
        Format = DateTimePickerFormat.Short, Width = 128,
        Value = DateTime.Today
    };
    private readonly Label _sales = Ui.Label("—", 15.5f, true);
    private readonly Label _invoices = Ui.Label("—", 15.5f, true);
    private readonly Label _average = Ui.Label("—", 15.5f, true);
    private readonly Label _items = Ui.Label("—", 15.5f, true);
    private readonly SalesChart _chart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _topMeals = Ui.Grid();

    public ReportsPage() : base("Sales reports", "Track revenue and menu performance over time")
    {
        if (!Session.RequireUser().IsManager) throw new UnauthorizedAccessException();
        var bar = Ui.Toolbar();
        var fromLabel = Ui.Label("From", 9, true);
        fromLabel.Dock = DockStyle.None;
        fromLabel.Width = 44;
        fromLabel.Height = 34;
        bar.Controls.Add(fromLabel);
        bar.Controls.Add(_from);
        var toLabel = Ui.Label("To", 9, true);
        toLabel.Dock = DockStyle.None;
        toLabel.Width = 30;
        toLabel.Height = 34;
        bar.Controls.Add(toLabel);
        bar.Controls.Add(_to);
        var refresh = Ui.Button("Apply dates", Theme.Orange, Theme.Navy, 118);
        refresh.Click += (_, _) => Reload();
        var today = Ui.Button("Today", Theme.Gold, Theme.Navy, 77);
        today.Click += (_, _) => { _from.Value = DateTime.Today; _to.Value = DateTime.Today; Reload(); };
        bar.Controls.Add(refresh); bar.Controls.Add(today);
        var all = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        all.RowStyles.Add(new RowStyle(SizeType.Absolute, 103));
        all.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        summary.Controls.Add(Kpi("TOTAL REVENUE", _sales), 0, 0);
        summary.Controls.Add(Kpi("PAID INVOICES", _invoices), 1, 0);
        summary.Controls.Add(Kpi("AVERAGE TICKET", _average), 2, 0);
        summary.Controls.Add(Kpi("ITEMS SOLD", _items), 3, 0);
        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(0, 14, 0, 0) };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        var trend = Ui.Card(13); trend.Dock = DockStyle.Fill; trend.Margin = new Padding(0, 0, 10, 0);
        var trendTitle = Ui.Label("REVENUE BY DAY / MONTH", 11.5f, true);
        trendTitle.Height = 42; trendTitle.Dock = DockStyle.Top;
        trend.Controls.Add(_chart); trend.Controls.Add(trendTitle);
        var popular = Ui.Card(13); popular.Dock = DockStyle.Fill; popular.Margin = new Padding(10, 0, 0, 0);
        var popularTitle = Ui.Label("BEST SELLING DISHES", 11.5f, true);
        popularTitle.Height = 42; popularTitle.Dock = DockStyle.Top;
        popular.Controls.Add(_topMeals); popular.Controls.Add(popularTitle);
        details.Controls.Add(trend, 0, 0); details.Controls.Add(popular, 1, 0);
        all.Controls.Add(summary, 0, 0); all.Controls.Add(details, 0, 1);
        Body.Controls.Add(all); Body.Controls.Add(bar);
    }

    private static Panel Kpi(string label, Label value)
    {
        var card = Ui.Card(10); card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 10, 0);
        var heading = Ui.Label(label, 9, true, Theme.Muted);
        heading.Dock = DockStyle.Top; heading.Height = 29;
        value.Dock = DockStyle.Fill;
        card.Controls.Add(value); card.Controls.Add(heading);
        return card;
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            if (_from.Value.Date > _to.Value.Date)
                throw new InvalidOperationException("Start date must be on or before the end date.");
            string start = _from.Value.Date.ToString("yyyy-MM-dd");
            string end = _to.Value.Date.ToString("yyyy-MM-dd");
            var totals = Database.Query("""
                SELECT COUNT(*) Count,COALESCE(SUM(FinalTotal),0) Revenue,
                  COALESCE(AVG(FinalTotal),0) Average FROM Invoices
                WHERE substr(InvoiceDate,1,10) BETWEEN @from AND @to
                """, ("@from", start), ("@to", end));
            var r = totals.Rows[0];
            _sales.Text = Database.FormatMoney(r["Revenue"]);
            _invoices.Text = Convert.ToInt64(r["Count"]).ToString("N0");
            _average.Text = Database.FormatMoney(r["Average"]);
            _items.Text = Database.Count("""
                SELECT COALESCE(SUM(oi.Quantity),0) FROM OrderItems oi
                  JOIN Invoices i ON i.OrderId=oi.OrderId
                WHERE substr(i.InvoiceDate,1,10) BETWEEN @from AND @to
                """, ("@from", start), ("@to", end)).ToString("N0");
            _topMeals.DataSource = Database.Query("""
                SELECT oi.MealName AS Dish,SUM(oi.Quantity) AS [Units sold],
                  SUM(oi.LineTotal) AS [Gross revenue]
                FROM OrderItems oi JOIN Invoices i ON i.OrderId=oi.OrderId
                WHERE substr(i.InvoiceDate,1,10) BETWEEN @from AND @to
                GROUP BY oi.MealName ORDER BY [Units sold] DESC LIMIT 15
                """, ("@from", start), ("@to", end));
            Ui.CurrencyColumn(_topMeals, "Gross revenue");
            var span = (_to.Value.Date - _from.Value.Date).TotalDays;
            if (span <= 45)
            {
                var query = Database.Query("""
                    SELECT substr(InvoiceDate,1,10) Label,SUM(FinalTotal) Total
                    FROM Invoices WHERE substr(InvoiceDate,1,10) BETWEEN @from AND @to
                    GROUP BY substr(InvoiceDate,1,10)
                    """, ("@from", start), ("@to", end));
                var dictionary = query.Rows.Cast<DataRow>().ToDictionary(
                    x => Convert.ToString(x["Label"])!, x => Database.Money(x["Total"]));
                _chart.Points = Enumerable.Range(0, (int)span + 1).Select(i =>
                {
                    var d = _from.Value.Date.AddDays(i);
                    var key = d.ToString("yyyy-MM-dd");
                    return (d.ToString("dd MMM"), dictionary.GetValueOrDefault(key));
                }).ToList();
            }
            else
            {
                var query = Database.Query("""
                    SELECT substr(InvoiceDate,1,7) Label,SUM(FinalTotal) Total
                    FROM Invoices WHERE substr(InvoiceDate,1,10) BETWEEN @from AND @to
                    GROUP BY substr(InvoiceDate,1,7)
                    """, ("@from", start), ("@to", end));
                _chart.Points = query.Rows.Cast<DataRow>().Select(r =>
                    (Convert.ToString(r["Label"])!, Database.Money(r["Total"]))).ToList();
            }
            _chart.Invalidate();
        });
    }
}
