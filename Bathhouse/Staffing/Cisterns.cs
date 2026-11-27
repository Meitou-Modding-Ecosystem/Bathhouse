using Bathhouse.Bathing;
using Bathhouse.Records;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Production;

namespace Bathhouse.Staffing;

/// <summary>
/// The water cistern (docs/bathhouse.md "Water cistern"): an automatic machine that makes nothing itself and whose output holds Water, so the game's
/// haulers draw from it as from a well; its Water carrier fills that output from the player's wells.
/// </summary>
public static class Cisterns
{
    /// <summary>
    /// The build mode text for a cistern ghost inside a building (the game's own text for a rain collector there).
    /// </summary>
    public const string OutsideOnly = "Must be placed outside or on a roof.";

    /// <summary>
    /// The placement rule that keeps the cistern out of buildings: refused on a building's floor or snap node, allowed on the ground and on a roof
    /// or wall top (docs/bathhouse.md "Water cistern").
    /// </summary>
    public static readonly PlacementRule Placement = new() { Refuse = Refuse };

    /// <summary>
    /// <see cref="OutsideOnly"/> for a cistern ghost standing in or on a building; null for any other ghost.
    /// </summary>
    public static string? Refuse(PlacementAsk ask) => ask.Building.Id == Ids.Cistern && ask.Parent is not null ? OutsideOnly : null;

    /// <summary>
    /// The cistern's output, while it is a finished cistern holding Water; null otherwise.
    /// </summary>
    public static OutputSlot? Tank(World world, BuildSite cistern) =>
        cistern.Complete && world.Machines.Find(cistern.Id)?.Output is { } output && output.Recipe.Item == FurnitureRecords.Water ? output : null;

    /// <summary>
    /// The Water the cistern has room for; null for no cistern.
    /// </summary>
    public static int? Room(World world, BuildSite cistern) => Tank(world, cistern) is { } tank ? Math.Max(0, tank.Recipe.Capacity - tank.Count) : null;

    /// <summary>
    /// The nearest of the player's finished wells (a machine, not a storage or a cistern, whose output holds Water) within <paramref name="reach"/>
    /// of the cistern (ties: the lower site id); null for none.
    /// </summary>
    public static BuildSite? Well(World world, BuildSite cistern, float reach) =>
        world.Machines.All
            .Where(m => !m.Data.IsStorage && m.Output is { Count: > 0 } o && o.Recipe.Item == FurnitureRecords.Water)
            .Select(m => world.Buildings.Find(m.Site))
            .OfType<BuildSite>()
            .Where(s => s.Record != Ids.Cistern && s.Complete && !s.Destroyed && s.Owner == world.Player.Faction)
            .Where(s => Soaks.Flat(s.Position, cistern.Position) <= reach)
            .OrderBy(s => Soaks.Flat(s.Position, cistern.Position))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .FirstOrDefault();
}
