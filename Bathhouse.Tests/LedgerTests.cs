using Bathhouse.Bathing;
using Bathhouse.Customers;
using Meitou.Data;
using Meitou.Data.Ui;
using Meitou.Game.Ui;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Bathhouse.Tests;

/// <summary>
/// The counter's ledger window (docs/bathhouse.md "Panels"): hidden until opened, every line inside the window, and its buttons queue the open and
/// price commands.
/// </summary>
public sealed class LedgerTests
{
    /// <summary>
    /// The screen the window is arranged on (a small one).
    /// </summary>
    const int ScreenWidth = 1280, ScreenHeight = 720;

    [Fact]
    public void The_window_shows_only_while_open()
    {
        var (host, ledger, _) = Host();

        Assert.Empty(Arrange(host));
        ledger.Toggle(LedgerPage.Counter, "counter");
        Assert.NotEmpty(Arrange(host));
        ledger.Toggle(LedgerPage.Counter, "counter");
        Assert.Empty(Arrange(host));
    }

    [Fact]
    public void Every_line_fits_inside_the_window_on_a_small_screen()
    {
        var (host, ledger, _) = Host();
        ledger.Toggle(LedgerPage.Counter, "counter");

        var controls = Arrange(host);
        var window = controls.Single(c => c.Type == "Widget");

        Assert.True(window.Bounds.Y + window.Bounds.Height <= ScreenHeight);
        Assert.All(controls, c => Assert.True(Inside(window.Bounds, c.Bounds), $"{c.Caption} at {c.Bounds}"));
    }

    [Fact]
    public void The_buttons_queue_the_open_switch_and_the_price_steps()
    {
        var (host, ledger, sent) = Host();
        ledger.Toggle(LedgerPage.Counter, "counter");
        var controls = Arrange(host);

        host.Dispatch(controls.Single(c => c.Caption == "Close" && c.Argument == "0"));
        host.Dispatch(controls.First(c => c.Caption == "+" && c.Argument.StartsWith("Soap:")));
        host.Dispatch(controls.First(c => c.Caption == "-" && c.Argument.StartsWith("Bath:")));

        Assert.Equal(new OpenCommand(false) { Tick = 7 }, sent[0]);
        Assert.Equal(new PriceCommand(Service.Soap, 125) { Tick = 7 }, sent[1]);
        Assert.Equal(new PriceCommand(Service.Bath, 75) { Tick = 7 }, sent[2]);
    }

    [Fact]
    public void The_close_button_hides_the_window()
    {
        var (host, ledger, _) = Host();
        ledger.Toggle(LedgerPage.Counter, "counter");

        host.Dispatch(Arrange(host).Single(c => c.Caption == "Close" && c.Argument.Length == 0));

        Assert.Empty(Arrange(host));
    }

    [Theory]
    [InlineData(LedgerPage.Guests)]
    [InlineData(LedgerPage.Reviews)]
    public void A_tab_shows_its_page(LedgerPage page)
    {
        var (host, ledger, _) = Host();
        ledger.Toggle(LedgerPage.Counter, "counter");

        host.Dispatch(Arrange(host).Single(c => c.Argument == page.ToString()));

        Assert.True(ledger.Shows(page));
        Assert.Contains(Arrange(host), c => c.Caption == (page == LedgerPage.Guests ? "On the way" : "Last three days"));
    }

    [Fact]
    public void The_guests_page_fits_a_small_screen_when_full()
    {
        var guest = new GuestView("Trader Boss Kinnersley the Third", "Traders", "soaking in the medium tub", "1.4 h", 0.75f,
            "lukewarm water, waited, dirty tub, no towel, no soap, no robe");
        var now = new GuestsNow(["Traders on their way: set out 1.0 h ago, give up after 23.0 h more."] , [.. Enumerable.Repeat(guest, 20)]);
        var rows = new LedgerRows(Keys());
        rows.Title("Bathhouse ledger", [("Counter", "Counter", false), ("Guests", "Guests", true), ("Reviews", "Reviews", false)]);

        GuestPages.Guests(rows, now);

        Assert.True(Fits(rows.Build()));
    }

    [Fact]
    public void The_reviews_page_fits_a_small_screen_and_pages_through_three_days()
    {
        var record = new GuestRecord(50, "Guild Private", "Traders", GuestOutcome.Paid, 52, 0.9f, "quality 90% (heat -0%, wait -10%)");
        var now = new ReviewsNow(new GuestSummary(40, 40, 13.3, 2, 1, 2000), [.. Enumerable.Repeat(record, 40)]);
        var rows = new LedgerRows(Keys());
        rows.Title("Bathhouse ledger", [("Counter", "Counter", false), ("Guests", "Guests", false), ("Reviews", "Reviews", true)]);

        GuestPages.Reviews(rows, now, from: 16, page: "page");
        var window = rows.Build();

        Assert.True(Fits(window));
        Assert.Contains(window.Children, n => n.Text == "Showing 17 to 32 of 40, newest first.");
        Assert.Contains(window.Children, n => n.Text == "Newer" && n.Argument == "0" && n.Enabled);
        Assert.Contains(window.Children, n => n.Text == "Older" && n.Argument == "32" && n.Enabled);
    }

    [Theory]
    [InlineData(0, "Day 0 00:00")]
    [InlineData(51.5, "Day 2 03:30")]
    public void A_review_shows_its_day_and_time(double hour, string text) => Assert.Equal(text, GuestPages.When(hour));

    /// <summary>
    /// Keys for laying pages without a host.
    /// </summary>
    static LedgerKeys Keys() => new("w", "t", "h", "x", "i", "b", "tab", "close", "open", "price", "tab", "page", "ledger");

    /// <summary>
    /// Whether a window fits below its top on the small screen.
    /// </summary>
    static bool Fits(UiNode window) => LedgerRows.Top + window.Placement.Height.Pixels <= ScreenHeight;

    /// <summary>
    /// A host with the bathhouse's interface over a fresh system, and the commands its ledger queues.
    /// </summary>
    static (UiHost Host, CounterLedger Ledger, List<SimCommand> Sent) Host()
    {
        var system = new BathhouseSystem(RngPurpose.Test, new PopulationData { Db = new GameDatabase(), Sites = [], Factions = [] });
        var sent = new List<SimCommand>();
        var ledger = new CounterLedger(() => system, sent.Add, () => 7);
        var host = new UiHost();
        host.Register(new BathhouseUi(() => system, ledger));
        return (host, ledger, sent);
    }

    /// <summary>
    /// The host's controls on the small screen.
    /// </summary>
    static IReadOnlyList<UiControl> Arrange(UiHost host) =>
        host.Arrange(UiMode.Meitou, ScreenWidth, ScreenHeight, new UiBindings(),
            new Dictionary<string, UiRect> { [UiSlots.Overlay] = new(0, 0, ScreenWidth, ScreenHeight) });

    /// <summary>
    /// Whether <paramref name="inner"/> lies within <paramref name="outer"/>.
    /// </summary>
    static bool Inside(UiRect outer, UiRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y && inner.X + inner.Width <= outer.X + outer.Width && inner.Y + inner.Height <= outer.Y + outer.Height;
}
