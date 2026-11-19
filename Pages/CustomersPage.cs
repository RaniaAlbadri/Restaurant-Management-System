using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class CustomersPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text("Search by name, phone or email");

    public CustomersPage() : base("Customers", "Keep track of your guests and contact information")
    {
        var bar = Ui.Toolbar();
        _search.Width = 255; _search.Dock = DockStyle.None;
        bar.Controls.Add(_search);
        var add = Ui.Button("+ Add customer", Theme.Orange, Theme.Navy, 145);
        var edit = Ui.Button("Edit", Theme.Gold, Theme.Navy, 80);
        var delete = Ui.Button("Delete", Theme.Red, Color.White, 90);
        add.Click += (_, _) => Edit(0);
        edit.Click += (_, _) => Edit(Ui.SelectedId(_grid));
        delete.Click += (_, _) => Ui.WithWaitCursor(this, () =>
        {
            var id = Ui.SelectedId(_grid);
            if (id == 0 || !Ui.Confirm("Delete this customer? Previous orders remain in history.")) return;
            Database.Execute("DELETE FROM Customers WHERE Id=@id", ("@id", id)); Reload();
        });
        bar.Controls.Add(add); bar.Controls.Add(edit); bar.Controls.Add(delete);
        Body.Controls.Add(_grid); Body.Controls.Add(bar);
        _search.TextChanged += (_, _) => Reload();
    }

    private void Edit(long id)
    {
        using var dlg = new CustomerDialog(id);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            _grid.DataSource = Database.Query("""
                SELECT Id,FullName AS Customer,Phone,Email,Notes FROM Customers
                WHERE FullName LIKE @search OR Phone LIKE @search OR Email LIKE @search
                ORDER BY FullName
                """, ("@search", "%" + _search.Text.Trim() + "%"));
            Ui.HideColumns(_grid, "Id");
        });
    }
}
