using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Bathhouse.Bathing;

/// <summary>
/// Towels (docs/bathhouse.md "Towels"): a used towel comes back dirty <see cref="BathhouseSettings.DirtyChance"/> percent of the time and is ruined
/// otherwise, rolled with the world's deterministic random numbers. A bather dries after a soak with its own towel, else one from a store in the
/// building, and drops the dirty one in a hamper there (on the floor without one); a player's character right-clicks a towel to use it.
/// </summary>
public static class Towels
{
    /// <summary>
    /// The roll's counter salt, so a towel roll never shares a number with another roll of the mod for the same character and tick.
    /// </summary>
    const ulong Salt = 0x7041_E15E;

    /// <summary>
    /// Whether a towel used by <paramref name="who"/> now comes back dirty.
    /// </summary>
    public static bool ComesBackDirty(World world, CharacterId who, RngPurpose purpose, int dirtyChance) =>
        ComesBackDirty(world.Seed, world.Tick, who, purpose, dirtyChance);

    /// <summary>
    /// The towel roll for a world seed and tick.
    /// </summary>
    public static bool ComesBackDirty(ulong seed, long tick, CharacterId who, RngPurpose purpose, int dirtyChance) =>
        Rng.Float(Rng.Hash(seed, Rng.Key(who), purpose, Salt ^ (ulong)tick)) * 100 < dirtyChance;

    /// <summary>
    /// A bather dries after its soak: takes a towel from its inventory, else from a store in the building, and puts the dirty one in a hamper.
    /// False when there was no towel.
    /// </summary>
    public static bool Dry(World world, int slot, string? building, RngPurpose purpose, int dirtyChance)
    {
        if (Holder(world, slot, building) is not { } from || TowelIn(from) is not { } towel || !from.RemoveOne(towel)) return false;

        var who = world.Characters.IdOf(slot);
        if (ComesBackDirty(world, who, purpose, dirtyChance) && Dirty(world) is { } dirty) Discard(world, slot, building, dirty);
        return true;
    }

    /// <summary>
    /// A player's character uses a towel from its inventory by a right click (<c>AddItemUse</c>): the dirty towel goes back into the same
    /// inventory, or onto the floor when it has no room.
    /// </summary>
    public static bool Use(ItemUse use, RngPurpose purpose)
    {
        var world = use.World;
        if (!use.Inventory.RemoveOne(use.Item)) return false;
        if (!ComesBackDirty(world, use.User, purpose, BathhouseSettings.Current.DirtyChance) || Dirty(world) is not { } dirty) return true;

        if (!use.Inventory.Add(dirty) && world.Characters.TryResolveNext(use.User, out int slot)) Drop(world, slot, dirty);
        return true;
    }

    /// <summary>
    /// Where the bather's towel comes from: its own inventory when it holds one, else the building's first store that does; null for none.
    /// </summary>
    static Inventory? Holder(World world, int slot, string? building)
    {
        if (world.Characters.Cold(slot)?.Inventory is { } own && TowelIn(own) is not null) return own;

        return building is null ? null : Heating.StoresIn(world, building).FirstOrDefault(s => TowelIn(s) is not null);
    }

    /// <summary>
    /// The first clean towel in an inventory; null for none or no inventory.
    /// </summary>
    static ItemInstance? TowelIn(Inventory? inventory) => inventory?.Items.Find(i => i.Record == Ids.Towel);

    /// <summary>
    /// A new dirty towel; null when the world has no item factory.
    /// </summary>
    static ItemInstance? Dirty(World world) => world.Systems.OfType<ProductionSystem>().FirstOrDefault()?.Items.Create(Ids.DirtyTowel);

    /// <summary>
    /// Puts a dirty towel in the first hamper of the building with room, else on the floor at the bather's feet.
    /// </summary>
    static void Discard(World world, int slot, string? building, ItemInstance dirty)
    {
        foreach (var site in world.Buildings.All)
        {
            if (site.Record != Ids.Hamper || site.Parent != building || building is null || !site.Complete) continue;

            if (world.Machines.Find(site.Id)?.Storage is { } hamper && hamper.Add(dirty)) return;
        }

        Drop(world, slot, dirty);
    }

    /// <summary>
    /// Drops an item on the floor at the character's feet.
    /// </summary>
    static void Drop(World world, int slot, ItemInstance item)
    {
        ref readonly var hot = ref world.Characters.Next[slot];
        world.GroundItems.Drop(item, hot.Position, hot.Yaw);
    }
}
