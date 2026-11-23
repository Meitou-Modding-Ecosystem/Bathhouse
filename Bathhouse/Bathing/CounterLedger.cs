using System.Globalization;
using System.Numerics;
using Bathhouse.Customers;
using Meitou.Game.Ui;
using Meitou.Simulation;

namespace Bathhouse.Bathing;

/// <summary>
/// A page of the bathhouse ledger.
/// </summary>
public enum LedgerPage
{
    /// <summary>
    /// Check-in, opening, prices and the latest customers.
    /// </summary>
    Counter,

    /// <summary>
    /// The groups on their way and the guests now.
    /// </summary>
    Guests,

    /// <summary>
    /// The review board: the last three days' guests.
    /// </summary>
    Reviews,
}

/// <summary>
/// The bathhouse ledger window (docs/bathhouse.md "Panels"), with a tab per <see cref="LedgerPage"/>: the counter's check-in, open switch, prices
/// and latest customers; the guest list; and the review board's record. The counter's and the review board's panel buttons open it on their page;
/// it reads only the system's published views on the host's thread and changes the world through commands.
/// </summary>
public sealed class CounterLedger(Func<BathhouseSystem?> system, Action<SimCommand> enqueue, Func<long> tick)
{
    #region Window

    /// <summary>
    /// The services the price lines show, in order.
    /// </summary>
    static readonly Service[] Priced = [Service.Bath, Service.Towel, Service.Soap, Service.Tea, Service.Massage];

    /// <summary>
    /// Whether the window is open. Written and read on the host's thread, as are the fields below.
    /// </summary>
    bool _open;

    /// <summary>
    /// The page shown.
    /// </summary>
    LedgerPage _page;

    /// <summary>
    /// The counter the Counter page shows (its site id); null for the first counter.
    /// </summary>
    string? _counter;

    /// <summary>
    /// The first review the Reviews page shows, from the newest.
    /// </summary>
    int _reviewsFrom;

    /// <summary>
    /// The registered keys: the window's styles and actions.
    /// </summary>
    LedgerKeys _keys = null!;

    /// <summary>
    /// Whether the window is open on <paramref name="page"/>.
    /// </summary>
    public bool Shows(LedgerPage page) => _open && _page == page;

    /// <summary>
    /// Opens the window on <paramref name="page"/> (and, for the Counter page, <paramref name="counter"/>), or closes it when it already shows
    /// that page.
    /// </summary>
    public void Toggle(LedgerPage page, string? counter = null)
    {
        if (Shows(page))
        {
            _open = false;
            return;
        }

        _open = true;
        _page = page;
        _counter = counter ?? _counter;
        _reviewsFrom = 0;
    }

    /// <summary>
    /// Registers the window's styles, actions and control, and puts it in the HUD's overlay.
    /// </summary>
    public void Register(UiRegistration registration)
    {
        _keys = new LedgerKeys(
            registration.RegisterStyle("ledger-window", new UiStyle { Background = new Vector4(0.07f, 0.07f, 0.06f, 0.94f) }),
            registration.RegisterStyle("ledger-title", Text(new Vector4(0.95f, 0.85f, 0.55f, 1), 1.15f)),
            registration.RegisterStyle("ledger-head", Text(new Vector4(0.85f, 0.75f, 0.5f, 1), 1)),
            registration.RegisterStyle("ledger-text", Text(new Vector4(0.92f, 0.92f, 0.88f, 1), 1)),
            registration.RegisterStyle("ledger-hint", Text(new Vector4(0.7f, 0.7f, 0.66f, 1), 0.9f)),
            registration.RegisterStyle("ledger-button", Button(new Vector4(0.24f, 0.21f, 0.16f, 1))),
            registration.RegisterStyle("ledger-tab", Button(new Vector4(0.45f, 0.36f, 0.2f, 1))),
            registration.RegisterAction("ledger-close", _ => _open = false),
            registration.RegisterAction("ledger-open", Open),
            registration.RegisterAction("ledger-price", Price),
            registration.RegisterAction("ledger-tab", Tab),
            registration.RegisterAction("ledger-page", Page),
            registration.Key("ledger"));

        string control = registration.RegisterControl("ledger", _ => Window());
        registration.Contribute(UiSlots.Overlay, frame => frame.CreateControl(control), order: 10);
    }

    /// <summary>
    /// A label style in <paramref name="colour"/> at <paramref name="scale"/>.
    /// </summary>
    static UiStyle Text(Vector4 colour, float scale) => new() { Text = colour, TextScale = scale, TextAlign = "Left VCenter" };

    /// <summary>
    /// A button style on <paramref name="background"/>.
    /// </summary>
    static UiStyle Button(Vector4 background) => new()
    {
        Background = background,
        Hover = new Vector4(0.36f, 0.31f, 0.22f, 1),
        Text = new Vector4(0.95f, 0.92f, 0.85f, 1),
        TextAlign = "Center",
    };

