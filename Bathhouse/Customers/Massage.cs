using Bathhouse.Bathing;
using Bathhouse.Records;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Locks;

namespace Bathhouse.Customers;

/// <summary>
/// Massage (docs/bathhouse.md "Massage"): each massage bed is paired with the nearest massage station in its building. A customer lies in the bed;
/// while someone works the station (operating it, as any production machine), the massage counts, pays by time and the attendant's Massage skill,
/// and slowly trains that skill.
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
    /// How near the station its operator stands.
    /// </summary>
    public const float AttendantReach = 40;

    /// <summary>
    /// What an attendant in a bathhouse robe earns more, as a factor (the pay still tops out at the price).
    /// </summary>
    public const double RobeBonus = 1.2;

    /// <summary>
    /// The pay factor of an untrained attendant (Massage 0): a tenth of the price.
    /// </summary>
    public const double UntrainedPay = 0.1;

    /// <summary>
    /// The Massage skill an hour of massage gives at level 1, on the game's curve (slower towards 100): about 50 after 100 hours' work, 80 after 400.
    /// </summary>
    public const float TrainingPerHour = 1;

    /// <summary>
    /// The attendant's skill (docs/bathhouse.md "Massage"), on the stats window's Sciences column.
    /// </summary>
    public static readonly ModSkill Skill = new("bathhouse.massage", "Massage", SkillColumn.Sciences)
    {
        Description = "Kneading tired travellers back into shape.",
        Affects = ["What a massage pays: a tenth of the massage price at 0, all of it at 100 (sooner in a bathhouse robe)"],
        Training = ["Working a massage station while a customer lies on its bed"],
    };

    /// <summary>
    /// A free massage bed with a station in the building; null for none. Beds other visits hold are taken, and so is one somebody lies in (one of
    /// the player's people the player ordered into it, <see cref="PrivateBeds"/>).
    /// </summary>
    public static BuildSite? FreeBed(World world, string? building, IEnumerable<Visit> visits)
    {
        if (building is null) return null;

        var held = visits.Where(v => v.Phase == VisitPhase.Massage).Select(v => v.Bed).ToHashSet();
        return world.Buildings.All.FirstOrDefault(s => IsPiece(s, Ids.MassageBed) && s.Parent == building && !held.Contains(s.Id)
            && Station(world, s) is not null && BedOf(world, s) is { } bed && FixtureRegistry.IsFree(world.Characters, bed));
    }

    /// <summary>
    /// The bed's station: the nearest finished massage station in its building; null for none.
    /// </summary>
    public static BuildSite? Station(World world, BuildSite bed) =>
        world.Buildings.All
            .Where(s => IsPiece(s, Ids.MassageStation) && s.Parent == bed.Parent)
            .OrderBy(s => System.Numerics.Vector3.DistanceSquared(s.Position, bed.Position))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Whoever works the station now and their pay factor (the best of the player's people operating a machine there, <see cref="Pay"/>); null with
    /// no attendant.
    /// </summary>
    public static (int Slot, double Pay)? Attendant(World world, BuildSite station)
    {
        if (world.Machines.Find(station.Id) is not { Operators: > 0 }) return null;

        var table = world.Characters;
        (int Slot, double Pay)? best = null;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (!table.Next[slot].Alive || table.Cold(slot) is not { IsPlayer: true, Running.Row: TaskType.OperateMachinery } cold) continue;
            if (Soaks.Flat(table.Next[slot].Position, station.Position) > AttendantReach) continue;

            bool robed = cold.Inventory?.Items.Any(i => i.Record == Ids.Robe && InventoryCommands.IsEquipmentSection(i.Section)) ?? false;
            double pay = Pay(cold.Stats is { } stats ? Skill.Level(stats) : 0, robed);
            if (best is null || pay > best.Value.Pay) best = (slot, pay);
        }

        return best;
    }

    /// <summary>
    /// The pay factor of an attendant's Massage level: <see cref="UntrainedPay"/> at 0 rising evenly to 1 at 100, times <see cref="RobeBonus"/> in a
    /// bathhouse robe, never past 1 (the massage price, 300 cats an hour as suggested).
    /// </summary>
    public static double Pay(float level, bool robed = false)
    {
        double factor = UntrainedPay + (1 - UntrainedPay) * Math.Clamp(level, 0, 100) / 100.0;
        return Math.Min(1, factor * (robed ? RobeBonus : 1));
    }

    /// <summary>
    /// Trains the attendant's Massage for <paramref name="hours"/> of work.
    /// </summary>
    public static void Train(World world, int slot, double hours)
    {
        if (world.Characters.Cold(slot)?.Stats is not { } stats || hours <= 0) return;

        Skill.Gain(stats, (float)(TrainingPerHour * hours));
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
