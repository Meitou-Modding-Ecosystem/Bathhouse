using System.Numerics;
using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Population;

namespace Bathhouse.Customers;

/// <summary>
/// Spawning customer groups (docs/bathhouse.md "Customers"): which counter is open, which kinds may come, and the held platoon sent toward the
/// counter.
/// </summary>
public static class Spawner
{
    /// <summary>
    /// How far in front of a counter (its local +Z) customers stand to check in.
    /// </summary>
    public const float Front = 20;

    /// <summary>
    /// The ring around the counter a group spawns in.
    /// </summary>
    public const float MinRadius = 800, MaxRadius = 1500;

    /// <summary>
    /// The game hours customers set out in: from 6 in the morning to 10 at night.
    /// </summary>
    public const int OpenFrom = 6, OpenUntil = 22;

    /// <summary>
    /// The roll's counter salt.
    /// </summary>
    const ulong Salt = 0xC057_0E25;

    /// <summary>
    /// Where customers stand to check in at a counter.
    /// </summary>
    public static Vector3 FrontOf(BuildSite counter) => counter.Position + Vector3.Transform(new Vector3(0, 0, Front), counter.Rotation);

    /// <summary>
    /// The first finished counter in a building with a warm tub; null for none.
    /// </summary>
    public static BuildSite? OpenCounter(World world, Func<string?, bool> warm) =>
        world.Buildings.All.FirstOrDefault(s => IsCounter(s) && s.Parent is not null && warm(s.Parent));

    /// <summary>
    /// Whether a site is a finished check-in counter.
    /// </summary>
    public static bool IsCounter(BuildSite site) => site.Record == Ids.Counter && site.Complete && !site.Destroyed && !site.Dismantling;

    /// <summary>
    /// Whether customers set out at this game hour.
    /// </summary>
    public static bool Daytime(double gameHours) => gameHours % 24 is >= OpenFrom and < OpenUntil;

    /// <summary>
    /// A kind that may come now, picked by the world's random numbers among the spawnable kinds the reputation allows whose faction is not hostile
    /// to the player's either way; null for none.
    /// </summary>
    public static CustomerType? Pick(World world, PopulationData data, int serial, RngPurpose purpose)
    {
        var kinds = CustomerTypes.All.Where(k => k.Reputation <= CustomerTypes.Reputation && Welcome(world, data, k)).ToList();
        if (kinds.Count == 0) return null;

        float roll = Rng.Float(Rng.Hash(world.Seed, (ulong)serial, purpose, Salt ^ (ulong)world.Tick));
        return kinds[(int)(roll * kinds.Count) % kinds.Count];
    }

    /// <summary>
    /// Whether a kind can be sent: it has a template, and its faction exists and is at peace with the player's.
    /// </summary>
    public static bool Welcome(World world, PopulationData data, CustomerType kind)
    {
        int faction = data.FactionIndex(kind.Faction);
        int player = world.Player.Faction;
        if (!kind.Spawnable || faction < 0 || player < 0) return false;

        return !data.Relations.IsHostile(faction, player) && !data.Relations.IsHostile(player, faction);
    }

    /// <summary>
    /// Spawns a held group of a kind and size (its bather template, <see cref="SquadRecords"/>) around the counter, homed at the nearest town,
    /// tagged and sent to the counter; false when the game refused.
    /// </summary>
    public static bool Spawn(World world, PopulationData data, CustomerType kind, int size, BuildSite counter, string tag)
    {
        var population = world.Systems.OfType<PopulationSystem>().FirstOrDefault();
        if (population is null || Home(data, counter.Position) is not { } home) return false;

        var around = Flat(counter.Position);
        string template = SquadRecords.Id(kind.Template, size) ?? kind.Template;
        int id = population.SpawnPlatoon(world, data.FactionIndex(kind.Faction), template, home, around, MinRadius, MaxRadius);
        if (id < 0 || world.Platoons.Find(id) is not { } platoon) return false;

        platoon.ModTag = tag;
        PopulationSystem.HoldPlatoon(world, id, true);
        PopulationSystem.SendPlatoon(world, id, Flat(FrontOf(counter)));
        return true;
    }

    /// <summary>
    /// The town site nearest a point; null with no sites.
    /// </summary>
    static int? Home(PopulationData data, Vector3 at) =>
        data.Sites.Count == 0 ? null : data.Sites.MinBy(s => Vector3.DistanceSquared(s.Position, at))!.Index;

    /// <summary>
    /// A point on the ground plane.
    /// </summary>
    public static Vector2 Flat(Vector3 at) => new(at.X, at.Z);
}
