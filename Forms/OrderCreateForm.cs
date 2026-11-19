using System.Data;
using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class OrderCreateForm : Form
{
    private readonly FlowLayoutPanel _menu = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
        Padding = new Padding(2, 12, 2, 12), BackColor = Theme.Canvas
    };
    private readonly DataGridView _cartGrid = Ui.Grid();
    private readonly List<CartItem> _cart = [];
    private readonly TextBox _search = Ui.Text("Find a dish...");
    private readonly ComboBox _category = Ui.Combo(["All categories", ..MealDialog.Categories]);
    private readonly ComboBox _table = Ui.Combo([]);
    private readonly ComboBox _orderType = Ui.Combo(["Dine-in", "Takeaway", "Delivery"]);
    private readonly TextBox _deliveryPhone = Ui.Text("Recipient phone number");
    private readonly TextBox _deliveryAddress = Ui.Text("Delivery address");
    private readonly ComboBox _customer = Ui.Combo([]);
    private readonly TextBox _notes = Ui.Text("Optional order notes");
    private readonly Label _total = Ui.Label("0 IQD", 19, true);
    private readonly Label _count = Ui.Label("0 items", 9, false, Theme.Muted);
    public long CreatedOrderId { get; private set; }

    public OrderCreateForm(long? preselectedTable = null)
    {
        if (!Session.RequireUser().CanManageOrders) throw new UnauthorizedAccessException();
        Text = "New order — Restaurant Management System";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1280, 825); MinimumSize = new Size(1020, 690);
        BackColor = Theme.Canvas;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 83, BackColor = Theme.Navy,
            Padding = new Padding(24, 8, 24, 7) };
        header.Controls.Add(Ui.Label("Create a new order", 21, true, Theme.Cream));
        var cartSide = new Panel { Dock = DockStyle.Right, Width = 421, BackColor = Theme.White,
            Padding = new Padding(16, 18, 16, 13) };
        var cartTitle = Ui.Label("YOUR ORDER", 15, true);
        cartTitle.Dock = DockStyle.Top; cartTitle.Height = 42;
        var comboArea = new TableLayoutPanel { Dock = DockStyle.Top, Height = 330,
            RowCount = 10, ColumnCount = 1 };
        for (var i = 0; i < 5; i++)
        {
            comboArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            comboArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        }
        comboArea.Controls.Add(Ui.Label("ORDER TYPE", 9, true, Theme.Muted), 0, 0);
        comboArea.Controls.Add(_orderType, 0, 1);
        var tableLabel = Ui.Label("TABLE", 9, true, Theme.Muted);
        comboArea.Controls.Add(tableLabel, 0, 2);
        comboArea.Controls.Add(_table, 0, 3);
        comboArea.Controls.Add(Ui.Label("CUSTOMER", 9, true, Theme.Muted), 0, 4);
        comboArea.Controls.Add(_customer, 0, 5);
        var phoneLabel = Ui.Label("DELIVERY PHONE", 9, true, Theme.Muted);
        comboArea.Controls.Add(phoneLabel, 0, 6);
        comboArea.Controls.Add(_deliveryPhone, 0, 7);
        var addressLabel = Ui.Label("DELIVERY ADDRESS", 9, true, Theme.Muted);
        comboArea.Controls.Add(addressLabel, 0, 8);
        comboArea.Controls.Add(_deliveryAddress, 0, 9);
        void typeChanged()
        {
            var dineIn = _orderType.Text == "Dine-in";
            var delivery = _orderType.Text == "Delivery";
            tableLabel.Visible = _table.Visible = dineIn;
            phoneLabel.Visible = _deliveryPhone.Visible = delivery;
            addressLabel.Visible = _deliveryAddress.Visible = delivery;
            comboArea.RowStyles[2].Height = dineIn ? 23 : 0;
            comboArea.RowStyles[3].Height = dineIn ? 43 : 0;
            comboArea.RowStyles[6].Height = delivery ? 23 : 0;
            comboArea.RowStyles[7].Height = delivery ? 43 : 0;
            comboArea.RowStyles[8].Height = delivery ? 23 : 0;
            comboArea.RowStyles[9].Height = delivery ? 43 : 0;
            comboArea.Height = delivery ? 264 : 198;
        }
        _orderType.SelectedIndexChanged += (_, _) => typeChanged();
        _orderType.SelectedIndex = preselectedTable is null ? 1 : 0;
        typeChanged();
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 241,
            Padding = new Padding(0, 10, 0, 0) };
        var changeBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44 };
        var plus = Ui.Button("+ Qty", Theme.Gold, Theme.Navy, 90, 34);
        var minus = Ui.Button("− Qty", Theme.Cream, Theme.Navy, 90, 34);
        var remove = Ui.Button("Remove", Theme.Red, Color.White, 90, 34);
        plus.Click += (_, _) => ChangeQuantity(1);
        minus.Click += (_, _) => ChangeQuantity(-1);
        remove.Click += (_, _) => ChangeQuantity(-999999);
        changeBar.Controls.Add(plus); changeBar.Controls.Add(minus); changeBar.Controls.Add(remove);
        var totalRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 2 };
        totalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        totalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        totalRow.Controls.Add(Ui.Label("SUBTOTAL", 10, true), 0, 0);
        totalRow.Controls.Add(_total, 1, 0);
        var create = Ui.Button("PLACE ORDER", Theme.Orange, Theme.Navy);
        create.Dock = DockStyle.Bottom; create.Height = 47;
        create.Click += (_, _) => PlaceOrder();
        _notes.Dock = DockStyle.Bottom;
        var notesLabel = Ui.Label("ORDER NOTES", 9, true, Theme.Muted);
        notesLabel.Dock = DockStyle.Bottom; notesLabel.Height = 25;
        _count.Dock = DockStyle.Bottom; _count.Height = 22;
        bottom.Controls.Add(create);
        bottom.Controls.Add(_notes);
        bottom.Controls.Add(notesLabel);
        bottom.Controls.Add(totalRow);
        bottom.Controls.Add(_count);
        bottom.Controls.Add(changeBar);

        _cartGrid.ColumnHeadersHeight = 36;
        _cartGrid.RowTemplate.Height = 38;
        _cartGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        cartSide.Controls.Add(_cartGrid);
        cartSide.Controls.Add(bottom);
        cartSide.Controls.Add(comboArea);
        cartSide.Controls.Add(cartTitle);

        var menuSide = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 17, 10) };
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 59, BackColor = Theme.Canvas };
        _search.Width = 250; _search.Dock = DockStyle.None;
        _category.Width = 165; _category.Dock = DockStyle.None;
        filters.Controls.Add(_search); filters.Controls.Add(_category);
        menuSide.Controls.Add(_menu); menuSide.Controls.Add(filters);

        Controls.Add(menuSide); Controls.Add(cartSide); Controls.Add(header);
        _search.TextChanged += (_, _) => LoadMenu();
        _category.SelectedIndexChanged += (_, _) => LoadMenu();
        FillSelections(preselectedTable);
        LoadMenu(); RenderCart();
    }

    private void FillSelections(long? preselectedTable)
    {
        var tables = Database.Query("SELECT Id,TableNumber AS Display FROM DiningTables WHERE Status='Available' ORDER BY TableNumber");
        var take = tables.NewRow(); take["Id"] = 0L; take["Display"] = "Takeaway / no table";
        tables.Rows.InsertAt(take, 0);
        _table.DisplayMember = "Display"; _table.ValueMember = "Id"; _table.DataSource = tables;
        if (preselectedTable is not null) _table.SelectedValue = preselectedTable.Value;
        var customers = Database.Query("SELECT Id,FullName AS Display FROM Customers ORDER BY FullName");
        var walkin = customers.NewRow(); walkin["Id"] = 0L; walkin["Display"] = "Walk-in guest";
        customers.Rows.InsertAt(walkin, 0);
        _customer.DisplayMember = "Display"; _customer.ValueMember = "Id"; _customer.DataSource = customers;
    }

    private void LoadMenu()
    {
        Ui.WithWaitCursor(this, () =>
        {
            _menu.SuspendLayout();
            var previousCards = _menu.Controls.Cast<Control>().ToArray();
            _menu.Controls.Clear();
            foreach (var old in previousCards)
            {
                if (old is Panel panel)
                    foreach (Control control in panel.Controls)
                        if (control is PictureBox picture) { picture.Image?.Dispose(); picture.Image = null; }
                old.Dispose();
            }
            var search = "%" + _search.Text.Trim() + "%";
            var category = _category.SelectedIndex <= 0 ? "%" : _category.Text;
            var meals = Database.Query("""
                SELECT * FROM Meals WHERE IsAvailable=1
                AND (Name LIKE @search OR Description LIKE @search)
                AND Category LIKE @cat ORDER BY Category,Name
                """, ("@search", search), ("@cat", category));
            foreach (DataRow row in meals.Rows)
            {
                var id = Convert.ToInt64(row["Id"]);
                var name = Convert.ToString(row["Name"]) ?? "";
                var description = Convert.ToString(row["Description"]) ?? "";
                var price = Database.Money(row["Price"]);
                var card = Ui.Card(10);
                card.Width = 207; card.Height = 225;
                card.Margin = new Padding(0, 0, 15, 15);
                var image = new PictureBox
                {
                    Dock = DockStyle.Top, Height = 91, SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Theme.Cream
                };
                image.Paint += (_, e) =>
                {
                    if (image.Image is not null) return;
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var p = new Pen(Theme.Navy, 3);
                    using var fill = new SolidBrush(Theme.Gold);
                    var center = image.ClientRectangle.Width / 2;
                    g.FillEllipse(fill, center - 28, 17, 56, 56);
                    g.DrawEllipse(p, center - 22, 23, 44, 44);
                    g.DrawLine(p, center - 44, 19, center - 44, 68);
                    g.DrawLine(p, center + 42, 19, center + 42, 68);
                };
                var path = Convert.ToString(row["ImagePath"]) ?? "";
                if (File.Exists(path))
                {
                    try
                    {
                        using var mem = new MemoryStream(File.ReadAllBytes(path));
                        using var temp = Image.FromStream(mem);
                        image.Image = new Bitmap(temp);
                    }
                    catch (Exception) { /* Missing or corrupt photo: keep themed background. */ }
                }
                var title = Ui.Label(name, 10, true);
                title.Dock = DockStyle.Top; title.Height = 26;
                var detail = Ui.Label(description, 8.5f, false, Theme.Muted);
                detail.Dock = DockStyle.Top; detail.Height = 29;
                var add = Ui.Button("+  " + Database.FormatMoney(price), Theme.Orange, Theme.Navy, 183, 35);
                add.Dock = DockStyle.Bottom;
                add.Click += (_, _) => AddItem(id, name, price);
                card.Controls.Add(add); card.Controls.Add(detail);
                card.Controls.Add(title); card.Controls.Add(image);
                _menu.Controls.Add(card);
            }
            _menu.ResumeLayout();
        });
    }

    private void AddItem(long mealId, string name, decimal price)
    {
        var item = _cart.FirstOrDefault(x => x.MealId == mealId);
        if (item is null) _cart.Add(new CartItem { MealId = mealId, Name = name, Price = price });
        else if (item.Quantity < 999) item.Quantity++;
        RenderCart();
    }

    private void ChangeQuantity(int delta)
    {
        if (_cartGrid.CurrentRow is null || !_cartGrid.Columns.Contains("MealId")) return;
        var id = Convert.ToInt64(_cartGrid.CurrentRow.Cells["MealId"].Value);
        var item = _cart.FirstOrDefault(x => x.MealId == id);
        if (item is null) return;
        item.Quantity += delta;
        if (item.Quantity <= 0) _cart.Remove(item);
        if (item.Quantity > 999) item.Quantity = 999;
        RenderCart();
    }

    private void RenderCart()
    {
        var t = new DataTable();
        t.Columns.Add("MealId", typeof(long));
        t.Columns.Add("Meal", typeof(string));
        t.Columns.Add("Qty", typeof(int));
        t.Columns.Add("Price", typeof(decimal));
        t.Columns.Add("Total", typeof(decimal));
        foreach (var item in _cart) t.Rows.Add(item.MealId, item.Name, item.Quantity, item.Price, item.Total);
        _cartGrid.DataSource = t;
        Ui.HideColumns(_cartGrid, "MealId");
        Ui.CurrencyColumn(_cartGrid, "Price", "Total");
        _total.Text = Database.FormatMoney(_cart.Sum(x => x.Total));
        _count.Text = $"{_cart.Sum(x => x.Quantity)} item(s)";
    }

    private void PlaceOrder()
    {
        Ui.WithWaitCursor(this, () =>
        {
            if (_cart.Count == 0) { Ui.Alert("Please select at least one meal."); return; }
            long tableId = Convert.ToInt64(_table.SelectedValue ?? 0);
            long customerId = Convert.ToInt64(_customer.SelectedValue ?? 0);
            var type = _orderType.Text switch
            {
                "Dine-in" => "DineIn", "Delivery" => "Delivery", _ => "Takeaway"
            };
            CreatedOrderId = OrderService.Create(type == "DineIn" && tableId != 0 ? tableId : null,
                customerId == 0 ? null : customerId, _notes.Text.Trim(), _cart,
                type, _deliveryPhone.Text, _deliveryAddress.Text);
            Ui.Alert($"Order created successfully!\nOrder ID: {CreatedOrderId}");
            DialogResult = DialogResult.OK;
            Close();
        });
    }
}
