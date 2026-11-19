# Restaurant Management System

**An end-to-end restaurant operations desktop application built with C# Windows Forms and SQLite.**

Restaurant Management System helps restaurant staff manage dining tables, reservations, orders, kitchen preparation, ingredients, cashier shifts, invoices, customers, and reports from one Windows desktop application.

The user interface is **created entirely in C# code**—no drag-and-drop Form Designer and no generated `*.Designer.cs` files.

> **Project type:** Desktop / Portfolio · **Platform:** Windows · **UI language:** English · **Database:** Local SQLite

## Highlights

- **Restaurant operations:** dine-in, takeaway, and delivery orders; tables and reservations; an automatically refreshing kitchen display.
- **Flexible ordering:** customize dishes with paid add-ons and kitchen notes; edit items and quantities before checkout.
- **Checkout and payments:** discounts, taxes, cash/card transactions, split bills, invoices, print previews, and 80 mm thermal receipts.
- **Inventory:** ingredient stock, per-meal recipes, optional ingredient usage for add-ons, low-stock thresholds, and waste tracking.
- **Business management:** customers, loyalty points, employee roles, cashier shifts, activity history, analytics, CSV export, and backups.

## Technology stack

| Layer | Technology |
|---|---|
| Language | C# |
| Desktop UI | Windows Forms (programmatically constructed) |
| Framework | .NET 10 (`net10.0-windows`) |
| Database | SQLite using `Microsoft.Data.Sqlite` |
| Native SQLite package | `SQLitePCLRaw.bundle_e_sqlite3` |
| Development environment | Visual Studio 2026 (with .NET desktop development) |
| Build automation | GitHub Actions (Windows runner) |
| Printing | Windows printing APIs, print preview, and optional Microsoft Print to PDF |

The package versions are declared in [`RestaurantManagementSystem.csproj`](RestaurantManagementSystem.csproj).

## Main features

### 1. Dashboard
- Daily sales overview, current orders, table availability, and a seven-day sales chart.

### 2. Meals and table management
- Manage meal names, categories, prices, descriptions, availability, and photos.
- Add or edit dining tables, capacity, and current status (`Available`, `Reserved`, `Occupied`).

### 3. Reservations and order types
- Book a dining table for a specified date, time range, and party size; reject overlapping active reservations.
- Create **Dine-in**, **Takeaway**, or **Delivery** orders; delivery captures contact and address details.

### 4. Orders and kitchen display
- Add dishes and quantities; attach per-dish kitchen notes and paid extras.
- Edit items on unpaid orders, then send the updated order back to the kitchen queue.
- Track the order stages `New` → `Preparing` → `Ready` → `Completed` (or `Cancelled`).
- Kitchen screen refreshes periodically.

### 5. Invoices, split payments, and printing
- Apply discounts and configurable tax at checkout.
- Accept locally recorded **cash** and **card** payment methods; card is **not** integrated with a payment gateway.
- Divide a bill into equal amounts, custom amounts, or groups of meal lines, with individual payer records.
- Preview or print invoices, receipts for individual payers, and **80 mm thermal-style** receipts.
- Save a PDF using Windows' **Microsoft Print to PDF** printer, where available.

### 6. Ingredient inventory, recipes, and waste
- Create ingredients with quantities, units, and low-stock thresholds.
- Define the ingredient quantities required by each meal; optionally associate ingredients with paid add-ons.
- Record stock additions/adjustments, usage, and food waste with reasons.
- Deduct recipe ingredient quantities **when checkout succeeds**, not when an order is merely created.
- Keep invoice creation and inventory deduction in one database transaction; block checkout when required tracked stock is insufficient.
- Preserve an order's ingredient requirements when a recipe is edited later.

### 7. Cashier shifts
- Open a shift with an opening cash amount.
- Track cash and card collections separately; reconcile expected cash with counted drawer cash at closing.
- Retain a history of shifts and variances.

### 8. Customers and loyalty
- Maintain customer profiles and contact information.
- Earn and redeem configurable-in-code loyalty points through recorded invoice transactions.
- View a customer-facing points ledger within the management application.

### 9. Reporting and audit
- Sales analytics, sales by date, popular meals, and invoice totals.
- Excel-compatible **CSV exports** for sales/payments, inventory, stock movements, waste, reservations, and activity logs.
- Log important staff actions such as order cancellations, changes to meal prices, payments, and inventory adjustments.

### 10. Accounts, security, and backups
- Staff sign-in with password hashing and role-based screen access.
- Roles: **Admin**, **Manager**, **Cashier**, and **Waiter**.
- Change personal passwords; require the seeded administrator password to be changed on first sign-in.
- Administrators can create consistent ZIP backups of the SQLite database and meal images, and restore backups with a safety copy created first.

## Roles and access

| Role | Primary access |
|---|---|
| Admin | All management functions, user accounts, restaurant settings, backups and restore |
| Manager | Day-to-day operations, inventory, billing, analytics, customer management, and audit/export tools |
| Cashier | Orders, tables, kitchen view, invoice/payment collection, and cashier shifts |
| Waiter | Meals, tables, reservations, order creation/updates, and kitchen view; no checkout access |

The exact permission rules live in [`Core/Session.cs`](Core/Session.cs) and are applied throughout the application.

## Getting started

### Requirements

