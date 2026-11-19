using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RestaurantManagementSystem.Core;

namespace RestaurantManagementSystem.Data;

public static class Database
{
    public static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RestaurantManagementSystem");
    public static readonly string ImagesFolder = Path.Combine(DataFolder, "Images");
    public static readonly string FilePath = Path.Combine(DataFolder, "restaurant.sqlite3");
    public static string Now => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath, Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared, ForeignKeys = true
        }.ToString());
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return c;
    }

    public static SqliteCommand Command(SqliteConnection c, string sql,
        SqliteTransaction? tx = null, params (string Name, object? Value)[] args)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        if (tx is not null) cmd.Transaction = tx;
        foreach (var (name, val) in args)
            cmd.Parameters.AddWithValue(name, val ?? DBNull.Value);
        return cmd;
    }

    public static DataTable Query(string sql, params (string Name, object? Value)[] args)
    {
        using var c = Open();
        using var cmd = Command(c, sql, null, args);
        using var reader = cmd.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public static object? Scalar(string sql, params (string Name, object? Value)[] args)
    {
        using var c = Open();
        using var cmd = Command(c, sql, null, args);
        return cmd.ExecuteScalar();
    }

    public static long Count(string sql, params (string Name, object? Value)[] args) =>
        Convert.ToInt64(Scalar(sql, args) ?? 0);

    public static int Execute(string sql, params (string Name, object? Value)[] args)
    {
        using var c = Open();
        using var cmd = Command(c, sql, null, args);
        return cmd.ExecuteNonQuery();
    }

    public static string Setting(string key, string fallback = "") =>
        Convert.ToString(Scalar("SELECT Value FROM Settings WHERE Key=@key", ("@key", key))) ?? fallback;

    public static void SetSetting(string key, string value) => Execute(
        "INSERT INTO Settings(Key,Value) VALUES(@key,@value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value",
        ("@key", key), ("@value", value));

    public static decimal Money(object? value) =>
        value is null || value is DBNull ? 0 : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    public static string FormatMoney(object? value) =>
        $"{Money(value):N0} {Setting("Currency", "IQD")}";

    private static void Migrate(SqliteConnection c)
    {
        // Preserve existing databases; upgrade in place without dropping any business data.
        static void AddColumn(SqliteConnection connection, string table, string name, string definition)
        {
            using var info = Command(connection, $"PRAGMA table_info({table})");
            using var r = info.ExecuteReader();
            bool exists = false;
            while (r.Read()) if (string.Equals(r.GetString(1), name, StringComparison.OrdinalIgnoreCase))
                exists = true;
            r.Close();
            if (!exists)
            {
                using var change = Command(connection, $"ALTER TABLE {table} ADD COLUMN {name} {definition}");
                change.ExecuteNonQuery();
            }
        }
        AddColumn(c, "Orders", "OrderType", "TEXT NOT NULL DEFAULT 'Takeaway'");
        AddColumn(c, "Orders", "DeliveryPhone", "TEXT NOT NULL DEFAULT ''");
        AddColumn(c, "Orders", "DeliveryAddress", "TEXT NOT NULL DEFAULT ''");
        AddColumn(c, "Invoices", "ShiftId", "INTEGER NULL REFERENCES CashierShifts(Id) ON DELETE SET NULL");
        using (var migrateTypes = Command(c, """
            UPDATE Orders SET OrderType='DineIn'
            WHERE TableId IS NOT NULL AND OrderType='Takeaway';
            """)) migrateTypes.ExecuteNonQuery();
        using var schema = Command(c, """
            CREATE TABLE IF NOT EXISTS Ingredients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                Unit TEXT NOT NULL CHECK(Unit IN ('g','kg','ml','L','pcs')),
                Quantity REAL NOT NULL DEFAULT 0 CHECK(Quantity>=0),
                LowStock REAL NOT NULL DEFAULT 0 CHECK(LowStock>=0)
            );
            CREATE TABLE IF NOT EXISTS Recipes (
                MealId INTEGER NOT NULL REFERENCES Meals(Id) ON DELETE CASCADE,
                IngredientId INTEGER NOT NULL REFERENCES Ingredients(Id) ON DELETE RESTRICT,
                Quantity REAL NOT NULL CHECK(Quantity>0),
                PRIMARY KEY(MealId,IngredientId)
            );
            CREATE TABLE IF NOT EXISTS OrderIngredientRequirements (
                OrderId INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
                IngredientId INTEGER NOT NULL REFERENCES Ingredients(Id) ON DELETE RESTRICT,
                IngredientName TEXT NOT NULL,
                Unit TEXT NOT NULL,
                Quantity REAL NOT NULL CHECK(Quantity>0),
                PRIMARY KEY(OrderId,IngredientId)
            );
            CREATE TABLE IF NOT EXISTS InventoryMovements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                IngredientId INTEGER NULL REFERENCES Ingredients(Id) ON DELETE SET NULL,
                IngredientName TEXT NOT NULL, Unit TEXT NOT NULL,
                QuantityChange REAL NOT NULL,
                Reason TEXT NOT NULL,
                OrderId INTEGER NULL REFERENCES Orders(Id) ON DELETE SET NULL,
                CreatedAt TEXT NOT NULL,
                UserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS CashierShifts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                OpenedAt TEXT NOT NULL,
                ClosedAt TEXT NULL,
                OpeningCash REAL NOT NULL DEFAULT 0 CHECK(OpeningCash>=0),
                ClosingCash REAL NULL,
                ExpectedCash REAL NULL,
                Notes TEXT NOT NULL DEFAULT ''
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Shifts_OneOpenPerUser
              ON CashierShifts(UserId) WHERE ClosedAt IS NULL;
            CREATE INDEX IF NOT EXISTS IX_Invoices_Shift ON Invoices(ShiftId);
            CREATE INDEX IF NOT EXISTS IX_Recipes_Ingredient ON Recipes(IngredientId);
            CREATE INDEX IF NOT EXISTS IX_InventoryMovements_Ingredient ON InventoryMovements(IngredientId,CreatedAt);
            """);
        schema.ExecuteNonQuery();
    }

    public static void Initialize()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(ImagesFolder);
        using var c = Open();
        using (var cmd = Command(c, "PRAGMA journal_mode=WAL;")) cmd.ExecuteNonQuery();
        const string schema = """
            CREATE TABLE IF NOT EXISTS Users(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              FullName TEXT NOT NULL, Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
              PasswordHash TEXT NOT NULL, PasswordSalt TEXT NOT NULL,
              Role TEXT NOT NULL CHECK(Role IN ('Admin','Manager','Cashier','Waiter')),
              Phone TEXT NOT NULL DEFAULT '', Salary REAL NOT NULL DEFAULT 0,
              IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Meals(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL, Category TEXT NOT NULL,
              Price REAL NOT NULL CHECK(Price>=0), Description TEXT NOT NULL DEFAULT '',
              IsAvailable INTEGER NOT NULL DEFAULT 1, ImagePath TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS DiningTables(
              Id INTEGER PRIMARY KEY AUTOINCREMENT, TableNumber TEXT NOT NULL UNIQUE,
              Capacity INTEGER NOT NULL CHECK(Capacity>0),
              Status TEXT NOT NULL DEFAULT 'Available'
                CHECK(Status IN ('Available','Reserved','Occupied'))
            );
            CREATE TABLE IF NOT EXISTS Customers(
              Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NOT NULL,
              Phone TEXT NOT NULL DEFAULT '', Email TEXT NOT NULL DEFAULT '',
              Notes TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Orders(
              Id INTEGER PRIMARY KEY AUTOINCREMENT, OrderNumber TEXT NOT NULL UNIQUE,
              TableId INTEGER NULL REFERENCES DiningTables(Id) ON DELETE SET NULL,
              TableLabel TEXT NOT NULL DEFAULT 'Takeaway',
              CustomerId INTEGER NULL REFERENCES Customers(Id) ON DELETE SET NULL,
              UserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
              OrderDate TEXT NOT NULL,
              Status TEXT NOT NULL DEFAULT 'New'
                CHECK(Status IN ('New','Preparing','Ready','Completed','Cancelled')),
              Subtotal REAL NOT NULL CHECK(Subtotal>=0), Notes TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS OrderItems(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              OrderId INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
              MealId INTEGER NULL REFERENCES Meals(Id) ON DELETE SET NULL,
              MealName TEXT NOT NULL, Quantity INTEGER NOT NULL CHECK(Quantity>0),
              UnitPrice REAL NOT NULL CHECK(UnitPrice>=0),
              LineTotal REAL NOT NULL CHECK(LineTotal>=0)
            );
            CREATE TABLE IF NOT EXISTS Invoices(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              InvoiceNumber TEXT NOT NULL UNIQUE,
              OrderId INTEGER NOT NULL UNIQUE REFERENCES Orders(Id) ON DELETE RESTRICT,
              InvoiceDate TEXT NOT NULL, Subtotal REAL NOT NULL,
              Discount REAL NOT NULL DEFAULT 0, TaxPercent REAL NOT NULL DEFAULT 0,
              TaxAmount REAL NOT NULL DEFAULT 0, FinalTotal REAL NOT NULL,
              AmountReceived REAL NOT NULL, ChangeDue REAL NOT NULL,
              PaymentMethod TEXT NOT NULL CHECK(PaymentMethod IN ('Cash','Card')),
              UserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS Settings(
              Key TEXT PRIMARY KEY, Value TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Orders_Date ON Orders(OrderDate);
            CREATE INDEX IF NOT EXISTS IX_Orders_Table ON Orders(TableId,Status);
            CREATE INDEX IF NOT EXISTS IX_Invoices_Date ON Invoices(InvoiceDate);
            CREATE INDEX IF NOT EXISTS IX_OrderItems_Order ON OrderItems(OrderId);
            """;
        using (var cmd = Command(c, schema)) cmd.ExecuteNonQuery();
        Migrate(c);

        foreach (var (key, value) in new[] {
            ("RestaurantName", "SUNSET BISTRO"), ("Phone", "+964 770 000 0000"),
            ("Address", "Iraq"), ("Currency", "IQD"), ("TaxPercent", "0")})
        {
            using var cmd = Command(c,
                "INSERT OR IGNORE INTO Settings(Key,Value) VALUES(@k,@v)",
                null, ("@k", key), ("@v", value));
            cmd.ExecuteNonQuery();
        }
        using (var count = Command(c, "SELECT COUNT(*) FROM Users"))
        {
            if (Convert.ToInt64(count.ExecuteScalar()) == 0)
            {
                var (hash, salt) = PasswordSecurity.Hash("Admin@12345");
                using var add = Command(c,
                    "INSERT INTO Users(FullName,Username,PasswordHash,PasswordSalt,Role,CreatedAt) " +
                    "VALUES('System Administrator','admin',@h,@s,'Admin',@dt)",
                    null, ("@h", hash), ("@s", salt), ("@dt", Now));
                add.ExecuteNonQuery();
            }
        }
        using (var count = Command(c, "SELECT COUNT(*) FROM DiningTables"))
        {
            if (Convert.ToInt64(count.ExecuteScalar()) == 0)
                for (var i = 1; i <= 12; i++)
                {
                    using var add = Command(c,
                        "INSERT INTO DiningTables(TableNumber,Capacity,Status) VALUES(@n,@c,'Available')",
                        null, ("@n", $"T{i:00}"), ("@c", i <= 6 ? 2 : 4));
                    add.ExecuteNonQuery();
                }
        }
        using (var count = Command(c, "SELECT COUNT(*) FROM Meals"))
        {
            if (Convert.ToInt64(count.ExecuteScalar()) == 0)
            {
                var seed = new (string Name, string Category, int Price, string Description)[]
                {
                    ("Classic Burger", "Main Course", 12000, "Beef patty, cheese and fries"),
                    ("Chicken Alfredo", "Main Course", 16000, "Creamy fettuccine and chicken"),
                    ("Margherita Pizza", "Main Course", 14000, "Tomato, mozzarella and basil"),
                    ("Grilled Chicken", "Main Course", 18000, "With seasonal vegetables"),
                    ("Caesar Salad", "Appetizers", 8500, "Fresh lettuce, croutons and dressing"),
                    ("French Fries", "Appetizers", 5000, "Golden crispy fries"),
                    ("Mozzarella Sticks", "Appetizers", 7000, "Crunchy cheese sticks"),
                    ("Fresh Orange Juice", "Beverages", 4500, "Freshly squeezed"),
                    ("Iced Latte", "Beverages", 5500, "Espresso with cold milk"),
                    ("Soft Drink", "Beverages", 2000, "Chilled canned beverage"),
                    ("Chocolate Cake", "Desserts", 6500, "Rich dark chocolate cake"),
                    ("Cheesecake", "Desserts", 7000, "Classic baked cheesecake")
                };
                foreach (var meal in seed)
                {
                    using var add = Command(c,
                        "INSERT INTO Meals(Name,Category,Price,Description) VALUES(@n,@c,@p,@d)",
                        null, ("@n", meal.Name), ("@c", meal.Category),
                        ("@p", meal.Price), ("@d", meal.Description));
                    add.ExecuteNonQuery();
                }
            }
        }
    }
}
