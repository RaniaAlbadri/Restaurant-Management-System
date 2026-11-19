using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class LoginForm : Form
{
    private readonly TextBox _username = Ui.Text("Username");
    private readonly TextBox _password = Ui.Text("Password");
    private readonly Label _status = Ui.Label("", 9, false, Theme.Red);
    private int _failedAttempts;

    public LoginForm()
    {
        Text = "Restaurant Management System — Sign in";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(880, 600);
        Size = new Size(1070, 695);
        BackColor = Theme.Canvas;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var brand = new Panel { Dock = DockStyle.Left, Width = 430, BackColor = Theme.Navy };
        var brandContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(42, 100, 25, 48),
            RowCount = 5, ColumnCount = 1
        };
        brandContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        brandContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        brandContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        brandContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        brandContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        brandContent.Controls.Add(Ui.Label("✦  RESTAURANT", 15, true, Theme.Orange), 0, 0);
        brandContent.Controls.Add(Ui.Label("Good food.\nGreat service.", 27, true, Theme.White), 0, 1);
        brandContent.Controls.Add(Ui.Label("Manage tables, orders, payments and performance from one place.",
            12, false, Theme.Cream), 0, 2);
        brandContent.Controls.Add(Ui.Label("Built with C#  •  Windows Forms  •  SQLite", 9, false,
            Theme.Gold), 0, 4);
        brand.Controls.Add(brandContent);

        var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
        var loginCard = Ui.Card(35);
        loginCard.Size = new Size(415, 498);
        loginCard.Anchor = AnchorStyles.None;
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 9, ColumnCount = 1 };
        foreach (var h in new[] { 55, 33, 32, 55, 32, 55, 58, 40, 58 })
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        stack.Controls.Add(Ui.Label("Welcome back", 24, true), 0, 0);
        stack.Controls.Add(Ui.Label("Please sign in to continue", 10, false, Theme.Muted), 0, 1);
        stack.Controls.Add(Ui.Label("USERNAME", 8.5f, true), 0, 2);
        _username.MaxLength = 50;
        stack.Controls.Add(_username, 0, 3);
        stack.Controls.Add(Ui.Label("PASSWORD", 8.5f, true), 0, 4);
        _password.UseSystemPasswordChar = true;
        _password.MaxLength = 200;
        stack.Controls.Add(_password, 0, 5);
        var loginButton = Ui.Button("SIGN IN", Theme.Orange, Theme.Navy);
        loginButton.Dock = DockStyle.Top;
        loginButton.Height = 45;
        loginButton.Click += (_, _) => SignIn();
        stack.Controls.Add(loginButton, 0, 6);
        stack.Controls.Add(_status, 0, 7);
        stack.Controls.Add(Ui.Label("Authorized staff only · Your credentials are private.",
            9, false, Theme.Muted), 0, 8);
        loginCard.Controls.Add(stack);
        right.Controls.Add(loginCard);
        void position()
        {
            loginCard.Left = Math.Max(12, (right.ClientSize.Width - loginCard.Width) / 2);
            loginCard.Top = Math.Max(10, (right.ClientSize.Height - loginCard.Height) / 2);
        }
        right.Resize += (_, _) => position();
        Controls.Add(right);
        Controls.Add(brand);
        AcceptButton = loginButton;
        Shown += (_, _) => { position(); _username.Focus(); };
    }

    private void SignIn()
    {
        if (string.IsNullOrWhiteSpace(_username.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            _status.Text = "Enter a username and password.";
            return;
        }
        try
        {
            var users = Database.Query(
                "SELECT Id,FullName,Username,PasswordHash,PasswordSalt,Role,IsActive " +
                "FROM Users WHERE Username=@name LIMIT 1", ("@name", _username.Text.Trim()));
            var valid = users.Rows.Count == 1 && Convert.ToInt32(users.Rows[0]["IsActive"]) == 1 &&
                PasswordSecurity.Verify(_password.Text, Convert.ToString(users.Rows[0]["PasswordHash"])!,
                    Convert.ToString(users.Rows[0]["PasswordSalt"])!);
            if (!valid)
            {
                _failedAttempts++;
                _status.Text = "Incorrect credentials or inactive account.";
                _password.Clear();
                // Simple local throttle; no account lockouts to avoid administrator lockout.
                if (_failedAttempts >= 5)
                {
                    _status.Text = "Too many attempts. Close and reopen the app to retry.";
                    _username.Enabled = false;
                    _password.Enabled = false;
                    if (AcceptButton is Button button) button.Enabled = false;
                }
                return;
            }
            _failedAttempts = 0;
            var r = users.Rows[0];
            Session.User = new LoggedInUser(Convert.ToInt64(r["Id"]),
                Convert.ToString(r["FullName"])!, Convert.ToString(r["Username"])!,
                Convert.ToString(r["Role"])!);
            _status.Text = "";
            if (string.Equals(Session.User.Username, "admin", StringComparison.OrdinalIgnoreCase) &&
                PasswordSecurity.Verify("Admin@12345", Convert.ToString(r["PasswordHash"])!,
                    Convert.ToString(r["PasswordSalt"])!))
            {
                using var change = new ChangePasswordDialog(required: true);
                if (change.ShowDialog(this) != DialogResult.OK)
                {
                    Session.User = null;
                    _password.Clear();
                    _status.Text = "Change the initial password before signing in.";
                    return;
                }
            }
            Hide();
            using var main = new MainForm();
            main.ShowDialog(this);
            Session.User = null;
            _password.Clear();
            Show();
            _username.Focus();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
}
