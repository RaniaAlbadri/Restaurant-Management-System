using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public abstract class EditDialog : Form
{
    protected readonly TableLayoutPanel Fields;
    protected readonly Button Save;

    protected EditDialog(string title, int width = 580, int height = 650)
    {
        Text = title; Size = new Size(width, height);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false; MaximizeBox = false;
        BackColor = Theme.Canvas; Font = Theme.Body;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var footer = new FlowLayoutPanel
        {
            Height = 74, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(21, 14, 21, 14), BackColor = Theme.White
        };
        Save = Ui.Button("Save changes", Theme.Orange, Theme.Navy, 143, 41);
        var cancel = Ui.Button("Cancel", Theme.Line, Theme.Navy, 100, 41);
        cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(Save); footer.Controls.Add(cancel);
        Fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 0, Margin = new Padding(0),
            Padding = new Padding(25, 16, 35, 15)
        };
        Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(Fields);
        Controls.Add(scroll);
        Controls.Add(footer);
        CancelButton = cancel;
        AcceptButton = Save;
    }

    protected T Field<T>(string caption, T control, int h = 77) where T : Control
    {
        int row = Fields.RowCount++;
        Fields.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        var panel = new Panel { Dock = DockStyle.Top, Height = h - 3 };
        var label = Ui.Label(caption.ToUpperInvariant(), 8.8f, true, Theme.Muted);
        label.Dock = DockStyle.Top;
        label.Height = 26;
        control.Dock = DockStyle.Top;
        panel.Controls.Add(control);
        panel.Controls.Add(label);
        Fields.Controls.Add(panel, 0, row);
        return control;
    }

    protected void SaveWith(Action action)
    {
        Save.Click += (_, _) =>
        {
            try { action(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { Ui.Error(ex); }
        };
    }

    protected static string Required(string value, string label)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) throw new InvalidOperationException($"{label} is required.");
        return trimmed;
    }
}

public sealed class MealDialog : EditDialog
{
    public static readonly string[] Categories = ["Main Course", "Appetizers", "Beverages", "Desserts", "Other"];
    public MealDialog(long id = 0) : base(id == 0 ? "Add menu item" : "Edit menu item", 600, 755)
    {
        DataRow? row = null;
        if (id != 0) row = Database.Query("SELECT * FROM Meals WHERE Id=@id", ("@id", id)).Rows.Cast<DataRow>().FirstOrDefault();
        var name = Field("Meal name", Ui.Text("Example: Classic Burger"));
        var category = Field("Category", Ui.Combo(Categories));
        var price = Field("Price (IQD)", Ui.Number(0, 100000000));
        var description = Field("Description", new TextBox
        {
            Multiline = true, Height = 63, ScrollBars = ScrollBars.Vertical, Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle
        }, 110);
        var available = Field("Availability", Ui.Combo(["Available", "Unavailable"]));
        var pathLabel = Ui.Label("No photo selected", 9, false, Theme.Muted);
        var pathHolder = new Panel { Height = 44 };
        var choose = Ui.Button("Choose photo", Theme.Gold, Theme.Navy, 136, 34);
        choose.Dock = DockStyle.Left;
        pathLabel.Dock = DockStyle.Fill;
        pathHolder.Controls.Add(pathLabel); pathHolder.Controls.Add(choose);
        Field("Menu photo (optional)", pathHolder, 83);
        var photoPath = row is null ? "" : Convert.ToString(row["ImagePath"]) ?? "";
        pathLabel.Text = Path.GetFileName(photoPath);
        choose.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog
            {
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*",
                Title = "Select meal photograph"
            };
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                photoPath = picker.FileName;
                pathLabel.Text = Path.GetFileName(photoPath);
            }
        };
        if (row is not null)
        {
            name.Text = Convert.ToString(row["Name"]);
            category.SelectedItem = Convert.ToString(row["Category"]);
            if (category.SelectedIndex < 0) category.SelectedIndex = 4;
            price.Value = Math.Clamp(Database.Money(row["Price"]), price.Minimum, price.Maximum);
            description.Text = Convert.ToString(row["Description"]);
            available.SelectedIndex = Convert.ToInt32(row["IsAvailable"]) == 1 ? 0 : 1;
        }
        SaveWith(() =>
        {
            var mealName = Required(name.Text, "Meal name");
            if (mealName.Length > 120) throw new InvalidOperationException("Meal name is too long.");
            string savedPath = photoPath;
            if (File.Exists(photoPath) && !Path.GetFullPath(photoPath).StartsWith(
                    Path.GetFullPath(Database.ImagesFolder) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                var extension = Path.GetExtension(photoPath).ToLowerInvariant();
                if (extension is not (".jpg" or ".jpeg" or ".png" or ".bmp"))
                    throw new InvalidOperationException("Choose a JPG, PNG or BMP photo.");
                savedPath = Path.Combine(Database.ImagesFolder, Guid.NewGuid().ToString("N") + extension);
                File.Copy(photoPath, savedPath);
            }
            if (id == 0)
                Database.Execute("INSERT INTO Meals(Name,Category,Price,Description,IsAvailable,ImagePath) " +
                    "VALUES(@n,@c,@p,@d,@a,@i)",
                    ("@n", mealName), ("@c", category.Text), ("@p", price.Value),
                    ("@d", description.Text.Trim()), ("@a", available.SelectedIndex == 0 ? 1 : 0),
                    ("@i", savedPath));
            else
                Database.Execute("UPDATE Meals SET Name=@n,Category=@c,Price=@p,Description=@d," +
                    "IsAvailable=@a,ImagePath=@i WHERE Id=@id",
                    ("@n", mealName), ("@c", category.Text), ("@p", price.Value),
                    ("@d", description.Text.Trim()), ("@a", available.SelectedIndex == 0 ? 1 : 0),
                    ("@i", savedPath), ("@id", id));
        });
    }
}

