using System.Runtime.CompilerServices;
using System.Text;
using Bathhouse.Customers;
using Bathhouse.Staffing;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Population;

namespace Bathhouse.Bathing;

/// <summary>
/// Keeps a bath effect of any mod from being cleared by soaks (the debuff list's switch), or clears it again.
/// </summary>
public sealed record KeepDebuffCommand(string Effect, bool Keep) : SimCommand;

/// <summary>
/// Sends the player's selected characters to bathe in a tub (the tub panel's "Take a bath" button).
/// </summary>
public sealed record BatheCommand(string Tub) : SimCommand;

/// <summary>
/// Opens the bathhouse to customers, or closes it (the counter's switch).
/// </summary>
public sealed record OpenCommand(bool Open) : SimCommand;

/// <summary>
/// Sets a service's price, in percent of its suggested price (the counter's price lines).
/// </summary>
public sealed record PriceCommand(Service Service, int Percent) : SimCommand;

/// <summary>
/// Sends a group of a customer kind to the open counter now (the debug row).
/// </summary>
public sealed record SendCustomersCommand(string Kind) : SimCommand;

/// <summary>
/// Gives the player's selected characters a staff job at a piece (a piece's "Assign selected" button).
/// </summary>
public sealed record AssignStaffCommand(string Site, StaffRole Role) : SimCommand;

/// <summary>
/// What a counter's panel shows: whether the bathhouse is open, each service's percent (in <see cref="Service"/> order) and a medium tub's visit
/// price, the groups and visits under way, the latest payment or complaint and the recent ones (newest first).
/// </summary>
public readonly record struct CounterView(bool Open, int[] Percents, long VisitPrice, int Groups, int Visits, string Latest, string[] Recent)
{
    /// <summary>
    /// A service's percent.
    /// </summary>
    public int Percent(Service service) => Percents[(int)service];
}

/// <summary>
/// What a counter's check-in line shows: what it waits for (null: ready), and whether a customer would get a towel and soap.
/// </summary>
public readonly record struct CheckInView(CheckInNeed? Need, bool Towel, bool Soap);

/// <summary>
/// What the ledger's guest list shows: the groups on their way and the guests whose visits run.
/// </summary>
public sealed record GuestsNow(string[] OnTheWay, GuestView[] Guests);

/// <summary>
/// What the review board shows: its summary and the finished visits of the last three days, newest first.
/// </summary>
public sealed record ReviewsNow(GuestSummary Summary, GuestRecord[] Records);

/// <summary>
/// What the panel of a tub or boiler shows: its building's heat, and for a tub the seats taken.
/// </summary>
public readonly record struct PieceView(BuildingHeat Heat, int Taken, int Seats);

/// <summary>
/// The bathhouse in one world (docs/bathhouse.md "Heated tubs", "Customers", "Staff"): heating checked, bathers sent, soaks, customers and staff
/// errands stepped once a game second in its serial step; the soaks, the water owed, the kept debuffs, the customers and the dirty tubs saved in
/// the mod's sidecar and hashed.
/// </summary>
public sealed class BathhouseSystem : ITickSystem, IStateHashed, IModSaveData, IChooserOwner
{
    #region Ticking

    /// <summary>
    /// Ticks between checks (one game second at 30 ticks a second).
    /// </summary>
    public const int CheckEveryTicks = 30;

    /// <summary>
    /// The boilers' heat.
    /// </summary>
    readonly Heating _heating = new();

    /// <summary>
    /// The soaks and the bath goal.
    /// </summary>
    readonly Soaks _soaks;

    /// <summary>
    /// The customers and the visit goal.
    /// </summary>
    readonly Clientele _clientele;

    /// <summary>
    /// The dirty tubs.
    /// </summary>
    readonly TubCare _care = new();

    /// <summary>
    /// The staff jobs and their errands.
    /// </summary>
    readonly Staff _staff;

    /// <summary>
    /// Keeps the massage beds out of the game's own bed searches.
    /// </summary>
    readonly PrivateBeds _privateBeds;

    /// <summary>
    /// The attended massage beds' fast healing.
    /// </summary>
    readonly BedHealing _bedHealing = new();

