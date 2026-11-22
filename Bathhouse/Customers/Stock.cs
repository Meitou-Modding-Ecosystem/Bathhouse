using Bathhouse.Bathing;
using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Bathhouse.Customers;

/// <summary>
/// The counter's stock (docs/bathhouse.md "Check-in"): a towel and a soap taken from the counter's own storage, else from a store in its building;
/// the towel goes into the customer's inventory (it dries with it after the soak) and the soap is used up.
/// </summary>
public static class Stock
{
    /// <summary>
    /// The counter's storage when it holds a towel and a soap, else the building's first store that does; null for none.
    /// </summary>
    public static Inventory? Of(World world, BuildSite counter)
    {
        if (world.Machines.Find(counter.Id)?.Storage is { } own && Holds(own)) return own;

        return counter.Parent is null ? null : Heating.StoresIn(world, counter.Parent).FirstOrDefault(Holds);
    }

    /// <summary>
    /// Takes a towel and a soap from the stock; the towel goes into the customer's inventory (a customer with no room for it dries without one).
    /// </summary>
    public static void Hand(World world, int slot, Inventory stock)
    {
        if (Item(stock, Ids.Soap) is { } soap) stock.RemoveOne(soap);
        if (Item(stock, Ids.Towel) is not { } towel || !stock.RemoveOne(towel)) return;

        var fresh = world.Systems.OfType<ProductionSystem>().FirstOrDefault()?.Items.Create(Ids.Towel);
        if (fresh is not null && world.Characters.Cold(slot)?.Inventory is { } bag) bag.Add(fresh);
    }

    /// <summary>
    /// Whether an inventory holds a towel and a soap.
    /// </summary>
    static bool Holds(Inventory inventory) => Item(inventory, Ids.Towel) is not null && Item(inventory, Ids.Soap) is not null;

    /// <summary>
    /// The first item of a record in an inventory; null for none.
    /// </summary>
    static ItemInstance? Item(Inventory inventory, string record) => inventory.Items.Find(i => i.Record == record);
}