    /// <summary>
    /// The window's node this frame: hidden while closed or before the first world.
    /// </summary>
    UiNode Window()
    {
        if (!_open || system() is not { } bathhouse) return new UiNode(_keys.Window) { Visible = false };

        var rows = new LedgerRows(_keys);
        rows.Title("Bathhouse ledger", [.. Enum.GetValues<LedgerPage>().Select(p => (p.ToString(), p.ToString(), p == _page))]);
        if (_page == LedgerPage.Counter) CounterPage(rows, bathhouse);
        else if (_page == LedgerPage.Guests) GuestPages.Guests(rows, bathhouse.Guests);
        else GuestPages.Reviews(rows, bathhouse.Reviews, _reviewsFrom, _keys.Page);

        return rows.Build();
    }

    #endregion

    #region Counter page

    /// <summary>
    /// The Counter page: check-in, customers, prices and the latest payments and complaints.
    /// </summary>
    void CounterPage(LedgerRows rows, BathhouseSystem bathhouse)
    {
        CheckInRows(rows, bathhouse.CheckIn(_counter ?? bathhouse.FirstCounter));
        CustomerRows(rows, bathhouse.Counter);
        PriceRows(rows, bathhouse.Counter);
        RecentRows(rows, bathhouse.Counter);
    }

    /// <summary>
    /// What check-in needs, and the fix.
    /// </summary>
    static void CheckInRows(LedgerRows rows, CheckInView? view)
    {
        rows.Head("Check-in");
        var (status, hint) = view switch
        {
            null => ("No check-in counter yet.", ""),
            { Need: CheckInNeed.Clerk } => ("Needs a clerk.", "Select one of your people and press Assign selected: Bathhouse clerk on the counter."),
            { Need: CheckInNeed.WarmTub } => ("Needs a warm tub with a free seat.",
                "Switch on a powered steam boiler in this building and keep Water in a store here."),
            { } ready => ($"Ready: customers get {Supplies(ready)}.", "Keep towels on a towel rack and soap on a soap shelf in this building."),
        };
        rows.Text(status);
        if (hint.Length > 0) rows.Hint(hint);
    }

    /// <summary>
    /// The towel and soap a customer would get.
    /// </summary>
    static string Supplies(CheckInView view) => (view.Towel, view.Soap) switch
    {
        (true, true) => "a towel and soap",
        (true, false) => "a towel but no soap",
        (false, true) => "soap but no towel",
        _ => "no towel or soap",
    };

    /// <summary>
    /// The open switch and the customers under way.
    /// </summary>
    void CustomerRows(LedgerRows rows, CounterView view)
    {
        rows.Head("Customers");
        string state = view.Open ? "Open: groups come from 6:00 to 22:00." : "Closed: no new groups come.";
        rows.Switch(state, view.Open ? "Close" : "Open", _keys.Open, view.Open ? "0" : "1");
        rows.Text($"{view.Groups} groups coming or visiting, {view.Visits} customers in. See them on the Guests page.");
    }

    /// <summary>
    /// One line per service: its percent of the suggested price and the price now, with the buttons that step it.
    /// </summary>
    static void PriceRows(LedgerRows rows, CounterView view)
    {
        rows.Head("Prices");
        foreach (var service in Priced)
        {
            int percent = view.Percent(service);
            long cats = (long)Math.Round(Pricing.Suggested(service, TubKind.Medium) * percent / 100);
            string text = $"{service}: {percent}% of suggested, {cats} cats{Unit(service)}";
            rows.Price(service.ToString(), percent, Pricing.Previous(percent), Pricing.Higher(percent), text);
        }

        rows.Hint($"A medium tub visit with a towel and soap costs {view.VisitPrice} cats.");
    }

    /// <summary>
    /// What a service's price buys, after the price.
    /// </summary>
    static string Unit(Service service) => service switch
    {
        Service.Bath => " (medium tub)",
        Service.Tea => " a tea set",
        Service.Massage => " an hour",
        _ => "",
    };

    /// <summary>
    /// The latest payments and complaints.
    /// </summary>
    static void RecentRows(LedgerRows rows, CounterView view)
    {
        rows.Head("Latest customers");
        if (view.Recent.Length == 0) rows.Hint("No customers yet.");
        foreach (string entry in view.Recent) rows.Hint(entry);
    }

    #endregion

    #region Actions

    /// <summary>
    /// The open switch: opens ("1") or closes the bathhouse to customers.
    /// </summary>
    void Open(UiActionContext context) => enqueue(new OpenCommand(context.Argument == "1") { Tick = tick() });

    /// <summary>
    /// A price step: "<c>service:percent</c>" sets the service's price to that percent.
    /// </summary>
    void Price(UiActionContext context)
    {
        string[] parts = context.Argument.Split(':');
        if (parts.Length != 2 || !Enum.TryParse<Service>(parts[0], out var service)) return;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)) return;

        enqueue(new PriceCommand(service, percent) { Tick = tick() });
    }

    /// <summary>
    /// A tab: shows the page it names.
    /// </summary>
    void Tab(UiActionContext context)
    {
        if (!Enum.TryParse<LedgerPage>(context.Argument, out var page)) return;

        _page = page;
        _reviewsFrom = 0;
    }

    /// <summary>
    /// The Reviews page's Newer and Older buttons: shows the reviews from the one the argument numbers.
    /// </summary>
    void Page(UiActionContext context)
    {
        if (int.TryParse(context.Argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)) _reviewsFrom = Math.Max(0, from);
    }

    #endregion
}