public sealed class TableDialog : EditDialog
{
    public TableDialog(long id = 0) : base(id == 0 ? "Add dining table" : "Edit dining table", 510, 420)
    {
        DataRow? row = id == 0 ? null : Database.Query(
            "SELECT * FROM DiningTables WHERE Id=@id", ("@id", id)).Rows.Cast<DataRow>().FirstOrDefault();
        var number = Field("Table number", Ui.Text("T13"));
        var capacity = Field("Number of seats", Ui.Number(1, 30));
        var status = Field("Table status", Ui.Combo(["Available", "Reserved"]));
        if (row is not null)
        {
            number.Text = Convert.ToString(row["TableNumber"]);
            capacity.Value = Math.Clamp(Database.Money(row["Capacity"]), 1, 30);
            if (Convert.ToString(row["Status"]) == "Occupied")
            {
                status.Items.Add("Occupied");
                status.SelectedItem = "Occupied";
                status.Enabled = false;
            }
            else status.SelectedItem = Convert.ToString(row["Status"]);
        }
        SaveWith(() =>
        {
            var tableNumber = Required(number.Text, "Table number");
            if (tableNumber.Length > 20) throw new InvalidOperationException("Table number is too long.");
            if (id == 0)
                Database.Execute("INSERT INTO DiningTables(TableNumber,Capacity,Status) VALUES(@n,@c,@s)",
                    ("@n", tableNumber), ("@c", capacity.Value), ("@s", status.Text));
            else
                Database.Execute("UPDATE DiningTables SET TableNumber=@n,Capacity=@c," +
                    "Status=CASE WHEN Status='Occupied' THEN 'Occupied' ELSE @s END WHERE Id=@id",
                    ("@n", tableNumber), ("@c", capacity.Value), ("@s", status.Text), ("@id", id));
        });
    }
}

public sealed class CustomerDialog : EditDialog
{
    public CustomerDialog(long id = 0) : base(id == 0 ? "Add customer" : "Edit customer", 550, 540)
    {
        DataRow? row = id == 0 ? null : Database.Query(
            "SELECT * FROM Customers WHERE Id=@id", ("@id", id)).Rows.Cast<DataRow>().FirstOrDefault();
        var name = Field("Full name", Ui.Text("Customer name"));
        var phone = Field("Phone", Ui.Text("07..."));
        var email = Field("Email (optional)", Ui.Text("name@example.com"));
        var notes = Field("Notes", new TextBox { Multiline = true, Height = 57, Font = Theme.Body }, 98);
        if (row is not null)
        {
            name.Text = Convert.ToString(row["FullName"]);
            phone.Text = Convert.ToString(row["Phone"]);
            email.Text = Convert.ToString(row["Email"]);
            notes.Text = Convert.ToString(row["Notes"]);
        }
        SaveWith(() =>
        {
            var value = Required(name.Text, "Customer name");
            if (id == 0)
                Database.Execute("INSERT INTO Customers(FullName,Phone,Email,Notes) VALUES(@n,@p,@e,@notes)",
                    ("@n", value), ("@p", phone.Text.Trim()), ("@e", email.Text.Trim()),
                    ("@notes", notes.Text.Trim()));
            else
                Database.Execute("UPDATE Customers SET FullName=@n,Phone=@p,Email=@e,Notes=@notes WHERE Id=@id",
                    ("@n", value), ("@p", phone.Text.Trim()), ("@e", email.Text.Trim()),
                    ("@notes", notes.Text.Trim()), ("@id", id));
        });
    }
}