    /// <summary>
    /// The customer kinds the debug row asked to send since the last check.
    /// </summary>
    readonly List<string> _sendKinds = [];

    /// <summary>
    /// The mod's random purpose, for towel rolls.
    /// </summary>
    readonly RngPurpose _purpose;

    /// <summary>
    /// The player's characters sent to bathe since the last check, with their tub's site, in command order.
    /// </summary>
    readonly List<(CharacterId Who, string Tub)> _sent = [];

    /// <summary>
    /// The game hour of the last check; NaN before the first.
    /// </summary>
    double _lastHours = double.NaN;

    /// <summary>
    /// A system for one world.
    /// </summary>
    public BathhouseSystem(RngPurpose purpose, PopulationData data)
    {
        _purpose = purpose;
        _soaks = new Soaks(_heating, Finished, data);
        _clientele = new Clientele(_heating, _soaks, _care, data, purpose);
        _staff = new Staff(new Plans(_soaks, _care));
        _privateBeds = new PrivateBeds(data.Relations);
    }

    public void Register(ChooserRegistry chooser)
    {
        chooser.Goal(Soaks.Row, _soaks);
        chooser.Goal(Clientele.Row, _clientele);
        foreach (var role in StaffRoles.All) chooser.Jobs(StaffRoles.TypeOf(role), _staff);
    }

    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command is BatheCommand bathe) Bathe(world, bathe.Tub);
            else if (command is KeepDebuffCommand keep) Keep(keep);
            else if (command is OpenCommand open) _clientele.Open = open.Open;
            else if (command is PriceCommand price) _clientele.Prices.Set(price.Service, price.Percent);
            else if (command is SendCustomersCommand send) _sendKinds.Add(send.Kind);
            else if (command is AssignStaffCommand assign) StaffRoles.Assign(world, assign.Site, assign.Role);
        }
    }

    /// <summary>
    /// Notes the player's selected characters as sent to the tub; the next check sends them.
    /// </summary>
    void Bathe(World world, string tub)
    {
        foreach (var who in world.Player.Selection) _sent.Add((who, tub));
    }

    /// <summary>
    /// Keeps a bad effect from being cleared by soaks, or clears it again.
    /// </summary>
    void Keep(KeepDebuffCommand keep)
    {
        if (keep.Keep) _kept.Add(keep.Effect);
        else _kept.Remove(keep.Effect);

        _keptView = new SortedSet<string>(_kept, StringComparer.Ordinal);
    }

    public void SlowWorld(World world)
    {
        if (world.Tick % CheckEveryTicks != 0) return;

        var settings = BathhouseSettings.Current;
        double hours = double.IsNaN(_lastHours) ? 0 : Math.Max(0, world.GameHours - _lastHours);
        _lastHours = world.GameHours;

        var tubs = Tubs.All(world);
        _heating.Check(world, tubs, settings);
        SendPlayers(world, tubs);
        SendCustomers(world, tubs);
        _soaks.Step(world, hours, tub => Length(tub.Kind, settings));
        _clientele.Step(world, tubs, settings, hours);
        StaffRoles.TakeClickedJobs(world);
        _staff.Step(world);
        _privateBeds.Step(world);
        _bedHealing.Step(world);
        Publish(world, tubs);
    }

    /// <summary>
    /// Sends the groups the debug row asked for; one that cannot come is posted.
    /// </summary>
    void SendCustomers(World world, List<Tub> tubs)
    {
        foreach (string key in _sendKinds)
        {
            if (CustomerTypes.Find(key) is { } kind && _clientele.SendNow(world, tubs, kind)) continue;

            world.Post($"Bathhouse: no {key} group sent (needs a known kind with a source, and a counter in a building with a warm tub).");
        }

        _sendKinds.Clear();
    }

    /// <summary>
    /// Sends the player's characters sent to bathe to their tub; one that cannot go says why.
    /// </summary>
    void SendPlayers(World world, List<Tub> tubs)
    {
        var sent = _sent.ToList();
        _sent.Clear();
        foreach (var (who, site) in sent)
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || !world.Characters.Next[slot].Alive) continue;
            if (world.Characters.Cold(slot) is not { IsPlayer: true }) continue;
            if (tubs.Find(t => t.Site.Id == site) is not { } tub) continue;
            if (_soaks.Send(world, slot, tub, player: true)) continue;

            var heat = _heating.Of(tub.Building);
            string why = heat.Warm ? "This tub is full." : $"The water is cold: {Describe(heat.Heat)}.";
            world.Bark(world.Characters.IdOf(slot), why);
        }
    }

    /// <summary>
    /// What a finished soak earns: the Soothed buff, bad effects cleared and a towel used, the tub dirty, then a customer pays or complains. An
    /// abandoned one earns nothing.
    /// </summary>
    void Finished(World world, int slot, Soak soak, SoakEnd end)
    {
        if (end == SoakEnd.Abandoned)
        {
            if (!soak.Player) _clientele.Bathed(world, slot, soak, end);
            return;
        }

        var settings = BathhouseSettings.Current;
        var who = world.Characters.IdOf(slot);
        Soothing.Soothe(world, who, settings.BuffDays);
        if (settings.ClearDebuffs) Soothing.Clear(world, who, _kept);

        _care.Used(soak.Tub);
        string? building = world.Buildings.Find(soak.Tub)?.Parent;
        Towels.Dry(world, slot, building, _purpose, settings.DirtyChance);
        if (!soak.Player) _clientele.Bathed(world, slot, soak, end);
    }

    /// <summary>
    /// The soak length of a kind of tub, in game hours.
    /// </summary>
    public static double Length(TubKind kind, BathhouseSettings settings) => kind switch
    {
        TubKind.Small => settings.SmallSoakHours,
        TubKind.Medium => settings.MediumSoakHours,
        TubKind.Large => settings.LargeSoakHours,
        _ => settings.SpecialSoakHours,
    };

    /// <summary>
    /// The soak length of a kind of tub under the current settings.
    /// </summary>
    static double LengthNow(TubKind kind) => Length(kind, BathhouseSettings.Current);

    /// <summary>
    /// A cold state's reason, for barks and panels.
    /// </summary>
    public static string Describe(Heat heat) => heat switch
    {
        Heat.Hot => "hot",
        Heat.Lukewarm => "lukewarm, more seats than the boilers heat",
        Heat.Outdoors => "a tub needs a building with a steam boiler",
        Heat.NoBoiler => "no steam boiler in this building",
        Heat.BoilerOff => "the steam boiler is switched off",
        Heat.NoPower => "the steam boiler has no power",
        _ => "no Water in this building's tanks",
    };

    #endregion

    #region Views

    /// <summary>
    /// The latest view of each tub and boiler by site id (written in the tick, read on the host's thread).
    /// </summary>
    volatile Dictionary<string, PieceView> _pieces = [];

    /// <summary>
    /// The latest debug view (written in the tick, read on the host's thread).
    /// </summary>
    volatile string _view = "No bathhouse yet.";

    /// <summary>
    /// The view of a tub or boiler; null for another site.
    /// </summary>
    public PieceView? Piece(string? site) => site is not null && _pieces.TryGetValue(site, out var view) ? view : null;

    /// <summary>
    /// The latest view of the counters (written in the tick, read on the host's thread).
    /// </summary>
    volatile StrongBox<CounterView> _counter = new(new CounterView(true, [100, 100, 100, 100, 100], 0, 0, 0, "No customers yet.", []));

    /// <summary>
    /// What a counter's panel shows.
    /// </summary>
    public CounterView Counter => _counter.Value;

    /// <summary>
    /// The latest check-in view of each counter by site id (written in the tick, read on the host's thread).
    /// </summary>
    volatile Dictionary<string, CheckInView> _checkIns = [];

    /// <summary>
    /// A counter's check-in view; null for another site.
    /// </summary>
    public CheckInView? CheckIn(string? site) => site is not null && _checkIns.TryGetValue(site, out var view) ? view : null;

    /// <summary>
    /// The first counter's site id; null for none.
    /// </summary>
    public string? FirstCounter => _checkIns.Keys.Order(StringComparer.Ordinal).FirstOrDefault();

    /// <summary>
    /// The debug row's view: each building's heat, then each soak.
    /// </summary>
    public string View => _view;

    /// <summary>
    /// The latest guest list (written in the tick, read on the host's thread).
    /// </summary>
    volatile GuestsNow _guests = new([], []);

    /// <summary>
    /// The ledger's guest list.
    /// </summary>
    public GuestsNow Guests => _guests;

    /// <summary>
    /// The latest review board view (written in the tick, read on the host's thread).
    /// </summary>
    volatile ReviewsNow _reviews = new(default, []);

    /// <summary>
    /// The review board's view.
    /// </summary>
    public ReviewsNow Reviews => _reviews;

    /// <summary>
    /// Publishes the panels' and the debug row's views.
    /// </summary>
    void Publish(World world, List<Tub> tubs)
    {
        var pieces = new Dictionary<string, PieceView>(StringComparer.Ordinal);
        foreach (var tub in tubs)
        {
            int taken = _soaks.All.Count(s => s.Tub == tub.Site.Id);
            pieces[tub.Site.Id] = new PieceView(_heating.Of(tub.Building), taken, tub.Seats);
        }

        foreach (var site in world.Buildings.All.Where(s => s.Record == Records.Ids.Boiler))
            pieces[site.Id] = new PieceView(_heating.Of(site.Parent), 0, 0);

        _pieces = pieces;
        _counter = new StrongBox<CounterView>(CounterNow());
        _checkIns = CheckInsNow(world, tubs);
        _guests = new GuestsNow(GuestList.OnTheWay(world, _clientele), GuestList.Of(world, _clientele, _soaks, tubs, LengthNow));
        var book = _clientele.Book;
        _reviews = new ReviewsNow(book.Summary(world.GameHours), [.. book.Records.Reverse()]);
        _view = Describe(world, tubs);
    }

    /// <summary>
    /// The counters' view now.
    /// </summary>
    CounterView CounterNow()
    {
        var prices = _clientele.Prices;
        int running = _clientele.Visits.Count(v => v.Phase != VisitPhase.Done);
        int[] percents = Enum.GetValues<Service>().Select(prices.Percent).ToArray();
        long visit = prices.Visit(TubKind.Medium).Price;
        return new CounterView(_clientele.Open, percents, visit, _clientele.Groups.Count, running, _clientele.Latest, [.. _clientele.Recent]);
    }

    /// <summary>
    /// What each counter's check-in lacks now.
    /// </summary>
    Dictionary<string, CheckInView> CheckInsNow(World world, List<Tub> tubs)
    {
        var views = new Dictionary<string, CheckInView>(StringComparer.Ordinal);
        foreach (var counter in world.Buildings.All.Where(Spawner.IsCounter))
        {
            var stock = Stock.Of(world, counter);
            views[counter.Id] = new CheckInView(_clientele.Waiting(world, counter, tubs), stock.Towels is not null, stock.Soap is not null);
        }

        return views;
    }

    /// <summary>
    /// The debug view's text.
    /// </summary>
    string Describe(World world, List<Tub> tubs)
    {
        var text = new StringBuilder();
        foreach (string? building in tubs.Select(t => t.Building).Distinct())
        {
            var heat = _heating.Of(building);
            text.AppendLine($"{building ?? "outdoors"}: {Describe(heat.Heat)} (boilers {heat.Lit}/{heat.On}/{heat.Boilers} lit/on/built, "
                + $"seats {heat.Seats} of {heat.Capacity} heated, water {heat.Water})");
        }

        foreach (var soak in _soaks.All)
        {
            string name = world.Characters.NameOf(world.LastingIds.Find(soak.Bather));
            text.AppendLine($"{name}: tub {soak.Tub} seat {soak.Seat}, {soak.Soaked:0.0} h soaked{(soak.Seated ? ", in the water" : "")}");
        }

        _clientele.Describe(world, text);
        text.AppendLine($"Staff: {_staff.Busy} on an errand; dirty tubs: {(_care.Dirty.Count == 0 ? "none" : string.Join(", ", _care.Dirty))}");
        return text.ToString();
    }

    #endregion

    #region Debuffs

    /// <summary>
    /// The ids of the bad effects soaks leave alone (the debuff list's switches off). Saved and hashed.
    /// </summary>
    readonly SortedSet<string> _kept = new(StringComparer.Ordinal);

    /// <summary>
    /// A copy of <see cref="_kept"/> for the panel (written in the tick, read on the host's thread).
    /// </summary>
    volatile SortedSet<string> _keptView = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether soaks leave the effect alone.
    /// </summary>
    public bool Keeps(string effect) => _keptView.Contains(effect);

    #endregion

    #region Saving

    /// <summary>
    /// The sidecar layout's version.
    /// </summary>
    public const int SaveVersion = 6;

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(SaveVersion);
        w.Write(_kept.Count);
        foreach (string effect in _kept) w.Write(effect);

        w.Write(_heating.Owed.Count);
        foreach (var (building, owed) in _heating.Owed)
        {
            w.Write(building);
            w.Write(owed);
        }

        WriteSoaks(w);
        ClienteleSave.Write(w, _clientele, _care);
    }

    /// <summary>
    /// Writes the soaks.
    /// </summary>
    void WriteSoaks(BinaryWriter w)
    {
        w.Write(_soaks.All.Count);
        foreach (var soak in _soaks.All)
        {
            w.Write(soak.Bather.Value);
            w.Write(soak.Tub);
            w.Write(soak.Seat);
            w.Write(soak.Player);
            w.Write(soak.SentAt);
            w.Write(soak.Soaked);
            w.Write(soak.Lukewarm);
            w.Write(soak.Robed switch { null => (byte)0, true => (byte)1, false => (byte)2 });
            w.Write(soak.Changed);
            w.Write(soak.Stowed);
        }
    }

    public void Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        int version = r.ReadInt32();
        if (version is < 1 or > SaveVersion) return;

        _kept.Clear();
        int kept = r.ReadInt32();
        for (int k = 0; k < kept; k++) _kept.Add(r.ReadString());
        _keptView = new SortedSet<string>(_kept, StringComparer.Ordinal);

        _heating.Owed.Clear();
        int owed = r.ReadInt32();
        for (int k = 0; k < owed; k++) _heating.Owed[r.ReadString()] = r.ReadDouble();

        ReadSoaks(r, version);
        if (version >= 2) ClienteleSave.Read(r, _clientele, _care, version);
    }

    /// <summary>
    /// Reads the soaks; their bathers are found again by lasting id at the next check and walk back into their tubs.
    /// </summary>
    void ReadSoaks(BinaryReader r, int version)
    {
        int soaks = r.ReadInt32();
        for (int k = 0; k < soaks; k++)
        {
            var soak = new Soak
            {
                Bather = new LastingId(r.ReadUInt64()),
                Tub = r.ReadString(),
                Seat = r.ReadInt32(),
                Player = r.ReadBoolean(),
                SentAt = r.ReadDouble(),
            };
            soak.Soaked = r.ReadDouble();
            soak.Lukewarm = r.ReadDouble();
            if (version >= 5) ReadRobe(r, soak);
            _soaks.Restore(soak);
        }
    }

    /// <summary>
    /// Reads a soak's robe (layout 5).
    /// </summary>
    static void ReadRobe(BinaryReader r, Soak soak)
    {
        soak.Robed = r.ReadByte() switch { 1 => true, 2 => false, _ => null };
        soak.Changed = r.ReadBoolean();
        soak.Stowed = r.ReadString();
    }

    #endregion

    #region Hashing

    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_kept.Count);
        foreach (string effect in _kept) hasher.Add(Rng.StableHash(effect));

        hasher.Add(_heating.Owed.Count);
        foreach (var (building, owed) in _heating.Owed)
        {
            hasher.Add(Rng.StableHash(building));
            hasher.Add(owed);
        }

        hasher.Add(_soaks.All.Count);
        foreach (var soak in _soaks.All)
        {
            hasher.Add(soak.Bather.Value);
            hasher.Add(Rng.StableHash(soak.Tub));
            hasher.Add(soak.Seat);
            hasher.Add(soak.Soaked);
            hasher.Add(soak.Seated);
        }

        ClienteleSave.Hash(ref hasher, _clientele, _care);
    }

    #endregion
}
