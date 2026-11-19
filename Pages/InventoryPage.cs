using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class InventoryPage : PageBase
{
    private readonly DataGridView _ingredients = Ui.Grid();
    private readonly DataGridView _recipes = Ui.Grid();
    private readonly DataGridView _movements = Ui.Grid();
    private readonly ComboBox _meal = new() { DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 280, Font = Theme.Body };
    private readonly Label _warning = Ui.Label("", 10, true, Theme.Red);

    public InventoryPage() : base("Inventory & recipes", "Ingredients, low-stock alerts, recipes and stock movement history")
    {
        if (!Session.RequireUser().IsManager) throw new UnauthorizedAccessException();
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.Body };
        var stockPage = new TabPage("Ingredients") { BackColor = Theme.Canvas };
        var recipePage = new TabPage("Meal recipes") { BackColor = Theme.Canvas };
        var historyPage = new TabPage("Movements") { BackColor = Theme.Canvas };
        tabs.TabPages.AddRange([stockPage, recipePage, historyPage]);

        var stockBar = Ui.Toolbar(); stockBar.Height = 94; stockBar.WrapContents = true;
        var add = Ui.Button("+ Ingredient", Theme.Orange, Theme.Navy, 135);
        var edit = Ui.Button("Edit", Theme.Gold, Theme.Navy, 90);
        var adjust = Ui.Button("+ / - Stock", Theme.Cream, Theme.Navy, 130);
        var refresh = Ui.Button("Refresh", Theme.Navy, Theme.White, 95);
        add.Click += (_, _) => OpenIngredient(0);
        edit.Click += (_, _) => OpenIngredient(Ui.SelectedId(_ingredients));
        adjust.Click += (_, _) =>
        {
            var id = Ui.SelectedId(_ingredients); if (id == 0) return;
            using var dlg = new StockAdjustDialog(id);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
        };
        refresh.Click += (_, _) => Reload();
        _warning.Dock = DockStyle.Bottom; _warning.Height = 38;
        stockBar.Controls.Add(add); stockBar.Controls.Add(edit); stockBar.Controls.Add(adjust);
        stockBar.Controls.Add(refresh);
        stockPage.Controls.Add(_ingredients); stockPage.Controls.Add(_warning);
        stockPage.Controls.Add(stockBar);

        var recipeBar = Ui.Toolbar(); recipeBar.Height = 108; recipeBar.WrapContents = true;
        _meal.Margin = new Padding(2, 4, 12, 4);
        var assign = Ui.Button("Add / update ingredient", Theme.Orange, Theme.Navy, 210);
        var remove = Ui.Button("Remove from recipe", Theme.Red, Theme.White, 170);
        assign.Click += (_, _) =>
        {
            if (_meal.SelectedValue is not long mealId) return;
            if (mealId == 0) return;
            try
            {
                using var dlg = new RecipeEditDialog(mealId);
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK) ReloadRecipes();
            }
            catch (Exception ex) { Ui.Error(ex); }
        };
        remove.Click += (_, _) =>
        {
            if (_meal.SelectedValue is not long mealId) return;
            if (mealId == 0 || !_recipes.Columns.Contains("IngredientId") || _recipes.CurrentRow is null) return;
            var ingredientId = Convert.ToInt64(_recipes.CurrentRow.Cells["IngredientId"].Value);
            if (!Ui.Confirm("Remove the ingredient from this recipe?")) return;
            Ui.WithWaitCursor(this, () => { InventoryService.RemoveRecipe(mealId, ingredientId); ReloadRecipes(); });
        };
        _meal.SelectedIndexChanged += (_, _) => ReloadRecipes();
        recipeBar.Controls.Add(_meal); recipeBar.Controls.Add(assign); recipeBar.Controls.Add(remove);
        recipePage.Controls.Add(_recipes); recipePage.Controls.Add(recipeBar);
        historyPage.Controls.Add(_movements);
        Body.Controls.Add(tabs);
    }

    private void OpenIngredient(long id)
    {
        using var dlg = new IngredientEditDialog(id);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            _ingredients.DataSource = Database.Query("""
                SELECT Id,Name AS Ingredient,Unit,Quantity AS [On hand],LowStock AS [Alert at],
                  CASE WHEN Quantity<=LowStock THEN 'LOW STOCK' ELSE 'OK' END AS Status
                FROM Ingredients ORDER BY Name
                """);
            Ui.HideColumns(_ingredients, "Id");
            _warning.Text = "Low-stock ingredients: " + Database.Count(
                "SELECT COUNT(*) FROM Ingredients WHERE Quantity<=LowStock");
            long selected = _meal.SelectedValue is long v ? v : 0;
            var meals = Database.Query("SELECT Id,Name FROM Meals ORDER BY Name");
            _meal.DisplayMember = "Name"; _meal.ValueMember = "Id"; _meal.DataSource = meals;
            if (selected > 0) _meal.SelectedValue = selected;
            ReloadRecipes();
            _movements.DataSource = Database.Query("""
                SELECT m.Id,m.CreatedAt AS [Date],m.IngredientName AS Ingredient,
                  m.QuantityChange AS [Change],m.Unit,m.Reason,
                  IFNULL(o.OrderNumber,'—') AS [Order #],IFNULL(u.FullName,'—') AS Staff
                FROM InventoryMovements m
                LEFT JOIN Orders o ON m.OrderId=o.Id
                LEFT JOIN Users u ON m.UserId=u.Id
                ORDER BY m.Id DESC LIMIT 500
                """);
            Ui.HideColumns(_movements, "Id");
        });
    }

    private void ReloadRecipes()
    {
        if (_meal.SelectedValue is not long mealId) return;
        if (mealId == 0) return;
        _recipes.DataSource = Database.Query("""
            SELECT r.IngredientId, i.Name AS Ingredient,r.Quantity AS [Required per meal], i.Unit,
                   i.Quantity AS [Available stock]
            FROM Recipes r JOIN Ingredients i ON i.Id=r.IngredientId
            WHERE r.MealId=@meal ORDER BY i.Name
            """, ("@meal", mealId));
        Ui.HideColumns(_recipes, "IngredientId");
    }
}
