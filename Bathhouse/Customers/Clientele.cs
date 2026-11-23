using System.Text;
using Bathhouse.Bathing;
using Bathhouse.Records;
using Bathhouse.Staffing;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Locks;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Population;

namespace Bathhouse.Customers;

/// <summary>
/// What a counter's check-in still lacks (<see cref="Clientele.Waiting"/>).
/// </summary>
public enum CheckInNeed
{
    /// <summary>
    /// One of the player's people within <see cref="Clientele.StaffReach"/> of the counter.
    /// </summary>
    Clerk,

    /// <summary>
    /// A warm tub with a free seat in the counter's building.
    /// </summary>
    WarmTub,
}

/// <summary>
/// The bathhouse's customers (docs/bathhouse.md "Customers"): groups sent on a schedule while a counter is open, their members' visits (the visit
/// goal walks each to the counter, where it checks in, takes a towel and soap, agrees to the price and is sent to soak), and the payment or
/// complaint after the soak. Steps in the system's serial check; the goal's desire reads only what that step wrote.
/// </summary>
public sealed class Clientele(Heating heating, Soaks soaks, TubCare care, PopulationData data, RngPurpose purpose) : IModGoal
{
    #region State

    /// <summary>
    /// The visit goal's desire for an arriving customer: urgent tier, under the bath goal's.
    /// </summary>
    public const float Want = 0.7f;

    /// <summary>
    /// How near (ground plane) to the counter's front counts as there.
    /// </summary>
    public const float Reach = 30;

    /// <summary>
    /// How near a player's character must stand to a counter to staff it.
    /// </summary>
    public const float StaffReach = 80;

    /// <summary>
    /// The most groups on their way or visiting at once.
    /// </summary>
    public const int MostGroups = 2;

    /// <summary>
    /// Game hours a customer waits at the counter before leaving with a complaint.
    /// </summary>
    public const double MostWait = 2;

    /// <summary>
    /// Game hours after which a group is let go whatever its visits say.
    /// </summary>
    public const double GroupHours = 24;

    /// <summary>
    /// The visit goal's type and its walk's row.
    /// </summary>
    public static readonly TaskType Row = (TaskType)Ids.VisitGoalType;

    /// <summary>
    /// How near (ground plane) to a massage bed counts as there.
    /// </summary>
    public const float BedReach = 60;

    /// <summary>
    /// The roll's counter salt for the price.
    /// </summary>
    const ulong PriceSalt = 0x9_21CE;

    /// <summary>
    /// The roll's counter salt for the tea.
    /// </summary>
    const ulong TeaSalt = 0x7EA;

    /// <summary>
    /// The roll's counter salt for the massage.
    /// </summary>
    const ulong MassageSalt = 0x3A55;

    /// <summary>
    /// Groups by tag. Saved and hashed.
    /// </summary>
    readonly SortedDictionary<string, CustomerGroup> _groups = new(StringComparer.Ordinal);

    /// <summary>
    /// Visits by the customer's lasting id. Saved and hashed.
    /// </summary>
    readonly SortedDictionary<ulong, Visit> _visits = [];

    /// <summary>
    /// The visits of customers in the world now, by character id: what <see cref="IModGoal.Desire"/> reads. Rebuilt in the serial step.
    /// </summary>
    Dictionary<CharacterId, Visit> _present = [];

    /// <summary>
    /// The price list. Saved and hashed.
    /// </summary>
    public Pricing Prices { get; } = new();

    /// <summary>
    /// The complaints' relation tally. Saved and hashed.
    /// </summary>
    public Complaints Complaints { get; } = new(data.Relations);

    /// <summary>
    /// Whether the bathhouse takes customers (the counter's switch). Saved and hashed.
    /// </summary>
    public bool Open { get; set; } = true;

    /// <summary>
    /// The game hour the next group may set out. Saved and hashed.
    /// </summary>
    public double NextGroupAt { get; set; }

    /// <summary>
    /// The number of the next group's tag. Saved and hashed.
    /// </summary>
    public int Serial { get; set; }

    /// <summary>
    /// The latest payment or complaint, for the counter's tooltip (not saved).
    /// </summary>
    public string Latest { get; private set; } = "No customers yet.";

