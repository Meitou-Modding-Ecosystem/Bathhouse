using Bathhouse.Bathing;
using Bathhouse.Records;
using Bathhouse.Staffing;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Locks;

namespace Bathhouse.Customers;

/// <summary>
/// Massage (docs/bathhouse.md "Massage"): each massage bed is paired with the nearest massage station in its building within 5 m. A customer lies
/// in the bed; while the station's Massage attendant stands at it and it holds a towel, the massage counts, pays by time and the attendant's
/// Dexterity, and trains it a little. The massage uses one towel from the station.
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
    /// How near the station its attendant stands.
    /// </summary>
    public const float AttendantReach = 40;

    /// <summary>
    /// How far from a bed (origin to origin) its station may stand: 5 m.
    /// </summary>
    public const float PairReach = 50;

    /// <summary>
    /// What an attendant in a bathhouse robe earns more, as a factor (the pay still tops out at the price).
    /// </summary>
    public const double RobeBonus = 1.2;

    /// <summary>
    /// The pay factor of an attendant with Dexterity 0: a tenth of the price.
    /// </summary>
    public const double UntrainedPay = 0.1;

    /// <summary>
    /// The Dexterity an hour of massage trains, on the game's curve (slower as it rises): about a third of a level an hour at Dexterity 20, so a
    /// little next to fighting.
    /// </summary>
    public const float TrainingPerHour = 0.5f;

    /// <summary>
    /// The Massage attendant job's row: the row its errands run on.
    /// </summary>
    static readonly TaskType Job = (TaskType)Ids.MassageJob;

    /// <summary>
    /// A free massage bed in the building whose station somebody holds the Massage attendant job at; null for none. Beds other visits hold are taken, and so is one somebody lies in (one of
    /// the player's people the player ordered into it, <see cref="PrivateBeds"/>).
    /// </summary>
    public static BuildSite? FreeBed(World world, string? building, IEnumerable<Visit> visits)
    {
        if (building is null) return null;

        var held = visits.Where(v => v.Phase == VisitPhase.Massage).Select(v => v.Bed).ToHashSet();
        return world.Buildings.All.FirstOrDefault(s => IsPiece(s, Ids.MassageBed) && s.Parent == building && !held.Contains(s.Id)
            && Station(world, s) is { } station && StaffRoles.StaffsPiece(world, station.Id, StaffRole.Massage) && BedOf(world, s) is { } bed
            && FixtureRegistry.IsFree(world.Characters, bed));
    }

    /// <summary>
    /// The bed's station: the nearest finished massage station in its building within <see cref="PairReach"/>; null for none.
    /// </summary>
    public static BuildSite? Station(World world, BuildSite bed) =>
        world.Buildings.All
            .Where(s => IsPiece(s, Ids.MassageStation) && s.Parent == bed.Parent && Soaks.Flat(s.Position, bed.Position) <= PairReach)
            .OrderBy(s => System.Numerics.Vector3.DistanceSquared(s.Position, bed.Position))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Whoever works the station now and their pay factor: the best of the player's people running the Massage attendant job at this station within
    /// <see cref="AttendantReach"/> of it (<see cref="Pay"/>); null with no attendant.
    /// </summary>
    public static (int Slot, double Pay)? Attendant(World world, BuildSite station)
    {
        var table = world.Characters;
        (int Slot, double Pay)? best = null;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (!table.Next[slot].Alive || table.Cold(slot) is not { IsPlayer: true, Running.Row: var row } cold || row != Job) continue;
            if (!WorksAt(cold, station.Id)) continue;
            if (Soaks.Flat(table.Next[slot].Position, station.Position) > AttendantReach) continue;

            bool robed = cold.Inventory?.Items.Any(i => i.Record == Ids.Robe && InventoryCommands.IsEquipmentSection(i.Section)) ?? false;
            double pay = Pay(cold.Stats?.Dexterity ?? 0, robed);
            if (best is null || pay > best.Value.Pay) best = (slot, pay);
        }

        return best;
    }

    /// <summary>
    /// Whether the character's running job is the one at this station (an attendant of the next station stays out of its reckoning).
    /// </summary>
    static bool WorksAt(CharacterCold cold, string station) =>
        cold.Running is { Job: var k } && k >= 0 && k < cold.Jobs.Count && cold.Jobs[k].Site == station;

    /// <summary>
    /// Whether the station has massage work: someone lies on a bed paired with it and it holds a towel.
    /// </summary>
    public static bool Busy(World world, BuildSite station) => TowelsIn(world, station) > 0 && Occupied(world, station);

    /// <summary>
    /// Whether someone lies on a massage bed paired with the station.
    /// </summary>
    public static bool Occupied(World world, BuildSite station) =>
        world.Buildings.All.Any(s => IsPiece(s, Ids.MassageBed) && s.Parent == station.Parent && BedOf(world, s) is { } bed
            && !FixtureRegistry.IsFree(world.Characters, bed) && Station(world, s)?.Id == station.Id);

    /// <summary>
    /// The towels in the station.
    /// </summary>
    public static int TowelsIn(World world, BuildSite station) => Goods.Count(Goods.StoreOf(world, station), Ids.Towel);

    /// <summary>
    /// A finished massage uses a towel from the station: it goes to the building's first hamper with room as a Dirty Towel, else on the floor at the
    /// customer's feet. False when the station had none.
    /// </summary>
    public static bool UseTowel(World world, int slot, BuildSite station)
    {
        if (Goods.StoreOf(world, station) is not { } store || !Goods.Take(store, Ids.Towel)) return false;

        Towels.Soil(world, slot, station.Parent);
        return true;
    }

    /// <summary>
    /// The pay factor of an attendant's Dexterity: <see cref="UntrainedPay"/> at 0 rising evenly to 1 at 100, times <see cref="RobeBonus"/> in a
    /// bathhouse robe, never past 1 (the massage price, 300 cats an hour as suggested).
    /// </summary>
    public static double Pay(float level, bool robed = false)
    {
        double factor = UntrainedPay + (1 - UntrainedPay) * Math.Clamp(level, 0, 100) / 100.0;
        return Math.Min(1, factor * (robed ? RobeBonus : 1));
    }

    /// <summary>
    /// Trains the attendant's Dexterity for <paramref name="hours"/> of work.
    /// </summary>
    public static void Train(World world, int slot, double hours)
    {
        if (world.Characters.Cold(slot)?.Stats is not { } stats || hours <= 0) return;

        stats.Gain(StatsEnumerated.Dexterity, (float)(TrainingPerHour * hours));
    }

    /// <summary>
    /// The bed's fixture; null for none.
    /// </summary>
    public static Fixture? BedOf(World world, BuildSite bed) => world.Fixtures.OfBuilding(bed.Id).FirstOrDefault(f => f.Kind == FixtureKind.Bed);

    /// <summary>
    /// Whether a site is a finished piece of a record.
    /// </summary>
    static bool IsPiece(BuildSite site, string record) => site.Record == record && site.Complete && !site.Destroyed && !site.Dismantling;
}
