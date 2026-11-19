using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Pages;

namespace RestaurantManagementSystem.Forms;

public sealed class MainForm : Form
{
    private readonly Panel _sidebar;
    private readonly Panel _content;
    private readonly Label _pageName;
    private readonly Dictionary<string, Button> _navButtons = [];
    private PageBase? _current;
    private string _active = "";

    public MainForm()
    {
        var user = Session.RequireUser();
        Text = "Restaurant Management System";
        MinimumSize = new Size(1080, 720);
        Size = new Size(1400, 890);
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Canvas;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _sidebar = new Panel { Dock = DockStyle.Left, Width = 237, BackColor = Theme.Navy };
        var brand = new Panel { Dock = DockStyle.Top, Height = 105, Padding = new Padding(20, 20, 8, 10) };
        var title = Ui.Label("✦  RESTAURANT", 12.5f, true, Theme.Cream);
        title.Dock = DockStyle.Top; title.Height = 38;
        var sub = Ui.Label("MANAGEMENT SYSTEM", 8, true, Theme.Orange);
        sub.Dock = DockStyle.Top; sub.Height = 24;
        brand.Controls.Add(sub); brand.Controls.Add(title);

        var navHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(12, 14, 12, 0)
        };
        navHost.SizeChanged += (_, _) =>
        {
            foreach (var button in _navButtons.Values)
                button.Width = Math.Max(160, navHost.ClientSize.Width - 31);
        };
        AddNav(navHost, "Dashboard", "Dashboard");
        AddNav(navHost, "Meals", "Menu & meals");
        AddNav(navHost, "Tables", "Dining tables");
        AddNav(navHost, "Orders", "Orders");
        AddNav(navHost, "Kitchen", "Kitchen display");
        if (user.IsManager) AddNav(navHost, "Inventory", "Inventory & recipes");
        if (user.CanSell) AddNav(navHost, "Shifts", "Cashier shifts");
        if (user.CanSell) AddNav(navHost, "Invoices", "Invoices & billing");
        if (user.IsManager) AddNav(navHost, "Customers", "Customers");
        if (user.IsManager) AddNav(navHost, "Reports", "Sales reports");
        if (user.IsAdmin) AddNav(navHost, "Employees", "Employees & access");
        if (user.IsAdmin) AddNav(navHost, "Settings", "Restaurant settings");
        AddNav(navHost, "Security", "Security & backups");

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 113, Padding = new Padding(14) };
        var logout = Ui.Button("⎋   Sign out", Theme.Red, Color.White, 188, 39);
        logout.Dock = DockStyle.Bottom;
        logout.Click += (_, _) => Close();
        var role = Ui.Label($"{user.Name}\n{user.Role}", 9.5f, false, Theme.Gold);
        role.Dock = DockStyle.Fill;
        footer.Controls.Add(role);
        footer.Controls.Add(logout);
        _sidebar.Controls.Add(navHost);
        _sidebar.Controls.Add(footer);
        _sidebar.Controls.Add(brand);

        var topbar = new Panel
        {
            Dock = DockStyle.Top, Height = 75, BackColor = Theme.White,
            Padding = new Padding(27, 15, 27, 13)
        };
        _pageName = Ui.Label("Dashboard", 12.5f, true);
        _pageName.Dock = DockStyle.Left;
        _pageName.Width = 320;
        var topRight = Ui.Label($"{DateTime.Now:dddd, MMMM d, yyyy}     •     {user.Role}",
            9.5f, false, Theme.Muted, ContentAlignment.MiddleRight);
        topRight.Dock = DockStyle.Fill;
        topbar.Controls.Add(topRight);
        topbar.Controls.Add(_pageName);

        _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
        var workArea = new Panel { Dock = DockStyle.Fill };
        workArea.Controls.Add(_content);
        workArea.Controls.Add(topbar);
        Controls.Add(workArea);
        Controls.Add(_sidebar);
        Shown += (_, _) => Navigate("Dashboard");
    }

    private void AddNav(FlowLayoutPanel host, string key, string text)
    {
        var btn = Ui.Button("   " + text, Theme.Navy, Theme.Cream, 195, 47);
        btn.TextAlign = ContentAlignment.MiddleLeft;
        btn.Font = new Font("Segoe UI Semibold", 10);
        btn.Margin = new Padding(0, 1, 0, 6);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 38, 61);
        btn.Click += (_, _) => Navigate(key);
        host.Controls.Add(btn);
        _navButtons.Add(key, btn);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        string? section = null;
        if (keyData == (Keys.Control | Keys.D1)) section = "Dashboard";
        else if (keyData == (Keys.Control | Keys.D2)) section = "Meals";
        else if (keyData == (Keys.Control | Keys.D3)) section = "Tables";
        else if (keyData == (Keys.Control | Keys.D4)) section = "Orders";
        else if (keyData == (Keys.Control | Keys.D5)) section = "Kitchen";
        else if (keyData == (Keys.Control | Keys.D6)) section = "Inventory";
        else if (keyData == (Keys.Control | Keys.D7)) section = "Shifts";
        if (section is not null && _navButtons.ContainsKey(section))
        {
            Navigate(section); return true;
        }
        if (keyData == Keys.F5) { _current?.Reload(); return true; }
        if (keyData == Keys.F2 && Session.RequireUser().CanManageOrders)
        {
            using var dialog = new OrderCreateForm();
            if (dialog.ShowDialog(this) == DialogResult.OK) _current?.Reload();
            return true;
        }
        if (keyData == (Keys.Control | Keys.P) && _current is InvoicesPage invoices)
        {
            invoices.PreviewSelected(); return true;
        }
        if (keyData == Keys.F1)
        {
            Ui.Alert("Keyboard shortcuts:\nF1: Help\nF2: New order\nF5: Refresh page\n" +
                "Ctrl+1: Dashboard\nCtrl+2: Meals\nCtrl+3: Tables\nCtrl+4: Orders\n" +
                "Ctrl+5: Kitchen\nCtrl+6: Inventory\nCtrl+7: Cashier shifts\n" +
                "Ctrl+P: Invoice print preview (from Invoices)", "Shortcuts");
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Navigate(string key)
    {
        if (_active == key) { _current?.Reload(); return; }
        if (!_navButtons.ContainsKey(key)) return;
        PageBase next = key switch
        {
            "Dashboard" => new DashboardPage(),
            "Meals" => new MealsPage(),
            "Tables" => new TablesPage(),
            "Orders" => new OrdersPage(),
            "Kitchen" => new KitchenPage(),
            "Inventory" => new InventoryPage(),
            "Shifts" => new ShiftsPage(),
            "Invoices" => new InvoicesPage(),
            "Customers" => new CustomersPage(),
            "Reports" => new ReportsPage(),
            "Employees" => new EmployeesPage(),
            "Settings" => new SettingsPage(),
            "Security" => new SecurityPage(),
            _ => throw new InvalidOperationException("Unknown navigation page.")
        };
        _content.SuspendLayout();
        _content.Controls.Clear();
        _current?.Dispose();
        _current = next;
        _current.Dock = DockStyle.Fill;
        _content.Controls.Add(_current);
        _content.ResumeLayout();
        _active = key;
        _pageName.Text = key switch { "Meals" => "Menu & meals", "Tables" => "Dining tables", _ => key };
        foreach (var (name, button) in _navButtons)
        {
            var selected = name == key;
            button.BackColor = selected ? Theme.Orange : Theme.Navy;
            button.ForeColor = selected ? Theme.Navy : Theme.Cream;
        }
        _current.Reload();
    }
}
