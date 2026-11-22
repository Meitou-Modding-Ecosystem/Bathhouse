using System.Numerics;
using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Construction;

namespace Bathhouse.Bathing;

/// <summary>
/// The kinds of bath (docs/bathhouse.md "Tubs").
/// </summary>
public enum TubKind
{
    /// <summary>
    /// One seat.
    /// </summary>
    Small,

    /// <summary>
    /// Two seats.
    /// </summary>
    Medium,

    /// <summary>
    /// Four seats.
    /// </summary>
    Large,

    /// <summary>
    /// The skeletons' oil bath, two seats.
    /// </summary>
    Oil,

    /// <summary>
    /// The cannibals' blood bath, two seats.
    /// </summary>
    Blood,
}

/// <summary>
/// A placed bath: its site, kind and the building it stands in.
/// </summary>
public sealed record Tub(BuildSite Site, TubKind Kind)
{
    /// <summary>
    /// The building the tub stands in (the furniture's parent site); null outdoors.
    /// </summary>
    public string? Building => Site.Parent;

    /// <summary>
    /// How many bathers the tub takes.
    /// </summary>
    public int Seats => Tubs.SeatsOf(Kind);
}

/// <summary>
/// Where the tubs are and where a bather sits in one. The seats are the mod's own spots, not chair functionality, so no town or Fiefdom AI sits in a
/// tub on its own.
/// </summary>
public static class Tubs
{
    /// <summary>
    /// How far in front of a tub (its local +Z, ground units) a bather walks to before stepping in, and where it steps out.
    /// </summary>
    public const float Approach = 15;

    /// <summary>
    /// The kind of a tub record; null for any other record.
    /// </summary>
    public static TubKind? KindOf(string record) => record switch
    {
        Ids.SmallTub => TubKind.Small,
        Ids.MediumTub => TubKind.Medium,
        Ids.LargeTub => TubKind.Large,
        Ids.OilBath => TubKind.Oil,
        Ids.BloodBath => TubKind.Blood,
        _ => null,
    };

    /// <summary>
    /// The seats of a kind of tub.
    /// </summary>
    public static int SeatsOf(TubKind kind) => kind switch
    {
        TubKind.Small => 1,
        TubKind.Large => 4,
        _ => 2,
    };

    /// <summary>
    /// The finished tubs standing in the world, in site order.
    /// </summary>
    public static List<Tub> All(World world)
    {
        var tubs = new List<Tub>();
        foreach (var site in world.Buildings.All)
        {
            if (!site.Complete || site.Destroyed || site.Dismantling || KindOf(site.Record) is not { } kind) continue;

            tubs.Add(new Tub(site, kind));
        }

        return tubs;
    }

    /// <summary>
    /// A seat's place: the tub's origin plus the seat's offset, turned with the tub.
    /// </summary>
    public static Vector3 SeatAt(Tub tub, int seat) => tub.Site.Position + Vector3.Transform(SeatOffset(tub.Kind, seat), tub.Site.Rotation);

    /// <summary>
    /// The point in front of a tub a bather walks to and steps out at.
    /// </summary>
    public static Vector3 ApproachOf(Tub tub) => tub.Site.Position + Vector3.Transform(new Vector3(0, 0, Approach), tub.Site.Rotation);

    /// <summary>
    /// A seat's offset from the tub's origin in the tub's own frame (ground units): the centre for one seat, side by side for two, the corners of a
    /// square for four.
    /// </summary>
    public static Vector3 SeatOffset(TubKind kind, int seat)
    {
        const float half = 6;
        return SeatsOf(kind) switch
        {
            1 => Vector3.Zero,
            2 => new Vector3(seat == 0 ? -half : half, 0, 0),
            _ => new Vector3(seat % 2 == 0 ? -half : half, 0, seat < 2 ? -half : half),
        };
    }
}
