namespace RestaurantManagementSystem.Core;

public sealed record LoggedInUser(long Id, string Name, string Username, string Role)
{
    public bool IsAdmin => Role == "Admin";
    public bool IsManager => Role is "Admin" or "Manager";
    public bool CanSell => Role is "Admin" or "Manager" or "Cashier";
    public bool CanManageOrders => Role is "Admin" or "Manager" or "Cashier" or "Waiter";
}

public static class Session
{
    public static LoggedInUser? User { get; set; }
    public static LoggedInUser RequireUser() => User ?? throw new InvalidOperationException("Please sign in again.");
}
