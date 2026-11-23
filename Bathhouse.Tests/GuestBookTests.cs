using Bathhouse.Customers;

namespace Bathhouse.Tests;

/// <summary>
/// The review board's record (docs/bathhouse.md "Guest book"): three game days kept, the totals and the sidecar round trip.
/// </summary>
public sealed class GuestBookTests
{
    [Fact]
    public void Visits_older_than_three_days_are_forgotten_but_still_counted()
    {
        var book = new GuestBook();
        book.Add(Record(1, GuestOutcome.Paid));
        book.Add(Record(80, GuestOutcome.Complained));

        Assert.Single(book.Records);
        Assert.Equal(2, book.Total);
    }

    [Fact]
    public void The_summary_counts_soaks_failures_refusals_and_takings_and_averages_per_day()
    {
        var book = new GuestBook();
        book.Add(Record(0, GuestOutcome.Paid, paid: 40));
        book.Add(Record(10, GuestOutcome.Complained));
        book.Add(Record(30, GuestOutcome.Failed));
        book.Add(Record(40, GuestOutcome.TurnedAway));
        book.Add(Record(48, GuestOutcome.Paid, paid: 60));

        var summary = book.Summary(48);

        Assert.Equal(new GuestSummary(3, 3, 1.5, 1, 1, 100), summary);
    }

    [Fact]
    public void A_new_bathhouse_averages_over_at_least_one_day()
    {
        var book = new GuestBook();
        book.Add(Record(5, GuestOutcome.Paid));
        book.Add(Record(6, GuestOutcome.Paid));

        Assert.Equal(2, book.Summary(6).PerDay);
    }

    [Fact]
    public void The_book_reads_back_what_it_wrote()
    {
        var book = new GuestBook();
        book.Add(Record(3, GuestOutcome.Paid, paid: 52));
        book.Add(new GuestRecord(4, "Shek group", "Shek", GuestOutcome.Failed, 0, float.NaN, "never made it, likely attacked on the road"));
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) book.Write(w);
        stream.Position = 0;

        var read = new GuestBook();
        using (var r = new BinaryReader(stream)) read.Read(r);

        Assert.Equal(book.Total, read.Total);
        Assert.Equal(book.Records, read.Records);
    }

    /// <summary>
    /// A visit that ended at <paramref name="hour"/>.
    /// </summary>
    static GuestRecord Record(double hour, GuestOutcome outcome, long paid = 0) =>
        new(hour, "Guild Private", "Traders", outcome, paid, outcome is GuestOutcome.Paid or GuestOutcome.Complained ? 0.9f : float.NaN, "why");
}
