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