- Windows 10 or 11.
- Visual Studio 2026 with **.NET desktop development**, or a compatible .NET 10 SDK on Windows.
- Internet connectivity on the first build to restore NuGet packages.
- Optional printer for physical receipts; **Microsoft Print to PDF** for PDF printing.

### Run from Visual Studio

1. Clone this repository or download its source ZIP.
2. Open **`RestaurantManagementSystem.sln`** in Visual Studio.
3. Restore NuGet dependencies if prompted.
4. Choose **Build → Rebuild Solution**.
5. Press **F5** to start the application.
6. On first launch, the app creates its local database and demo records automatically.

### Command-line build (Windows)

```powershell
dotnet restore RestaurantManagementSystem.sln
dotnet build RestaurantManagementSystem.sln --configuration Release
```

A Windows GitHub Actions workflow at [`.github/workflows/build.yml`](.github/workflows/build.yml) runs the project build on pushes and pull requests.

### Initial local demo account

The first-run seed creates the administrator account `admin` with a temporary demonstration password (`Admin@12345`). The application requires changing this password at initial sign-in. **Do not use seeded credentials for a real restaurant environment.**

## Typical usage flow

1. Sign in as an authorized member of staff.
2. Configure restaurant information, meals, tables, ingredients, and recipes.
3. Optionally create a reservation or open a cashier shift.
4. Create a dine-in, takeaway, or delivery order.
5. Customize dishes; track their progress in the kitchen.
6. Adjust the order before payment if needed.
7. Take a single or split payment; the application saves invoice records and deducts tracked ingredients.
8. Print receipts, review sales and inventory, and close the shift.
9. Export reports and create a backup regularly.

## Project structure

```text
RestaurantManagementSystem/
├── .github/workflows/build.yml   # Windows GitHub Actions build
├── Assets/restaurant.ico         # Application icon
├── Core/                         # Business rules, services, UI helpers and theme
├── Data/Database.cs              # SQLite initialization, schema, migrations, queries
├── Forms/                        # Code-built forms and dialogs
├── Pages/                        # Dashboard and management pages
├── Program.cs                    # Application entry point
├── RestaurantManagementSystem.csproj
├── RestaurantManagementSystem.sln
├── README.md                      # Project documentation (English)
├── README_AR.md                   # Project documentation (Arabic)
├── UPGRADE_AR.md                  # Earlier version upgrade notes
├── UPGRADE_V3_AR.md               # Feature update and testing notes
└── .gitignore                     # Excludes local/user data and build artifacts
```

## Data storage and privacy

SQLite data is created outside the repository at:

```text
%LOCALAPPDATA%\RestaurantManagementSystem\restaurant.sqlite3
%LOCALAPPDATA%\RestaurantManagementSystem\Images\
```

**Do not commit real restaurant data, exported sales, receipts, backups, or credentials.** The repository `.gitignore` excludes common database files, exports, backups, secrets, and Visual Studio build outputs if such files are copied into the working folder. A `.gitignore` does not untrack files already committed: remove those from Git tracking separately.

Backups contain potentially sensitive customer/business data and are **not encrypted** by the application. Store backup files privately and securely.

## Design system

A warm restaurant-inspired palette is applied consistently throughout the UI:

| Token | Hex | Usage |
|---|---|---|
| Cream | `#F2F3AE` | Light accents and backgrounds |
| Gold | `#EDD382` | Secondary actions and highlights |
| Orange | `#FC9E4F` | Primary actions and active navigation |
| Red | `#F4442E` | Destructive actions and warnings |
| Navy | `#020122` | Sidebar, headings and contrast |

All pages and dialogs use **WinForms controls written in C#** rather than the drag-and-drop designer.

## Useful keyboard shortcuts

| Shortcut | Action |
|---|---|
| `F1` | Show shortcut help |
| `F2` | New order (subject to role permissions) |
| `F5` | Refresh current page |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` / `Ctrl+4` | Dashboard / Meals / Tables / Orders |
| `Ctrl+5` / `Ctrl+6` / `Ctrl+7` | Kitchen / Inventory / Cashier shifts |
| `Ctrl+8` / `Ctrl+9` | Reservations / Meal extras |
| `Ctrl+P` | Preview selected invoice from the Invoices page |

Shortcuts are handled by the main form and may depend on the signed-in user's role.

## Notes and limitations

- This is a **local Windows desktop portfolio project**, not a cloud or multi-branch ordering platform.
- Card payment is recorded **for accounting only**; no bank/payment provider is connected.
- Invoice PDF output depends on an installed PDF printer. Thermal printing depends on compatible printer drivers and paper settings.
- Restaurant data persists locally; use the built-in backup/restore feature to safeguard it.
- The project does **not** contain a packaged installer or published EXE. Open and run the source in Visual Studio.
- Unit tests are not included in the supplied source tree; the repository has a **build** workflow rather than an automated end-to-end test suite.
- Screenshots can be added later under `docs/screenshots/` using images from the tested application, without including real customer details.

## Arabic documentation

For an Arabic guide to running the program and details of the later feature additions, see [`README_AR.md`](README_AR.md) and [`UPGRADE_V3_AR.md`](UPGRADE_V3_AR.md).

---

**Restaurant Management System** — a C# Windows Forms portfolio project focused on practical restaurant workflows, relational data, and desktop UI development.
