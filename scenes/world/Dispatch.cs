using Godot;

namespace PleasureToBurn;

/// <summary>
/// The dispatch console at the depot. One interactable, three meanings depending on state:
/// open the job board, settle the invoice, or sign off for the day.
///
/// Scene setup: StaticBody3D on layer 2 with a mesh and collision; set Manager. The UI panels are
/// found by group ("job_board", "invoice_panel", "day_end_panel") so the scene needs no wiring.
/// </summary>
public partial class Dispatch : StaticBody3D, IInteractable
{
    [Export] public ContractManager? Manager { get; set; }

    public string Prompt => Manager?.State switch
    {
        ContractState.Idle when Manager.ShiftOver => "[E] Sign off for the day",
        ContractState.Idle => "[E] Check the job board",
        ContractState.Cleared => "[E] Settle the invoice",
        ContractState.Accepted => "Dispatch: job in progress",
        _ => "",
    };

    public void Interact(Player player)
    {
        if (Manager is null)
            return;
        switch (Manager.State)
        {
            case ContractState.Idle when Manager.ShiftOver:
                Find<DayEndPanel>(DayEndPanel.Group)?.Open(Manager);
                break;
            case ContractState.Idle:
                Find<JobBoard>(JobBoard.Group)?.Open(Manager);
                break;
            case ContractState.Cleared:
                var invoice = Manager.Settle();
                if (invoice is not null)
                    Find<InvoicePanel>(InvoicePanel.Group)?.Open(invoice);
                break;
        }
    }

    private T? Find<T>(string group) where T : Node => GetTree().GetFirstNodeInGroup(group) as T;
}
