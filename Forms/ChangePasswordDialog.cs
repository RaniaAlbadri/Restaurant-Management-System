using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class ChangePasswordDialog : EditDialog
{
    public ChangePasswordDialog(bool required = false) : base(
        required ? "Security: change initial password" : "Change my password", 565, 485)
    {
        var current = Field("Current password", Ui.Text());
        var password = Field("New password (8 or more characters)", Ui.Text());
        var confirm = Field("Confirm new password", Ui.Text());
        current.UseSystemPasswordChar = password.UseSystemPasswordChar = confirm.UseSystemPasswordChar = true;
        if (required)
            Field("Security requirement", Ui.Label("Change the initial admin password before continuing.",
                10, true, Theme.Red), 64);
        Save.Text = "Update password";
        SaveWith(() =>
        {
            var user = Session.RequireUser();
            var rows = Database.Query("SELECT PasswordHash,PasswordSalt FROM Users WHERE Id=@id",
                ("@id", user.Id));
            if (rows.Rows.Count != 1 || !PasswordSecurity.Verify(current.Text,
                Convert.ToString(rows.Rows[0][0])!, Convert.ToString(rows.Rows[0][1])!))
                throw new InvalidOperationException("Current password is incorrect.");
            if (password.Text.Length < 8 || password.Text.Length > 200)
                throw new InvalidOperationException("New password must have 8-200 characters.");
            if (password.Text != confirm.Text)
                throw new InvalidOperationException("Password confirmation does not match.");
            if (current.Text == password.Text)
                throw new InvalidOperationException("Choose a different new password.");
            var (hash, salt) = PasswordSecurity.Hash(password.Text);
            Database.Execute("UPDATE Users SET PasswordHash=@hash,PasswordSalt=@salt WHERE Id=@id",
                ("@hash", hash), ("@salt", salt), ("@id", user.Id));
        });
    }
}
