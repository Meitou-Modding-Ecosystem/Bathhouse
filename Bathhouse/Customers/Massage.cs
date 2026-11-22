using Bathhouse.Bathing;
using Bathhouse.Records;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Locks;

namespace Bathhouse.Customers;

/// <summary>
/// Massage (docs/bathhouse.md "Massage"): each massage bed is paired with the nearest massage station in its building. A customer lies in the bed;
/// while someone works the station (operating it, as any production machine), the massage counts and pays by time and the attendant's Dexterity.
/// </summary>
public static class Massage
{
    /// <summary>
    /// Game hours a massage lasts.
    /// </summary>
    public const double Hours = 1;

    /// <summary>
    /// Game hours a customer lies waiting for an attendant before complaining.
    /// </summary>
    public const double MostWait = 1;

    /// <summary>
    /// How near the station its operator stands.
    /// </summary>
    public const float AttendantReach = 40;

    /// <summary>
    /// A free massage bed with a station in the building; null for none. Beds other visits hold are taken.
    /// </summary>
    public static BuildSite? FreeBed(World world, string? building, IEnumerable<Visit> visits)
    {
        if (building is null) return null;

        var held = visits.Where(v => v.Phase == VisitPhase.Massage).Select(v => v.Bed).ToHashSet();
        return world.Buildings.All.FirstOrDefault(s => IsPiece(s, Ids.MassageBed) && s.Parent == building && !held.Contains(s.Id)
            && Station(world, s) is not null && BedOf(world, s) is not null);
    }

    /// <summary>
    /// The bed's station: the nearest finished massage station in its building; null for none.
    /// </summary>
    public static BuildSite? Station(World world, BuildSite bed) =>
        world.Buildings.All
            .Where(s => IsPiece(s, Ids.MassageStation) && s.Parent == bed.Parent)
            .OrderBy(s => System.Numerics.Vector3.DistanceSquared(s.Position, bed.Position))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The Dexterity of whoever works the station now (the nearest of the player's people operating a machine there); null with no attendant.
    /// </summary>
    public static float? Attendant(World world, BuildSite station)
    {
        if (world.Machines.Find(station.Id) is not { Operators: > 0 }) return null;

        var table = world.Characters;
        float? best = null;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (!table.Next[slot].Alive || table.Cold(slot) is not { IsPlayer: true, Running.Row: TaskType.OperateMachinery } cold) continue;
            if (Soaks.Flat(table.Next[slot].Position, station.Position) > AttendantReach) continue;

            float dexterity = cold.Stats?.Dexterity ?? 0;
            best = Math.Max(best ?? 0, dexterity);
        }

        return best;
    }

    /// <summary>
    /// The pay factor of an attendant's Dexterity: 0.5 at 0, 1 at 50, 1.5 at 100.
    /// </summary>
    public static double Skill(float dexterity) => 0.5 + Math.Clamp(dexterity, 0, 100) / 100.0;

    /// <summary>
    /// The bed's fixture; null for none.
    /// </summary>
    public static Fixture? BedOf(World world, BuildSite bed) => world.Fixtures.OfBuilding(bed.Id).FirstOrDefault(f => f.Kind == FixtureKind.Bed);

    /// <summary>
    /// Whether a site is a finished piece of a record.
    /// </summary>
    static bool IsPiece(BuildSite site, string record) => site.Record == record && site.Complete && !site.Destroyed && !site.Dismantling;
}
