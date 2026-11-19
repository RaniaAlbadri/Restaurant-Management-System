using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Forms;

namespace RestaurantManagementSystem;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Database.Initialize();
            Application.Run(new LoginForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to start Restaurant Management System.\n\n{ex.Message}",
                "Startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
