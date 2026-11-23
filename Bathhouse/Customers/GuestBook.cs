namespace Bathhouse.Customers;

/// <summary>
/// The review board's record (docs/bathhouse.md "Guest book"): every finished visit of the last <see cref="KeptHours"/> game hours and the count of
/// guests who ever soaked. Saved in the sidecar (layout 6); not hashed, since nothing in the simulation reads it.
/// </summary>
public sealed class GuestBook
{
    /// <summary>
    /// Game hours a finished visit stays on the board (three game days).
    /// </summary>
    public const double KeptHours = 72;

    /// <summary>
    /// The finished visits, oldest first.
    /// </summary>
    readonly List<GuestRecord> _records = [];

    /// <summary>
    /// The finished visits of the last <see cref="KeptHours"/>, oldest first.
    /// </summary>
    public IReadOnlyList<GuestRecord> Records => _records;

    /// <summary>
    /// Guests who ever soaked here (paid or complained), however long ago.
    /// </summary>
    public int Total { get; private set; }

    /// <summary>
    /// Writes down a finished visit and forgets those older than <see cref="KeptHours"/>.
    /// </summary>
    public void Add(GuestRecord record)
    {
        _records.Add(record);
        if (record.Outcome is GuestOutcome.Paid or GuestOutcome.Complained) Total++;

        Prune(record.Hour);
    }

    /// <summary>
    /// Forgets the visits that ended more than <see cref="KeptHours"/> before <paramref name="now"/>.
    /// </summary>
    public void Prune(double now) => _records.RemoveAll(r => now - r.Hour > KeptHours);

    /// <summary>
    /// Forgets everything (before a save is read).
    /// </summary>
    public void Clear()
    {
        _records.Clear();
        Total = 0;
    }

    /// <summary>
    /// The board's summary at <paramref name="now"/>.
    /// </summary>
    public GuestSummary Summary(double now)
    {
        var recent = _records.Where(r => now - r.Hour <= KeptHours).ToList();
        int soaked = recent.Count(r => r.Outcome is GuestOutcome.Paid or GuestOutcome.Complained);
        double days = recent.Count == 0 ? 1 : Math.Clamp((now - recent[0].Hour) / 24, 1, KeptHours / 24);
        int failed = recent.Count(r => r.Outcome == GuestOutcome.Failed);
        int refused = recent.Count(r => r.Outcome == GuestOutcome.TurnedAway);
        long earned = recent.Sum(r => r.Paid);
        return new GuestSummary(Total, soaked, soaked / days, failed, refused, earned);
    }

    /// <summary>
    /// Writes the record.
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Total);
        w.Write(_records.Count);
        foreach (var record in _records)
        {
            w.Write(record.Hour);
            w.Write(record.Name);
            w.Write(record.Kind);
            w.Write((int)record.Outcome);
            w.Write(record.Paid);
            w.Write(record.Quality);
            w.Write(record.Reason);
        }
    }

    /// <summary>
    /// Reads the record <see cref="Write"/> wrote.
    /// </summary>
    public void Read(BinaryReader r)
    {
        Clear();
        Total = r.ReadInt32();
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            var record = new GuestRecord(r.ReadDouble(), r.ReadString(), r.ReadString(), (GuestOutcome)r.ReadInt32(), r.ReadInt64(), r.ReadSingle(),
                r.ReadString());
            _records.Add(record);
        }
    }
}

/// <summary>
/// The review board's summary: guests who ever soaked, those of the last three days and their daily average (over the days the record covers, one
/// to three), and the failed arrivals, refusals and takings of the last three days.
/// </summary>
public readonly record struct GuestSummary(int Total, int Recent, double PerDay, int Failed, int Refused, long Earned);
