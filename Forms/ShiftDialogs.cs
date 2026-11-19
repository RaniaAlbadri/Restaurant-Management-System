using RestaurantManagementSystem.Core;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Forms;

public sealed class OpenShiftDialog : EditDialog
{
    public OpenShiftDialog() : base("Open cashier shift", 530, 310)
    {
        var initial = Field("Cash currently in the drawer (opening float)", Ui.Number(0, 1_000_000_000));
        Save.Text = "Open shift";
        SaveWith(() => ShiftService.Open(initial.Value));
    }
}

public sealed class CloseShiftDialog : EditDialog
{
    public CloseShiftDialog(long id) : base("Close cashier shift", 555, 500)
    {
        var totals = ShiftService.Totals(id);

        var expectedText = Ui.Label(Database.FormatMoney(totals.Expected), 15, true);
        expectedText.Height = 40;
        Field("Expected cash", expectedText, 95);

        var cardText = Ui.Label(Database.FormatMoney(totals.CardSales), 13, true);
        cardText.Height = 38;
        Field("Card sales (not counted as drawer cash)", cardText, 95);

        var actual = Field("Cash actually counted in drawer", Ui.Number(0, 1_000_000_000));
        actual.Value = Math.Clamp(totals.Expected, 0, actual.Maximum);
        var notes = Field("Closing notes (optional)", Ui.Text("Reason for difference / notes"));
        Save.Text = "Close shift";
        Save.Click += (_, _) =>
        {
            if (!Ui.Confirm($"Close shift?\nExpected: {Database.FormatMoney(totals.Expected)}\n" +
                $"Counted: {Database.FormatMoney(actual.Value)}\n" +
                $"Difference: {Database.FormatMoney(actual.Value - totals.Expected)}")) return;
            try
            {
                ShiftService.Close(id, actual.Value, notes.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { Ui.Error(ex); }
        };
    }
}
