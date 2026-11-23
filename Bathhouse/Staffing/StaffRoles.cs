using Bathhouse.Records;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Production;

namespace Bathhouse.Staffing;

/// <summary>
/// A bathhouse job (docs/bathhouse.md "Staff").
/// </summary>
public enum StaffRole
{
    /// <summary>
    /// Stands behind the check-in counter, so it is staffed.
    /// </summary>
    Clerk,

    /// <summary>
    /// Scrubs the dirty tubs of the building.
    /// </summary>
    Scrubber,

    /// <summary>
    /// Carries dirty towels and soap to the washing machine and clean towels to the racks and the counter.
    /// </summary>
    Laundry,

    /// <summary>
    /// Stocks the tea tables with Tea Sets and clears their Used Tea Sets.
    /// </summary>
    Tea,
}

/// <summary>
/// The jobs' task types, names and pieces, and giving one to the player's selected characters (a piece's panel button). A job sits in the
/// character's job list (<c>CharacterCold.Jobs</c>) on the piece it was given at; the jobs panel shows and removes it and the game saves it.
/// </summary>
public static class StaffRoles
{
    /// <summary>
    /// Every role.
    /// </summary>
    public static readonly StaffRole[] All = Enum.GetValues<StaffRole>();

    /// <summary>
    /// A role's task type.
    /// </summary>
    public static TaskType TypeOf(StaffRole role) => (TaskType)(role switch
    {
        StaffRole.Clerk => Ids.ClerkJob,
        StaffRole.Scrubber => Ids.ScrubberJob,
        StaffRole.Laundry => Ids.LaundryJob,
        _ => Ids.TeaJob,
    });

    /// <summary>
    /// The role of a task type; null for another type.
    /// </summary>
    public static StaffRole? RoleOf(TaskType type) => All.Cast<StaffRole?>().FirstOrDefault(r => TypeOf(r!.Value) == type);

    /// <summary>
    /// A role's name in the jobs panel and on buttons.
    /// </summary>
    public static string Name(StaffRole role) => role switch
    {
        StaffRole.Clerk => "Bathhouse clerk",
        StaffRole.Scrubber => "Tub scrubber",
        StaffRole.Laundry => "Laundry runner",
        _ => "Tea server",
    };

    /// <summary>
    /// The role a piece's panel offers; null for a piece that offers none.
    /// </summary>
    public static StaffRole? OfferedBy(string? record) => record switch
    {
        Ids.Counter => StaffRole.Clerk,
        Ids.SmallTub or Ids.MediumTub or Ids.LargeTub or Ids.OilBath or Ids.BloodBath => StaffRole.Scrubber,
        Ids.WashingMachine => StaffRole.Laundry,
        Ids.TeaTable => StaffRole.Tea,
        _ => null,
    };

    /// <summary>
    /// Whether one of the player's characters holds the role's job at a piece in the building (its jobs switch on or off).
    /// </summary>
    public static bool Staffs(World world, string? building, StaffRole role)
    {
        if (building is null) return false;

        var type = TypeOf(role);
        var table = world.Characters;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (table.Cold(slot) is not { IsPlayer: true, Jobs: { } jobs } || !table.Next[slot].Alive) continue;
            if (jobs.Any(j => j.Type == type && j.Site is { } site && world.Buildings.Find(site)?.Parent == building)) return true;
        }

        return false;
    }

    /// <summary>
    /// Turns the game's Hauling to job (124, what a Shift + right click on a storage gives) at a piece that offers a role into that role's job, in
    /// its place in the list: the counter and the tubs are storages only so that the click gives a job (docs/bathhouse.md "Staff"). A haul it
    /// started stops.
    /// </summary>
    public static void TakeClickedJobs(World world)
    {
        var table = world.Characters;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (table.Cold(slot) is not { IsPlayer: true, Jobs: { Count: > 0 } jobs } cold) continue;

            for (int k = 0; k < jobs.Count; k++)
            {
                if (jobs[k].Type != TaskType.OperateStorage || jobs[k].Site is not { } site) continue;
                if (OfferedBy(world.Buildings.Find(site)?.Record) is not { } role || role == StaffRole.Laundry || role == StaffRole.Tea) continue;

                Convert(world, slot, cold, k, role, site);
            }
        }
    }

    /// <summary>
    /// Puts the role's job in place of the Hauling to job <paramref name="k"/>; one of the role held elsewhere goes.
    /// </summary>
    static void Convert(World world, int slot, CharacterCold cold, int k, StaffRole role, string site)
    {
        var type = TypeOf(role);
        var job = new PermaJob(type, site);
        if (cold.Running is { Source: TaskSource.Job, Row: TaskType.OperateStorage } run && run.Job == k) TaskStart.Stand(world.Characters, slot);

        if (cold.Running is { Source: TaskSource.Job } held && held.Row == type) TaskStart.Stand(world.Characters, slot);

        cold.Jobs[k] = job;
        for (int i = cold.Jobs.Count - 1; i >= 0; i--)
        {
            if (i != k && cold.Jobs[i].Type == type) RemoveAt(cold, i);
        }

        DecisionSystem.Request(cold);
    }

    /// <summary>
    /// Takes job <paramref name="index"/> out of the list; the running job's index follows, as the jobs panel's X does.
    /// </summary>
    static void RemoveAt(CharacterCold cold, int index)
    {
        cold.Jobs.RemoveAt(index);
        if (cold.Running is { Source: TaskSource.Job, Job: >= 0 } run && run.Job >= index)
            cold.Running = run with { Job = run.Job == index ? -1 : run.Job - 1 };
    }

    /// <summary>
    /// Gives each of the player's selected characters the role's job at a piece (one job of a role per character: one held elsewhere moves here).
    /// </summary>
    public static void Assign(World world, string site, StaffRole role)
    {
        var type = TypeOf(role);
        foreach (var who in world.Player.Selection)
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot) is not { IsPlayer: true } cold) continue;

            cold.Jobs.RemoveAll(j => j.Type == type);
            cold.Jobs.Add(new PermaJob(type, site));
            cold.Orders |= Meitou.Simulation.Player.CharacterOrders.Jobs;
            DecisionSystem.Request(cold);
        }
    }
}
