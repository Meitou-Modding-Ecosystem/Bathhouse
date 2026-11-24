using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Bathhouse.Bathing;

/// <summary>
/// How hot a building's tubs are (docs/bathhouse.md "Heating"); every state but <see cref="Hot"/> and <see cref="Lukewarm"/> is cold.
/// </summary>
public enum Heat
{
    /// <summary>
    /// Hot: every seat in the building is heated.
    /// </summary>
    Hot,

    /// <summary>
    /// Warm but short: the building has more seats than its lit boilers heat. A soak still works, at lower quality.
    /// </summary>
    Lukewarm,

    /// <summary>
    /// The tub stands outdoors: no building, no boiler.
    /// </summary>
    Outdoors,

    /// <summary>
    /// No finished steam boiler in the building.
    /// </summary>
    NoBoiler,

    /// <summary>
    /// Every boiler in the building is switched off.
    /// </summary>
    BoilerOff,

    /// <summary>
    /// The boilers are on but get no power.
    /// </summary>
    NoPower,

    /// <summary>
    /// The building's stores hold no Water.
    /// </summary>
    NoWater,
}

/// <summary>
/// One building's heat: its state, its boilers (all, switched on, lit), its tub seats, the seats its lit boilers heat and the Water in its stores.
/// </summary>
public readonly record struct BuildingHeat(Heat Heat, int Boilers, int On, int Lit, int Seats, int Capacity, int Water)
{
    /// <summary>
    /// Whether a soak works: hot or lukewarm.
    /// </summary>
    public bool Warm => Heat is Heat.Hot or Heat.Lukewarm;
}

/// <summary>
/// The boilers' heat by building (docs/bathhouse.md "Heating"): a tub works while a steam boiler inside the same building is switched on, gets its
/// power and has Water in the building's stores; lit boilers drink that Water at <see cref="BathhouseSettings.WaterPerDay"/>. Serial steps only.
/// </summary>
public sealed class Heating
{
    #region State

    /// <summary>
    /// Water each building's boilers have used and not yet taken from its stores (under one unit), by building site id. Saved and hashed.
    /// </summary>
    readonly SortedDictionary<string, double> _owed = new(StringComparer.Ordinal);

    /// <summary>
    /// The heat of every building with a tub or a boiler at the last check, by building site id.
    /// </summary>
    readonly Dictionary<string, BuildingHeat> _now = new(StringComparer.Ordinal);

    /// <summary>
    /// The game hour of the last check; NaN before the first.
    /// </summary>
    double _lastHours = double.NaN;

    /// <summary>
    /// The water owed, for saving and hashing.
    /// </summary>
    public SortedDictionary<string, double> Owed => _owed;

    /// <summary>
    /// The heat of a building at the last check; <see cref="Heat.Outdoors"/> for none, <see cref="Heat.NoBoiler"/> for one not seen.
    /// </summary>
    public BuildingHeat Of(string? building) =>
        building is null ? new BuildingHeat(Heat.Outdoors, 0, 0, 0, 0, 0, 0)
        : _now.TryGetValue(building, out var heat) ? heat
        : new BuildingHeat(Heat.NoBoiler, 0, 0, 0, 0, 0, 0);

    #endregion

    #region Checking

    /// <summary>
    /// Works out every building's heat from its boilers, stores and tubs, and takes the Water its lit boilers used since the last check.
    /// </summary>
    public void Check(World world, IReadOnlyList<Tub> tubs, BathhouseSettings settings)
    {
        double hours = _lastHours is double.NaN ? 0 : Math.Max(0, world.GameHours - _lastHours);
        _lastHours = world.GameHours;

        var boilers = Boilers(world);
        var seats = SeatsByBuilding(tubs);
        _now.Clear();
        var buildings = boilers.Keys.Union(seats.Keys).Union(TeaBuildings(world));
        foreach (string building in buildings.Order(StringComparer.Ordinal))
        {
            var heat = Judge(world, building, boilers.GetValueOrDefault(building) ?? [], seats.GetValueOrDefault(building), settings);
            _now[building] = heat;
            Drink(world, building, heat, hours, settings.WaterPerDay);
        }

        foreach (string gone in _owed.Keys.Where(b => !_now.ContainsKey(b)).ToList()) _owed.Remove(gone);
    }

