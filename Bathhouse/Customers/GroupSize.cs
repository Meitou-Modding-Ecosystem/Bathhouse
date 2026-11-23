using Bathhouse.Records;
using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// How many bathers come in a group (docs/bathhouse.md "Customers"): 1 to a cap, rolled; the cap is the start setting while the bathhouse is new,
/// rising with reputation, never past the building's tub seats plus 2.
/// </summary>
public static class GroupSize
{
    /// <summary>
    /// What each reputation level past 1 adds to the cap.
    /// </summary>
    public const int PerReputation = 2;

    /// <summary>
    /// How many more than the building's tub seats a group may be.
    /// </summary>
    public const int PastSeats = 2;

    /// <summary>
    /// The roll's counter salt.
    /// </summary>
    const ulong Salt = 0x6205_517E;

    /// <summary>
    /// The most bathers a group may have: <paramref name="start"/> plus <see cref="PerReputation"/> a reputation level past 1, at most the tub
    /// seats plus <see cref="PastSeats"/> and <see cref="SquadRecords.MostMembers"/>, at least 1.
    /// </summary>
    public static int Cap(int reputation, int seats, int start)
    {
        int most = Math.Clamp(seats + PastSeats, 1, SquadRecords.MostMembers);
        return Math.Clamp(start + (reputation - 1) * PerReputation, 1, most);
    }

    /// <summary>
    /// A group's size, 1 to <paramref name="cap"/>, by the world's random numbers.
    /// </summary>
    public static int Roll(World world, int serial, RngPurpose purpose, int cap)
    {
        float roll = Rng.Float(Rng.Hash(world.Seed, (ulong)serial, purpose, Salt ^ (ulong)world.Tick));
        return Math.Clamp(1 + (int)(roll * cap), 1, Math.Max(cap, 1));
    }
}
