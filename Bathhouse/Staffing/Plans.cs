using System.Numerics;
using Bathhouse.Bathing;
using Bathhouse.Customers;
using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Bathhouse.Staffing;

/// <summary>
/// What each staff job does next (docs/bathhouse.md "Staff"), as an errand of stops; null when its job has nothing to do. A plan only reads the
/// world: the stops' ends move the goods, checking again that they are still there.
/// </summary>
public sealed class Plans(Soaks soaks, TubCare care)
{
    #region State

    /// <summary>
    /// Game hours a clerk stands at the counter before its job is asked again.
    /// </summary>
    public const double ClerkShift = 1;

    /// <summary>
    /// Game hours taking or putting goods takes at a stop.
    /// </summary>
    public const double Handling = 0.05;

    /// <summary>
    /// The most towels a laundry runner carries at once.
    /// </summary>
    public const int Armful = 10;

    /// <summary>
    /// The soap a laundry runner keeps in the washing machine.
    /// </summary>
    public const int SoapKept = 3;

    /// <summary>
    /// How far behind a counter (its local -Z) the clerk stands.
    /// </summary>
    public const float BehindCounter = 15;

    /// <summary>
    /// The errand of a role's job at its piece, for the character at <paramref name="slot"/>; null for nothing to do.
    /// </summary>
    public Queue<Leg>? Plan(World world, int slot, StaffRole role, string? site)
    {
        if (site is null || world.Buildings.Find(site) is not { Complete: true, Destroyed: false } piece) return null;

        var legs = role switch
        {
            StaffRole.Clerk => Clerk(piece),
            StaffRole.Scrubber => Scrub(world, piece.Parent),
            StaffRole.Laundry => Laundry(world, slot, piece),
            _ => Tea(world, slot, piece.Parent),
        };
        return legs is null ? null : new Queue<Leg>(legs);
    }

    #endregion

    #region Clerk and scrubber

    /// <summary>
    /// The clerk stands behind its counter for a shift.
    /// </summary>
    static Leg[] Clerk(BuildSite counter) => [new Leg(Behind(counter), ClerkShift, (_, _) => true)];

    /// <summary>
    /// The scrubber scrubs the building's first dirty tub nobody soaks in.
    /// </summary>
    Leg[]? Scrub(World world, string? building)
    {
        var tub = Tubs.All(world).FirstOrDefault(t => t.Building == building && building is not null && care.IsDirty(t.Site.Id) && Empty(t));
        if (tub is null) return null;

        string id = tub.Site.Id;
        return [new Leg(Tubs.ApproachOf(tub), TubCare.HoursFor(tub.Kind), (_, _) => Scrubbed(id))];
    }

    /// <summary>
    /// Marks a tub scrubbed.
    /// </summary>
    bool Scrubbed(string tub)
    {
        care.Scrubbed(tub);
        return true;
    }

    /// <summary>
    /// Whether no soak holds a seat in the tub.
    /// </summary>
    bool Empty(Tub tub) => !soaks.All.Any(s => s.Tub == tub.Site.Id);

    #endregion

    #region Laundry

    /// <summary>
    /// The laundry runner's next errand, in order: deliver what it carries, unload clean towels to a rack or the counter, fetch dirty towels from a
    /// hamper, top up the soap.
    /// </summary>
    static Leg[]? Laundry(World world, int slot, BuildSite washer)
    {
        if (world.Machines.Find(washer.Id) is not { } machine || washer.Parent is not { } building) return null;

        var carried = Goods.Carried(world, slot);
        var at = washer.Position;
        if (Goods.Count(carried, Ids.DirtyTowel) + Goods.Count(carried, Ids.Soap) > 0 && Room(machine, Ids.DirtyTowel) + Room(machine, Ids.Soap) > 0)
            return [new Leg(at, Handling, (w, s) => Load(w, s, washer))];

        if (Goods.Count(carried, Ids.Towel) > 0 && CleanTarget(world, building) is { } shelf)
            return [new Leg(shelf.Position, Handling, (w, s) => Put(w, s, shelf, Ids.Towel))];

        return Unload(world, washer, machine, building) ?? Fetch(world, washer, machine, building);
    }

