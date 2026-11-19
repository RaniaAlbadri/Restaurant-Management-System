using System.Data;
using Microsoft.Data.Sqlite;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Core;

public static class InventoryService
{
    private static void ManagerOnly()
    {
        if (!Session.RequireUser().IsManager) throw new UnauthorizedAccessException("Inventory access is for managers.");
    }

    public static void SaveIngredient(long id, string name, string unit, decimal lowStock)
    {
        ManagerOnly();
        name = name.Trim();
        if (name.Length is < 2 or > 100) throw new InvalidOperationException("Ingredient name must be 2-100 characters.");
        if (unit is not ("g" or "kg" or "ml" or "L" or "pcs")) throw new InvalidOperationException("Invalid stock unit.");
        if (lowStock < 0) throw new InvalidOperationException("Low stock threshold cannot be negative.");
        if (id == 0)
            Database.Execute("INSERT INTO Ingredients(Name,Unit,LowStock) VALUES(@n,@u,@l)",
                ("@n", name), ("@u", unit), ("@l", lowStock));
        else
        {
            // Units cannot change if a recipe or stock movement already references the ingredient.
            var existing = Database.Query("SELECT Unit FROM Ingredients WHERE Id=@id", ("@id", id));
            if (existing.Rows.Count == 0) throw new InvalidOperationException("Ingredient not found.");
            var oldUnit = Convert.ToString(existing.Rows[0][0]);
            if (oldUnit != unit && Database.Count("SELECT COUNT(*) FROM Recipes WHERE IngredientId=@id", ("@id", id)) > 0)
                throw new InvalidOperationException("Remove this ingredient from recipes before changing its unit.");
            if (oldUnit != unit && Database.Count("SELECT COUNT(*) FROM InventoryMovements WHERE IngredientId=@id", ("@id", id)) > 0)
                throw new InvalidOperationException("An ingredient with movement history cannot change units.");
            if (oldUnit != unit && Database.Count("SELECT COUNT(*) FROM OrderIngredientRequirements WHERE IngredientId=@id", ("@id", id)) > 0)
                throw new InvalidOperationException("An ingredient used in existing orders cannot change units.");
            Database.Execute("UPDATE Ingredients SET Name=@n,Unit=@u,LowStock=@l WHERE Id=@id",
                ("@n", name), ("@u", unit), ("@l", lowStock), ("@id", id));
        }
    }

    public static void Adjust(long id, decimal change, string reason)
    {
        ManagerOnly();
        reason = reason.Trim();
        if (change == 0 || Math.Abs(change) > 1_000_000_000m)
            throw new InvalidOperationException("Enter a non-zero stock adjustment.");
        if (reason.Length is < 3 or > 250) throw new InvalidOperationException("Enter a reason (3-250 characters).");
        using var c = Database.Open();
        using var tx = c.BeginTransaction();
        string name, unit;
        decimal quantity;
        using (var read = Database.Command(c, "SELECT Name,Unit,Quantity FROM Ingredients WHERE Id=@id", tx, ("@id", id)))
        using (var r = read.ExecuteReader())
        {
            if (!r.Read()) throw new InvalidOperationException("Ingredient not found.");
            name = r.GetString(0); unit = r.GetString(1); quantity = Database.Money(r[2]);
        }
        if (quantity + change < 0) throw new InvalidOperationException("Stock cannot become negative.");
        using (var update = Database.Command(c, "UPDATE Ingredients SET Quantity=Quantity+@change WHERE Id=@id",
            tx, ("@change", change), ("@id", id))) update.ExecuteNonQuery();
        using (var history = Database.Command(c,
            "INSERT INTO InventoryMovements(IngredientId,IngredientName,Unit,QuantityChange,Reason,CreatedAt,UserId) " +
            "VALUES(@id,@n,@unit,@change,@reason,@date,@user)", tx,
            ("@id", id), ("@n", name), ("@unit", unit), ("@change", change),
            ("@reason", reason), ("@date", Database.Now), ("@user", Session.RequireUser().Id)))
            history.ExecuteNonQuery();
        tx.Commit();
    }

    public static void SaveRecipe(long mealId, long ingredientId, decimal quantity)
    {
        ManagerOnly();
        if (quantity <= 0) throw new InvalidOperationException("Recipe quantity must be greater than zero.");
        Database.Execute("""
            INSERT INTO Recipes(MealId,IngredientId,Quantity) VALUES(@m,@i,@q)
            ON CONFLICT(MealId,IngredientId) DO UPDATE SET Quantity=excluded.Quantity
            """, ("@m", mealId), ("@i", ingredientId), ("@q", quantity));
    }

    public static void RemoveRecipe(long mealId, long ingredientId)
    {
        ManagerOnly();
        Database.Execute("DELETE FROM Recipes WHERE MealId=@m AND IngredientId=@i",
            ("@m", mealId), ("@i", ingredientId));
    }

    // Called INSIDE the payment transaction; no stock is deducted for cancelled/unpaid orders.
    public static void ConsumeForOrder(SqliteConnection c, SqliteTransaction tx, long orderId)
    {
        var requirements = new List<(long Id, string Name, string Unit, decimal Needed)>();
        using (var read = Database.Command(c, """
            SELECT r.IngredientId,r.IngredientName,r.Unit,r.Quantity AS Needed
            FROM OrderIngredientRequirements r WHERE r.OrderId=@id
            """, tx, ("@id", orderId)))
        using (var r = read.ExecuteReader())
            while (r.Read())
                requirements.Add((r.GetInt64(0), r.GetString(1), r.GetString(2), Database.Money(r[3])));
        foreach (var part in requirements)
        {
            using var update = Database.Command(c,
                "UPDATE Ingredients SET Quantity=Quantity-@q WHERE Id=@id AND Quantity>=@q",
                tx, ("@q", part.Needed), ("@id", part.Id));
            if (update.ExecuteNonQuery() != 1)
                throw new InvalidOperationException($"Insufficient stock of {part.Name}. Add stock before checkout.");
            using var move = Database.Command(c,
                "INSERT INTO InventoryMovements(IngredientId,IngredientName,Unit,QuantityChange,Reason,OrderId,CreatedAt,UserId) " +
                "VALUES(@i,@n,@unit,@q,'Order sale',@oid,@date,@user)", tx,
                ("@i", part.Id), ("@n", part.Name), ("@unit", part.Unit),
                ("@q", -part.Needed), ("@oid", orderId), ("@date", Database.Now),
                ("@user", Session.RequireUser().Id));
            move.ExecuteNonQuery();
        }
    }
}
