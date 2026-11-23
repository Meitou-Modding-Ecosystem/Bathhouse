using System.Numerics;
using Bathhouse.Records;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Population;

namespace Bathhouse.Bathing;

/// <summary>
/// One bather's soak: who, which tub and seat, how long it has soaked, whether it sits in the water now, when it was sent and whether it is one of
/// the player's characters (who don't pay).
/// </summary>
public sealed class Soak
{
    /// <summary>
    /// The bather's lasting id, kept across saves.
    /// </summary>
    public required LastingId Bather { get; init; }

    /// <summary>
    /// The tub's site id.
    /// </summary>
    public required string Tub { get; init; }

    /// <summary>
    /// The seat in the tub, from 0.
    /// </summary>
    public required int Seat { get; init; }

    /// <summary>
    /// Whether the bather is one of the player's characters.
    /// </summary>
    public required bool Player { get; init; }

    /// <summary>
    /// The game hour it was sent.
    /// </summary>
    public required double SentAt { get; init; }

    /// <summary>
    /// Game hours soaked in warm water so far.
    /// </summary>
    public double Soaked { get; set; }

    /// <summary>
    /// Game hours of it in lukewarm water, for the visit's quality.
    /// </summary>
    public double Lukewarm { get; set; }

    /// <summary>
    /// Whether it soaks in a robe: null before it first sat down, then whether it changed into one or already wore one (<see cref="Robes"/>).
    /// </summary>
    public bool? Robed { get; set; }

    /// <summary>
    /// Whether it changed into a robe from the lockers, which it takes off after the soak.
    /// </summary>
    public bool Changed { get; set; }

    /// <summary>
    /// The record of the piece it took off for the robe, now in its main grid or bag; empty for none.
    /// </summary>
    public string Stowed { get; set; } = "";

    /// <summary>
    /// Whether it sits in the water now (not saved: tasks are not, so a loaded bather walks back in).
    /// </summary>
    public bool Seated { get; set; }
}

/// <summary>
/// How a soak ended, for its rewards and (from milestone 3) the visit's quality.
/// </summary>
public enum SoakEnd
{
    /// <summary>
    /// The full soak.
    /// </summary>
    Full,

    /// <summary>
    /// The water went cold before the end.
    /// </summary>
    Cold,

    /// <summary>
    /// The bather never got in, or left (an order, gone from the world).
    /// </summary>
    Abandoned,
}

/// <summary>
/// The bath goal and the soaks it acts out (docs/bathhouse.md "Bath goal"): a character the mod sends gets the goal's desire, the chooser starts it,
/// the bather walks to its tub, steps into its seat and rests there (sitting on the ground in the water) until its soak ends. Its act-out runs on
/// the goal's own row. Desire is asked in parallel and reads only what the serial steps wrote.
/// </summary>
public sealed class Soaks(Heating heating, Action<World, int, Soak, SoakEnd> finished, PopulationData? data = null) : IModGoal
{
    #region State

    /// <summary>
    /// The goal's desire for a sent bather: urgent tier, under Self preservation's in a fight.
    /// </summary>
    public const float Want = 0.75f;

    /// <summary>
    /// How near (ground plane) to the tub's approach point or seat counts as arrived.
    /// </summary>
    public const float Reach = 20;

    /// <summary>
    /// Game hours a bather has to get into its tub before the soak is called off.
    /// </summary>
    public const double GiveUpHours = 3;

    /// <summary>
    /// The goal's type and its act-out's row.
    /// </summary>
    public static readonly TaskType Row = (TaskType)Ids.BathGoalType;

    /// <summary>
    /// Every soak by its bather's lasting id. Saved and hashed.
    /// </summary>
    readonly SortedDictionary<ulong, Soak> _soaks = [];

    /// <summary>
    /// The soaks of the bathers in the world now, by character id: what <see cref="IModGoal.Desire"/> reads. Rebuilt in the serial step.
    /// </summary>
    Dictionary<CharacterId, Soak> _present = [];

    /// <summary>
    /// Every soak, for saving, hashing and the debug view.
    /// </summary>
    public IReadOnlyCollection<Soak> All => _soaks.Values;

    /// <summary>
    /// Puts back a soak read from a save.
    /// </summary>
    public void Restore(Soak soak) => _soaks[soak.Bather.Value] = soak;

    /// <summary>
    /// Whether the character is out to soak now.
    /// </summary>
    public bool Bathing(CharacterId who) => _present.ContainsKey(who);

