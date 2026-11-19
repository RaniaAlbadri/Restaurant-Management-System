using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class TablesPage : PageBase
{
    private readonly FlowLayoutPanel _tiles = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true,
        WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
        BackColor = Theme.Canvas, Padding = new Padding(0, 7, 0, 15)
    };

    public TablesPage() : base("Dining tables", "Live table status and seating capacity")
    {
        var bar = Ui.Toolbar();
        var statusLegend = Ui.Label("● Available      ● Reserved      ● Occupied", 9.5f,
            false, Theme.Muted);
        statusLegend.Width = 350;
        statusLegend.Dock = DockStyle.None;
        bar.Controls.Add(statusLegend);
        if (Session.RequireUser().IsManager)
        {
            var add = Ui.Button("+ Add table", Theme.Orange, Theme.Navy, 116);
            add.Click += (_, _) => Edit(0);
            bar.Controls.Add(add);
        }
        var refresh = Ui.Button("Refresh", Theme.Gold, Theme.Navy, 90);
        refresh.Click += (_, _) => Reload(); bar.Controls.Add(refresh);
        Body.Controls.Add(_tiles);
        Body.Controls.Add(bar);
    }

    private void Edit(long id)
    {
        using var dlg = new TableDialog(id);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            _tiles.SuspendLayout();
            _tiles.Controls.Clear();
            var tables = Database.Query("SELECT * FROM DiningTables ORDER BY TableNumber");
            foreach (DataRow row in tables.Rows)
            {
                long id = Convert.ToInt64(row["Id"]);
                string number = Convert.ToString(row["TableNumber"])!;
                string status = Convert.ToString(row["Status"])!;
                int capacity = Convert.ToInt32(row["Capacity"]);
                var card = Ui.Card(14);
                card.Size = new Size(287, 183);
                card.Margin = new Padding(0, 0, 16, 16);
                var accent = new Panel
                {
                    Dock = DockStyle.Top, Height = 6,
                    BackColor = status switch { "Available" => Theme.Green,
                        "Reserved" => Theme.Gold, _ => Theme.Red }
                };
                var text = new Label
                {
                    Text = $"{number}\n{capacity} seats\n{status}",
                    Dock = DockStyle.Top, Height = 104,
                    ForeColor = Theme.Navy, Font = new Font("Segoe UI Semibold", 13),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                var actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false
                };
                if (Session.RequireUser().CanManageOrders && status == "Available")
                {
                    var order = Ui.Button("New order", Theme.Orange, Theme.Navy, 112, 31);
                    order.Click += (_, _) =>
                    {
                        using var dlg = new OrderCreateForm(id);
                        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
                    };
                    actions.Controls.Add(order);
                }
                if (Session.RequireUser().IsManager)
                {
                    var edit = Ui.Button("Edit", Theme.Gold, Theme.Navy, 62, 31);
                    edit.Click += (_, _) => Edit(id);
                    actions.Controls.Add(edit);
                    if (status != "Occupied")
                    {
                        var delete = Ui.Button("×", Theme.Red, Color.White, 36, 31);
                        delete.Click += (_, _) => Ui.WithWaitCursor(this, () =>
                        {
                            if (!Ui.Confirm($"Delete table {number}?")) return;
                            Database.Execute("DELETE FROM DiningTables WHERE Id=@id", ("@id", id));
                            Reload();
                        });
                        actions.Controls.Add(delete);
                    }
                }
                card.Controls.Add(actions); card.Controls.Add(text); card.Controls.Add(accent);
                _tiles.Controls.Add(card);
            }
            _tiles.ResumeLayout();
        });
    }
}
