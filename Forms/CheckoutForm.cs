using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class CheckoutForm : EditDialog
{
    public long InvoiceId { get; private set; }

    public CheckoutForm(long orderId) : base("Checkout and payment", 590, 795)
    {
        if (!Session.RequireUser().CanSell) throw new UnauthorizedAccessException();
        var order = Database.Query(
            "SELECT OrderNumber,Subtotal,Status FROM Orders WHERE Id=@id", ("@id", orderId));
        if (order.Rows.Count == 0) throw new InvalidOperationException("Order not found.");
        if (Convert.ToString(order.Rows[0]["Status"]) is "Completed" or "Cancelled")
            throw new InvalidOperationException("This order is already completed or cancelled.");
        var sub = Database.Money(order.Rows[0]["Subtotal"]);
        var openShift = ShiftService.CurrentShiftId();
        Field("Cashier shift", Ui.Label(openShift is null
            ? "No open shift - payment will be unassigned"
            : $"Active shift #{openShift}", 10, true,
            openShift is null ? Theme.Red : Theme.Green), 63);
        Field("Order", Ui.Label(Convert.ToString(order.Rows[0]["OrderNumber"])!, 12, true), 58);
        var subLabel = Ui.Label(Database.FormatMoney(sub), 14, true);
        subLabel.Height = 39;
        Field("Subtotal", subLabel, 85);
        var discount = Field("Discount (IQD)", Ui.Number(0, sub));
        var tax = Field("Tax percentage", Ui.Number(0, 100, 2));
        if (decimal.TryParse(Database.Setting("TaxPercent"),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var defaultTax))
            tax.Value = Math.Clamp(defaultTax, 0, 100);
        var method = Field("Payment method", Ui.Combo(["Cash", "Card"]));
        var received = Field("Amount received", Ui.Number(0, 1000000000));
        var dueLabel = Ui.Label("", 16, true, Theme.Red);
        dueLabel.Height = 41;
        var due = Field("Total due", dueLabel, 92);
        decimal total = sub;
        void recalculate()
        {
            decimal taxable = sub - discount.Value;
            total = taxable + Math.Round(taxable * tax.Value / 100m, 0,
                MidpointRounding.AwayFromZero);
            due.Text = Database.FormatMoney(total);
            received.Enabled = method.Text == "Cash";
            if (method.Text == "Card") received.Value = Math.Clamp(total, 0, received.Maximum);
        }
        discount.ValueChanged += (_, _) => recalculate();
        tax.ValueChanged += (_, _) => recalculate();
        method.SelectedIndexChanged += (_, _) => recalculate();
        recalculate();
        Save.Text = "Confirm payment";
        SaveWith(() =>
        {
            InvoiceId = OrderService.Pay(orderId, discount.Value, tax.Value,
                method.Text, method.Text == "Card" ? total : received.Value);
        });
    }
}
