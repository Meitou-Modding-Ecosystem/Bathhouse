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
        ledger.Toggle("counter");
        Assert.NotEmpty(Arrange(host));
        ledger.Toggle("counter");
        Assert.Empty(Arrange(host));
    }

    [Fact]
    public void Every_line_fits_inside_the_window_on_a_small_screen()
    {
        var (host, ledger, _) = Host();
        ledger.Toggle("counter");

        var controls = Arrange(host);
        var window = controls.Single(c => c.Type == "Widget");

        Assert.True(window.Bounds.Y + window.Bounds.Height <= ScreenHeight);
        Assert.All(controls, c => Assert.True(Inside(window.Bounds, c.Bounds), $"{c.Caption} at {c.Bounds}"));
    }

    [Fact]
    public void The_buttons_queue_the_open_switch_and_the_price_steps()
    {
        var (host, ledger, sent) = Host();
        ledger.Toggle("counter");
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
        ledger.Toggle("counter");

        host.Dispatch(Arrange(host).Single(c => c.Caption == "Close" && c.Argument.Length == 0));

        Assert.Empty(Arrange(host));
    }

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
