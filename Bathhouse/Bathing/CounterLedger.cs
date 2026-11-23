using System.Globalization;
using System.Numerics;
using Bathhouse.Customers;
using Meitou.Game.Ui;
using Meitou.Simulation;

namespace Bathhouse.Bathing;

/// <summary>
/// The check-in counter's ledger window (docs/bathhouse.md "Panels"): what check-in needs and how to fix it, the open switch and the customers under
/// way, each service's price with buttons to lower and raise it, and the latest payments and complaints. The counter's "Bathhouse ledger" button
/// opens and closes it; it reads only the system's published views on the host's thread and changes the world through commands.
/// </summary>
public sealed class CounterLedger(Func<BathhouseSystem?> system, Action<SimCommand> enqueue, Func<long> tick)
{
    #region Window

    /// <summary>
    /// The window's width in pixels.
    /// </summary>
    const float Width = 640;

    /// <summary>
    /// One line's height in pixels.
    /// </summary>
    const float Line = 28;

    /// <summary>
    /// The window's inner margin in pixels.
    /// </summary>
    const float Margin = 14;

    /// <summary>
    /// A small button's width in pixels (the price steps).
    /// </summary>
    const float Small = 36;

    /// <summary>
    /// The services the price lines show, in order.
    /// </summary>
    static readonly Service[] Priced = [Service.Bath, Service.Towel, Service.Soap, Service.Tea, Service.Massage];

    /// <summary>
    /// The counter the window shows (its site id), null while it is closed. Written and read on the host's thread.
    /// </summary>
    string? _counter;

    /// <summary>
    /// The registered keys: the window's styles and actions.
    /// </summary>
    Keys _keys = null!;

    /// <summary>
    /// Whether the window shows <paramref name="counter"/>.
    /// </summary>
    public bool Shows(string counter) => _counter == counter;

    /// <summary>
    /// Opens the window on <paramref name="counter"/>, or closes it when it already shows that counter.
    /// </summary>
    public void Toggle(string counter) => _counter = _counter == counter ? null : counter;

    /// <summary>
    /// Registers the window's styles, actions and control, and puts it in the HUD's overlay.
    /// </summary>
    public void Register(UiRegistration registration)
    {
        _keys = new Keys(
            registration.RegisterStyle("ledger-window", new UiStyle { Background = new Vector4(0.07f, 0.07f, 0.06f, 0.94f) }),
            registration.RegisterStyle("ledger-title", Text(new Vector4(0.95f, 0.85f, 0.55f, 1), 1.15f)),
            registration.RegisterStyle("ledger-head", Text(new Vector4(0.85f, 0.75f, 0.5f, 1), 1)),
            registration.RegisterStyle("ledger-text", Text(new Vector4(0.92f, 0.92f, 0.88f, 1), 1)),
            registration.RegisterStyle("ledger-hint", Text(new Vector4(0.7f, 0.7f, 0.66f, 1), 0.9f)),
            registration.RegisterStyle("ledger-button", Button()),
            registration.RegisterAction("ledger-close", _ => _counter = null),
            registration.RegisterAction("ledger-open", Open),
            registration.RegisterAction("ledger-price", Price),
            registration.Key("ledger"));

        string control = registration.RegisterControl("ledger", _ => Window());
        registration.Contribute(UiSlots.Overlay, frame => frame.CreateControl(control), order: 10);
    }

    /// <summary>
    /// A label style in <paramref name="colour"/> at <paramref name="scale"/>.
    /// </summary>
    static UiStyle Text(Vector4 colour, float scale) => new() { Text = colour, TextScale = scale, TextAlign = "Left VCenter" };

    /// <summary>
    /// The buttons' style.
    /// </summary>
    static UiStyle Button() => new()
    {
        Background = new Vector4(0.24f, 0.21f, 0.16f, 1),
        Hover = new Vector4(0.36f, 0.31f, 0.22f, 1),
        Text = new Vector4(0.95f, 0.92f, 0.85f, 1),
        TextAlign = "Center",
    };

    /// <summary>
    /// The window's node this frame: hidden while closed or before the first world.
    /// </summary>
    UiNode Window()
    {
        if (_counter is not { } counter || system() is not { } bathhouse) return new UiNode(_keys.Window) { Visible = false };

        var rows = new Rows(this);
        rows.Title();
        CheckInRows(rows, bathhouse.CheckIn(counter));
        CustomerRows(rows, bathhouse.Counter);
        PriceRows(rows, bathhouse.Counter);
        RecentRows(rows, bathhouse.Counter);
        return rows.Build(_keys.Window, _keys.WindowStyle);
    }

    #endregion

    #region Sections

