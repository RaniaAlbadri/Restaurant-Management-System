using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages;

public sealed class DashboardPage : PageBase
{
    private readonly Label _sales = Ui.Label("—", 23, true);
    private readonly Label _orders = Ui.Label("—", 23, true);
    private readonly Label _active = Ui.Label("—", 23, true);
    private readonly Label _tables = Ui.Label("—", 23, true);
    private readonly SalesChart _chart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _latest = Ui.Grid();

    public DashboardPage() : base("Dashboard", "Your restaurant at a glance")
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Theme.Canvas
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            Padding = new Padding(0, 0, 0, 17)
        };
        for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(Metric("TODAY'S SALES", _sales, Theme.Orange), 0, 0);
        metrics.Controls.Add(Metric("TODAY'S ORDERS", _orders, Theme.Gold), 1, 0);
        metrics.Controls.Add(Metric("ACTIVE ORDERS", _active, Theme.Red), 2, 0);
        metrics.Controls.Add(Metric("AVAILABLE TABLES", _tables, Theme.Green), 3, 0);
        var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        var chartCard = Ui.Card(15); chartCard.Dock = DockStyle.Fill;
        chartCard.Margin = new Padding(0, 0, 10, 0);
        var chartTitle = Ui.Label("SALES • LAST 7 DAYS", 12, true);
        chartTitle.Dock = DockStyle.Top; chartTitle.Height = 46;
        chartCard.Controls.Add(_chart); chartCard.Controls.Add(chartTitle);
        var gridCard = Ui.Card(15); gridCard.Dock = DockStyle.Fill;
        gridCard.Margin = new Padding(10, 0, 0, 0);
        var gridTitle = Ui.Label("RECENT ORDERS", 12, true);
        gridTitle.Dock = DockStyle.Top; gridTitle.Height = 46;
        gridCard.Controls.Add(_latest); gridCard.Controls.Add(gridTitle);
        lower.Controls.Add(chartCard, 0, 0); lower.Controls.Add(gridCard, 1, 0);
        layout.Controls.Add(metrics, 0, 0); layout.Controls.Add(lower, 0, 1);
        Body.Controls.Add(layout);
    }

    private static Panel Metric(string title, Label value, Color accent)
    {
        var p = Ui.Card(12); p.Dock = DockStyle.Fill; p.Margin = new Padding(0, 0, 13, 0);
        var stripe = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = accent };
        var t = Ui.Label(title, 9, true, Theme.Muted);
        t.Dock = DockStyle.Top; t.Height = 43;
        value.Dock = DockStyle.Fill;
        p.Controls.Add(value); p.Controls.Add(t); p.Controls.Add(stripe);
        return p;
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            var day = DateTime.Today.ToString("yyyy-MM-dd");
            _sales.Text = Database.FormatMoney(Database.Scalar(
                "SELECT COALESCE(SUM(FinalTotal),0) FROM Invoices WHERE substr(InvoiceDate,1,10)=@day",
                ("@day", day)));
            _orders.Text = Database.Count(
                "SELECT COUNT(*) FROM Orders WHERE substr(OrderDate,1,10)=@day", ("@day", day)).ToString("N0");
            _active.Text = Database.Count(
                "SELECT COUNT(*) FROM Orders WHERE Status NOT IN ('Completed','Cancelled')").ToString("N0");
            _tables.Text = Database.Count(
                "SELECT COUNT(*) FROM DiningTables WHERE Status='Available'").ToString("N0");
            var salesRows = Database.Query("""
                SELECT substr(InvoiceDate,1,10) AS Day, SUM(FinalTotal) AS Sales
                FROM Invoices WHERE substr(InvoiceDate,1,10)>=@start
                GROUP BY substr(InvoiceDate,1,10)
                """, ("@start", DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd")));
            var data = salesRows.Rows.Cast<DataRow>().ToDictionary(
                r => Convert.ToString(r["Day"])!, r => Database.Money(r["Sales"]));
            _chart.Points = Enumerable.Range(0, 7).Select(i =>
            {
                var d = DateTime.Today.AddDays(-6 + i);
                return (d.ToString("ddd"), data.GetValueOrDefault(d.ToString("yyyy-MM-dd")));
            }).ToList();
            _chart.Invalidate();
            _latest.DataSource = Database.Query("""
                SELECT OrderNumber AS [Order #],TableLabel AS [Table],Status,Subtotal
                FROM Orders ORDER BY Id DESC LIMIT 12
                """);
            Ui.CurrencyColumn(_latest, "Subtotal");
        });
    }
}
