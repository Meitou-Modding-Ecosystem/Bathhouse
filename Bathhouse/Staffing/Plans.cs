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
    /// The washer's crafts: each clean item, the dirty one it is washed from, the hamper that holds those and where the clean ones go.
    /// </summary>
    static readonly (string Clean, string Dirty, string Hamper)[] Washes = [(Ids.Towel, Ids.DirtyTowel, Ids.Hamper), (Ids.Robe, Ids.DirtyRobe, Ids.RobeHamper)];

    /// <summary>
    /// The laundry runner's next errand, in order: load what the washer's current craft takes, put away carried clean goods, take dirty goods the
    /// washer does not take now back to their hamper, unload the washer's finished goods, fetch dirty goods for the queued crafts, top up the soap.
    /// </summary>
    static Leg[]? Laundry(World world, int slot, BuildSite washer)
    {
        if (world.Machines.Find(washer.Id) is not { Craft: { } state } machine || washer.Parent is not { } building) return null;

        var carried = Goods.Carried(world, slot);
        if (Washes.Any(w => Goods.Count(carried, w.Dirty) > 0 && Room(machine, w.Dirty) > 0) || Goods.Count(carried, Ids.Soap) > 0 && Room(machine, Ids.Soap) > 0)
            return [new Leg(WorkSpots.Front(washer), Handling, (w, s) => Load(w, s, washer))];

        return PutAway(world, carried, building) ?? Unload(world, washer, state, building) ?? Fetch(world, washer, machine, state, building);
    }

    /// <summary>
    /// Takes carried clean goods to where they go, else carried dirty goods back to a hamper with room.
    /// </summary>
    static Leg[]? PutAway(World world, Inventory? carried, string building)
    {
        foreach (var (clean, dirty, hamper) in Washes)
        {
            if (Goods.Count(carried, clean) > 0 && CleanTarget(world, building, clean) is { } shelf)
                return [new Leg(WorkSpots.Front(shelf), Handling, (w, s) => Put(w, s, shelf, clean))];
            if (Goods.Count(carried, dirty) > 0 && WithRoom(world, building, hamper, dirty) is { } back)
                return [new Leg(WorkSpots.Front(back), Handling, (w, s) => Put(w, s, back, dirty))];
        }

        return null;
    }

    /// <summary>
    /// Takes the washer's first finished goods that have somewhere to go there.
    /// </summary>
    static Leg[]? Unload(World world, BuildSite washer, CraftState state, string building)
    {
        foreach (var (clean, _, _) in Washes)
        {
            if (!state.Done.Any(d => d.Record == clean) || CleanTarget(world, building, clean) is not { } shelf) continue;

            return
            [
                new Leg(WorkSpots.Front(washer), Handling, (w, s) => TakeClean(w, s, washer, clean)),
                new Leg(WorkSpots.Front(shelf), Handling, (w, s) => Put(w, s, shelf, clean)),
            ];
        }

        return null;
    }

    /// <summary>
    /// Fetches the dirty goods the queued crafts of the current kind still need from a hamper, else soap from a store. Only as many as those crafts
    /// use: the washer drops what is left in its sections when the next craft takes another item.
    /// </summary>
    static Leg[]? Fetch(World world, BuildSite washer, Machine machine, CraftState state, string building)
    {
        if (state.Queue.Count == 0) return null;

        var current = state.Queue[0].Option;
        if (Washes.FirstOrDefault(w => w.Clean == current.Item) is { Dirty: { } dirty, Hamper: { } hamperRecord })
        {
            int run = state.Queue.TakeWhile(j => j.Option.Key == current.Key).Count();
            int most = Math.Min(Armful, Math.Min(Room(machine, dirty), run - InputCount(machine, dirty)));
            var hamper = Goods.PiecesIn(world, building, hamperRecord).FirstOrDefault(h => Goods.Count(Goods.StoreOf(world, h), dirty) > 0);
            if (most > 0 && hamper is not null) return [Pick(hamper, dirty, most), new Leg(WorkSpots.Front(washer), Handling, (w, s) => Load(w, s, washer))];
        }

        var store = SoapStore(world, building);
        if (store is null || InputCount(machine, Ids.Soap) >= SoapKept || Room(machine, Ids.Soap) == 0) return null;

        return [Pick(store, Ids.Soap, SoapKept), new Leg(WorkSpots.Front(washer), Handling, (w, s) => Load(w, s, washer))];
    }

    /// <summary>
    /// Where clean goods go: towels to the first rack, else counter, robes to the first lockers, in the building with room for one.
    /// </summary>
    static BuildSite? CleanTarget(World world, string building, string clean) =>
        clean == Ids.Robe
            ? WithRoom(world, building, Ids.Lockers, Ids.Robe)
            : WithRoom(world, building, Ids.TowelRack, Ids.Towel) ?? WithRoom(world, building, Ids.Counter, Ids.Towel);

    /// <summary>
    /// The first piece of a record in the building with room for one of an item; null for none.
    /// </summary>
    static BuildSite? WithRoom(World world, string building, string piece, string item) =>
        Goods.PiecesIn(world, building, piece)
            .FirstOrDefault(s => Goods.StoreOf(world, s) is { } store && Goods.Make(world, item) is { } unit && store.HasRoomFor(unit));

    /// <summary>
    /// The first storage piece in the building (not a hamper or the counter) holding soap.
    /// </summary>
    static BuildSite? SoapStore(World world, string building) =>
        world.Buildings.All.FirstOrDefault(s => s.Parent == building && s.Record is not Ids.Hamper and not Ids.RobeHamper and not Ids.Counter
            && s.Complete && world.Machines.Find(s.Id) is { Data.IsStorage: true, Storage: { } store } && Goods.Count(store, Ids.Soap) > 0);

    /// <summary>
    /// Puts the carried goods the washer's sections take (dirty goods of the current craft, soap) into them, as many as fit.
    /// </summary>
    static bool Load(World world, int slot, BuildSite washer)
    {
        if (world.Machines.Find(washer.Id) is not { } machine || Goods.Carried(world, slot) is not { } carried) return false;

        foreach (var input in machine.Inputs.Where(i => !i.Idle && i.Item is Ids.DirtyTowel or Ids.DirtyRobe or Ids.Soap))
        {
            while (input.Count < input.Recipe.Capacity && Goods.Take(carried, input.Item)) input.Count++;
        }

        world.Machines.Touch();
        return true;
    }

    /// <summary>
    /// Takes up to an armful of a clean record out of the washer's finished goods into the runner's inventory.
    /// </summary>
    static bool TakeClean(World world, int slot, BuildSite washer, string clean)
    {
        if (world.Machines.Find(washer.Id)?.Craft is not { } state || Goods.Carried(world, slot) is not { } carried) return false;

        int taken = 0;
        while (taken < Armful && state.Done.Find(d => d.Record == clean) is { } done && Goods.Make(world, clean) is { } unit && carried.Add(unit))
        {
            if (--done.Quantity <= 0) state.Done.Remove(done);
            taken++;
        }

        if (taken > 0) world.Machines.Touch();
        return taken > 0;
    }

    /// <summary>
    /// Free places in a machine's input for a record (0 without such an input).
    /// </summary>
    static int Room(Machine machine, string record) =>
        machine.Inputs.Where(i => i.Takes(record)).Sum(i => Math.Max(0, i.Recipe.Capacity - i.Count));

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
            return [new Leg(WorkSpots.Front(bare), Handling, (w, s) => Put(w, s, bare, Ids.TeaSet, 1))];

        var used = tables.FirstOrDefault(t => Goods.Count(Goods.StoreOf(world, t), Ids.UsedTeaSet) > 0);
        if (used is not null && TeaStore(world, building, Ids.Teaware, room: true) is { } shelf)
            return [Pick(used, Ids.UsedTeaSet, Armful), new Leg(WorkSpots.Front(shelf), Handling, (w, s) => Rinse(w, s, shelf))];

        if (bare is null || TeaStore(world, building, Ids.TeaSet, room: false) is not { } stock) return null;

        return [Pick(stock, Ids.TeaSet, 1), new Leg(WorkSpots.Front(bare), Handling, (w, s) => Put(w, s, bare, Ids.TeaSet, 1))];
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
    /// The first store in the building that takes a record and is not a tea table or counter, tea shelves first: with room for it, or holding one.
    /// </summary>
    static BuildSite? TeaStore(World world, string building, string record, bool room) =>
        world.Buildings.All
            .OrderBy(s => s.Record != Ids.TeaShelf)
            .FirstOrDefault(s => IsTeaStore(world, s, building, record, room));

    /// <summary>
    /// Whether a site is a finished store in the building, not a tea table or counter, that takes a record: with room for it, or holding one.
    /// </summary>
    static bool IsTeaStore(World world, BuildSite site, string building, string record, bool room)
    {
        if (site.Parent != building || !site.Complete || site.Destroyed || site.Record is Ids.TeaTable or Ids.Counter) return false;
        if (world.Machines.Find(site.Id) is not { Data.IsStorage: true, Storage: { } store } machine) return false;
        if (!machine.Data.Accepts(record, Meitou.Data.Fcs.FcsRecordType.Item)) return false;

        return room ? Goods.Make(world, record) is { } unit && store.HasRoomFor(unit) : Goods.Count(store, record) > 0;
    }

    #endregion

    #region Moving goods

    /// <summary>
    /// A stop that takes up to <paramref name="most"/> of a record from a piece's storage into the staff member's inventory.
    /// </summary>
    static Leg Pick(BuildSite from, string record, int most) => new(WorkSpots.Front(from), Handling, (w, s) => Take(w, s, from, record, most));

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