    /// <summary>
    /// The most entries <see cref="Recent"/> keeps.
    /// </summary>
    public const int RecentKept = 6;

    /// <summary>
    /// The latest payments and complaints, newest first, for the counter's ledger (not saved).
    /// </summary>
    readonly List<string> _recent = [];

    /// <summary>
    /// The latest payments and complaints, newest first (at most <see cref="RecentKept"/>).
    /// </summary>
    public IReadOnlyList<string> Recent => _recent;

    /// <summary>
    /// The review board's record of finished visits. Saved, not hashed.
    /// </summary>
    public GuestBook Book { get; } = new();

    /// <summary>
    /// The groups, for saving, hashing and the views.
    /// </summary>
    public IReadOnlyCollection<CustomerGroup> Groups => _groups.Values;

    /// <summary>
    /// The visits, for saving, hashing and the views.
    /// </summary>
    public IReadOnlyCollection<Visit> Visits => _visits.Values;

    /// <summary>
    /// Puts back a group read from a save.
    /// </summary>
    public void Restore(CustomerGroup group) => _groups[group.Tag] = group;

    /// <summary>
    /// Puts back a visit read from a save.
    /// </summary>
    public void Restore(Visit visit) => _visits[visit.Customer.Value] = visit;

    /// <summary>
    /// Forgets every group and visit (before a save is read).
    /// </summary>
    public void Clear()
    {
        _groups.Clear();
        _visits.Clear();
        _present.Clear();
    }

    #endregion

    #region Stepping

    /// <summary>
    /// The serial step: a group sent when one is due, the groups kept on course or let go, and each present customer's visit stepped
    /// (<paramref name="hours"/>: game hours since the last step).
    /// </summary>
    public void Step(World world, IReadOnlyList<Tub> tubs, BathhouseSettings settings, double hours)
    {
        Book.Prune(world.GameHours);
        Schedule(world, tubs, settings);
        StepGroups(world);
        Find(world);
        foreach (var (who, visit) in _present.ToList())
        {
            if (!world.Characters.TryResolveNext(who, out int slot)) continue;
            if (visit.Phase == VisitPhase.Arriving && Hurt(world, slot))
            {
                Fail(world, slot, visit, "was hurt on the way in");
                continue;
            }

            StepVisit(world, slot, visit, tubs, hours);
        }
    }

    /// <summary>
    /// Sends a group of a kind to the open counter now, whatever the schedule and reputation say (the debug row); false when it could not.
    /// </summary>
    public bool SendNow(World world, IReadOnlyList<Tub> tubs, CustomerType kind)
    {
        if (Spawner.OpenCounter(world, b => Warm(tubs, b)) is not { } counter || !kind.Spawnable) return false;

        return SpawnGroup(world, kind, counter);
    }

    /// <summary>
    /// Sends a group when one is due: open, daytime, room for another group, an open counter and a kind that may come.
    /// </summary>
    void Schedule(World world, IReadOnlyList<Tub> tubs, BathhouseSettings settings)
    {
        if (!Open || settings.HoursBetweenGroups <= 0 || _groups.Count >= MostGroups || world.GameHours < NextGroupAt) return;
        if (!Spawner.Daytime(world.GameHours) || Spawner.OpenCounter(world, b => Warm(tubs, b)) is not { } counter) return;

        NextGroupAt = world.GameHours + settings.HoursBetweenGroups;
        if (Spawner.Pick(world, data, Serial, purpose) is { } kind) SpawnGroup(world, kind, counter);
    }

    /// <summary>
    /// Spawns a group and keeps it; false when the game refused the spawn.
    /// </summary>
    bool SpawnGroup(World world, CustomerType kind, BuildSite counter)
    {
        string tag = $"bathhouse.customers.{Serial}";
        if (!Spawner.Spawn(world, data, kind, counter, tag)) return false;

        Serial++;
        _groups[tag] = new CustomerGroup { Tag = tag, Kind = kind.Key, Counter = counter.Id, SpawnedAt = world.GameHours };
        world.Post($"{kind.Name} are on their way to the bathhouse.");
        return true;
    }

    /// <summary>
    /// Whether a building has a warm tub.
    /// </summary>
    bool Warm(IReadOnlyList<Tub> tubs, string? building) => tubs.Any(t => t.Building == building && heating.Of(building).Warm);

