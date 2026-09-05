using Godot;

namespace PleasureToBurn;

/// <summary>
/// The dispatch console at the depot. One interactable, three meanings depending on contract state:
/// take a report, nothing (job in progress), or collect payment.
///
/// Scene setup: StaticBody3D on layer 2 with a mesh and collision; set Manager.
/// </summary>
public partial class Dispatch : StaticBody3D, IInteractable
{
    [Export] public ContractManager? Manager { get; set; }

    public string Prompt => Manager?.State switch
    {
        ContractState.Idle => "[E] Take the next report",
        ContractState.Cleared => "[E] Collect payment",
        ContractState.Accepted => "Dispatch: job in progress",
        _ => "",
    };

    public void Interact(Player player)
    {
        if (Manager is null)
            return;
        switch (Manager.State)
        {
            case ContractState.Idle:
                Manager.TakeReport();
                break;
            case ContractState.Cleared:
                Manager.CollectPayment();
                break;
        }
    }
}
