using System.Numerics;
using Bathhouse.Bathing;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Production;

namespace Bathhouse.Staffing;

/// <summary>
/// One stop of an errand: where to go, how long to work there and what is done at the end (false: the errand is called off).
/// </summary>
public sealed record Leg(Vector3 Point, double Hours, Func<World, int, bool> Done);

/// <summary>
/// A staff member's errand: the job it serves (its index in the job list and its role), its stops left, and when the work at the current stop ends
/// (NaN while walking). Not saved: tasks are not, so a loaded staff member plans its job again.
/// </summary>
public sealed class Errand(StaffRole role, int job, Queue<Leg> legs)
{
    /// <summary>
    /// The role.
    /// </summary>
    public StaffRole Role { get; } = role;

    /// <summary>
    /// The job's index in the character's job list.
    /// </summary>
    public int Job { get; } = job;

    /// <summary>
    /// The stops left, the current one first.
    /// </summary>
    public Queue<Leg> Legs { get; } = legs;

    /// <summary>
    /// The game hour the work at the current stop ends; NaN while walking.
    /// </summary>
    public double WorkUntil { get; set; } = double.NaN;
}

/// <summary>
/// The staff jobs' contributor (docs/bathhouse.md "Staff"): for each listed bathhouse job the chooser asks whether there is work, plans it as an
/// errand of walks and timed work (<see cref="Plans"/>) and starts it on the job's row; the system's serial check walks each errand on.
/// </summary>
public sealed class Staff(Plans plans) : IJobSource
{
    /// <summary>
    /// How near (ground plane) to a stop counts as there.
    /// </summary>
    public const float Reach = 25;

    /// <summary>
    /// Each staff member's errand.
    /// </summary>
    readonly Dictionary<CharacterId, Errand> _errands = [];

    /// <summary>
    /// The errands under way, for the debug row.
    /// </summary>
    public int Busy => _errands.Count;

    #region Chooser

    /// <summary>
    /// Runs while the character works this job's errand; else whether its job has work now.
    /// </summary>
    public DecisionCheck CanRun(World world, int slot, int k)
    {
        if (JobAt(world, slot, k) is not { } job || StaffRoles.RoleOf(job.Type) is not { } role) return DecisionCheck.No(null);
        if (_errands.TryGetValue(world.Characters.IdOf(slot), out var errand) && errand.Job == k) return DecisionCheck.Runs;

        return plans.Plan(world, slot, role, job.Site) is not null ? DecisionCheck.Run : DecisionCheck.No(null);
    }

    /// <summary>
    /// Plans the job's errand and starts its first stop; an errand under way for it goes on at its current stop.
    /// </summary>
    public void Start(World world, int slot, int k)
    {
        if (_errands.TryGetValue(world.Characters.IdOf(slot), out var current) && current.Job == k)
        {
            Resume(world, slot, current);
            return;
        }

        if (JobAt(world, slot, k) is not { } job || StaffRoles.RoleOf(job.Type) is not { } role) return;
        if (plans.Plan(world, slot, role, job.Site) is not { Count: > 0 } legs) return;

        var errand = new Errand(role, k, legs);
        _errands[world.Characters.IdOf(slot)] = errand;
        Walk(world, slot, errand);
    }

    /// <summary>
    /// The job can no longer run: its errand ends.
    /// </summary>
    public void Stop(World world, int slot, int k)
    {
        var who = world.Characters.IdOf(slot);
        if (!_errands.TryGetValue(who, out var errand) || errand.Job != k) return;

        _errands.Remove(who);
        TaskStart.Stand(world.Characters, slot);
    }

    #endregion

    #region Stepping

    /// <summary>
    /// The serial step: each errand whose character left its row (an order, a fight) is dropped; an arrived one starts working; finished work
    /// moves on to the next stop.
    /// </summary>
    public void Step(World world)
    {
        foreach (var (who, errand) in _errands.ToList())
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || !OnRow(world, slot, StaffRoles.TypeOf(errand.Role)))
            {
                _errands.Remove(who);
                continue;
            }

            StepOne(world, slot, who, errand);
        }
    }

    /// <summary>
    /// One errand: start the work on arrival, end it when its time is up.
    /// </summary>
    void StepOne(World world, int slot, CharacterId who, Errand errand)
    {
        var leg = errand.Legs.Peek();
        if (double.IsNaN(errand.WorkUntil))
        {
            if (!Arrived(world, slot, leg.Point)) return;

            errand.WorkUntil = world.GameHours + leg.Hours;
            Work(world, slot, errand);
        }

        if (world.GameHours < errand.WorkUntil) return;

        errand.Legs.Dequeue();
        errand.WorkUntil = double.NaN;
        if (!leg.Done(world, slot) || errand.Legs.Count == 0) Finish(world, slot, who);
        else Walk(world, slot, errand);
    }

    /// <summary>
    /// Goes on with an errand: back to work at its stop, or on the walk there.
    /// </summary>
    static void Resume(World world, int slot, Errand errand)
    {
        if (double.IsNaN(errand.WorkUntil)) Walk(world, slot, errand);
        else Work(world, slot, errand);
    }

    /// <summary>
    /// Walks the character to its current stop on the job's row.
    /// </summary>
    static void Walk(World world, int slot, Errand errand)
    {
        var row = StaffRoles.TypeOf(errand.Role);
        var request = new TaskRequest(CharacterTask.GoTo, row, TaskSource.Job) { Point = errand.Legs.Peek().Point, Job = errand.Job };
        TaskStart.Start(world, slot, request);
    }

    /// <summary>
    /// Stands the character working at its stop on the job's row.
    /// </summary>
    static void Work(World world, int slot, Errand errand)
    {
        var row = StaffRoles.TypeOf(errand.Role);
        TaskStart.Start(world, slot, new TaskRequest(CharacterTask.Idle, row, TaskSource.Job) { Job = errand.Job });
    }

    /// <summary>
    /// Ends an errand: the character stands, and the chooser picks its next job (this one again when there is more work).
    /// </summary>
    void Finish(World world, int slot, CharacterId who)
    {
        _errands.Remove(who);
        TaskStart.Stand(world.Characters, slot);
        if (world.Characters.Cold(slot) is { } cold) DecisionSystem.Request(cold);
    }

    #endregion

    #region Reading

    /// <summary>
    /// The character's job <paramref name="k"/>; null past the list.
    /// </summary>
    static PermaJob? JobAt(World world, int slot, int k) =>
        world.Characters.Cold(slot)?.Jobs is { } jobs && (uint)k < (uint)jobs.Count ? jobs[k] : null;

    /// <summary>
    /// Whether the character's last start was the row's, from a job: its walk or work, or the stand a finished walk leaves (an order, a fight or
    /// another job starts something else).
    /// </summary>
    static bool OnRow(World world, int slot, TaskType row) =>
        world.Characters.Cold(slot)?.Running is { Source: TaskSource.Job } run && run.Row == row;

    /// <summary>
    /// Whether the character stands at the point and has stopped walking.
    /// </summary>
    static bool Arrived(World world, int slot, Vector3 point)
    {
        ref readonly var hot = ref world.Characters.Next[slot];
        return Soaks.Flat(hot.Position, point) <= Reach && hot.Task != (byte)CharacterTask.GoTo;
    }

    #endregion
}
