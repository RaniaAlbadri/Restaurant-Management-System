using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class EmployeesPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text("Search name, username or role");

    public EmployeesPage() : base("Employees & access", "Staff accounts, roles, status and salaries")
    {
        if (!Session.RequireUser().IsAdmin) throw new UnauthorizedAccessException();
        var bar = Ui.Toolbar();
        _search.Width = 260; _search.Dock = DockStyle.None;
        bar.Controls.Add(_search);
        var add = Ui.Button("+ Add employee", Theme.Orange, Theme.Navy, 145);
        var edit = Ui.Button("Edit / password", Theme.Gold, Theme.Navy, 145);
        var status = Ui.Button("Enable / disable", Theme.Cream, Theme.Navy, 145);
        add.Click += (_, _) => Edit(0);
        edit.Click += (_, _) => Edit(Ui.SelectedId(_grid));
        status.Click += (_, _) => Ui.WithWaitCursor(this, () =>
        {
            var id = Ui.SelectedId(_grid);
            if (id == 0 || id == Session.RequireUser().Id)
            { Ui.Alert("You cannot disable your own account."); return; }
            if (!Ui.Confirm("Change this employee's active status?")) return;
            Database.Execute("UPDATE Users SET IsActive=1-IsActive WHERE Id=@id", ("@id", id));
            Reload();
        });
        bar.Controls.Add(add); bar.Controls.Add(edit); bar.Controls.Add(status);
        Body.Controls.Add(_grid); Body.Controls.Add(bar);
        _search.TextChanged += (_, _) => Reload();
    }

    private void Edit(long id)
    {
        using var dlg = new EmployeeDialog(id);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    public override void Reload()
    {
        Ui.WithWaitCursor(this, () =>
        {
            _grid.DataSource = Database.Query("""
                SELECT Id,FullName AS Employee,Username,Role,Phone,Salary,
                 CASE WHEN IsActive=1 THEN 'Active' ELSE 'Disabled' END AS Status
                FROM Users WHERE FullName LIKE @s OR Username LIKE @s OR Role LIKE @s
                ORDER BY Role,FullName
                """, ("@s", "%" + _search.Text.Trim() + "%"));
            Ui.HideColumns(_grid, "Id");
            Ui.CurrencyColumn(_grid, "Salary");
        });
    }
}