    /// <summary>
    /// The soak of a character in the world now; null for none.
    /// </summary>
    public Soak? Of(CharacterId who) => _present.GetValueOrDefault(who);

    #endregion

    #region Sending

    /// <summary>
    /// Sends a character to the first free seat of a warm tub; false when the tub is cold or full, or the character is on a soak already.
    /// </summary>
    public bool Send(World world, int slot, Tub tub, bool player)
    {
        var who = world.Characters.IdOf(slot);
        if (_present.ContainsKey(who) || !heating.Of(tub.Building).Warm || FreeSeat(tub) is not { } seat) return false;

        var lasting = world.LastingIds.GetOrGive(who);
        var soak = new Soak { Bather = lasting, Tub = tub.Site.Id, Seat = seat, Player = player, SentAt = world.GameHours };
        _soaks[lasting.Value] = soak;
        _present[who] = soak;
        return true;
    }

    /// <summary>
    /// The tub's first seat no soak holds; null when it is full.
    /// </summary>
    public int? FreeSeat(Tub tub)
    {
        for (int seat = 0; seat < tub.Seats; seat++)
            if (!_soaks.Values.Any(s => s.Tub == tub.Site.Id && s.Seat == seat)) return seat;

        return null;
    }

    #endregion

    #region Goal

    float IModGoal.Desire(in ModGoalAsk ask) => _present.ContainsKey(ask.Id) ? Want : 0;

    /// <summary>
    /// Starts the act-out: steps into the seat when the bather is there already, else walks it to the tub.
    /// </summary>
    bool IModGoal.Start(World world, in ModGoalStart ask)
    {
        if (!_present.TryGetValue(ask.Id, out var soak) || TubOf(world, soak) is not { } tub) return false;
        if (Near(world, ask.Slot, tub, soak)) return SitDown(world, ask.Slot, tub, soak);

        return TaskStart.Walk(world, ask.Slot, Tubs.ApproachOf(tub), SpeedMode.Walk, Row, TaskSource.Goal) == StartResult.Started;
    }

    /// <summary>
    /// An order or leaving the world calls the soak off; a fight or another goal only gets the bather out of the water until the goal starts again.
    /// </summary>
    void IModGoal.End(World world, int slot, CharacterId id, TaskType type, ModGoalEnd why)
    {
        if (!_present.TryGetValue(id, out var soak)) return;

        soak.Seated = false;
        if (why is ModGoalEnd.Order or ModGoalEnd.Gone) Drop(world, slot, soak, SoakEnd.Abandoned);
    }

    #endregion

    #region Soaking

    /// <summary>
    /// The serial step: finds each bather in the world, steps arrived ones in, counts the soaked hours of seated ones and ends soaks that are done,
    /// went cold or never started.
    /// </summary>
    public void Step(World world, double hours, Func<Tub, double> length)
    {
        Find(world);
        foreach (var (who, soak) in _present.ToList())
        {
            if (!world.Characters.TryResolveNext(who, out int slot)) continue;

            StepOne(world, slot, soak, hours, length);
        }

        DropStale(world);
    }

    /// <summary>
    /// One bather: called off when its tub is gone, it is down or it waited too long; stepped in on arrival; its soak counted while seated.
    /// </summary>
    void StepOne(World world, int slot, Soak soak, double hours, Func<Tub, double> length)
    {
        var cold = world.Characters.Cold(slot);
        if (TubOf(world, soak) is not { } tub || cold?.Medical is not { Incapacitated: false }) Drop(world, slot, soak, SoakEnd.Abandoned);
        else if (soak.Seated) Count(world, slot, tub, soak, hours, length(tub));
        else if (world.GameHours - soak.SentAt > GiveUpHours + soak.Soaked) Drop(world, slot, soak, SoakEnd.Abandoned);
        else if (!heating.Of(tub.Building).Warm) Drop(world, slot, soak, soak.Soaked > 0 ? SoakEnd.Cold : SoakEnd.Abandoned);
        else if (ModGoals.Holds(world, slot, Row) && Near(world, slot, tub, soak) && Arrived(world, slot)) SitDown(world, slot, tub, soak);
    }

    /// <summary>
    /// A seated bather's hour count: ends the soak when it is long enough or the water went cold, and notices when a fight or an order got it out.
    /// </summary>
    void Count(World world, int slot, Tub tub, Soak soak, double hours, double length)
    {
        var heat = heating.Of(tub.Building);
        if (!ModGoals.Holds(world, slot, Row) || world.Characters.Next[slot].Task != (byte)CharacterTask.Rest)
        {
            soak.Seated = false;
            return;
        }

        if (!heat.Warm)
        {
            Finish(world, slot, tub, soak, SoakEnd.Cold);
            return;
        }

        soak.Soaked += hours;
        if (heat.Heat == Heat.Lukewarm) soak.Lukewarm += hours;
        if (soak.Soaked >= length) Finish(world, slot, tub, soak, SoakEnd.Full);
    }