    /// <summary>
    /// What check-in needs, and the fix.
    /// </summary>
    static void CheckInRows(Rows rows, CheckInView? view)
    {
        rows.Head("Check-in");
        var (status, hint) = view switch
        {
            null => ("Starting.", ""),
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
    static void CustomerRows(Rows rows, CounterView view)
    {
        rows.Head("Customers");
        string state = view.Open ? "Open: groups come from 6:00 to 22:00." : "Closed: no new groups come.";
        rows.Switch(state, view.Open ? "Close" : "Open", view.Open ? "0" : "1");
        rows.Text($"{view.Groups} groups coming or visiting, {view.Visits} customers in.");
    }

    /// <summary>
    /// One line per service: its percent of the suggested price and the price now, with the buttons that step it.
    /// </summary>
    static void PriceRows(Rows rows, CounterView view)
    {
        rows.Head("Prices");
        foreach (var service in Priced)
        {
            int percent = view.Percent(service);
            long cats = (long)Math.Round(Pricing.Suggested(service, TubKind.Medium) * percent / 100);
            rows.Price(service, percent, $"{service}: {percent}% of suggested, {cats} cats{Unit(service)}");
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
    static void RecentRows(Rows rows, CounterView view)
    {
        rows.Head("Recent customers");
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

    #endregion

    /// <summary>
    /// The registered style and action keys, and the window's node id.
    /// </summary>
    sealed record Keys(string WindowStyle, string TitleStyle, string HeadStyle, string TextStyle, string HintStyle, string ButtonStyle,
        string Close, string Open, string Price, string Window);

    /// <summary>
    /// The window's lines, laid top to bottom.
    /// </summary>
    sealed class Rows(CounterLedger ledger)
    {
        /// <summary>
        /// The nodes so far.
        /// </summary>
        readonly List<UiNode> _nodes = [];

        /// <summary>
        /// The top of the next line.
        /// </summary>
        float _top = Margin;

        /// <summary>
        /// The keys.
        /// </summary>
        Keys K => ledger._keys;

        /// <summary>
        /// The title and the Close button.
        /// </summary>
        public void Title()
        {
            Label("Bathhouse ledger", K.TitleStyle, Width - 2 * Margin - 90);
            Add(new UiNode(Id("close"), UiNodeKind.Button)
            {
                Placement = new UiPlacement(80, Line - 4, Left: Width - Margin - 80, Top: _top + 2), Style = K.ButtonStyle, Text = "Close",
                Action = K.Close,
            });
            _top += Line + 4;
        }

        /// <summary>
        /// A section heading.
        /// </summary>
        public void Head(string text)
        {
            _top += 6;
            Label(text, K.HeadStyle, Width - 2 * Margin);
            _top += Line;
        }

        /// <summary>
        /// A line of text.
        /// </summary>
        public void Text(string text)
        {
            Label(text, K.TextStyle, Width - 2 * Margin);
            _top += Line;
        }

        /// <summary>
        /// A dimmer, smaller line of text.
        /// </summary>
        public void Hint(string text)
        {
            Label(text, K.HintStyle, Width - 2 * Margin);
            _top += Line - 4;
        }

        /// <summary>
        /// A line of text with a button at its right that runs <see cref="Keys.Open"/> with <paramref name="argument"/>.
        /// </summary>
        public void Switch(string text, string caption, string argument)
        {
            Label(text, K.TextStyle, Width - 2 * Margin - 110);
            Add(Button(caption, K.Open, argument, 100, Width - Margin - 100, true));
            _top += Line;
        }

        /// <summary>
        /// A price line: lower and raise buttons, then the text; a button at the end of the steps is disabled.
        /// </summary>
        public void Price(Service service, int percent, string text)
        {
            int lower = Pricing.Previous(percent), higher = Pricing.Higher(percent);
            Add(Button("-", K.Price, $"{service}:{lower}", Small, Margin, lower != percent));
            Add(Button("+", K.Price, $"{service}:{higher}", Small, Margin + Small + 4, higher != percent));

            float left = Margin + 2 * Small + 14;
            Add(new UiNode(Id("label"), UiNodeKind.Label)
            {
                Placement = new UiPlacement(Width - Margin - left, Line, Left: left, Top: _top), Style = K.TextStyle, Text = text,
            });
            _top += Line;
        }

        /// <summary>
        /// The window: its lines inside a panel centred across the screen, below the top bar.
        /// </summary>
        public UiNode Build(string id, string style)
        {
            float height = _top + Margin;
            var window = new UiNode(id) { Placement = new UiPlacement(Width, height, Left: new UiLength(-Width / 2, 0.5f), Top: 90), Style = style };
            window.Children.AddRange(_nodes);
            return window;
        }

        /// <summary>
        /// A label across <paramref name="width"/> at the current line.
        /// </summary>
        void Label(string text, string style, float width) => Add(new UiNode(Id("label"), UiNodeKind.Label)
        {
            Placement = new UiPlacement(width, Line, Left: Margin, Top: _top), Style = style, Text = text,
        });

        /// <summary>
        /// A button at the current line; a disabled one never runs its action.
        /// </summary>
        UiNode Button(string caption, string action, string argument, float width, float left, bool enabled) => new(Id("button"), UiNodeKind.Button)
        {
            Placement = new UiPlacement(width, Line - 4, Left: left, Top: _top + 2), Style = K.ButtonStyle, Text = caption, Action = action,
            Argument = argument, Enabled = enabled,
        };

        /// <summary>
        /// Adds a node.
        /// </summary>
        void Add(UiNode node) => _nodes.Add(node);

        /// <summary>
        /// A node id unique in the window.
        /// </summary>
        string Id(string kind) => $"{K.Window}.{kind}{_nodes.Count}";
    }
}
