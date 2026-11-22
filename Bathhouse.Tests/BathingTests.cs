using Bathhouse.Customers;
using Bathhouse.Bathing;
using Meitou.Data;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Population;

namespace Bathhouse.Tests;

/// <summary>
/// The heated tubs' rules that need no world (docs/bathhouse.md "Heated tubs"): the heat rule, the seats, the towel roll, which effects a soak
/// clears, the soak lengths and the sidecar round trip.
/// </summary>
public sealed class BathingTests
{
    [Theory]
    [InlineData(0, 0, 0, 5, 1, Heat.NoBoiler)]
    [InlineData(2, 0, 0, 5, 1, Heat.BoilerOff)]
    [InlineData(2, 1, 0, 5, 1, Heat.NoPower)]
    [InlineData(1, 1, 1, 0, 1, Heat.NoWater)]
    [InlineData(1, 1, 1, 5, 4, Heat.Hot)]
    [InlineData(1, 1, 1, 5, 5, Heat.Lukewarm)]
    [InlineData(2, 2, 2, 5, 8, Heat.Hot)]
    public void A_building_is_heated_by_its_lit_boilers_with_water(int boilers, int on, int lit, int water, int seats, Heat heat) =>
        Assert.Equal(heat, Heating.Rule(boilers, on, lit, water, seats, seatsPerBoiler: 4, needsWater: true));

    [Fact]
    public void Boilers_that_drink_no_water_need_none() =>
        Assert.Equal(Heat.Hot, Heating.Rule(1, 1, 1, water: 0, seats: 1, seatsPerBoiler: 4, needsWater: false));

    [Theory]
    [InlineData(TubKind.Small, 1)]
    [InlineData(TubKind.Medium, 2)]
    [InlineData(TubKind.Large, 4)]
    [InlineData(TubKind.Oil, 2)]
    [InlineData(TubKind.Blood, 2)]
    public void Each_seat_of_a_tub_has_its_own_spot(TubKind kind, int seats)
    {
        var spots = Enumerable.Range(0, Tubs.SeatsOf(kind)).Select(seat => Tubs.SeatOffset(kind, seat)).ToList();

        Assert.Equal(seats, spots.Count);
        Assert.Equal(seats, spots.Distinct().Count());
        Assert.All(spots, spot => Assert.True(spot.Length() < Tubs.Approach));
    }

    [Fact]
    public void Towels_come_back_dirty_as_often_as_the_setting_says()
    {
        var who = new CharacterId(3, 1);

        int dirty = Enumerable.Range(0, 10_000).Count(tick => Towels.ComesBackDirty(42, tick, who, RngPurpose.Test, dirtyChance: 80));

        Assert.InRange(dirty, 7_800, 8_200);
    }

    [Fact]
    public void A_soak_clears_bad_effects_but_the_kept_ones()
    {
        var poison = Register(new StatusEffect("bathhouse.test.poison", "Poisoned") { Tone = StatusEffectTone.Bad });
        var curse = Register(new StatusEffect("bathhouse.test.curse", "Cursed") { Tone = StatusEffectTone.Bad });
        var blessing = Register(new StatusEffect("bathhouse.test.blessing", "Blessed") { Tone = StatusEffectTone.Good });
        string[] held = [poison.Id, blessing.Id, curse.Id, "bathhouse.test.unregistered"];

        var cleared = Soothing.Cleared(held, new HashSet<string> { curse.Id });

        Assert.Equal([poison.Id], cleared.Select(e => e.Id));
    }

    [Fact]
    public void Each_kind_of_tub_has_its_soak_length()
    {
        var settings = new BathhouseSettings { SmallSoakHours = 1, MediumSoakHours = 2, LargeSoakHours = 3, SpecialSoakHours = 4 };

        double[] lengths = [.. Enum.GetValues<TubKind>().Select(kind => BathhouseSystem.Length(kind, settings))];

        Assert.Equal([1, 2, 3, 4, 4], lengths);
    }

    [Fact]
    public void The_sidecar_keeps_the_kept_debuffs_and_the_counter()
    {
        var first = NewSystem();
        SimCommand[] commands =
        [
            new KeepDebuffCommand("bathhouse.test.poison", Keep: true) { Tick = 0 },
            new OpenCommand(false) { Tick = 0 },
            new PriceCommand(Service.Towel, 150) { Tick = 0 },
            new PriceCommand(Service.Massage, 200) { Tick = 0 },
        ];
        first.Inputs(null!, commands);
        var saved = Saved(first);

        var second = NewSystem();
        second.Read(new MemoryStream(saved));

        Assert.True(second.Keeps("bathhouse.test.poison"));
        Assert.Equal(saved, Saved(second));
    }

    [Fact]
    public void A_version_2_sidecar_reads_with_tea_and_massage_at_full_price()
    {
        var old = NewSystem();
        old.Read(new MemoryStream(Version2(open: false, bath: 75, towel: 150, soap: 200)));

        var now = NewSystem();
        SimCommand[] commands =
        [
            new OpenCommand(false) { Tick = 0 },
            new PriceCommand(Service.Bath, 75) { Tick = 0 },
            new PriceCommand(Service.Towel, 150) { Tick = 0 },
            new PriceCommand(Service.Soap, 200) { Tick = 0 },
        ];
        now.Inputs(null!, commands);

        Assert.Equal(Saved(now), Saved(old));
    }

    /// <summary>
    /// A version 2 sidecar with no debuffs, water, soaks, groups or visits, the next group at hour 0 and no complaints yet.
    /// </summary>
    static byte[] Version2(bool open, int bath, int towel, int soap)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        int[] header = [2, 0, 0, 0];
        foreach (int value in header) w.Write(value);

        w.Write(open);
        int[] percents = [bath, towel, soap];
        foreach (int percent in percents) w.Write(percent);

        w.Write(0.0);
        int[] rest = [0, -1, 0, 0, 0];
        foreach (int value in rest) w.Write(value);

        w.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// A system over empty population data.
    /// </summary>
    static BathhouseSystem NewSystem() => new(RngPurpose.Test, new PopulationData { Db = new GameDatabase(), Sites = [], Factions = [] });

    /// <summary>
    /// The system's sidecar bytes.
    /// </summary>
    static byte[] Saved(BathhouseSystem system)
    {
        using var stream = new MemoryStream();
        system.Write(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Registers a test effect once per test run (the registry is the process's).
    /// </summary>
    static StatusEffect Register(StatusEffect effect)
    {
        if (StatusEffects.Find(effect.Id) is { } known) return known;

        StatusEffects.Add(effect);
        return effect;
    }
}
