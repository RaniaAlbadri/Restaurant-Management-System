using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages;

public sealed class KitchenPage : PageBase
{
    private readonly FlowLayoutPanel _tickets = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
        Padding = new Padding(5, 8, 5, 8), BackColor = Theme.Canvas
    };
    private readonly ComboBox _filter = Ui.Combo(["Active tickets", "New", "Preparing", "Ready", "All tickets"]);
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 10000 };

    public KitchenPage() : base("Kitchen display", "New orders and cooking status · refreshes every 10 seconds")
    {
        if (!Session.RequireUser().CanManageOrders) throw new UnauthorizedAccessException();
        var bar = Ui.Toolbar();
        _filter.Dock = DockStyle.None; _filter.Width = 200;
        _filter.Margin = new Padding(0, 4, 12, 4);
        _filter.SelectedIndexChanged += (_, _) => Reload();
        var reload = Ui.Button("Refresh (F5)", Theme.Gold, Theme.Navy, 125);
        reload.Click += (_, _) => Reload();
        bar.Controls.Add(_filter); bar.Controls.Add(reload);
        Body.Controls.Add(_tickets); Body.Controls.Add(bar);
        _refresh.Tick += (_, _) => { if (Visible && IsHandleCreated) Reload(); };
        _refresh.Start();
        Disposed += (_, _) => _refresh.Dispose();
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            string where = _filter.Text switch
            {
                "New" or "Preparing" or "Ready" => "o.Status=@status",
                "All tickets" => "1=1",
                _ => "o.Status IN ('New','Preparing','Ready')"
            };
            var rows = Database.Query($"""
                SELECT o.Id,o.OrderNumber,o.OrderDate,o.Status,o.OrderType,o.TableLabel,
                       o.Notes,o.DeliveryAddress
                FROM Orders o WHERE {where} ORDER BY o.Id DESC LIMIT 100
                """, ("@status", _filter.Text));
            _tickets.SuspendLayout();
            foreach (Control previous in _tickets.Controls.Cast<Control>().ToArray())
            {
                _tickets.Controls.Remove(previous);
                previous.Dispose();
            }
            foreach (DataRow r in rows.Rows)
            {
                var id = Convert.ToInt64(r["Id"]);
                var status = Convert.ToString(r["Status"]) ?? "";
                var ticket = Ui.Card(14);
                ticket.Width = 326; ticket.Height = 281;
                ticket.Margin = new Padding(0, 0, 14, 14);
                var stripe = new Panel { Dock = DockStyle.Top, Height = 6,
                    BackColor = status switch { "New" => Theme.Orange, "Preparing" => Theme.Gold,
                        "Ready" => Theme.Green, _ => Theme.Muted } };
                var header = Ui.Label($"{r["OrderNumber"]}\n{r["OrderType"]} · {r["TableLabel"]} · {status}", 10.5f, true);
                header.Dock = DockStyle.Top; header.Height = 54;
                var itemLines = Database.Query("SELECT MealName,Quantity FROM OrderItems WHERE OrderId=@id ORDER BY Id",
                    ("@id", id)).Rows.Cast<DataRow>()
                    .Select(x => $"{x["Quantity"]} × {x["MealName"]}");
                var details = Ui.Label(string.Join("\n", itemLines), 10);
                details.Dock = DockStyle.Fill; details.AutoEllipsis = true;
                var notes = Ui.Label($"{r["OrderDate"]}  ·  {r["Notes"]}", 8.7f, false, Theme.Muted);
                notes.Dock = DockStyle.Bottom; notes.Height = 38;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44,
                    WrapContents = false };
                var next = status switch
                {
                    "New" => "Preparing",
                    "Preparing" => "Ready",
                    _ => ""
                };
                if (next.Length > 0)
                {
                    var button = Ui.Button(next == "Ready" ? "Mark ready" : "Start preparing",
                        Theme.Orange, Theme.Navy, 154, 34);
                    button.Click += (_, _) => Ui.WithWaitCursor(this, () =>
                    {
                        OrderService.ChangeStatus(id, next);
                        Reload();
                    });
                    buttons.Controls.Add(button);
                }
                ticket.Controls.Add(details);
                ticket.Controls.Add(notes);
                ticket.Controls.Add(buttons);
                ticket.Controls.Add(header);
                ticket.Controls.Add(stripe);
                _tickets.Controls.Add(ticket);
            }
            if (_tickets.Controls.Count == 0)
            {
                var empty = Ui.Label("No kitchen tickets for this filter.", 14, true, Theme.Muted);
                empty.Width = 500; empty.Height = 70; empty.Dock = DockStyle.None;
                _tickets.Controls.Add(empty);
            }
            _tickets.ResumeLayout();
        });
    }
}
