using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Bathhouse.Staffing;

/// <summary>
/// Counting and moving items between the inventories staff carry goods through (docs/bathhouse.md "Staff").
/// </summary>
public static class Goods
{
    /// <summary>
    /// The units of a record in an inventory.
    /// </summary>
    public static int Count(Inventory? inventory, string record) =>
        inventory?.Items.Where(i => i.Record == record).Sum(i => i.Quantity) ?? 0;

    /// <summary>
    /// Moves up to <paramref name="most"/> units of a record from one inventory to another, unit by unit, stopping when the source runs out or the
    /// target is full (a unit that does not fit goes back). Returns the units moved.
    /// </summary>
    public static int Move(World world, Inventory from, Inventory to, string record, int most)
    {
        int moved = 0;
        while (moved < most && Take(from, record))
        {
            if (Make(world, record) is not { } unit || !to.Add(unit))
            {
                if (Make(world, record) is { } back) from.Add(back);
                break;
            }

            moved++;
        }

        return moved;
    }

    /// <summary>
    /// Takes one unit of a record from an inventory; false when it holds none.
    /// </summary>
    public static bool Take(Inventory inventory, string record) =>
        inventory.Items.Find(i => i.Record == record) is { } item && inventory.RemoveOne(item);

    /// <summary>
    /// A new unit of a record; null when the world has no item factory.
    /// </summary>
    public static ItemInstance? Make(World world, string record) =>
        world.Systems.OfType<ProductionSystem>().FirstOrDefault()?.Items.Create(record);

    /// <summary>
    /// The storage of a piece; null for none.
    /// </summary>
    public static Inventory? StoreOf(World world, BuildSite site) => world.Machines.Find(site.Id)?.Storage;

    /// <summary>
    /// The finished pieces of a record standing in a building, in site order.
    /// </summary>
    public static IEnumerable<BuildSite> PiecesIn(World world, string? building, string record) =>
        world.Buildings.All.Where(s => s.Record == record && s.Parent == building && building is not null && s.Complete && !s.Destroyed);

    /// <summary>
    /// The building's general stores (storage pieces), leaving out the given records' pieces.
    /// </summary>
    public static IEnumerable<Inventory> StoresIn(World world, string building, params string[] except)
    {
        foreach (var site in world.Buildings.All)
        {
            if (site.Parent != building || !site.Complete || site.Destroyed || except.Contains(site.Record)) continue;

            if (world.Machines.Find(site.Id) is { Data.IsStorage: true, Storage: { } storage }) yield return storage;
        }
    }

    /// <summary>
    /// The character's own inventory; null for none.
    /// </summary>
    public static Inventory? Carried(World world, int slot) => world.Characters.Cold(slot)?.Inventory;

    /// <summary>
    /// Whether any of the stores holds a record.
    /// </summary>
    public static bool AnyIn(IEnumerable<Inventory> stores, string record) => stores.Any(s => Count(s, record) > 0);
}
