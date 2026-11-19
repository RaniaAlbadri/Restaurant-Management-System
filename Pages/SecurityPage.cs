using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem.Pages;

public sealed class SecurityPage : PageBase
{
    public SecurityPage() : base("Security & backups", "Protect staff accounts and preserve the local database and meal images")
    {
        var holder = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            AutoScroll = true, WrapContents = false, Padding = new Padding(5)
        };
        var password = Ui.Card(18); password.Size = new Size(650, 130);
        var passwordTitle = Ui.Label("MY ACCOUNT", 12, true); passwordTitle.Dock = DockStyle.Top;
        passwordTitle.Height = 36;
        var passButton = Ui.Button("Change my password", Theme.Orange, Theme.Navy, 205, 41);
        passButton.Dock = DockStyle.Top;
        passButton.Click += (_, _) =>
        {
            using var dlg = new ChangePasswordDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Ui.Alert("Password updated.");
        };
        password.Controls.Add(passButton); password.Controls.Add(passwordTitle);
        holder.Controls.Add(password);
        if (Session.RequireUser().IsAdmin)
        {
            var backup = Ui.Card(18); backup.Size = new Size(650, 270);
            backup.Margin = new Padding(0, 15, 0, 15);
            var title = Ui.Label("DATABASE & IMAGE BACKUP", 12, true);
            title.Dock = DockStyle.Top; title.Height = 40;
            var explanation = Ui.Label("A single ZIP stores a consistent SQLite snapshot and meal photos.\n" +
                "Restoring replaces the live database and signs you out; a safety copy is created first.",
                10, false, Theme.Muted);
            explanation.Dock = DockStyle.Top; explanation.Height = 76;
            var location = Ui.Label("Data folder: " + Database.DataFolder, 9, false, Theme.Muted);
            location.Dock = DockStyle.Top; location.Height = 43;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 53 };
            var save = Ui.Button("Create ZIP backup", Theme.Orange, Theme.Navy, 182, 43);
            var restore = Ui.Button("Restore from ZIP", Theme.Red, Theme.White, 180, 43);
            save.Click += (_, _) =>
            {
                using var dlg = new SaveFileDialog { Title = "Save restaurant backup",
                    Filter = "Restaurant backup|*.zip", DefaultExt = "zip", AddExtension = true,
                    FileName = "RestaurantBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip" };
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                Ui.WithWaitCursor(this, () => { BackupService.Create(dlg.FileName);
                    Ui.Alert("Backup saved:\n" + dlg.FileName); });
            };
            restore.Click += (_, _) =>
            {
                using var dlg = new OpenFileDialog { Title = "Choose a restaurant backup",
                    Filter = "Restaurant backup|*.zip" };
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (!Ui.Confirm("RESTORE WILL REPLACE THE LIVE DATABASE.\n" +
                    "Close any other restaurant application windows first.\n" +
                    "A safety ZIP will be created automatically. Continue?", "Confirm database restore")) return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    var safety = BackupService.Restore(dlg.FileName);
                    Cursor = Cursors.Default;
                    Ui.Alert("Restore completed. You will be signed out.\n\nSafety copy:\n" + safety);
                    FindForm()?.Close();
                }
                catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex); }
            };
            actions.Controls.Add(save); actions.Controls.Add(restore);
            backup.Controls.Add(actions); backup.Controls.Add(location);
            backup.Controls.Add(explanation); backup.Controls.Add(title);
            holder.Controls.Add(backup);
        }
        Body.Controls.Add(holder);
    }
}
