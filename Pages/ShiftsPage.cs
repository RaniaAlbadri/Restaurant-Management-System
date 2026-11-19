using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class ShiftsPage : PageBase
{
    private readonly Label _current = Ui.Label("", 12, true);
    private readonly Label _totals = Ui.Label("", 11, false, Theme.Muted);
    private readonly DataGridView _history = Ui.Grid();
    private readonly Button _open = Ui.Button("Open shift", Theme.Orange, Theme.Navy, 139, 43);
    private readonly Button _close = Ui.Button("Close & count cash", Theme.Red, Theme.White, 170, 43);

    public ShiftsPage() : base("Cashier shifts", "Opening float, cash/card sales, counting cash and variances")
    {
        if (!Session.RequireUser().CanSell) throw new UnauthorizedAccessException();
        var wrap = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 225));
        wrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = Ui.Card(18); top.Dock = DockStyle.Fill; top.Margin = new Padding(0, 0, 0, 15);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 49 };
        _open.Click += (_, _) =>
        {
            using var dlg = new OpenShiftDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
        };
        _close.Click += (_, _) =>
        {
            var id = ShiftService.CurrentShiftId(); if (id is null) return;
            using var dlg = new CloseShiftDialog(id.Value);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
        };
        var reload = Ui.Button("Refresh", Theme.Gold, Theme.Navy, 110);
        reload.Click += (_, _) => Reload();
        buttons.Controls.Add(_open); buttons.Controls.Add(_close); buttons.Controls.Add(reload);
        _current.Dock = DockStyle.Top; _current.Height = 49;
        _totals.Dock = DockStyle.Fill;
        top.Controls.Add(_totals); top.Controls.Add(buttons); top.Controls.Add(_current);
        wrap.Controls.Add(top, 0, 0);
        wrap.Controls.Add(_history, 0, 1);
        Body.Controls.Add(wrap);
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            var id = ShiftService.CurrentShiftId();
            _open.Enabled = id is null; _close.Enabled = id is not null;
            _current.Text = id is null ? "No open shift for your account" : $"Active shift #{id}";
            if (id is null)
                _totals.Text = "Open your shift to track its cash and card takings. Payments outside a shift remain valid but unassigned.";
            else
            {
                var totals = ShiftService.Totals(id.Value);
                _totals.Text = $"Cash sales: {Database.FormatMoney(totals.CashSales)}   •   " +
                    $"Card: {Database.FormatMoney(totals.CardSales)}   •   Payments: {totals.Payments}\n" +
                    $"Expected cash in drawer (opening float + cash sales): {Database.FormatMoney(totals.Expected)}";
            }
            _history.DataSource = Database.Query("""
                SELECT s.Id,IFNULL(u.FullName,'Unknown') AS Cashier,s.OpenedAt AS Opened,
                  IFNULL(s.ClosedAt,'OPEN') AS Closed,s.OpeningCash AS [Opening float],
                  IFNULL(s.ExpectedCash,0) AS [Expected cash],IFNULL(s.ClosingCash,0) AS [Counted cash],
                  CASE WHEN s.ClosedAt IS NULL THEN 0 ELSE s.ClosingCash-s.ExpectedCash END AS Variance,
                  s.Notes
                FROM CashierShifts s LEFT JOIN Users u ON s.UserId=u.Id
                WHERE @all=1 OR s.UserId=@user
                ORDER BY s.Id DESC LIMIT 200
                """, ("@all", Session.RequireUser().IsManager ? 1 : 0),
                ("@user", Session.RequireUser().Id));
            Ui.HideColumns(_history, "Id");
            Ui.CurrencyColumn(_history, "Opening float", "Expected cash", "Counted cash", "Variance");
        });
    }
}
