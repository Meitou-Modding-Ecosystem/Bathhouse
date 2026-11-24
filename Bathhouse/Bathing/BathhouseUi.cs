using Bathhouse.Customers;
using Bathhouse.Records;
using Bathhouse.Staffing;
using Meitou.Game.Ui;
using Meitou.Simulation;

namespace Bathhouse.Bathing;

/// <summary>
/// The bathhouse's panel lines (docs/bathhouse.md "Panels"): a tub shows its water and seats and the bath button, a boiler what it heats and the
/// switches for the bad effects soaks clear, a counter its check-in line and the button of its ledger window (<see cref="CounterLedger"/>), a tea
/// table and a massage station their price; a piece with
/// a staff job (<see cref="StaffRoles.OfferedBy"/>) adds the button that assigns it. The sources run on the host's thread and read only the
/// system's published views.
/// </summary>
public sealed class BathhouseUi(Func<BathhouseSystem?> system, CounterLedger ledger) : IUiExtension
{
    /// <summary>
    /// The most debuff switches a boiler's panel shows (the panel holds about six lines).
    /// </summary>
    public const int MaxDebuffLines = 4;

    /// <summary>
    /// The tooltip of a counter without a clerk.
    /// </summary>
    const string ClerkTip = "Keep one of your people beside the counter: press Assign selected: Bathhouse clerk with them selected.";

    /// <summary>
    /// The tooltip of a counter without a warm tub.
    /// </summary>
    const string WarmTip = "No tub in this building is warm with a free seat: switch a powered steam boiler on and keep Water in a store here.";

    /// <summary>
    /// The tooltip of a counter whose warm tubs are all dirty and wait for their scrubber.
    /// </summary>
    const string ScrubTip = "The warm tubs here are dirty: their Tub scrubber cleans each once it is empty. The tub's Scrubber row says what they are doing.";

    /// <summary>
    /// The tooltip of a ready counter.
    /// </summary>
    const string SuppliesTip = "Customers bathe without a towel or soap the counter and this building's stores lack, for a lower quality and price.";

    public string Id => BathhouseMod.Id;

    public void Register(UiRegistration registration)
    {
        registration.AddBuildingActions(Lines);
        ledger.Register(registration);
    }

    /// <summary>
    /// The lines of the selected piece: none for a piece that is not the bathhouse's or not the player's.
    /// </summary>
    IEnumerable<UiBuildingAction> Lines(UiBuildingContext context)
    {
        var building = context.Building;
        if (!building.Owned || system() is not { } bathhouse) return [];

        // The staff button first: the panel clips lines past its lower edge, and a storage piece's own lines push ours down.
        var lines = new List<UiBuildingAction>();
        if (StaffRoles.OfferedBy(building.Record) is { } role && building.SiteId is { } site) lines.Add(AssignLine(context, site, role));

        lines.AddRange(PieceLines(context, bathhouse));
        return lines;
    }

    /// <summary>
    /// The piece's own lines, after its staff button.
    /// </summary>
    IEnumerable<UiBuildingAction> PieceLines(UiBuildingContext context, BathhouseSystem bathhouse)
    {
        var building = context.Building;
        var counter = bathhouse.Counter;
        if (building.Record == Ids.Counter && building.SiteId is { } site) return [CheckInLine(bathhouse.CheckIn(site)), LedgerLine(site)];
        if (building.Record == Ids.ReviewBoard) return ReviewLines(bathhouse.Reviews.Summary);
        if (building.Record == Ids.TeaTable) return [PriceLine(context, Service.Tea, counter.Percent(Service.Tea), Tooltip(Service.Tea))];
        if (building.Record == Ids.MassageStation)
            return [PriceLine(context, Service.Massage, counter.Percent(Service.Massage), Tooltip(Service.Massage))];
        if (bathhouse.Piece(building.SiteId) is not { } view) return [];

        if (building.Record == Ids.Boiler) return [BoilerLine(view), .. DebuffLines(context, bathhouse)];
        if (view.Tea) return ConditionBars.Active ? [] : [HotWaterLine(view)];
        return ConditionBars.Active ? [BatheLine(context)] : [TubLine(view), BatheLine(context)];
    }

