using Godot;

namespace PleasureToBurn;

/// <summary>Dispatch's job board: three offers, pick one. Content is built in code from JobOffers.</summary>
public partial class JobBoard : ModalPanel
{
    public const string Group = "job_board";

    private ContractManager? _manager;
    private VBoxContainer _offersBox = null!;
    private Label _header = null!;

    public override void _Ready()
    {
        AddToGroup(Group);
        _offersBox = GetNode<VBoxContainer>("Panel/VBox/Offers");
        _header = GetNode<Label>("Panel/VBox/Header");
        GetNode<Button>("Panel/VBox/CloseButton").Pressed += Close;
    }

    public void Open(ContractManager manager)
    {
        _manager = manager;
        var offers = manager.GenerateOffers();
        _header.Text = $"Day {Career.Instance.Day}  ·  {ContractManager.FormatTime(manager.ShiftTimeLeft)} left in shift  ·  Rep {Career.Instance.Reputation}";
        ClearChildren(_offersBox);
        for (var i = 0; i < offers.Count; i++)
            _offersBox.AddChild(MakeOfferCard(offers[i], i));
        OpenModal();
    }

    private Control MakeOfferCard(JobOffer offer, int index)
    {
        var card = new PanelContainer();
        var box = new VBoxContainer();
        card.AddChild(box);

        var kind = offer.IsPrecision ? "PRECISION — furniture is insured" : "STANDARD — burn what you like";
        var kindColor = offer.IsPrecision ? new Color(1f, 0.75f, 0.35f) : new Color(0.7f, 0.9f, 0.7f);
        box.AddChild(MakeLabel(offer.Site.Address, 20));
        box.AddChild(MakeLabel(kind, 13, kindColor));
        box.AddChild(MakeLabel(offer.ReportLine, 14, new Color(0.85f, 0.85f, 0.8f)));
        box.AddChild(MakeLabel($"{offer.ContrabandCount} items  ·  est. ${offer.EstimatedPay}  ·  {offer.DistanceMetres:0} m  ·  bonus if back in {ContractManager.FormatTime(offer.BonusSeconds)}", 13));

        var accept = new Button { Text = "Accept" };
        accept.Pressed += () =>
        {
            if (_manager?.Accept(index) == true)
                Close();
        };
        box.AddChild(accept);
        return card;
    }
}
