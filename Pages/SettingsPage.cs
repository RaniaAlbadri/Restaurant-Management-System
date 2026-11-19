using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages;

public sealed class SettingsPage : PageBase
{
    private readonly TextBox _name = Ui.Text("Restaurant name");
    private readonly TextBox _phone = Ui.Text("Contact phone");
    private readonly TextBox _address = Ui.Text("Restaurant address");
    private readonly ComboBox _currency = Ui.Combo(["IQD", "USD", "EUR"]);
    private readonly NumericUpDown _tax = Ui.Number(0, 100, 2);

    public SettingsPage() : base("Restaurant settings", "Branding and payment defaults on printed receipts")
    {
        if (!Session.RequireUser().IsAdmin) throw new UnauthorizedAccessException();
        var card = Ui.Card(24); card.Dock = DockStyle.Left; card.Width = 550;
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 6, Height = 462,
            Padding = new Padding(4)
        };
        for (int i = 0; i < 5; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, 77));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        AddRow(form, "RESTAURANT NAME", _name, 0);
        AddRow(form, "PHONE", _phone, 1);
        AddRow(form, "ADDRESS", _address, 2);
        AddRow(form, "CURRENCY", _currency, 3);
        AddRow(form, "DEFAULT TAX PERCENTAGE", _tax, 4);
        var save = Ui.Button("Save settings", Theme.Orange, Theme.Navy, 170, 44);
        save.Dock = DockStyle.Top;
        save.Click += (_, _) => Ui.WithWaitCursor(this, () =>
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
                throw new InvalidOperationException("Restaurant name is required.");
            Database.SetSetting("RestaurantName", _name.Text.Trim());
            Database.SetSetting("Phone", _phone.Text.Trim());
            Database.SetSetting("Address", _address.Text.Trim());
            Database.SetSetting("Currency", _currency.Text);
            Database.SetSetting("TaxPercent", _tax.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Ui.Alert("Restaurant settings saved.");
        });
        form.Controls.Add(save, 0, 5);
        card.Controls.Add(form);
        Body.Controls.Add(card);
    }

    private static void AddRow(TableLayoutPanel form, string text, Control input, int row)
    {
        var container = new Panel { Dock = DockStyle.Fill };
        var heading = Ui.Label(text, 8.5f, true, Theme.Muted);
        heading.Dock = DockStyle.Top; heading.Height = 25;
        input.Dock = DockStyle.Top;
        container.Controls.Add(input); container.Controls.Add(heading);
        form.Controls.Add(container, 0, row);
    }

    public override void Reload()
    {
        _name.Text = Database.Setting("RestaurantName");
        _phone.Text = Database.Setting("Phone");
        _address.Text = Database.Setting("Address");
        _currency.SelectedItem = Database.Setting("Currency", "IQD");
        if (decimal.TryParse(Database.Setting("TaxPercent"),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var tax))
            _tax.Value = Math.Clamp(tax, _tax.Minimum, _tax.Maximum);
    }
}