    /// <summary>
    /// A staff button: gives the selected characters the piece's job.
    /// </summary>
    static UiBuildingAction AssignLine(UiBuildingContext context, string site, StaffRole role)
    {
        var assign = new AssignStaffCommand(site, role) { Tick = 0 };
        string tooltip = "Adds this job to the selected characters' jobs (remove it in their jobs panel).";
        return new UiBuildingAction($"Assign selected: {StaffRoles.Name(role)}", tooltip, Send(context, assign));
    }

    /// <summary>
    /// The tooltip of the tea or massage price line.
    /// </summary>
    static string Tooltip(Service service)
    {
        long price = (long)Math.Round(Pricing.Suggested(service, TubKind.Small));
        string what = service == Service.Tea ? "a Tea Set after the soak" : "an hour of massage: a tenth at the attendant's Dexterity 0, all of it at 100";
        return $"Suggested {price} cats for {what}. Press for the next step.";
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
    /// A tea bench's or used tea rack's line in a game without the Condition bars: whether its building has hot water.
    /// </summary>
    static UiBuildingAction HotWaterLine(PieceView view)
    {
        var (name, _, value, tip) = ConditionBars.HotWater(view.Heat);
        return new UiBuildingAction(value.Length > 0 ? $"{name} ({value})" : name, tip, Nothing);
    }

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
    /// The counter's check-in line: ready with the towel and soap a customer gets, or what it waits for, with what to do about it as the tooltip.
    /// </summary>
    static UiBuildingAction CheckInLine(CheckInView? view)
    {
        if (view is not { } now) return new UiBuildingAction("Check-in: starting", null, Nothing);

        return now.Need switch
        {
            CheckInNeed.Clerk => new UiBuildingAction("Check-in needs: a clerk", ClerkTip, Nothing),
            CheckInNeed.WarmTub => new UiBuildingAction("Check-in needs: a warm tub", WarmTip, Nothing),
            CheckInNeed.Scrub => new UiBuildingAction("Check-in needs: a tub scrubbed", ScrubTip, Nothing),
            _ => new UiBuildingAction($"Check-in: ready, {Supplies(now)}", SuppliesTip, Nothing),
        };
    }

    /// <summary>
    /// The towel and soap a customer would get, as the ready line names them.
    /// </summary>
    static string Supplies(CheckInView view) => (view.Towel, view.Soap) switch
    {
        (true, true) => "towel and soap",
        (true, false) => "towel, no soap",
        (false, true) => "soap, no towel",
        _ => "no towel or soap",
    };

    /// <summary>
    /// The counter's ledger button: opens its window, or closes it.
    /// </summary>
    UiBuildingAction LedgerLine(string counter)
    {
        string caption = ledger.Shows(LedgerPage.Counter) ? "Close the bathhouse ledger" : "Open the bathhouse ledger";
        string tooltip = "Check-in, opening, prices, the guests now and the last three days' reviews, in a window of their own.";
        return new UiBuildingAction(caption, tooltip, () => ledger.Toggle(LedgerPage.Counter, counter));
    }

    /// <summary>
    /// The review board's lines: the last three days' guests and failed arrivals, and the button that opens the reviews.
    /// </summary>
    UiBuildingAction[] ReviewLines(GuestSummary summary)
    {
        string guests = $"Guests: {summary.Recent} in 3 days ({summary.PerDay:0.0} a day), {summary.Total} in all";
        string failed = $"Failed to arrive: {summary.Failed} in 3 days";
        string read = ledger.Shows(LedgerPage.Reviews) ? "Close the reviews" : "Read the reviews";
        return
        [
            new UiBuildingAction(guests, "Guests who soaked, paid or not.", Nothing),
            new UiBuildingAction(failed, "Gave up on the way, attacked on the road, found it closed or waited too long at the counter.", Nothing),
            new UiBuildingAction(read, "Every guest of the last three game days, with how their visit went.", () => ledger.Toggle(LedgerPage.Reviews)),
        ];
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
