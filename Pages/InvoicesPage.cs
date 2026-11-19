using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class InvoicesPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text("Search invoice number or order");

    public InvoicesPage() : base("Invoices & billing", "Completed payments and printable invoices")
    {
        if (!Session.RequireUser().CanSell) throw new UnauthorizedAccessException();
        var bar = Ui.Toolbar();
        _search.Width = 267; _search.Dock = DockStyle.None;
        bar.Controls.Add(_search);
        var preview = Ui.Button("Print preview", Theme.Gold, Theme.Navy, 135);
        var pdf = Ui.Button("Save PDF", Theme.Orange, Theme.Navy, 112);
        preview.Click += (_, _) => Print(false);
        pdf.Click += (_, _) => Print(true);
        bar.Controls.Add(preview); bar.Controls.Add(pdf);
        _search.TextChanged += (_, _) => Reload();
        _grid.CellDoubleClick += (_, _) => preview.PerformClick();
        Body.Controls.Add(_grid); Body.Controls.Add(bar);
    }

    public void PreviewSelected() => Print(false);

    private void Print(bool pdf)
    {
        var id = Ui.SelectedId(_grid); if (id == 0) return;
        try
        {
            var printer = new ReceiptPrinter(id);
            if (pdf) printer.SavePdf(FindForm());
            else printer.Preview(FindForm());
        }
        catch (Exception ex) { Ui.Error(ex); }
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            _grid.DataSource = Database.Query("""
                SELECT i.Id,i.InvoiceNumber AS [Invoice #],o.OrderNumber AS [Order #],
                  i.InvoiceDate AS [Paid at],o.TableLabel AS [Table],
                  i.Subtotal,i.Discount,i.TaxAmount AS Tax,i.FinalTotal AS Total,
                  i.PaymentMethod AS [Method],i.AmountReceived AS Received,i.ChangeDue AS [Change]
                FROM Invoices i JOIN Orders o ON i.OrderId=o.Id
                WHERE i.InvoiceNumber LIKE @s OR o.OrderNumber LIKE @s
                ORDER BY i.Id DESC
                """, ("@s", "%" + _search.Text.Trim() + "%"));
            Ui.HideColumns(_grid, "Id");
            Ui.CurrencyColumn(_grid, "Subtotal", "Discount", "Tax", "Total", "Received", "Change");
        });
    }
}