public sealed class EmployeeDialog : EditDialog
{
    public EmployeeDialog(long id = 0) : base(id == 0 ? "Add employee account" : "Edit employee account", 565, 770)
    {
        if (!Session.RequireUser().IsAdmin) throw new UnauthorizedAccessException();
        DataRow? row = id == 0 ? null : Database.Query(
            "SELECT * FROM Users WHERE Id=@id", ("@id", id)).Rows.Cast<DataRow>().FirstOrDefault();
        var name = Field("Full name", Ui.Text("Employee name"));
        var username = Field("Username", Ui.Text("username"));
        var phone = Field("Phone", Ui.Text("Phone"));
        var role = Field("Access role", Ui.Combo(["Admin", "Manager", "Cashier", "Waiter"]));
        var salary = Field("Monthly salary (IQD)", Ui.Number(0, 100000000));
        var password = Field(id == 0 ? "Password (8+ characters)" : "New password (leave blank to keep)",
            Ui.Text("Password"));
        password.UseSystemPasswordChar = true;
        var active = Field("Account status", Ui.Combo(["Active", "Disabled"]));
        if (row is not null)
        {
            name.Text = Convert.ToString(row["FullName"]);
            username.Text = Convert.ToString(row["Username"]);
            phone.Text = Convert.ToString(row["Phone"]);
            role.SelectedItem = Convert.ToString(row["Role"]);
            salary.Value = Math.Clamp(Database.Money(row["Salary"]), 0, salary.Maximum);
            active.SelectedIndex = Convert.ToInt32(row["IsActive"]) == 1 ? 0 : 1;
        }
        if (id == Session.RequireUser().Id)
        {
            role.Enabled = false;
            active.Enabled = false;
        }
        SaveWith(() =>
        {
            var fullName = Required(name.Text, "Full name");
            var login = Required(username.Text, "Username");
            if (login.Length < 3 || login.Length > 50 || login.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
                throw new InvalidOperationException("Username must be 3–50 characters: letters, digits or underscore.");
            if (id == 0 || password.Text.Length > 0)
                if (password.Text.Length < 8) throw new InvalidOperationException("Password requires at least 8 characters.");
            var currentUser = id == Session.RequireUser().Id;
            var newRole = currentUser ? Session.RequireUser().Role : role.Text;
            var newActive = currentUser ? 1 : (active.SelectedIndex == 0 ? 1 : 0);
            if (id == 0)
            {
                var (hash, salt) = PasswordSecurity.Hash(password.Text);
                Database.Execute("INSERT INTO Users(FullName,Username,Phone,Role,Salary,PasswordHash,PasswordSalt,IsActive,CreatedAt) " +
                    "VALUES(@name,@login,@phone,@role,@salary,@hash,@salt,@active,@date)",
                    ("@name", fullName), ("@login", login), ("@phone", phone.Text.Trim()),
                    ("@role", newRole), ("@salary", salary.Value),
                    ("@hash", hash), ("@salt", salt), ("@active", newActive),
                    ("@date", Database.Now));
            }
            else
            {
                Database.Execute("UPDATE Users SET FullName=@name,Username=@login,Phone=@phone," +
                    "Role=@role,Salary=@salary,IsActive=@active WHERE Id=@id",
                    ("@name", fullName), ("@login", login), ("@phone", phone.Text.Trim()),
                    ("@role", newRole), ("@salary", salary.Value),
                    ("@active", newActive), ("@id", id));
                if (password.Text.Length > 0)
                {
                    var (hash, salt) = PasswordSecurity.Hash(password.Text);
                    Database.Execute("UPDATE Users SET PasswordHash=@hash,PasswordSalt=@salt WHERE Id=@id",
                        ("@hash", hash), ("@salt", salt), ("@id", id));
                }
            }
        });
    }
}