    /// <summary>
    /// A building's heat from its boilers' machines, its seats and its Water.
    /// </summary>
    static BuildingHeat Judge(World world, string building, List<Machine> boilers, int seats, BathhouseSettings settings)
    {
        int on = boilers.Count(m => m.PowerOn && !m.Broken);
        int lit = boilers.Count(m => m.PowerOn && !m.Broken && m.HasPower && m.Active);
        int water = WaterIn(world, building);
        var heat = Rule(boilers.Count, on, lit, water, seats, settings.SeatsPerBoiler, settings.WaterPerDay > 0);
        return new BuildingHeat(heat, boilers.Count, on, lit, seats, lit * settings.SeatsPerBoiler, water);
    }

    /// <summary>
    /// The heat rule: no boiler, every boiler off, none lit, no Water (when boilers drink any), more seats than the lit boilers heat, else hot.
    /// </summary>
    public static Heat Rule(int boilers, int on, int lit, int water, int seats, int seatsPerBoiler, bool needsWater)
    {
        if (boilers == 0) return Heat.NoBoiler;
        if (on == 0) return Heat.BoilerOff;
        if (lit == 0) return Heat.NoPower;
        if (needsWater && water <= 0) return Heat.NoWater;

        return seats > lit * seatsPerBoiler ? Heat.Lukewarm : Heat.Hot;
    }

    /// <summary>
    /// The finished steam boilers' machines by building, in site order (a boiler outdoors heats nothing).
    /// </summary>
    static Dictionary<string, List<Machine>> Boilers(World world)
    {
        var byBuilding = new Dictionary<string, List<Machine>>(StringComparer.Ordinal);
        foreach (var site in world.Buildings.All)
        {
            if (site.Record != Ids.Boiler || !site.Complete || site.Destroyed || site.Parent is not { } building) continue;
            if (world.Machines.Find(site.Id) is not { } machine) continue;

            if (!byBuilding.TryGetValue(building, out var list)) byBuilding[building] = list = [];
            list.Add(machine);
        }

        return byBuilding;
    }

    /// <summary>
    /// The buildings holding a finished tea bench or used tea rack, which need hot water too (their panels show its state).
    /// </summary>
    static IEnumerable<string> TeaBuildings(World world) =>
        world.Buildings.All
            .Where(s => s.Record is Ids.TeaBench or Ids.UsedTeaRack && s.Complete && !s.Destroyed && s.Parent is not null)
            .Select(s => s.Parent!);

    /// <summary>
    /// The tub seats by building.
    /// </summary>
    static Dictionary<string, int> SeatsByBuilding(IReadOnlyList<Tub> tubs)
    {
        var seats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var tub in tubs)
            if (tub.Building is { } building) seats[building] = seats.GetValueOrDefault(building) + tub.Seats;

        return seats;
    }

    #endregion

    #region Water

    /// <summary>
    /// The Water units in the stores standing in a building.
    /// </summary>
    public static int WaterIn(World world, string building) =>
        StoresIn(world, building).SelectMany(s => s.Items).Where(i => i.Record == FurnitureRecords.Water).Sum(i => i.Quantity);

    /// <summary>
    /// The inventories of the finished storage pieces standing in a building, in site order.
    /// </summary>
    public static IEnumerable<Inventory> StoresIn(World world, string building)
    {
        foreach (var site in world.Buildings.All)
        {
            if (site.Parent != building || !site.Complete || site.Destroyed) continue;

            if (world.Machines.Find(site.Id) is { Data.IsStorage: true, Storage: { } storage }) yield return storage;
        }
    }

    /// <summary>
    /// Adds the Water the lit boilers used over <paramref name="hours"/> to what the building owes and takes each whole unit from its stores. A
    /// building out of Water owes nothing more.
    /// </summary>
    void Drink(World world, string building, BuildingHeat heat, double hours, int perDay)
    {
        if (heat.Lit == 0 || perDay <= 0) return;

        double owed = _owed.GetValueOrDefault(building) + hours * heat.Lit * perDay / 24;
        while (owed >= 1 && TakeWater(world, building)) owed -= 1;

        if (owed >= 1) owed = 0;
        if (owed > 0) _owed[building] = owed;
        else _owed.Remove(building);
    }

    /// <summary>
    /// Takes one Water unit from the building's first store that has any; false when none has.
    /// </summary>
    static bool TakeWater(World world, string building)
    {
        foreach (var store in StoresIn(world, building))
        {
            if (store.Items.Find(i => i.Record == FurnitureRecords.Water) is { } water) return store.RemoveOne(water);
        }

        return false;
    }

    #endregion
}
