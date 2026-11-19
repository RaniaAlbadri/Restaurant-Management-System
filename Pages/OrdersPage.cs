using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class OrdersPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text("Search order number / table / customer");
    private readonly ComboBox _status = Ui.Combo(["All statuses", "New", "Preparing", "Ready", "Completed", "Cancelled"]);

    public OrdersPage() : base("Orders", "Create, track and fulfill orders")
    {
        var bar = Ui.Toolbar();
        bar.WrapContents = true;
        bar.Height = 110;
        _search.Dock = DockStyle.None; _search.Width = 250;
        _status.Dock = DockStyle.None; _status.Width = 137;
        _search.Margin = new Padding(0, 5, 12, 5);
        _status.Margin = new Padding(0, 5, 12, 5);
        bar.Controls.Add(_search); bar.Controls.Add(_status);
        var add = Ui.Button("+ New order", Theme.Orange, Theme.Navy, 125);
        var details = Ui.Button("Details", Theme.Gold, Theme.Navy, 92);
        var preparing = Ui.Button("Preparing", Theme.Cream, Theme.Navy, 99);
        var ready = Ui.Button("Ready", Theme.Cream, Theme.Navy, 82);
        var cancel = Ui.Button("Cancel order", Theme.Red, Color.White, 118);
        var pay = Ui.Button("Checkout", Theme.Navy, Theme.White, 100);
        add.Click += (_, _) =>
        {
            using var dlg = new OrderCreateForm();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
        };
        details.Click += (_, _) =>
        {
            var id = Ui.SelectedId(_grid);
            if (id == 0) return;
            try { using var dlg = new OrderDetailsForm(id); dlg.ShowDialog(FindForm()); }
            catch (Exception ex) { Ui.Error(ex); }
        };
        preparing.Click += (_, _) => Change("Preparing");
        ready.Click += (_, _) => Change("Ready");
        cancel.Click += (_, _) => Change("Cancelled");
        pay.Click += (_, _) =>
        {
            var id = Ui.SelectedId(_grid); if (id == 0) return;
            try
            {
                using var dlg = new CheckoutForm(id);
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    Reload();
                    Ui.Alert("Payment saved, invoice created and table released.");
                    if (Ui.Confirm("Open print preview for the new invoice?", "Invoice created"))
                        new ReceiptPrinter(dlg.InvoiceId).Preview(FindForm());
                }
            }
            catch (Exception ex) { Ui.Error(ex); }
        };
        bar.Controls.Add(add); bar.Controls.Add(details); bar.Controls.Add(preparing);
        bar.Controls.Add(ready); bar.Controls.Add(cancel);
        if (Session.RequireUser().CanSell) bar.Controls.Add(pay);
        _search.TextChanged += (_, _) => Reload();
        _status.SelectedIndexChanged += (_, _) => Reload();
        _grid.CellDoubleClick += (_, _) => details.PerformClick();
        Body.Controls.Add(_grid); Body.Controls.Add(bar);
    }

    private void Change(string status)
    {
        Ui.WithWaitCursor(this, () =>
        {
            var id = Ui.SelectedId(_grid); if (id == 0) return;
            if (status == "Cancelled" && !Ui.Confirm("Cancel this order? This action cannot be undone.")) return;
            OrderService.ChangeStatus(id, status);
            Reload();
        });
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            var status = _status.SelectedIndex <= 0 ? "%" : _status.Text;
            _grid.DataSource = Database.Query("""
                SELECT o.Id,o.OrderNumber AS [Order #],o.OrderDate AS [Date & time],
                  o.OrderType AS [Type],o.TableLabel AS [Table],IFNULL(c.FullName,'Walk-in') AS Customer,
                  o.Status,o.Subtotal,IFNULL(u.FullName,'Former employee') AS Staff
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerId=c.Id
                LEFT JOIN Users u ON o.UserId=u.Id
                WHERE o.Status LIKE @status AND
                 (o.OrderNumber LIKE @search OR o.TableLabel LIKE @search
                  OR o.OrderType LIKE @search OR o.DeliveryPhone LIKE @search
                  OR IFNULL(c.FullName,'Walk-in') LIKE @search)
                ORDER BY o.Id DESC
                """, ("@status", status), ("@search", "%" + _search.Text.Trim() + "%"));
            Ui.HideColumns(_grid, "Id");
            Ui.CurrencyColumn(_grid, "Subtotal");
        });
    }
}
