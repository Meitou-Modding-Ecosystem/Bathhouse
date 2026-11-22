using Bathhouse.Bathing;
using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Bathhouse.Customers;

/// <summary>
/// Where a check-in's towel and soap come from: each from the counter's own storage, else from the first store in its building that holds one.
/// </summary>
public readonly record struct Supplies(Inventory Towels, Inventory Soap);

/// <summary>
/// The counter's stock (docs/bathhouse.md "Check-in"): a towel and a soap, each taken from the counter's own storage, else from a store in its
/// building (the towel rack holds towels only, so the two often come from different stores); the towel goes into the customer's inventory (it
/// dries with it after the soak) and the soap is used up.
/// </summary>
public static class Stock
{
    /// <summary>
    /// Where the counter's next towel and soap come from; null when the counter and its building lack either.
    /// </summary>
    public static Supplies? Of(World world, BuildSite counter)
    {
        var own = world.Machines.Find(counter.Id)?.Storage;
        var stores = counter.Parent is null ? [] : Heating.StoresIn(world, counter.Parent).ToList();
        if (Source(own, stores, Ids.Towel) is not { } towels || Source(own, stores, Ids.Soap) is not { } soap) return null;

        return new Supplies(towels, soap);
    }

    /// <summary>
    /// Takes a towel and a soap from the supplies; the towel goes into the customer's inventory (a customer with no room for it dries without one).
    /// </summary>
    public static void Hand(World world, int slot, Supplies supplies)
    {
        if (Item(supplies.Soap, Ids.Soap) is { } soap) supplies.Soap.RemoveOne(soap);
        if (Item(supplies.Towels, Ids.Towel) is not { } towel || !supplies.Towels.RemoveOne(towel)) return;

        var fresh = world.Systems.OfType<ProductionSystem>().FirstOrDefault()?.Items.Create(Ids.Towel);
        if (fresh is not null && world.Characters.Cold(slot)?.Inventory is { } bag) bag.Add(fresh);
    }

    /// <summary>
    /// The counter's own storage when it holds a record, else the first of the stores that does; null for none.
    /// </summary>
    static Inventory? Source(Inventory? own, List<Inventory> stores, string record)
    {
        if (own is not null && Item(own, record) is not null) return own;

        return stores.FirstOrDefault(s => Item(s, record) is not null);
    }

    /// <summary>
    /// The first item of a record in an inventory; null for none.
    /// </summary>
    static ItemInstance? Item(Inventory inventory, string record) => inventory.Items.Find(i => i.Record == record);
}