    #endregion

    #region Groups

    /// <summary>
    /// Each group: forgotten when its platoon is gone, let go when it is done, too old or its counter is gone, else kept held and on course until
    /// it loads, when its members are enlisted.
    /// </summary>
    void StepGroups(World world)
    {
        foreach (var group in _groups.Values.ToList())
        {
            var platoon = world.Platoons.All.FirstOrDefault(p => p.ModTag == group.Tag);
            if (platoon is null) Lost(world, group);
            else if (Finished(world, group)) Release(world, group, platoon);
            else Steer(world, group, platoon);
        }
    }

    /// <summary>
    /// Whether a group is done: too old, its counter gone, or enlisted with no visit still running.
    /// </summary>
    bool Finished(World world, CustomerGroup group)
    {
        if (world.GameHours - group.SpawnedAt > GroupHours || world.Buildings.Find(group.Counter) is not { } counter || !Spawner.IsCounter(counter))
            return true;

        return group.Enlisted && !_visits.Values.Any(v => v.Group == group.Tag && v.Phase != VisitPhase.Done);
    }

    /// <summary>
    /// Keeps a group held (a load lets go of holds) and, until it is enlisted, its stand-in on its way; enlists its members once it loads.
    /// </summary>
    void Steer(World world, CustomerGroup group, Platoon platoon)
    {
        if (!platoon.Held) PopulationSystem.HoldPlatoon(world, platoon.Id, true);
        if (group.Enlisted) return;

        var counter = world.Buildings.Find(group.Counter)!;
        PopulationSystem.SendPlatoon(world, platoon.Id, Spawner.Flat(Spawner.FrontOf(counter)));
        if (platoon.SquadId >= 0 && world.Squads.Find(platoon.SquadId) is { } squad) Enlist(world, group, squad.Members);
    }

