using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class MealsPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text("Search meals or category...");
    private readonly ComboBox _filter = Ui.Combo(["All categories", ..MealDialog.Categories]);

    public MealsPage() : base("Menu & meals", "Manage your food and beverage catalog")
    {
        var bar = Ui.Toolbar();
        bar.WrapContents = true;
        bar.Height = 110;
        _search.Width = 235; _search.Dock = DockStyle.None;
        _filter.Width = 162; _filter.Dock = DockStyle.None;
        bar.Controls.Add(_search); bar.Controls.Add(_filter);
        if (Session.RequireUser().IsManager)
        {
            var add = Ui.Button("+ Add meal", Theme.Orange, Theme.Navy, 112);
            var edit = Ui.Button("Edit", Theme.Gold, Theme.Navy, 84);
            var availability = Ui.Button("Toggle availability", Theme.Cream, Theme.Navy, 162);
            var delete = Ui.Button("Delete", Theme.Red, Color.White, 92);
            add.Click += (_, _) => EditMeal(0);
            edit.Click += (_, _) => EditMeal(Ui.SelectedId(_grid));
            availability.Click += (_, _) => Ui.WithWaitCursor(this, () =>
            {
                var id = Ui.SelectedId(_grid); if (id == 0) return;
                Database.Execute("UPDATE Meals SET IsAvailable=1-IsAvailable WHERE Id=@id", ("@id", id));
                Reload();
            });
            delete.Click += (_, _) => Ui.WithWaitCursor(this, () =>
            {
                var id = Ui.SelectedId(_grid);
                if (id != 0 && Ui.Confirm("Delete this meal from the menu? Old invoices retain the meal name and price."))
                { Database.Execute("DELETE FROM Meals WHERE Id=@id", ("@id", id)); Reload(); }
            });
            bar.Controls.Add(add); bar.Controls.Add(edit); bar.Controls.Add(availability); bar.Controls.Add(delete);
        }
        _grid.CellDoubleClick += (_, _) => { if (Session.RequireUser().IsManager) EditMeal(Ui.SelectedId(_grid)); };
        _search.TextChanged += (_, _) => Reload();
        _filter.SelectedIndexChanged += (_, _) => Reload();
        Body.Controls.Add(_grid);
        Body.Controls.Add(bar);
    }

    private void EditMeal(long id)
    {
        using var dlg = new MealDialog(id);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            var term = "%" + _search.Text.Trim() + "%";
            var category = _filter.SelectedIndex <= 0 ? "%" : _filter.Text;
            _grid.DataSource = Database.Query("""
                SELECT Id,Name AS Meal,Category,Price,
                  CASE WHEN IsAvailable=1 THEN 'Available' ELSE 'Unavailable' END AS Availability,
                  Description FROM Meals
                WHERE (Name LIKE @term OR Category LIKE @term) AND Category LIKE @cat
                ORDER BY Category,Name
                """, ("@term", term), ("@cat", category));
            Ui.HideColumns(_grid, "Id");
            Ui.CurrencyColumn(_grid, "Price");
        });
    }
}
