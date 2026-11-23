using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// Where a customer's visit stands.
/// </summary>
public enum VisitPhase
{
    /// <summary>
    /// Walking to the check-in counter or waiting there.
    /// </summary>
    Arriving,

    /// <summary>
    /// Checked in and out to soak.
    /// </summary>
    Soaking,

    /// <summary>
    /// Paid, complained or turned away; leaves with its group.
    /// </summary>
    Done,

    /// <summary>
    /// Soaked and lying on a massage bed (after <see cref="Done"/> in number, as saves keep it).
    /// </summary>
    Massage,

    /// <summary>
    /// Soaked and going to a tea table, or sitting on the floor at it drinking (from layout 7).
    /// </summary>
    Tea,
}

/// <summary>
/// One customer's visit: who, its group and kind, where it stands, when it reached the counter, how long it waited and the price it agreed to.
/// </summary>
public sealed class Visit
{
    /// <summary>
    /// The customer's lasting id.
    /// </summary>
    public required LastingId Customer { get; init; }

    /// <summary>
    /// Its group's tag.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// Its kind's key.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Where the visit stands.
    /// </summary>
    public VisitPhase Phase { get; set; }

    /// <summary>
    /// The game hour it reached the counter; NaN before.
    /// </summary>
    public double ArrivedAt { get; set; } = double.NaN;

    /// <summary>
    /// Game hours it waited at the counter before checking in.
    /// </summary>
    public double Waited { get; set; }

    /// <summary>
    /// The price it agreed to at check-in, in cats.
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Whether it has said its line on getting into the tub (not saved).
    /// </summary>
    public bool InTub { get; set; }

    /// <summary>
    /// Whether its tub was dirty when it checked in.
    /// </summary>
    public bool DirtyTub { get; set; }

    /// <summary>
    /// Whether it got a towel at check-in.
    /// </summary>
    public bool Towel { get; set; } = true;

    /// <summary>
    /// Whether it got soap at check-in.
    /// </summary>
    public bool Soap { get; set; } = true;

    /// <summary>
    /// What it owes for the soak and tea, paid when its massage ends.
    /// </summary>
    public long Owed { get; set; }

    /// <summary>
    /// The massage bed's site id; null for none.
    /// </summary>
    public string? Bed { get; set; }

    /// <summary>
    /// Whether it lies on its massage bed (not saved: a loaded customer lies down again).
    /// </summary>
    public bool Lying { get; set; }

    /// <summary>
    /// Game hours massaged so far.
    /// </summary>
    public double Massaged { get; set; }

    /// <summary>
    /// Game hours lying on the bed with no attendant.
    /// </summary>
    public double BedWait { get; set; }

    /// <summary>
    /// What the massage has earned so far, in cats.
    /// </summary>
    public double MassagePay { get; set; }

    /// <summary>
    /// The soak's quality once it ended, for the review board; NaN before, and after a load (not saved).
    /// </summary>
    public float Quality { get; set; } = float.NaN;

    /// <summary>
    /// The tea table it drinks at while on <see cref="VisitPhase.Tea"/>.
    /// </summary>
    public string? Table { get; set; }

    /// <summary>
    /// Whether it sits at its tea table.
    /// </summary>
    public bool Sitting { get; set; }

    /// <summary>
    /// Game hours it has sat drinking.
    /// </summary>
    public double Drunk { get; set; }

    /// <summary>
    /// Game hours it has spent getting to its tea table.
    /// </summary>
    public double TeaWait { get; set; }
}

/// <summary>
/// A group of customers the mod sent: its platoon's tag (<c>Platoon.ModTag</c>, saved by the game, how the group finds its platoon after a load),
/// its kind, the counter it is sent to, when it was spawned and whether its members have been enlisted as visits.
/// </summary>
public sealed class CustomerGroup
{
    /// <summary>
    /// The platoon's tag.
    /// </summary>
    public required string Tag { get; init; }

    /// <summary>
    /// Its kind's key.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The counter's site id.
    /// </summary>
    public required string Counter { get; init; }

    /// <summary>
    /// The game hour it was spawned.
    /// </summary>
    public required double SpawnedAt { get; init; }

    /// <summary>
    /// Whether its members have visits.
    /// </summary>
    public bool Enlisted { get; set; }
}

/// <summary>
/// How a guest's visit ended, as the review board lists it.
/// </summary>
public enum GuestOutcome
{
    /// <summary>
    /// Soaked and paid.
    /// </summary>
    Paid,

    /// <summary>
    /// Soaked and left without paying, complaining about the quality.
    /// </summary>
    Complained,

    /// <summary>
    /// Reached the counter and would not pay the price.
    /// </summary>
    TurnedAway,

    /// <summary>
    /// Set out for the bathhouse but never got a soak: gave up, was attacked on the way, found it closed or waited too long at the counter.
    /// </summary>
    Failed,
}

/// <summary>
/// One guest's finished visit on the review board: the game hour it ended, who (a group's kind for a group lost on the road) and of what kind, how
/// it ended, what it paid, its quality (NaN when it never soaked) and why.
/// </summary>
public sealed record GuestRecord(double Hour, string Name, string Kind, GuestOutcome Outcome, long Paid, float Quality, string Reason);
