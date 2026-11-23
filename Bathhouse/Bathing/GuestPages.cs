using System.Globalization;
using Bathhouse.Customers;

namespace Bathhouse.Bathing;

/// <summary>
/// The ledger's Guests and Reviews pages (docs/bathhouse.md "Guest book"), laid from the system's published views.
/// </summary>
public static class GuestPages
{
    /// <summary>
    /// The most guests the Guests page lists.
    /// </summary>
    public const int MostGuests = 14;

    /// <summary>
    /// The reviews the Reviews page lists at once.
    /// </summary>
    public const int ReviewsShown = 16;

    /// <summary>
    /// The guest table's columns: name, kind, what it does, time left, quality so far, issues.
    /// </summary>
    static readonly float[] GuestColumns = [170, 120, 220, 80, 70, LedgerRows.Inner - 660];

    /// <summary>
    /// The review table's columns: when, guest, kind, outcome, quality, why.
    /// </summary>
    static readonly float[] ReviewColumns = [110, 160, 110, 130, 70, LedgerRows.Inner - 580];

    #region Guests

    /// <summary>
    /// The groups on their way, then a line per guest whose visit runs.
    /// </summary>
    public static void Guests(LedgerRows rows, GuestsNow now)
    {
        rows.Head("On the way");
        if (now.OnTheWay.Length == 0) rows.Hint("No groups on their way.");
        foreach (string line in now.OnTheWay) rows.Text(line);

        rows.Head("Guests");
        if (now.Guests.Length == 0)
        {
            rows.Hint("No guests in the bathhouse.");
            return;
        }

        rows.Hint(Demands(now.Guests));
        rows.Cells(["Guest", "Kind", "Doing", "Time left", "Quality", "Costing quality"], GuestColumns, header: true);
        foreach (var guest in now.Guests.Take(MostGuests))
            rows.Cells([guest.Name, guest.Kind, guest.Status, guest.Left, Percent(guest.Quality), guest.Issues], GuestColumns);

        if (now.Guests.Length > MostGuests) rows.Hint($"and {now.Guests.Length - MostGuests} more.");
    }

    /// <summary>
    /// What the guests' kinds demand: full pay from 80% quality, and each kind's complaint threshold.
    /// </summary>
    static string Demands(GuestView[] guests)
    {
        var kinds = guests.Select(g => g.Kind).Distinct().Select(k => CustomerTypes.All.FirstOrDefault(t => t.Name == k)).OfType<CustomerType>();
        string complain = string.Join(", ", kinds.Select(k => $"{k.Name} under {k.Demand:P0}"));
        return $"Guests pay in full from {Quality.FullPay:P0} quality, less below it, and complain: {complain}.";
    }

    #endregion

    #region Reviews

    /// <summary>
    /// The last three days' totals, then the reviews from <paramref name="from"/> with Newer and Older buttons (<paramref name="page"/>).
    /// </summary>
    public static void Reviews(LedgerRows rows, ReviewsNow now, int from, string page)
    {
        var sum = now.Summary;
        rows.Head("Last three days");
        rows.Text($"{sum.Recent} guests soaked ({sum.PerDay:0.0} a day), {sum.Total} since the bathhouse opened; {sum.Earned} cats taken.");
        rows.Text($"{sum.Failed} failed to arrive or get a soak, {sum.Refused} refused the price.");

        rows.Head("Reviews");
        if (now.Records.Length == 0)
        {
            rows.Hint("No guests in the last three days.");
            return;
        }

        int start = Math.Min(from, Math.Max(0, now.Records.Length - 1));
        rows.Cells(["When", "Guest", "Kind", "Outcome", "Quality", "Why"], ReviewColumns, header: true);
        foreach (var record in now.Records.Skip(start).Take(ReviewsShown)) rows.Cells(Cells(record), ReviewColumns);

        PageButtons(rows, now.Records.Length, start, page);
    }

    /// <summary>
    /// A review's cells.
    /// </summary>
    static string[] Cells(GuestRecord record) =>
        [When(record.Hour), record.Name, record.Kind, Outcome(record), Percent(record.Quality), record.Reason];

    /// <summary>
    /// The Newer and Older buttons and which reviews show.
    /// </summary>
    static void PageButtons(LedgerRows rows, int count, int start, string page)
    {
        int end = Math.Min(count, start + ReviewsShown);
        string newer = Math.Max(0, start - ReviewsShown).ToString(CultureInfo.InvariantCulture);
        string older = end.ToString(CultureInfo.InvariantCulture);
        rows.Buttons([("Newer", page, newer, start > 0), ("Older", page, older, end < count)], $"Showing {start + 1} to {end} of {count}, newest first.");
    }

    /// <summary>
    /// How a visit ended, in a few words.
    /// </summary>
    static string Outcome(GuestRecord record) => record.Outcome switch
    {
        GuestOutcome.Paid => $"paid {record.Paid} cats",
        GuestOutcome.Complained => "complained",
        GuestOutcome.TurnedAway => "refused the price",
        _ => "failed to arrive",
    };

    /// <summary>
    /// A game hour as a day and a time of day.
    /// </summary>
    public static string When(double hour)
    {
        int day = (int)Math.Floor(hour / 24);
        double inDay = hour - day * 24;
        return $"Day {day} {(int)inDay:00}:{(int)(inDay % 1 * 60):00}";
    }

    #endregion

    /// <summary>
    /// A quality as a percent; a dash for none.
    /// </summary>
    static string Percent(float quality) => float.IsNaN(quality) ? "-" : quality.ToString("P0", CultureInfo.InvariantCulture);
}