    /// <summary>
    /// Takes the washer's clean towels to the first rack or counter with room.
    /// </summary>
    static Leg[]? Unload(World world, BuildSite washer, Machine machine, string building)
    {
        if (machine.Output is not { Count: > 0 } || CleanTarget(world, building) is not { } shelf) return null;

        return
        [
            new Leg(washer.Position, Handling, (w, s) => TakeClean(w, s, washer)),
            new Leg(shelf.Position, Handling, (w, s) => Put(w, s, shelf, Ids.Towel)),
        ];
    }

    /// <summary>
    /// Fetches dirty towels from a hamper, else soap from a store, when the washer has room for them.
    /// </summary>
    static Leg[]? Fetch(World world, BuildSite washer, Machine machine, string building)
    {
        var hamper = Goods.PiecesIn(world, building, Ids.Hamper).FirstOrDefault(h => Goods.Count(Goods.StoreOf(world, h), Ids.DirtyTowel) > 0);
        if (hamper is not null && Room(machine, Ids.DirtyTowel) > 0)
        {
            int most = Math.Min(Armful, Room(machine, Ids.DirtyTowel));
            return [Pick(hamper, Ids.DirtyTowel, most), new Leg(washer.Position, Handling, (w, s) => Load(w, s, washer))];
        }

        var store = SoapStore(world, building);
        if (store is null || InputCount(machine, Ids.Soap) >= SoapKept || Room(machine, Ids.Soap) == 0) return null;

        return [Pick(store, Ids.Soap, SoapKept), new Leg(washer.Position, Handling, (w, s) => Load(w, s, washer))];
    }

    /// <summary>
    /// The first rack, else counter, in the building with room for a towel.
    /// </summary>
    static BuildSite? CleanTarget(World world, string building) =>
        Goods.PiecesIn(world, building, Ids.TowelRack)
            .Concat(Goods.PiecesIn(world, building, Ids.Counter))
            .FirstOrDefault(s => Goods.StoreOf(world, s) is { } store && Goods.Make(world, Ids.Towel) is { } towel && store.HasRoomFor(towel));

    /// <summary>
    /// The first storage piece in the building (not a hamper or the counter) holding soap.
    /// </summary>
    static BuildSite? SoapStore(World world, string building) =>
        world.Buildings.All.FirstOrDefault(s => s.Parent == building && s.Record is not Ids.Hamper and not Ids.Counter && s.Complete
            && world.Machines.Find(s.Id) is { Data.IsStorage: true, Storage: { } store } && Goods.Count(store, Ids.Soap) > 0);

    /// <summary>
    /// Puts the carried dirty towels and soap into the washer's inputs, as many as fit.
    /// </summary>
    static bool Load(World world, int slot, BuildSite washer)
    {
        if (world.Machines.Find(washer.Id) is not { } machine || Goods.Carried(world, slot) is not { } carried) return false;

        foreach (var input in machine.Inputs.Where(i => i.Item is Ids.DirtyTowel or Ids.Soap))
        {
            while (input.Count < input.Recipe.Capacity && Goods.Take(carried, input.Item)) input.Count++;
        }

        return true;
    }

    /// <summary>
    /// Takes up to an armful of clean towels out of the washer's output into the runner's inventory.
    /// </summary>
    static bool TakeClean(World world, int slot, BuildSite washer)
    {
        if (world.Machines.Find(washer.Id)?.Output is not { } output || Goods.Carried(world, slot) is not { } carried) return false;

        int taken = 0;
        while (taken < Armful && output.Count > 0 && Goods.Make(world, Ids.Towel) is { } towel && carried.Add(towel))
        {
            output.Count--;
            taken++;
        }

        return taken > 0;
    }

    /// <summary>
    /// Free places in a machine's input for a record (0 without such an input).
    /// </summary>
    static int Room(Machine machine, string record) =>
        machine.Inputs.Where(i => i.Item == record).Sum(i => Math.Max(0, i.Recipe.Capacity - i.Count));

    /// <summary>
    /// The units of a record in a machine's inputs.
    /// </summary>
    static int InputCount(Machine machine, string record) =>
        machine.Inputs.Where(i => i.Item == record).Sum(i => i.Count);

    #endregion

    #region Tea

