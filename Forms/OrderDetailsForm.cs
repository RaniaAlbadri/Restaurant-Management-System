using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class OrderDetailsForm : Form
{
    public OrderDetailsForm(long id)
    {
        var results = Database.Query("""
            SELECT o.*,IFNULL(c.FullName,'Walk-in') CustomerName,
              IFNULL(u.FullName,'Former employee') StaffName
            FROM Orders o LEFT JOIN Customers c ON o.CustomerId=c.Id
            LEFT JOIN Users u ON o.UserId=u.Id WHERE o.Id=@id
            """, ("@id", id));
        if (results.Rows.Count == 0) throw new InvalidOperationException("Order not found.");
        var r = results.Rows[0];
        Text = "Order details — " + Convert.ToString(r["OrderNumber"]);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(850, 630);
        MinimumSize = new Size(680, 520);
        BackColor = Theme.Canvas;
        var heading = new Panel { Dock = DockStyle.Top, Height = 160, BackColor = Theme.Navy,
            Padding = new Padding(25, 10, 25, 7) };
        heading.Controls.Add(Ui.Label(
            $"{r["OrderNumber"]}     •     {r["Status"]}\n" +
            $"{r["OrderType"]} · {r["TableLabel"]}  |  {r["CustomerName"]}  |  {r["StaffName"]}\n" +
            $"{r["OrderDate"]}  |  Phone: {r["DeliveryPhone"]}  |  Address: {r["DeliveryAddress"]}  |  Notes: {r["Notes"]}",
            12.5f, true, Theme.Cream));
        var grid = Ui.Grid();
        grid.DataSource = OrderService.Items(id);
        Ui.CurrencyColumn(grid, "UnitPrice", "LineTotal");
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 79,
            BackColor = Theme.White, Padding = new Padding(22) };
        footer.Controls.Add(Ui.Label("SUBTOTAL: " + Database.FormatMoney(r["Subtotal"]), 17, true));
        Controls.Add(grid); Controls.Add(footer); Controls.Add(heading);
    }
}
