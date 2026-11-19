using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class IngredientEditDialog : EditDialog
{
    public IngredientEditDialog(long id) : base(id == 0 ? "New ingredient" : "Edit ingredient", 520, 440)
    {
        var original = id == 0 ? null : Database.Query("SELECT * FROM Ingredients WHERE Id=@id", ("@id", id))
            .Rows.Cast<System.Data.DataRow>().FirstOrDefault();
        var name = Field("Ingredient name", Ui.Text("Example: Flour"));
        var unit = Field("Unit", Ui.Combo(["g", "kg", "ml", "L", "pcs"]));
        var minimum = Field("Low-stock alert quantity", Ui.Number(0, 1000000000, 2));
        if (original is not null)
        {
            name.Text = Convert.ToString(original["Name"]);
            unit.SelectedItem = Convert.ToString(original["Unit"]);
            minimum.Value = Math.Clamp(Database.Money(original["LowStock"]), minimum.Minimum, minimum.Maximum);
        }
        SaveWith(() => InventoryService.SaveIngredient(id, name.Text, unit.Text, minimum.Value));
    }
}

public sealed class StockAdjustDialog : EditDialog
{
    public StockAdjustDialog(long id) : base("Receive / adjust stock", 540, 435)
    {
        var ingredient = Database.Query("SELECT Name,Unit,Quantity FROM Ingredients WHERE Id=@id", ("@id", id));
        if (ingredient.Rows.Count == 0) throw new InvalidOperationException("Ingredient not found.");
        var row = ingredient.Rows[0];
        Field("Ingredient", Ui.Label($"{row["Name"]} · {row["Quantity"]} {row["Unit"]} in stock", 12, true));
        var amount = Field("Change (+ delivery, - correction)", Ui.Number(-100000000, 100000000, 2));
        var reason = Field("Reason / supplier / note", Ui.Text("Stock received or adjustment reason"));
        SaveWith(() => InventoryService.Adjust(id, amount.Value, reason.Text));
    }
}

public sealed class RecipeEditDialog : EditDialog
{
    public RecipeEditDialog(long mealId) : base("Add ingredient to meal recipe", 540, 390)
    {
        var parts = Database.Query("SELECT Id, Name || ' (' || Unit || ')' AS Name FROM Ingredients ORDER BY Name");
        if (parts.Rows.Count == 0) throw new InvalidOperationException("Add ingredients to inventory first.");
        var ingredient = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Body };
        ingredient.DisplayMember = "Name"; ingredient.ValueMember = "Id"; ingredient.DataSource = parts;
        Field("Ingredient", ingredient);
        var qty = Field("Quantity needed per ONE meal", Ui.Number(0.01m, 100000000, 2));
        SaveWith(() => InventoryService.SaveRecipe(mealId, Convert.ToInt64(ingredient.SelectedValue), qty.Value));
    }
}
