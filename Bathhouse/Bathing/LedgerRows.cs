using Meitou.Game.Ui;

namespace Bathhouse.Bathing;

/// <summary>
/// The ledger's registered style and action keys, and its window's node id.
/// </summary>
public sealed record LedgerKeys(string WindowStyle, string TitleStyle, string HeadStyle, string TextStyle, string HintStyle, string ButtonStyle,
    string TabStyle, string Close, string Open, string Price, string Tab, string Page, string Window);

/// <summary>
/// The ledger window's lines, laid top to bottom inside a panel centred across the screen (docs/bathhouse.md "Panels").
/// </summary>
public sealed class LedgerRows(LedgerKeys keys)
{
    #region Layout

    /// <summary>
    /// The window's width in pixels.
    /// </summary>
    public const float Width = 900;

    /// <summary>
    /// The window's top, below the HUD's top bar.
    /// </summary>
    public const float Top = 90;

    /// <summary>
    /// One line's height in pixels.
    /// </summary>
    public const float Line = 28;

    /// <summary>
    /// A table row's or hint's height in pixels.
    /// </summary>
    public const float Row = 24;

    /// <summary>
    /// The window's inner margin in pixels.
    /// </summary>
    public const float Margin = 14;

    /// <summary>
    /// A small button's width in pixels (the price steps).
    /// </summary>
    const float Small = 36;

    /// <summary>
    /// A tab button's width in pixels.
    /// </summary>
    const float TabWidth = 96;

    /// <summary>
    /// Pixels a character of the text font takes on average, for cutting a cell's text to its column.
    /// </summary>
    const float CharWidth = 7.5f;

    /// <summary>
    /// The width inside the margins.
    /// </summary>
    public const float Inner = Width - 2 * Margin;

    /// <summary>
    /// The nodes so far.
    /// </summary>
    readonly List<UiNode> _nodes = [];

    /// <summary>
    /// The top of the next line.
    /// </summary>
    float _top = Margin;

    #endregion

    #region Lines

    /// <summary>
    /// The title, a tab button per page (the shown one highlighted) and the Close button.
    /// </summary>
    public void Title(string title, IReadOnlyList<(string Caption, string Page, bool Shown)> tabs)
    {
        float tabsLeft = Width - Margin - 90 - tabs.Count * (TabWidth + 6);
        Label(title, keys.TitleStyle, tabsLeft - Margin - 8, Margin, Line);
        for (int k = 0; k < tabs.Count; k++)
        {
            var (caption, page, shown) = tabs[k];
            var tab = Button(caption, keys.Tab, page, TabWidth, tabsLeft + k * (TabWidth + 6), true, shown ? keys.TabStyle : keys.ButtonStyle);
            Add(tab);
        }

        Add(Button("Close", keys.Close, "", 80, Width - Margin - 80, true, keys.ButtonStyle));
        _top += Line + 4;
    }

    /// <summary>
    /// A section heading.
    /// </summary>
    public void Head(string text)
    {
        _top += 6;
        Label(text, keys.HeadStyle, Inner, Margin, Line);
        _top += Line;
    }

    /// <summary>
    /// A line of text.
    /// </summary>
    public void Text(string text)
    {
        Label(text, keys.TextStyle, Inner, Margin, Line);
        _top += Line;
    }

    /// <summary>
    /// A dimmer, smaller line of text.
    /// </summary>
    public void Hint(string text)
    {
        Label(text, keys.HintStyle, Inner, Margin, Row);
        _top += Row;
    }

    /// <summary>
    /// A table row: each cell cut to its column's width, in the text style or the dimmer <paramref name="header"/> style.
    /// </summary>
    public void Cells(IReadOnlyList<string> cells, IReadOnlyList<float> widths, bool header = false)
    {
        float left = Margin;
        for (int k = 0; k < cells.Count; k++)
        {
            Label(Fit(cells[k], widths[k] - 8), header ? keys.HintStyle : keys.TextStyle, widths[k] - 8, left, Row);
            left += widths[k];
        }

        _top += Row;
    }

    /// <summary>
    /// A line of text with a button at its right that runs <paramref name="action"/> with <paramref name="argument"/>.
    /// </summary>
    public void Switch(string text, string caption, string action, string argument)
    {
        Label(text, keys.TextStyle, Inner - 110, Margin, Line);
        Add(Button(caption, action, argument, 100, Width - Margin - 100, true, keys.ButtonStyle));
        _top += Line;
    }

    /// <summary>
    /// A row of buttons from the left, each (caption, action, argument, enabled), then <paramref name="text"/>.
    /// </summary>
    public void Buttons(IReadOnlyList<(string Caption, string Action, string Argument, bool Enabled)> buttons, string text)
    {
        float left = Margin;
        foreach (var (caption, action, argument, enabled) in buttons)
        {
            Add(Button(caption, action, argument, 90, left, enabled, keys.ButtonStyle));
            left += 96;
        }

        Label(text, keys.HintStyle, Width - Margin - left - 8, left + 8, Line);
        _top += Line;
    }

    /// <summary>
    /// A price line: lower and raise buttons (disabled at the end of the steps), then the text.
    /// </summary>
    public void Price(string service, int percent, int lower, int higher, string text)
    {
        Add(Button("-", keys.Price, $"{service}:{lower}", Small, Margin, lower != percent, keys.ButtonStyle));
        Add(Button("+", keys.Price, $"{service}:{higher}", Small, Margin + Small + 4, higher != percent, keys.ButtonStyle));

        float left = Margin + 2 * Small + 14;
        Label(text, keys.TextStyle, Width - Margin - left, left, Line);
        _top += Line;
    }

    /// <summary>
    /// The window: its lines inside a panel centred across the screen, below the top bar.
    /// </summary>
    public UiNode Build()
    {
        float height = _top + Margin;
        var placement = new UiPlacement(Width, height, Left: new UiLength(-Width / 2, 0.5f), Top: Top);
        var window = new UiNode(keys.Window) { Placement = placement, Style = keys.WindowStyle };
        window.Children.AddRange(_nodes);
        return window;
    }

    #endregion

    #region Nodes

    /// <summary>
    /// <paramref name="text"/> cut with an ellipsis to fit <paramref name="width"/> pixels.
    /// </summary>
    public static string Fit(string text, float width)
    {
        int most = Math.Max(1, (int)(width / CharWidth));
        return text.Length <= most ? text : text[..(most - 1)] + "…";
    }

    /// <summary>
    /// A label at the current line.
    /// </summary>
    void Label(string text, string style, float width, float left, float height) => Add(new UiNode(Id("label"), UiNodeKind.Label)
    {
        Placement = new UiPlacement(width, height, Left: left, Top: _top), Style = style, Text = text,
    });

    /// <summary>
    /// A button at the current line; a disabled one never runs its action.
    /// </summary>
    UiNode Button(string caption, string action, string argument, float width, float left, bool enabled, string style) =>
        new(Id("button"), UiNodeKind.Button)
        {
            Placement = new UiPlacement(width, Line - 4, Left: left, Top: _top + 2), Style = style, Text = caption, Action = action,
            Argument = argument, Enabled = enabled,
        };

    /// <summary>
    /// Adds a node.
    /// </summary>
    void Add(UiNode node) => _nodes.Add(node);

    /// <summary>
    /// A node id unique in the window.
    /// </summary>
    string Id(string kind) => $"{keys.Window}.{kind}{_nodes.Count}";

    #endregion
}
