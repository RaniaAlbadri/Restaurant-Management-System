using System.Data;
using Microsoft.Data.Sqlite;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Core;

public sealed class CartItem
{
    public long MealId { get; init; }
    public string Name { get; init; } = "";
    public decimal Price { get; init; }
    public int Quantity { get; set; } = 1;
    public decimal Total => Price * Quantity;
}

public static class OrderService
{
    public static long Create(long? tableId, long? customerId, string notes,
        IReadOnlyCollection<CartItem> items, string orderType = "Takeaway",
        string deliveryPhone = "", string deliveryAddress = "")
    {
        if (items.Count == 0) throw new InvalidOperationException("Add at least one item to the order.");
        var user = Session.RequireUser();
        if (!user.CanManageOrders) throw new UnauthorizedAccessException("No access to orders.");
        if (orderType is not ("DineIn" or "Takeaway" or "Delivery"))
            throw new InvalidOperationException("Choose a valid order type.");
        deliveryPhone = deliveryPhone.Trim(); deliveryAddress = deliveryAddress.Trim();
        if (orderType == "DineIn" && tableId is null)
            throw new InvalidOperationException("Select a dining table.");
        if (orderType != "DineIn" && tableId is not null)
            throw new InvalidOperationException("Only dine-in orders may use a table.");
        if (orderType == "Delivery" && (deliveryPhone.Length < 6 || deliveryAddress.Length < 5))
            throw new InvalidOperationException("Delivery requires a valid phone and address.");
        if (deliveryPhone.Length > 50 || deliveryAddress.Length > 400)
            throw new InvalidOperationException("Delivery details are too long.");
        using var c = Database.Open();
        using var tx = c.BeginTransaction();
        var label = orderType == "Delivery" ? "Delivery" : "Takeaway";
        if (tableId is not null)
        {
            using var check = Database.Command(c,
                "SELECT TableNumber FROM DiningTables WHERE Id=@id AND Status='Available'",
                tx, ("@id", tableId));
            label = Convert.ToString(check.ExecuteScalar()) ??
                throw new InvalidOperationException("The selected table is no longer available.");
            using var occupy = Database.Command(c,
                "UPDATE DiningTables SET Status='Occupied' WHERE Id=@id AND Status='Available'",
                tx, ("@id", tableId));
            if (occupy.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The table is already occupied or reserved.");
        }
        decimal subtotal = 0;
        foreach (var item in items)
        {
            if (item.Quantity < 1) throw new InvalidOperationException("Invalid quantity.");
            using var check = Database.Command(c,
                "SELECT Name,Price FROM Meals WHERE Id=@id AND IsAvailable=1",
                tx, ("@id", item.MealId));
            using var r = check.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException($"{item.Name} is no longer available.");
            var currentPrice = Database.Money(r["Price"]);
            if (currentPrice != item.Price)
                throw new InvalidOperationException($"Price changed for {item.Name}. Please reload the menu.");
            subtotal += item.Total;
        }
        var number = "ORD-" + DateTime.Now.ToString("yyMMdd") + "-" +
            Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using (var create = Database.Command(c,
            "INSERT INTO Orders(OrderNumber,TableId,TableLabel,CustomerId,UserId,OrderDate,Status,Subtotal,Notes," +
            "OrderType,DeliveryPhone,DeliveryAddress) " +
            "VALUES(@n,@tid,@tl,@cid,@uid,@date,'New',@sum,@notes,@type,@phone,@address)", tx,
            ("@n", number), ("@tid", tableId), ("@tl", label),
            ("@cid", customerId), ("@uid", user.Id),
            ("@date", Database.Now), ("@sum", subtotal), ("@notes", notes),
            ("@type", orderType), ("@phone", deliveryPhone), ("@address", deliveryAddress)))
            create.ExecuteNonQuery();
        long id;
        using (var last = Database.Command(c, "SELECT last_insert_rowid()", tx))
            id = Convert.ToInt64(last.ExecuteScalar());
        foreach (var item in items)
        {
            using var add = Database.Command(c,
                "INSERT INTO OrderItems(OrderId,MealId,MealName,Quantity,UnitPrice,LineTotal) " +
                "VALUES(@oid,@mid,@name,@qty,@price,@total)", tx,
                ("@oid", id), ("@mid", item.MealId), ("@name", item.Name),
                ("@qty", item.Quantity), ("@price", item.Price), ("@total", item.Total));
            add.ExecuteNonQuery();
        }
        // Snapshot ingredient requirements now. Recipe edits/deleted meals must not alter the
        // stock that the original order will use when it is eventually paid.
        using (var snapshot = Database.Command(c, """
            INSERT INTO OrderIngredientRequirements(OrderId,IngredientId,IngredientName,Unit,Quantity)
            SELECT @oid,i.Id,i.Name,i.Unit,SUM(oi.Quantity*r.Quantity)
            FROM OrderItems oi JOIN Recipes r ON r.MealId=oi.MealId
            JOIN Ingredients i ON i.Id=r.IngredientId
            WHERE oi.OrderId=@oid GROUP BY i.Id,i.Name,i.Unit
            """, tx, ("@oid", id))) snapshot.ExecuteNonQuery();
        tx.Commit();
        return id;
    }

    public static void ChangeStatus(long id, string newStatus)
    {
        if (!Session.RequireUser().CanManageOrders)
            throw new UnauthorizedAccessException("No access to orders.");
        if (newStatus is not ("Preparing" or "Ready" or "Cancelled"))
            throw new InvalidOperationException("Unsupported order status.");
        using var c = Database.Open();
        using var tx = c.BeginTransaction();
        string oldStatus;
        long? tableId;
        using (var check = Database.Command(c,
            "SELECT Status,TableId FROM Orders WHERE Id=@id", tx, ("@id", id)))
        using (var r = check.ExecuteReader())
        {
            if (!r.Read()) throw new InvalidOperationException("Order not found.");
            oldStatus = r.GetString(0);
            tableId = r.IsDBNull(1) ? null : r.GetInt64(1);
        }
        var allowed = oldStatus switch
        {
            "New" => newStatus is "Preparing" or "Ready" or "Cancelled",
            "Preparing" => newStatus is "Ready" or "Cancelled",
            "Ready" => newStatus is "Cancelled",
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("This status change is not allowed.");
        using (var update = Database.Command(c,
            "UPDATE Orders SET Status=@status WHERE Id=@id", tx,
            ("@status", newStatus), ("@id", id))) update.ExecuteNonQuery();
        if (newStatus == "Cancelled") ReleaseTable(c, tx, tableId, id);
        tx.Commit();
    }

    public static void ReleaseTable(SqliteConnection c, SqliteTransaction tx, long? tableId, long orderId)
    {
        if (tableId is null) return;
        using var update = Database.Command(c,
            "UPDATE DiningTables SET Status='Available' WHERE Id=@tid AND Status='Occupied' " +
            "AND NOT EXISTS(SELECT 1 FROM Orders WHERE TableId=@tid AND Id<>@oid " +
            "AND Status NOT IN ('Cancelled','Completed'))", tx,
            ("@tid", tableId), ("@oid", orderId));
        update.ExecuteNonQuery();
    }

    public static long Pay(long orderId, decimal discount, decimal taxPercent,
        string paymentMethod, decimal received)
    {
        if (!Session.RequireUser().CanSell)
            throw new UnauthorizedAccessException("Only Cashier, Manager or Admin can take payments.");
        if (discount < 0 || taxPercent < 0 || taxPercent > 100)
            throw new InvalidOperationException("Invalid discount or tax rate.");
        if (paymentMethod is not ("Cash" or "Card"))
            throw new InvalidOperationException("Choose a valid payment method.");
        using var c = Database.Open();
        using var tx = c.BeginTransaction();
        decimal subtotal;
        string status;
        long? tableId;
        using (var read = Database.Command(c,
            "SELECT Subtotal,Status,TableId FROM Orders WHERE Id=@id", tx,
            ("@id", orderId)))
        using (var r = read.ExecuteReader())
        {
            if (!r.Read()) throw new InvalidOperationException("Order not found.");
            subtotal = Database.Money(r[0]);
            status = r.GetString(1);
            tableId = r.IsDBNull(2) ? null : r.GetInt64(2);
        }
        if (status is "Cancelled" or "Completed")
            throw new InvalidOperationException("This order cannot be paid again.");
        if (discount > subtotal) throw new InvalidOperationException("Discount cannot exceed subtotal.");
        var tax = Math.Round((subtotal - discount) * taxPercent / 100m, 0,
            MidpointRounding.AwayFromZero);
        var total = subtotal - discount + tax;
        if (paymentMethod == "Card") received = total;
        if (received < total) throw new InvalidOperationException("Amount received is less than the total.");
        // Ingredient usage and invoice insertion commit together; insufficient stock rolls back payment.
        InventoryService.ConsumeForOrder(c, tx, orderId);
        long? shiftId;
        using (var shift = Database.Command(c,
            "SELECT Id FROM CashierShifts WHERE UserId=@uid AND ClosedAt IS NULL", tx,
            ("@uid", Session.RequireUser().Id)))
        {
            var value = shift.ExecuteScalar();
            shiftId = value is null or DBNull ? null : Convert.ToInt64(value);
        }
        var invoiceNumber = "INV-" + DateTime.Now.ToString("yyMMdd") + "-" +
            Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using (var add = Database.Command(c,
            "INSERT INTO Invoices(InvoiceNumber,OrderId,InvoiceDate,Subtotal,Discount,TaxPercent,TaxAmount," +
            "FinalTotal,AmountReceived,ChangeDue,PaymentMethod,UserId,ShiftId) " +
            "VALUES(@num,@oid,@date,@sub,@disc,@pct,@tax,@total,@rec,@change,@method,@user,@shift)", tx,
            ("@num", invoiceNumber), ("@oid", orderId), ("@date", Database.Now),
            ("@sub", subtotal), ("@disc", discount), ("@pct", taxPercent),
            ("@tax", tax), ("@total", total), ("@rec", received),
            ("@change", received - total), ("@method", paymentMethod),
            ("@user", Session.RequireUser().Id), ("@shift", shiftId))) add.ExecuteNonQuery();
        long invoiceId;
        using (var last = Database.Command(c, "SELECT last_insert_rowid()", tx))
            invoiceId = Convert.ToInt64(last.ExecuteScalar());
        using (var update = Database.Command(c,
            "UPDATE Orders SET Status='Completed' WHERE Id=@id", tx,
            ("@id", orderId))) update.ExecuteNonQuery();
        ReleaseTable(c, tx, tableId, orderId);
        tx.Commit();
        return invoiceId;
    }

    public static DataTable Invoice(long id) => Database.Query("""
        SELECT i.*,o.OrderNumber,o.OrderType,o.TableLabel,o.OrderDate,o.Notes,
          o.DeliveryPhone,o.DeliveryAddress,
          IFNULL(c.FullName,'Walk-in') CustomerName,
          IFNULL(u.FullName,'Unknown') CashierName
        FROM Invoices i JOIN Orders o ON i.OrderId=o.Id
        LEFT JOIN Customers c ON o.CustomerId=c.Id
        LEFT JOIN Users u ON i.UserId=u.Id WHERE i.Id=@id
        """, ("@id", id));

    public static DataTable Items(long orderId) => Database.Query(
        "SELECT MealName AS Meal,Quantity,UnitPrice,LineTotal FROM OrderItems WHERE OrderId=@id ORDER BY Id",
        ("@id", orderId));
}
