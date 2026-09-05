using Godot;

namespace PleasureToBurn;

/// <summary>The reward beat: an itemised receipt for the job just settled, with a star rating.</summary>
public partial class InvoicePanel : ModalPanel
{
    public const string Group = "invoice_panel";

    private VBoxContainer _lines = null!;
    private Label _title = null!;
    private Label _stars = null!;

    public override void _Ready()
    {
        AddToGroup(Group);
        _lines = GetNode<VBoxContainer>("Panel/VBox/Lines");
        _title = GetNode<Label>("Panel/VBox/Title");
        _stars = GetNode<Label>("Panel/VBox/Stars");
        GetNode<Button>("Panel/VBox/CloseButton").Pressed += Close;
    }

    public void Open(Invoice invoice)
    {
        _title.Text = $"Invoice — {invoice.Address}";
        _stars.Text = new string('★', invoice.Stars) + new string('☆', 3 - invoice.Stars);
        ClearChildren(_lines);

        Line($"{(invoice.IsPrecision ? "Precision" : "Standard")} job, completed in {ContractManager.FormatTime(invoice.SecondsTaken)}", "");
        Line($"Contraband destroyed  {invoice.ContrabandBurned}/{invoice.ContrabandTotal} × ${invoice.RatePerItem}", $"${invoice.BasePay}");
        if (invoice.IsPrecision)
            Line($"Insured furniture charred  {invoice.CollateralCount} × ${invoice.CollateralPenalty / Math.Max(1, invoice.CollateralCount)}",
                invoice.CollateralPenalty > 0 ? $"-${invoice.CollateralPenalty}" : "$0", invoice.CollateralPenalty > 0 ? new Color(1f, 0.5f, 0.4f) : null);
        Line(invoice.MadeDeadline ? "Deadline bonus" : "Deadline missed", invoice.MadeDeadline ? $"+${invoice.DeadlineBonus}" : "$0",
            invoice.MadeDeadline ? new Color(0.6f, 1f, 0.6f) : new Color(0.8f, 0.8f, 0.8f));
        Line($"Reputation multiplier ×{invoice.ReputationMultiplier:0.00}", "");
        Line("TOTAL PAID", $"${invoice.Total}", new Color(0.6f, 1f, 0.6f), 22);
        var rep = invoice.ReputationDelta;
        Line("Reputation", rep > 0 ? $"+{rep}" : rep.ToString(), rep >= 0 ? new Color(0.85f, 0.85f, 0.8f) : new Color(1f, 0.5f, 0.4f));
        OpenModal();
    }

    private void Line(string left, string right, Color? color = null, int size = 16)
    {
        var row = new HBoxContainer();
        var l = MakeLabel(left, size, color);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(l);
        row.AddChild(MakeLabel(right, size, color, HorizontalAlignment.Right));
        _lines.AddChild(row);
    }
}
