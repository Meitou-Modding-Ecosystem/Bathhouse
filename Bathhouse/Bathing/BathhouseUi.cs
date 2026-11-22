using Bathhouse.Customers;
using Bathhouse.Records;
using Meitou.Game.Ui;
using Meitou.Simulation;

namespace Bathhouse.Bathing;

/// <summary>
/// The bathhouse's panel lines (docs/bathhouse.md "Panels"): a tub shows its water and seats and the bath button, a boiler what it heats and the
/// switches for the bad effects soaks clear, a counter the open switch and the prices. The sources run on the host's thread and read only the
/// system's published views.
/// </summary>
public sealed class BathhouseUi(Func<BathhouseSystem?> system) : IUiExtension
{
    /// <summary>
    /// The most debuff switches a boiler's panel shows (the panel holds about six lines).
    /// </summary>
    public const int MaxDebuffLines = 4;

    public string Id => BathhouseMod.Id;

    public void Register(UiRegistration registration) => registration.AddBuildingActions(Lines);

    /// <summary>
    /// The lines of the selected piece: none for a piece that is not the bathhouse's or not the player's.
    /// </summary>
    IEnumerable<UiBuildingAction> Lines(UiBuildingContext context)
    {
        var building = context.Building;
        if (!building.Owned || system() is not { } bathhouse) return [];
        if (building.Record == Ids.Counter) return CounterLines(context, bathhouse.Counter);
        if (bathhouse.Piece(building.SiteId) is not { } view) return [];

        if (building.Record == Ids.Boiler) return [BoilerLine(view), .. DebuffLines(context, bathhouse)];
        return [TubLine(view), BatheLine(context)];
    }

    /// <summary>
    /// A tub's line: its water and its seats taken.
    /// </summary>
    static UiBuildingAction TubLine(PieceView view)
    {
        string water = view.Heat.Warm ? view.Heat.Heat == Heat.Hot ? "hot" : "lukewarm" : "cold";
        return new UiBuildingAction($"Water: {water}, {view.Taken} of {view.Seats} seats taken", BathhouseSystem.Describe(view.Heat.Heat), Nothing);
    }

    /// <summary>
    /// A tub's button: sends the selected characters to bathe in it.
    /// </summary>
    static UiBuildingAction BatheLine(UiBuildingContext context)
    {
        string tub = context.Building.SiteId!;
        return new UiBuildingAction("Take a bath", "Sends the selected characters to soak in this tub.", Bathe(context, tub));
    }

    /// <summary>
    /// The press of a tub's button: queues the command that sends the selection.
    /// </summary>
    static Action Bathe(UiBuildingContext context, string tub) => () => context.Enqueue(new BatheCommand(tub) { Tick = context.Tick });

    /// <summary>
    /// A boiler's line: its building's heat and how many seats the lit boilers heat.
    /// </summary>
    static UiBuildingAction BoilerLine(PieceView view)
    {
        var heat = view.Heat;
        string caption = $"Tubs: {BathhouseSystem.Describe(heat.Heat)}";
        string tooltip = $"{heat.Lit} of {heat.Boilers} boilers lit, heating {heat.Capacity} seats for {heat.Seats} in tubs; "
            + $"{heat.Water} Water in store.";
        return new UiBuildingAction(caption, tooltip, Nothing);
    }

    /// <summary>
    /// One switch per bad effect of any mod (the first <see cref="MaxDebuffLines"/>): whether a soak clears it.
    /// </summary>
    static IEnumerable<UiBuildingAction> DebuffLines(UiBuildingContext context, BathhouseSystem bathhouse)
    {
        foreach (var effect in Soothing.Debuffs.Take(MaxDebuffLines))
        {
            bool kept = bathhouse.Keeps(effect.Id);
            string caption = $"Soaks clear {effect.Name}: {(kept ? "off" : "on")}";
            yield return new UiBuildingAction(caption, effect.Description, Switch(context, effect.Id, !kept));
        }
    }

    /// <summary>
    /// The press of a debuff switch: queues the command that keeps the effect, or clears it again.
    /// </summary>
    static Action Switch(UiBuildingContext context, string effect, bool keep) =>
        () => context.Enqueue(new KeepDebuffCommand(effect, keep) { Tick = context.Tick });

    /// <summary>
    /// A counter's lines: the open switch (its tooltip the customers under way and the latest payment), then one price line per service.
    /// </summary>
    static IEnumerable<UiBuildingAction> CounterLines(UiBuildingContext context, CounterView view)
    {
        string tooltip = $"{view.Groups} groups coming or visiting, {view.Visits} customers in. {view.Latest}";
        var open = new OpenCommand(!view.Open) { Tick = 0 };
        yield return new UiBuildingAction($"Customers: {(view.Open ? "open" : "closed")}", tooltip, Send(context, open));

        string visit = $"A medium tub visit costs {view.VisitPrice} cats. Press for the next step.";
        yield return PriceLine(context, Service.Bath, view.Bath, visit);
        yield return PriceLine(context, Service.Towel, view.Towel, visit);
        yield return PriceLine(context, Service.Soap, view.Soap, visit);
    }

    /// <summary>
    /// A service's price line: its percent of the suggested price; a press steps it on.
    /// </summary>
    static UiBuildingAction PriceLine(UiBuildingContext context, Service service, int percent, string tooltip)
    {
        var next = new PriceCommand(service, Pricing.Next(percent)) { Tick = 0 };
        return new UiBuildingAction($"{service} price: {percent}% of suggested", tooltip, Send(context, next));
    }

    /// <summary>
    /// A press that queues a command, stamped with the tick it is pressed at (its own tick is a placeholder).
    /// </summary>
    static Action Send(UiBuildingContext context, SimCommand command) => () => context.Enqueue(command with { Tick = context.Tick });

    /// <summary>
    /// A press that does nothing: a line that only tells.
    /// </summary>
    static void Nothing()
    {
    }
}