    /// <summary>
    /// Gives each living person of a loaded group a visit.
    /// </summary>
    void Enlist(World world, CustomerGroup group, IEnumerable<CharacterId> members)
    {
        foreach (var who in members)
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot) is not { Animal: null, DiedTick: < 0 }) continue;

            var lasting = world.LastingIds.GetOrGive(who);
            _visits[lasting.Value] = new Visit { Customer = lasting, Group = group.Tag, Kind = group.Kind };
        }

        group.Enlisted = true;
    }

    /// <summary>
    /// Lets a group go: the game's travel takes its platoon back and may despawn it, and its visits end.
    /// </summary>
    void Release(World world, CustomerGroup group, Platoon platoon)
    {
        PopulationSystem.HoldPlatoon(world, platoon.Id, false);
        platoon.ModSpawned = false;
        platoon.ModTag = "";
        GaveUp(world, group);
        foreach (var (who, visit) in _present.Where(p => p.Value.Group == group.Tag))
        {
            if (world.Characters.TryResolveNext(who, out int slot) && ModGoals.Holds(world, slot, Row)) ModGoals.Done(world, slot, Row);
        }

        Forget(group);
    }

    /// <summary>
    /// Writes down a let-go group's guests who never got a soak: the group when it never arrived, else each visit still arriving.
    /// </summary>
    void GaveUp(World world, CustomerGroup group)
    {
        string kind = CustomerTypes.Find(group.Kind)?.Name ?? group.Kind;
        if (!group.Enlisted)
        {
            Book.Add(new GuestRecord(world.GameHours, $"{kind} group", kind, GuestOutcome.Failed, 0, float.NaN, "gave up on the way"));
            return;
        }

        foreach (var visit in _visits.Values.Where(v => v.Group == group.Tag && v.Phase == VisitPhase.Arriving))
        {
            string name = world.Characters.NameOf(world.LastingIds.Find(visit.Customer));
            Book.Add(new GuestRecord(world.GameHours, name, kind, GuestOutcome.Failed, 0, float.NaN, "gave up before reaching the counter"));
        }
    }

    /// <summary>
    /// A group whose platoon the game removed: posted and forgotten, and the next group may set out at once. One lost before it arrived was most
    /// likely wiped out on the road (Meitou's Fights far away setting lets held stand-ins fight off screen, by design: README).
    /// </summary>
    void Lost(World world, CustomerGroup group)
    {
        string name = CustomerTypes.Find(group.Kind)?.Name ?? group.Kind;
        string what = group.Enlisted ? "left the world" : "never made it, likely attacked on the road";
        world.Post($"{name} on their way to the bathhouse {what}.");
        if (!group.Enlisted) Book.Add(new GuestRecord(world.GameHours, $"{name} group", name, GuestOutcome.Failed, 0, float.NaN, what));

        NextGroupAt = Math.Min(NextGroupAt, world.GameHours);
        Forget(group);
    }

    /// <summary>
    /// Forgets a group and its visits.
    /// </summary>
    void Forget(CustomerGroup group)
    {
        _groups.Remove(group.Tag);
        foreach (var visit in _visits.Values.Where(v => v.Group == group.Tag).ToList()) _visits.Remove(visit.Customer.Value);
    }

    /// <summary>
    /// Rebuilds <see cref="_present"/>: each visit's customer found by lasting id, in lasting-id order.
    /// </summary>
    void Find(World world)
    {
        var present = new Dictionary<CharacterId, Visit>();
        foreach (var visit in _visits.Values)
        {
            var who = world.LastingIds.Find(visit.Customer);
            if (!who.IsNone) present[who] = visit;
        }

        _present = present;
    }

    #endregion

    #region Goal

    float IModGoal.Desire(in ModGoalAsk ask) =>
        _present.TryGetValue(ask.Id, out var visit) && visit.Phase is VisitPhase.Arriving or VisitPhase.Massage ? Want : 0;

    /// <summary>
    /// Walks the customer to its counter's front, or stands it there when it has arrived; one going for a massage walks to its bed.
    /// </summary>
    bool IModGoal.Start(World world, in ModGoalStart ask)
    {
        if (!_present.TryGetValue(ask.Id, out var visit)) return false;
        if (visit.Phase == VisitPhase.Massage) return ToBed(world, ask.Slot, visit);
        if (CounterOf(world, visit) is not { } counter) return false;
        if (AtCounter(world, ask.Slot, counter))
            return TaskStart.Become(world, ask.Slot, CharacterTask.Idle, Row, TaskSource.Goal) == StartResult.Started;

        return TaskStart.Walk(world, ask.Slot, Spawner.FrontOf(counter), SpeedMode.Walk, Row, TaskSource.Goal) == StartResult.Started;
    }

    /// <summary>
    /// Nothing to undo: the goal starts again while the visit is arriving.
    /// </summary>
    void IModGoal.End(World world, int slot, CharacterId id, TaskType type, ModGoalEnd why)
    {
    }

    #endregion

    #region Visits

    /// <summary>
    /// One customer: an arriving one checks in at the counter, waits or leaves; a soaking one says its line on getting in; one on a massage bed is
    /// massaged.
    /// </summary>
    void StepVisit(World world, int slot, Visit visit, IReadOnlyList<Tub> tubs, double hours)
    {
        if (visit.Phase == VisitPhase.Arriving) Arrive(world, slot, visit, tubs);
        else if (visit.Phase == VisitPhase.Soaking) Soaking(world, slot, visit);
        else if (visit.Phase == VisitPhase.Massage) Massaging(world, slot, visit, hours);
    }

    /// <summary>
    /// An arriving customer: leaves when the bathhouse closed or its counter is gone; at the counter, checks in, or leaves complaining after
    /// waiting too long.
    /// </summary>
    void Arrive(World world, int slot, Visit visit, IReadOnlyList<Tub> tubs)
    {
        if (!Open || CounterOf(world, visit) is not { } counter)
        {
            Fail(world, slot, visit, Open ? "its check-in counter was gone" : "found the bathhouse closed");
            return;
        }

        if (!AtCounter(world, slot, counter)) return;

        if (double.IsNaN(visit.ArrivedAt)) visit.ArrivedAt = world.GameHours;
        visit.Waited = world.GameHours - visit.ArrivedAt;
        if (CheckIn(world, slot, visit, counter, tubs)) return;
        if (visit.Waited > MostWait) Unhappy(world, slot, visit, "waited too long at the counter", GuestOutcome.Failed);
    }

    /// <summary>
    /// A soaking customer says its line once it sits in the water; one whose soak is gone (a load, a fight) leaves.
    /// </summary>
    void Soaking(World world, int slot, Visit visit)
    {
        var who = world.Characters.IdOf(slot);
        if (soaks.Of(who) is not { } soak)
        {
            Fail(world, slot, visit, "never got into the tub");
            return;
        }

        if (!soak.Seated || visit.InTub) return;

        visit.InTub = true;
        Barks.Say(world, who, visit.Kind, BarkEvent.InTub, purpose);
    }

    /// <summary>
    /// The check-in: a clerk at the counter and a free warm tub in its building, then the price of the bath and the towel and soap in stock. True
    /// when the visit moved on (sent to soak, or turned away by the price); false to keep waiting.
    /// </summary>
    bool CheckIn(World world, int slot, Visit visit, BuildSite counter, IReadOnlyList<Tub> tubs)
    {
        if (!Staffed(world, counter) || FreeTub(counter, tubs) is not { } tub) return false;

        var stock = Stock.Of(world, counter);
        bool towel = stock.Towels is not null;
        bool soap = stock.Soap is not null;
        var (price, suggested) = Prices.Visit(tub.Kind, towel, soap);
        if (!Accepts(world, slot, visit, price / suggested))
        {
            TurnedAway(world, slot, visit, price);
            return true;
        }

        if (!soaks.Send(world, slot, tub, player: false)) return false;

        Stock.Hand(world, slot, stock);
        visit.Towel = towel;
        visit.Soap = soap;
        visit.Price = price;
        visit.DirtyTub = care.IsDirty(tub.Site.Id);
        visit.Phase = VisitPhase.Soaking;
        if (ModGoals.Holds(world, slot, Row)) ModGoals.Done(world, slot, Row);
        return true;
    }

    /// <summary>
    /// Whether the customer takes a price <paramref name="ratio"/> times the suggested one (<see cref="Pricing.AcceptChance"/>).
    /// </summary>
    bool Accepts(World world, int slot, Visit visit, double ratio, ulong salt = PriceSalt)
    {
        double tolerance = CustomerTypes.Find(visit.Kind)?.Tolerance ?? 1;
        ulong key = Rng.Key(world.Characters.IdOf(slot));
        double roll = Rng.Float(Rng.Hash(world.Seed, key, purpose, salt ^ (ulong)world.Tick));
        return roll < Pricing.AcceptChance(ratio, tolerance);
    }

    /// <summary>
    /// The first clean tub, else the first dirty one, with a free seat and warm water in the counter's building; null for none.
    /// </summary>
    Tub? FreeTub(BuildSite counter, IReadOnlyList<Tub> tubs)
    {
        var free = tubs.Where(t => t.Building == counter.Parent && heating.Of(t.Building).Warm && soaks.FreeSeat(t) is not null).ToList();
        return free.FirstOrDefault(t => !care.IsDirty(t.Site.Id)) ?? free.FirstOrDefault();
    }

    /// <summary>
    /// Whether one of the player's people, alive and on their feet, stands at the counter.
    /// </summary>
    static bool Staffed(World world, BuildSite counter)
    {
        var table = world.Characters;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (!table.Next[slot].Alive || table.Cold(slot) is not { IsPlayer: true, Medical.Incapacitated: false }) continue;
            if (Soaks.Flat(table.Next[slot].Position, counter.Position) <= StaffReach) return true;
        }

        return false;
    }

    /// <summary>
    /// The visit's counter while it stands; null once it is gone.
    /// </summary>
    BuildSite? CounterOf(World world, Visit visit)
    {
        if (_groups.GetValueOrDefault(visit.Group) is not { } group || world.Buildings.Find(group.Counter) is not { } site) return null;

        return Spawner.IsCounter(site) ? site : null;
    }

    /// <summary>
    /// Whether the customer stands at the counter's front and has stopped walking.
    /// </summary>
    static bool AtCounter(World world, int slot, BuildSite counter)
    {
        ref readonly var hot = ref world.Characters.Next[slot];
        return Soaks.Flat(hot.Position, Spawner.FrontOf(counter)) <= Reach && hot.Task != (byte)CharacterTask.GoTo;
    }

    #endregion

    #region Payment

    /// <summary>
    /// A customer's soak ended (the system's callback for soaks of people not the player's): for a visit good enough for its kind it buys tea when a
    /// table has some, then takes a massage or pays; a visit not good enough complains; one that never soaked just leaves.
    /// </summary>
    public void Bathed(World world, int slot, Soak soak, SoakEnd end)
    {
        if (!_visits.TryGetValue(soak.Bather.Value, out var visit) || visit.Phase != VisitPhase.Soaking) return;
        if (end == SoakEnd.Abandoned)
        {
            Fail(world, slot, visit, "never got into the tub");
            return;
        }

        var kind = CustomerTypes.Find(visit.Kind);
        var quality = Quality.Of(soak.Soaked, soak.Lukewarm, visit.Waited, end == SoakEnd.Cold, visit.DirtyTub, visit.Towel, visit.Soap,
            soak.Robed == true);
        visit.Quality = quality.Total;
        if (end == SoakEnd.Cold) Barks.Say(world, world.Characters.IdOf(slot), visit.Kind, BarkEvent.Cold, purpose);
        if (kind is null || quality.Total < kind.Demand)
        {
            Unhappy(world, slot, visit, $"quality {quality}");
            return;
        }

        var tub = world.Buildings.Find(soak.Tub);
        visit.Owed = quality.Pay(visit.Price) + Tea(world, slot, visit, tub?.Parent);
        if (!OfferMassage(world, slot, visit, tub?.Parent)) Pay(world, slot, visit, kind, $"quality {quality}");
    }

    /// <summary>
    /// The customer pays what it owes and leaves happy.
    /// </summary>
    void Pay(World world, int slot, Visit visit, CustomerType kind, string why)
    {
        world.Player.Money += visit.Owed;
        string name = world.Characters.NameOf(world.Characters.IdOf(slot));
        Note(world, $"{name} ({kind.Name}) paid {visit.Owed} cats, {why}.");
        Book.Add(new GuestRecord(world.GameHours, name, kind.Name, GuestOutcome.Paid, visit.Owed, visit.Quality, why));

        Barks.Say(world, world.Characters.IdOf(slot), visit.Kind, BarkEvent.Happy, purpose);
        visit.Phase = VisitPhase.Done;
    }

    /// <summary>
    /// The customer buys a Tea Set from a tea table in the building when one holds a set and it takes the tea's price: the set becomes a Used Tea Set
    /// on the table. Returns the price (0 for no tea).
    /// </summary>
    long Tea(World world, int slot, Visit visit, string? building)
    {
        var table = Goods.PiecesIn(world, building, Ids.TeaTable).FirstOrDefault(t => Goods.Count(Goods.StoreOf(world, t), Ids.TeaSet) > 0);
        if (table is null || Goods.StoreOf(world, table) is not { } store) return 0;
        if (!Accepts(world, slot, visit, Prices.Percent(Service.Tea) / 100.0, TeaSalt) || !Goods.Take(store, Ids.TeaSet)) return 0;

        if (Goods.Make(world, Ids.UsedTeaSet) is { } used) store.Add(used);
        return Prices.Price(Service.Tea, TubKind.Small);
    }

    /// <summary>
    /// The customer leaves without paying: a complaint, maybe a fight; the board lists it as <paramref name="outcome"/>.
    /// </summary>
    void Unhappy(World world, int slot, Visit visit, string why, GuestOutcome outcome = GuestOutcome.Complained)
    {
        var kind = CustomerTypes.Find(visit.Kind);
        bool fight = kind is not null && Complaints.Complain(world, slot, kind, purpose, BathhouseSettings.Current);
        Barks.Say(world, world.Characters.IdOf(slot), visit.Kind, fight ? BarkEvent.Fight : BarkEvent.Complaint, purpose);

        string name = world.Characters.NameOf(world.Characters.IdOf(slot));
        string reason = $"{why}{(fight ? ", and picked a fight" : "")}";
        Note(world, $"{name} ({kind?.Name ?? visit.Kind}) left without paying: {reason}.");
        Book.Add(new GuestRecord(world.GameHours, name, KindName(visit), outcome, 0, visit.Quality, reason));
        Leave(world, slot, visit);
    }

    /// <summary>
    /// The customer leaves without a visit: the price was too high for it.
    /// </summary>
    void TurnedAway(World world, int slot, Visit visit, long price)
    {
        var who = world.Characters.IdOf(slot);
        Barks.Say(world, who, visit.Kind, BarkEvent.Pricey, purpose);
        string name = world.Characters.NameOf(who);
        Note(world, $"{name} ({KindName(visit)}) would not pay {price} cats.");
        Book.Add(new GuestRecord(world.GameHours, name, KindName(visit), GuestOutcome.TurnedAway, 0, float.NaN, $"would not pay {price} cats"));
        Leave(world, slot, visit);
    }

    /// <summary>
    /// A payment or complaint: the latest one, at the top of the recent ones, and a message to the player.
    /// </summary>
    void Note(World world, string text)
    {
        Latest = text;
        _recent.Insert(0, text);
        if (_recent.Count > RecentKept) _recent.RemoveAt(RecentKept);

        world.Post(text);
    }

    /// <summary>
    /// The visit ends without a soak, written down as a failed arrival.
    /// </summary>
    void Fail(World world, int slot, Visit visit, string why)
    {
        string name = world.Characters.NameOf(world.Characters.IdOf(slot));
        Book.Add(new GuestRecord(world.GameHours, name, KindName(visit), GuestOutcome.Failed, 0, float.NaN, why));
        Leave(world, slot, visit);
    }

    /// <summary>
    /// Whether the customer is dead or down.
    /// </summary>
    static bool Hurt(World world, int slot) =>
        !world.Characters.Next[slot].Alive || world.Characters.Cold(slot) is not { Medical.Incapacitated: false };

    /// <summary>
    /// The name of a visit's kind.
    /// </summary>
    static string KindName(Visit visit) => CustomerTypes.Find(visit.Kind)?.Name ?? visit.Kind;

    /// <summary>
    /// The visit ends; the customer goes when its group does.
    /// </summary>
    void Leave(World world, int slot, Visit visit)
    {
        visit.Phase = VisitPhase.Done;
        if (ModGoals.Holds(world, slot, Row)) ModGoals.Done(world, slot, Row);
    }

    #endregion

    #region Massage

    /// <summary>
    /// Offers a soaked customer a massage: a free bed with a station in the building and a price it takes send it to the bed. False for none.
    /// </summary>
    bool OfferMassage(World world, int slot, Visit visit, string? building)
    {
        if (Massage.FreeBed(world, building, _visits.Values) is not { } bed) return false;
        if (!Accepts(world, slot, visit, Prices.Percent(Service.Massage) / 100.0, MassageSalt)) return false;

        visit.Bed = bed.Id;
        visit.Phase = VisitPhase.Massage;
        return true;
    }

    /// <summary>
    /// Walks the customer to its bed, or lies it down when it is there (the visit goal's start); true when it lies there already.
    /// </summary>
    bool ToBed(World world, int slot, Visit visit)
    {
        if (BedOf(world, visit) is not { } bed) return false;
        if (visit.Lying) return true;
        if (Soaks.Flat(world.Characters.Next[slot].Position, bed.Site.Position) <= BedReach) return LieDown(world, slot, visit, bed.Fixture);

        return TaskStart.Walk(world, slot, bed.Site.Position, SpeedMode.Walk, Row, TaskSource.Goal) == StartResult.Started;
    }

    /// <summary>
    /// A customer on the massage: lies down on arrival; massaged while someone works the station, else waiting; paid once the massage is done, and
    /// leaving after waiting too long or when it got up.
    /// </summary>
    void Massaging(World world, int slot, Visit visit, double hours)
    {
        if (BedOf(world, visit) is not { } bed)
        {
            Settle(world, slot, visit, "its massage bed is gone");
            return;
        }

        if (!visit.Lying)
        {
            Approach(world, slot, visit, bed, hours);
            return;
        }

        if (world.Characters.Cold(slot) is not { InSomething: HeldState.Bed })
        {
            Settle(world, slot, visit, "it got up from the massage bed");
            return;
        }

        Rub(world, visit, bed.Site, hours);
        if (visit.Massaged >= Massage.Hours) Settle(world, slot, visit, $"massage {visit.Massaged:0.0} h");
        else if (visit.BedWait > Massage.MostWait) Settle(world, slot, visit, "nobody came to the massage station");
    }

    /// <summary>
    /// A customer not lying yet: lies down at the bed when it is there and free; waits there meanwhile.
    /// </summary>
    void Approach(World world, int slot, Visit visit, (BuildSite Site, Fixture Fixture) bed, double hours)
    {
        ref readonly var hot = ref world.Characters.Next[slot];
        if (Soaks.Flat(hot.Position, bed.Site.Position) > BedReach || hot.Task == (byte)CharacterTask.GoTo) return;
        if (LieDown(world, slot, visit, bed.Fixture)) return;

        visit.BedWait += hours;
        if (visit.BedWait > Massage.MostWait) Settle(world, slot, visit, "its massage bed was taken");
    }

    /// <summary>
    /// Lies the customer on the bed; false when the bed would not take it.
    /// </summary>
    bool LieDown(World world, int slot, Visit visit, Fixture bed)
    {
        visit.Lying = world.Fixtures.EnterBed(world, world.Characters.IdOf(slot), bed);
        return visit.Lying;
    }

    /// <summary>
    /// An hour of massage pays the massage price times the attendant's pay factor (Dexterity, robe); time with nobody at the station is waiting.
    /// </summary>
    void Rub(World world, Visit visit, BuildSite bed, double hours)
    {
        double? attendant = Massage.Station(world, bed) is { } station ? Massage.Attendant(world, station) : null;
        if (attendant is not { } skill)
        {
            visit.BedWait += hours;
            return;
        }

        visit.Massaged += hours;
        visit.MassagePay += Prices.Price(Service.Massage, TubKind.Small) * hours * skill;
    }

    /// <summary>
    /// The massage is over: the customer gets up and pays for the soak, the tea and the massage so far.
    /// </summary>
    void Settle(World world, int slot, Visit visit, string why)
    {
        if (visit.Lying) world.Fixtures.LeaveBed(world, world.Characters.IdOf(slot));
        visit.Lying = false;
        visit.Owed += (long)Math.Round(visit.MassagePay);
        if (ModGoals.Holds(world, slot, Row)) ModGoals.Done(world, slot, Row);

        var kind = CustomerTypes.Find(visit.Kind);
        if (kind is null) Leave(world, slot, visit);
        else Pay(world, slot, visit, kind, why);
    }

    /// <summary>
    /// The visit's massage bed and its fixture while both stand; null once either is gone.
    /// </summary>
    static (BuildSite Site, Fixture Fixture)? BedOf(World world, Visit visit)
    {
        if (visit.Bed is null || world.Buildings.Find(visit.Bed) is not { Destroyed: false } site) return null;

        return Massage.BedOf(world, site) is { } fixture ? (site, fixture) : null;
    }

    #endregion

    #region Views

    /// <summary>
    /// What a counter's check-in waits for, by <see cref="CheckIn"/>'s order: a clerk, then a warm tub with a free seat in its building; null when
    /// a customer at it would check in now.
    /// </summary>
    public CheckInNeed? Waiting(World world, BuildSite counter, IReadOnlyList<Tub> tubs)
    {
        if (!Staffed(world, counter)) return CheckInNeed.Clerk;

        return FreeTub(counter, tubs) is null ? CheckInNeed.WarmTub : null;
    }

    /// <summary>
    /// The debug row's lines: each group and each visit.
    /// </summary>
    public void Describe(World world, StringBuilder text)
    {
        text.AppendLine($"Customers: {(Open ? "open" : "closed")}, next group at hour {NextGroupAt:0.0}, prices "
            + string.Join("/", Enum.GetValues<Service>().Select(Prices.Percent)) + "%");
        foreach (var group in _groups.Values)
            text.AppendLine($"{group.Tag}: {group.Kind} to {group.Counter}, {(group.Enlisted ? "arrived" : "on the way")}");

        foreach (var visit in _visits.Values)
        {
            string name = world.Characters.NameOf(world.LastingIds.Find(visit.Customer));
            text.AppendLine($"{name}: {visit.Phase}, waited {visit.Waited:0.0} h, price {visit.Price}, owes {visit.Owed}"
                + $"{(visit.DirtyTub ? ", dirty tub" : "")}{(visit.Bed is null ? "" : $", massaged {visit.Massaged:0.0} h")}");
        }
    }

    #endregion
}
