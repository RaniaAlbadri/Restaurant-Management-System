using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Core;

public static class ShiftService
{
    public static long? CurrentShiftId(long? userId = null)
    {
        var id = userId ?? Session.RequireUser().Id;
        var value = Database.Scalar("SELECT Id FROM CashierShifts WHERE UserId=@id AND ClosedAt IS NULL",
            ("@id", id));
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    public static void Open(decimal opening)
    {
        if (!Session.RequireUser().CanSell) throw new UnauthorizedAccessException();
        if (opening < 0) throw new InvalidOperationException("Opening cash cannot be negative.");
        using var c = Database.Open();
        using var tx = c.BeginTransaction();
        using var insert = Database.Command(c,
            "INSERT INTO CashierShifts(UserId,OpenedAt,OpeningCash) VALUES(@u,@date,@cash)", tx,
            ("@u", Session.RequireUser().Id), ("@date", Database.Now), ("@cash", opening));
        insert.ExecuteNonQuery();
        tx.Commit();
    }

    public static (decimal Expected, decimal CashSales, decimal CardSales, long Payments) Totals(long id)
    {
        var shift = Database.Query("SELECT OpeningCash FROM CashierShifts WHERE Id=@id", ("@id", id));
        if (shift.Rows.Count == 0) throw new InvalidOperationException("Shift not found.");
        var sums = Database.Query("""
            SELECT COALESCE(SUM(CASE WHEN PaymentMethod='Cash' THEN FinalTotal ELSE 0 END),0) AS Cash,
            COALESCE(SUM(CASE WHEN PaymentMethod='Card' THEN FinalTotal ELSE 0 END),0) AS Card,
            COUNT(*) AS Payments FROM Invoices WHERE ShiftId=@id
            """, ("@id", id));
        decimal cash = Database.Money(sums.Rows[0]["Cash"]);
        decimal card = Database.Money(sums.Rows[0]["Card"]);
        decimal opening = Database.Money(shift.Rows[0][0]);
        return (opening + cash, cash, card, Convert.ToInt64(sums.Rows[0]["Payments"]));
    }

    public static void Close(long id, decimal actual, string notes)
    {
        if (!Session.RequireUser().CanSell) throw new UnauthorizedAccessException();
        if (actual < 0) throw new InvalidOperationException("Actual cash cannot be negative.");
        using var c = Database.Open();
        using var tx = c.BeginTransaction();
        long owner;
        using (var read = Database.Command(c,
            "SELECT UserId FROM CashierShifts WHERE Id=@id AND ClosedAt IS NULL", tx, ("@id", id)))
        {
            var value = read.ExecuteScalar();
            if (value is null) throw new InvalidOperationException("Shift is already closed.");
            owner = Convert.ToInt64(value);
        }
        if (owner != Session.RequireUser().Id)
            throw new UnauthorizedAccessException("Only the cashier who opened this shift can close it.");
        decimal opening;
        using (var read = Database.Command(c, "SELECT OpeningCash FROM CashierShifts WHERE Id=@id", tx, ("@id", id)))
            opening = Database.Money(read.ExecuteScalar());
        decimal cashSales;
        using (var read = Database.Command(c,
            "SELECT COALESCE(SUM(FinalTotal),0) FROM Invoices WHERE ShiftId=@id AND PaymentMethod='Cash'", tx, ("@id", id)))
            cashSales = Database.Money(read.ExecuteScalar());
        using (var update = Database.Command(c,
            "UPDATE CashierShifts SET ClosedAt=@dt,ClosingCash=@actual,ExpectedCash=@expected,Notes=@notes " +
            "WHERE Id=@id AND ClosedAt IS NULL", tx,
            ("@dt", Database.Now), ("@actual", actual), ("@expected", opening + cashSales),
            ("@notes", notes.Trim()), ("@id", id)))
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Shift status changed.");
        tx.Commit();
    }
}
