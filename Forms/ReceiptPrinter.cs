using System.Data;
using System.Drawing.Printing;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class ReceiptPrinter
{
    private readonly DataRow _invoice;
    private readonly DataTable _items;
    private int _nextItem;

    public ReceiptPrinter(long invoiceId)
    {
        var data = OrderService.Invoice(invoiceId);
        if (data.Rows.Count == 0) throw new InvalidOperationException("Invoice not found.");
        _invoice = data.Rows[0];
        _items = OrderService.Items(Convert.ToInt64(_invoice["OrderId"]));
    }

    private PrintDocument CreateDocument()
    {
        var document = new PrintDocument
        {
            DocumentName = "Invoice_" + Convert.ToString(_invoice["InvoiceNumber"]),
            OriginAtMargins = true,
            DefaultPageSettings = { Margins = new Margins(50, 50, 55, 55) }
        };
        document.BeginPrint += (_, _) => _nextItem = 0;
        document.PrintPage += DrawPage;
        return document;
    }

    private void DrawPage(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics;
        if (g is null) return;
        float x = e.MarginBounds.Left, y = e.MarginBounds.Top,
            w = e.MarginBounds.Width;
        using var title = new Font("Segoe UI Semibold", 24);
        using var regular = new Font("Segoe UI", 10);
        using var bold = new Font("Segoe UI Semibold", 10.5f);
        using var small = new Font("Segoe UI", 9);
        using var ink = new SolidBrush(Theme.Navy);
        using var muted = new SolidBrush(Theme.Muted);
        using var accent = new SolidBrush(Theme.Orange);
        using var pen = new Pen(Theme.Line);
        g.FillRectangle(ink, x, y, w, 69);
        g.DrawString(Database.Setting("RestaurantName", "RESTAURANT"), title,
            Brushes.White, x + 14, y + 13);
        y += 88;
        g.DrawString("RESTAURANT INVOICE", bold, ink, x, y);
        g.DrawString("# " + Convert.ToString(_invoice["InvoiceNumber"]), bold,
            ink, x + w * .49f, y);
        y += 27;
        g.DrawString($"Date: {_invoice["InvoiceDate"]}", regular, ink, x, y);
        g.DrawString($"Order: {_invoice["OrderNumber"]}", regular, ink, x + w * .49f, y);
        y += 24;
        g.DrawString($"Type: {_invoice["OrderType"]} / {_invoice["TableLabel"]}", regular, ink, x, y);
        g.DrawString($"Guest: {_invoice["CustomerName"]}", regular, ink, x + w * .49f, y);
        y += 24;
        g.DrawString($"Phone: {Database.Setting("Phone")}", small, muted, x, y);
        y += 18;
        g.DrawString($"Address: {Database.Setting("Address")}", small, muted, x, y);
        y += 20;
        if (Convert.ToString(_invoice["OrderType"]) == "Delivery")
        {
            g.DrawString($"Delivery: {_invoice["DeliveryPhone"]} · {_invoice["DeliveryAddress"]}",
                small, muted, new RectangleF(x, y, w, 35));
            y += 35;
        }
        else y += 17;
        g.FillRectangle(accent, x, y, w, 31);
        g.DrawString("ITEM", bold, ink, x + 10, y + 5);
        g.DrawString("QTY", bold, ink, x + w * .55f, y + 5);
        g.DrawString("TOTAL", bold, ink, x + w * .76f, y + 5);
        y += 35;
        while (_nextItem < _items.Rows.Count && y + 170 < e.MarginBounds.Bottom)
        {
            var r = _items.Rows[_nextItem];
            var name = Convert.ToString(r["Meal"]) ?? "";
            // Truncate only in the printed row, never in stored order data.
            if (name.Length > 37) name = name[..34] + "...";
            g.DrawString(name, regular, ink, x + 10, y);
            g.DrawString(Convert.ToString(r["Quantity"]), regular, ink, x + w * .55f, y);
            g.DrawString(Database.Money(r["LineTotal"]).ToString("N0"),
                regular, ink, x + w * .76f, y);
            y += 29;
            g.DrawLine(pen, x, y, x + w, y);
            y += 7;
            _nextItem++;
        }
        if (_nextItem < _items.Rows.Count)
        {
            e.HasMorePages = true;
            return;
        }
        y += 16;
        var left = x + w * .56f;
        void printTotal(string label, object value, bool emphasized = false)
        {
            g.DrawString(label, emphasized ? bold : regular, ink, left, y);
            g.DrawString(Database.Money(value).ToString("N0"), emphasized ? bold : regular,
                ink, x + w * .79f, y);
            y += 27;
        }
        printTotal("Subtotal", _invoice["Subtotal"]);
        printTotal("Discount", _invoice["Discount"]);
        g.DrawString($"Tax ({_invoice["TaxPercent"]}%)", regular, ink, left, y);
        g.DrawString(Database.Money(_invoice["TaxAmount"]).ToString("N0"),
            regular, ink, x + w * .79f, y);
        y += 31;
        printTotal("TOTAL", _invoice["FinalTotal"], true);
        printTotal("Received", _invoice["AmountReceived"]);
        printTotal("Change", _invoice["ChangeDue"]);
        y += 18;
        g.DrawString($"Payment: {_invoice["PaymentMethod"]}     Currency: {Database.Setting("Currency", "IQD")}",
            regular, ink, x, y);
        y += 42;
        g.DrawString("Thank you for dining with us!", bold, ink, x, y);
        e.HasMorePages = false;
    }

    public void Preview(IWin32Window? owner = null)
    {
        using var doc = CreateDocument();
        using var preview = new PrintPreviewDialog
        {
            Document = doc, Width = 1000, Height = 750,
            StartPosition = FormStartPosition.CenterParent,
            Text = "Invoice print preview"
        };
        preview.ShowDialog(owner);
    }

    public void SavePdf(IWin32Window? owner = null)
    {
        using var doc = CreateDocument();
        doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
        if (!doc.PrinterSettings.IsValid)
            throw new InvalidOperationException(
                "The 'Microsoft Print to PDF' printer is not available. Enable it in Windows Features, " +
                "or choose Microsoft Print to PDF from Print Preview.");
        using var save = new SaveFileDialog
        {
            Filter = "PDF documents (*.pdf)|*.pdf",
            FileName = Convert.ToString(_invoice["InvoiceNumber"]) + ".pdf",
            OverwritePrompt = true, Title = "Save invoice as PDF"
        };
        if (save.ShowDialog(owner) != DialogResult.OK) return;
        doc.PrinterSettings.PrintToFile = true;
        doc.PrinterSettings.PrintFileName = save.FileName;
        doc.PrintController = new StandardPrintController();
        doc.Print();
        // The Windows PDF driver may prompt for a file name on some installations.
        if (File.Exists(save.FileName)) Ui.Alert("PDF saved:\n" + save.FileName);
        else Ui.Alert("The Windows PDF printer was started. If prompted, choose a file name to finish saving.");
    }
}