    /// <summary>
    /// Puts the bather in its seat (changing into a robe the first time) and starts it resting there on the goal's row.
    /// </summary>
    bool SitDown(World world, int slot, Tub tub, Soak soak)
    {
        soak.Robed ??= Robes.PutOn(world, slot, tub.Building, soak, data);
        Place(world, slot, Tubs.SeatAt(tub, soak.Seat));
        var started = TaskStart.Start(world, slot, new TaskRequest(CharacterTask.Rest, Row, TaskSource.Goal));
        soak.Seated = started == StartResult.Started;
        return soak.Seated;
    }

    /// <summary>
    /// Ends a soak that ran: the bather steps out in front of the tub, the goal lets it go, and the owner hands out what the soak earned.
    /// </summary>
    void Finish(World world, int slot, Tub tub, Soak soak, SoakEnd end)
    {
        Place(world, slot, Tubs.ApproachOf(tub));
        Drop(world, slot, soak, end);
    }

    /// <summary>
    /// Forgets a soak, takes off the robe it changed into, lets the goal go and tells the owner how it ended.
    /// </summary>
    void Drop(World world, int slot, Soak soak, SoakEnd end)
    {
        Robes.TakeOff(world, slot, world.Buildings.Find(soak.Tub)?.Parent, soak, data);
        _soaks.Remove(soak.Bather.Value);
        _present.Remove(world.Characters.IdOf(slot));
        if (ModGoals.Holds(world, slot, Row)) ModGoals.Done(world, slot, Row);

        finished(world, slot, soak, end);
    }

    #endregion

    #region Finding

    /// <summary>
    /// Rebuilds <see cref="_present"/>: each soak's bather found by its lasting id, in lasting-id order.
    /// </summary>
    void Find(World world)
    {
        var present = new Dictionary<CharacterId, Soak>();
        foreach (var soak in _soaks.Values)
        {
            var who = world.LastingIds.Find(soak.Bather);
            if (!who.IsNone) present[who] = soak;
        }

        _present = present;
    }

    /// <summary>
    /// Forgets the soaks of bathers the game no longer keeps (dead and removed, despawned), and of absent ones past their give-up time.
    /// </summary>
    void DropStale(World world)
    {
        var absent = _soaks.Values.Where(s => !_present.ContainsValue(s)).ToList();
        if (absent.Count == 0) return;

        var kept = world.LastingIds.KeptIds();
        foreach (var soak in absent)
            if (!kept.Contains(soak.Bather) || world.GameHours - soak.SentAt > GiveUpHours * 4) _soaks.Remove(soak.Bather.Value);
    }

    /// <summary>
    /// A soak's tub while it still stands finished; null once it is gone.
    /// </summary>
    static Tub? TubOf(World world, Soak soak)
    {
        if (world.Buildings.Find(soak.Tub) is not { Complete: true, Destroyed: false } site) return null;

        return Tubs.KindOf(site.Record) is { } kind ? new Tub(site, kind) : null;
    }

    /// <summary>
    /// Whether the bather stands within reach of its tub's approach point or its seat.
    /// </summary>
    static bool Near(World world, int slot, Tub tub, Soak soak)
    {
        var at = world.Characters.Next[slot].Position;
        return Flat(at, Tubs.ApproachOf(tub)) <= Reach || Flat(at, Tubs.SeatAt(tub, soak.Seat)) <= Reach;
    }

    /// <summary>
    /// Whether the bather has stopped walking.
    /// </summary>
    static bool Arrived(World world, int slot) => world.Characters.Next[slot].Task != (byte)CharacterTask.GoTo;

    /// <summary>
    /// The ground-plane distance between two points.
    /// </summary>
    internal static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    /// <summary>
    /// Stands the character at a point, on the floor nearest the point's height there, as the debug menu's teleport does.
    /// </summary>
    static void Place(World world, int slot, Vector3 point)
    {
        var table = world.Characters;
        TaskStart.Stand(table, slot);
        table.Next[slot].Position = new Vector3(point.X, world.Walkability.GroundHeight(point.X, point.Z, point.Y), point.Z);
        table.Next[slot].Velocity = Vector3.Zero;
    }

    #endregion
}
