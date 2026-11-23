using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Items;
using Meitou.Simulation.Population;
using Meitou.Simulation.Production;
using Meitou.Simulation.Saving;

namespace Bathhouse.Bathing;

/// <summary>
/// Robes (docs/bathhouse.md "Robes"): a bather sitting down in a tub changes into a bathhouse robe from the lockers of the tub's building. Only the
/// robe's equipment slot changes: what is worn there goes into the bather's main grid or bag, and without room for it (or with it locked on) the
/// bather soaks as it is. After the soak it puts that back on and drops the robe, now a Dirty Robe, in a robe hamper there (on the floor without
/// one). One already wearing a robe (staff) keeps it.
/// </summary>
public static class Robes
{
    #region Changing

    /// <summary>
    /// The bather changes into a robe from the building's lockers: true when it wears a robe now (<see cref="Soak.Changed"/> and
    /// <see cref="Soak.Stowed"/> say what to undo); false when no locker has one, it has no room for its own piece or that is locked on.
    /// </summary>
    public static bool PutOn(World world, int slot, string? building, Soak soak, PopulationData? data)
    {
        if (world.Characters.Cold(slot) is not { Inventory: { } root } cold) return false;
        if (Worn(root) is not null) return true;
        if (LockerWithRobe(world, building) is not { } locker || Create(world, Ids.Robe) is not { } robe) return false;

        var worn = root.Items.Find(i => i.Section == robe.EquipmentSection);
        if (worn is { LockedOn: true } || worn is not null && !Stow(root, worn)) return false;
        if (!InventoryCommands.TryEquip(root, robe, cold))
        {
            if (worn is not null) Unstow(root, worn, cold);
            return false;
        }

        locker.Inventory.RemoveOne(locker.Robe);
        soak.Changed = true;
        soak.Stowed = worn?.Record ?? "";
        Redress(cold, data);
        return true;
    }

    /// <summary>
    /// The bather takes off the robe it changed into: puts its stowed piece back on and drops the robe, dirty, in a robe hamper of the building.
    /// </summary>
    public static void TakeOff(World world, int slot, string? building, Soak soak, PopulationData? data)
    {
        if (!soak.Changed || world.Characters.Cold(slot) is not { Inventory: { } root } cold) return;
        if (Worn(root) is not { } robe) return;

        root.Items.Remove(robe);
        var stowed = soak.Stowed.Length == 0 ? null : root.All().FirstOrDefault(i => i.Record == soak.Stowed && IsGrid(i.Section));
        if (stowed is not null) Unstow(root, stowed, cold);

        if (Create(world, Ids.DirtyRobe) is { } dirty) Discard(world, slot, building, dirty);
        soak.Changed = false;
        Redress(cold, data);
    }

    #endregion

    #region Steps

    /// <summary>
    /// The robe the character wears; null for none.
    /// </summary>
    static ItemInstance? Worn(Inventory root) => root.Items.Find(i => i.Record == Ids.Robe && InventoryCommands.IsEquipmentSection(i.Section));

    /// <summary>
    /// Whether a section is the main grid or the bag's.
    /// </summary>
    static bool IsGrid(string section) => section is "main" or "backpack_content";

    /// <summary>
    /// The first finished locker of the building holding a robe, with that robe; null for none.
    /// </summary>
    static (Inventory Inventory, ItemInstance Robe)? LockerWithRobe(World world, string? building)
    {
        if (building is null) return null;

        foreach (var site in world.Buildings.All)
        {
            if (site.Record != Ids.Lockers || site.Parent != building || !site.Complete) continue;
            if (world.Machines.Find(site.Id)?.Storage is { } store && store.Items.Find(i => i.Record == Ids.Robe) is { } robe) return (store, robe);
        }

        return null;
    }

    /// <summary>
    /// Moves a worn piece into the main grid, else the bag; false (and still worn) without room.
    /// </summary>
    static bool Stow(Inventory root, ItemInstance worn)
    {
        var (section, x, y) = (worn.Section, worn.X, worn.Y);
        root.Items.Remove(worn);
        if (root.Add(worn)) return true;

        worn.Section = section;
        worn.X = x;
        worn.Y = y;
        root.Items.Add(worn);
        return false;
    }

    /// <summary>
    /// Puts a stowed piece back on; one that no longer fits its slot goes back into the grid.
    /// </summary>
    static void Unstow(Inventory root, ItemInstance item, CharacterCold cold)
    {
        root.Remove(item);
        if (!InventoryCommands.TryEquip(root, item, cold)) root.Add(item);
    }

    /// <summary>
    /// A new item of a record; null when the world has no item factory.
    /// </summary>
    static ItemInstance? Create(World world, string record) => world.Systems.OfType<ProductionSystem>().FirstOrDefault()?.Items.Create(record);

    /// <summary>
    /// Puts a dirty robe in the first robe hamper of the building with room, else on the floor at the bather's feet.
    /// </summary>
    static void Discard(World world, int slot, string? building, ItemInstance dirty)
    {
        foreach (var site in world.Buildings.All)
        {
            if (site.Record != Ids.RobeHamper || site.Parent != building || building is null || !site.Complete) continue;

            if (world.Machines.Find(site.Id)?.Storage is { } hamper && hamper.Add(dirty)) return;
        }

        ref readonly var hot = ref world.Characters.Next[slot];
        world.GroundItems.Drop(dirty, hot.Position, hot.Yaw);
    }

    /// <summary>
    /// Builds the character's look again from what it wears now, as the game does when its kit changes: the look's body and seed with the worn
    /// items and limbs. The engine has no public call for it yet; its fighter (armour cover) catches up at the engine's next refresh.
    /// </summary>
    static void Redress(CharacterCold cold, PopulationData? data)
    {
        if (data?.Appearances is not { } source || cold.Inventory is not { } root || cold.Appearance is not { Animal: null } look) return;
        if ((look.Loadout?.Appearance.Record ?? look.Body?.Record) is not { } body) return;

        var faction = (uint)cold.Faction < (uint)data.Factions.Count ? data.Db.Find(data.Factions[cold.Faction].Id) : null;
        if (SavedKit.LoadoutOf(body, SavedKit.FromInventory(root), data.Db, look.Loadout?.Seed ?? 0, faction) is not { } kit) return;

        SavedKit.SetLimbs(kit, cold.Medical, data.Db);
        if (source.Create(cold.RecordId, kit, cold.Shaved) is { } updated) cold.Appearance = updated;
    }

    #endregion
}