    /// <summary>
    /// The tea server's next errand: deliver carried Tea Sets, rinse a table's Used Tea Sets into Teaware in a store, or bring a Tea Set to a table without one.
    /// </summary>
    static Leg[]? Tea(World world, int slot, string? building)
    {
        if (building is null) return null;

        var tables = Goods.PiecesIn(world, building, Ids.TeaTable).ToList();
        var bare = tables.FirstOrDefault(t => Goods.Count(Goods.StoreOf(world, t), Ids.TeaSet) == 0);
        if (Goods.Count(Goods.Carried(world, slot), Ids.TeaSet) > 0 && bare is not null)
            return [new Leg(bare.Position, Handling, (w, s) => Put(w, s, bare, Ids.TeaSet, 1))];

        var used = tables.FirstOrDefault(t => Goods.Count(Goods.StoreOf(world, t), Ids.UsedTeaSet) > 0);
        if (used is not null && TeaStore(world, building, Ids.Teaware, room: true) is { } shelf)
            return [Pick(used, Ids.UsedTeaSet, Armful), new Leg(shelf.Position, Handling, (w, s) => Rinse(w, s, shelf))];

        if (bare is null || TeaStore(world, building, Ids.TeaSet, room: false) is not { } stock) return null;

        return [Pick(stock, Ids.TeaSet, 1), new Leg(bare.Position, Handling, (w, s) => Put(w, s, bare, Ids.TeaSet, 1))];
    }

    /// <summary>
    /// Rinses the carried Used Tea Sets: each becomes a Teaware in the store, while it has room; the rest stay carried.
    /// </summary>
    static bool Rinse(World world, int slot, BuildSite shelf)
    {
        if (Goods.StoreOf(world, shelf) is not { } store || Goods.Carried(world, slot) is not { } bag) return false;

        int rinsed = 0;
        while (Goods.Count(bag, Ids.UsedTeaSet) > 0 && Goods.Make(world, Ids.Teaware) is { } clean && store.HasRoomFor(clean))
        {
            Goods.Take(bag, Ids.UsedTeaSet);
            store.Add(clean);
            rinsed++;
        }

        return rinsed > 0;
    }

    /// <summary>
    /// The first store in the building that is not a tea table, hamper or counter: with room for a record, or holding one.
    /// </summary>
    static BuildSite? TeaStore(World world, string building, string record, bool room) =>
        world.Buildings.All.FirstOrDefault(s => s.Parent == building && s.Complete && !s.Destroyed
            && s.Record is not Ids.TeaTable and not Ids.Hamper and not Ids.Counter
            && world.Machines.Find(s.Id) is { Data.IsStorage: true, Storage: { } store }
            && (room ? Goods.Make(world, record) is { } unit && store.HasRoomFor(unit) : Goods.Count(store, record) > 0));

    #endregion

    #region Moving goods

    /// <summary>
    /// A stop that takes up to <paramref name="most"/> of a record from a piece's storage into the staff member's inventory.
    /// </summary>
    static Leg Pick(BuildSite from, string record, int most) => new(from.Position, Handling, (w, s) => Take(w, s, from, record, most));

    /// <summary>
    /// Takes up to <paramref name="most"/> of a record from a piece's storage into the staff member's inventory.
    /// </summary>
    static bool Take(World world, int slot, BuildSite from, string record, int most)
    {
        if (Goods.StoreOf(world, from) is not { } store || Goods.Carried(world, slot) is not { } bag) return false;

        return Goods.Move(world, store, bag, record, most) > 0;
    }

    /// <summary>
    /// Puts up to <paramref name="most"/> of a carried record into a piece's storage.
    /// </summary>
    static bool Put(World world, int slot, BuildSite to, string record, int most = int.MaxValue)
    {
        if (Goods.StoreOf(world, to) is not { } store || Goods.Carried(world, slot) is not { } bag) return false;

        return Goods.Move(world, bag, store, record, most) > 0;
    }

    /// <summary>
    /// Where the clerk stands: behind the counter.
    /// </summary>
    static Vector3 Behind(BuildSite counter) => counter.Position + Vector3.Transform(new Vector3(0, 0, -BehindCounter), counter.Rotation);

    #endregion
}
