using System.Text;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;

namespace Bathhouse.Bathing;

/// <summary>
/// Keeps a bath effect of any mod from being cleared by soaks (the debuff list's switch), or clears it again.
/// </summary>
public sealed record KeepDebuffCommand(string Effect, bool Keep) : SimCommand;

/// <summary>
/// What the panel of a tub or boiler shows: its building's heat, and for a tub the seats taken.
/// </summary>
public readonly record struct PieceView(BuildingHeat Heat, int Taken, int Seats);

/// <summary>
/// The bathhouse in one world (docs/bathhouse.md "Heated tubs"): heating checked, bath verbs turned into soaks and soaks stepped once a game second
/// in its serial step; the soaks, the water owed and the kept debuffs saved in the mod's sidecar and hashed.
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
    /// The mod's random purpose, for towel rolls.
    /// </summary>
    readonly RngPurpose _purpose;

    /// <summary>
    /// The game hour of the last check; NaN before the first.
    /// </summary>
    double _lastHours = double.NaN;

    /// <summary>
    /// A system for one world.
    /// </summary>
    public BathhouseSystem(RngPurpose purpose)
    {
        _purpose = purpose;
        _soaks = new Soaks(_heating, Finished);
    }

    public void Register(ChooserRegistry chooser) => chooser.Goal(Soaks.Row, _soaks);

    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command is not KeepDebuffCommand keep) continue;

            if (keep.Keep) _kept.Add(keep.Effect);
            else _kept.Remove(keep.Effect);
            _keptView = new SortedSet<string>(_kept, StringComparer.Ordinal);
        }
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
        _soaks.Step(world, hours, tub => Length(tub.Kind, settings));
        Publish(world, tubs);
    }

    /// <summary>
    /// Sends the player's characters that took a bath verb to their tub; one that cannot go says why.
    /// </summary>
    void SendPlayers(World world, List<Tub> tubs)
    {
        foreach (var (slot, site) in BathJobs.Take(world))
        {
            if (tubs.Find(t => t.Site.Id == site) is not { } tub) continue;
            if (_soaks.Send(world, slot, tub, player: true)) continue;

            var heat = _heating.Of(tub.Building);
            string why = heat.Warm ? "This tub is full." : $"The water is cold: {Describe(heat.Heat)}.";
            world.Bark(world.Characters.IdOf(slot), why);
        }
    }

    /// <summary>
    /// What a finished soak earns: the Soothed buff, bad effects cleared and a towel used. An abandoned one earns nothing.
    /// </summary>
    void Finished(World world, int slot, Soak soak, SoakEnd end)
    {
        if (end == SoakEnd.Abandoned) return;

        var settings = BathhouseSettings.Current;
        var who = world.Characters.IdOf(slot);
        Soothing.Soothe(world, who, settings.BuffDays);
        if (settings.ClearDebuffs) Soothing.Clear(world, who, _kept);

        string? building = world.Buildings.Find(soak.Tub)?.Parent;
        Towels.Dry(world, slot, building, _purpose, settings.DirtyChance);
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
    /// The debug row's view: each building's heat, then each soak.
    /// </summary>
    public string View => _view;

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
        _view = Describe(world, tubs);
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

        return text.Length == 0 ? "No tubs built." : text.ToString();
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
    public const int SaveVersion = 1;

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
        }
    }

    public void Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (r.ReadInt32() is < 1 or > SaveVersion) return;

        _kept.Clear();
        int kept = r.ReadInt32();
        for (int k = 0; k < kept; k++) _kept.Add(r.ReadString());
        _keptView = new SortedSet<string>(_kept, StringComparer.Ordinal);

        _heating.Owed.Clear();
        int owed = r.ReadInt32();
        for (int k = 0; k < owed; k++) _heating.Owed[r.ReadString()] = r.ReadDouble();

        ReadSoaks(r);
    }

    /// <summary>
    /// Reads the soaks; their bathers are found again by lasting id at the next check and walk back into their tubs.
    /// </summary>
    void ReadSoaks(BinaryReader r)
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
            _soaks.Restore(soak);
        }
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
    }

    #endregion
}
