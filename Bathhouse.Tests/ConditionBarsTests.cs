using Bathhouse.Bathing;
using Meitou.Data.Gameplay.Construction;
using Meitou.Simulation.Production;

namespace Bathhouse.Tests;

/// <summary>
/// The tub's and tea pieces' Condition-column bars, and the cold tea bench's give-back: the engine members the postfix reaches by name still exist (a rename would silently drop the bars), and
/// the bars say what the tub's state is.
/// </summary>
public sealed class ConditionBarsTests
{
    [Fact]
    public void The_engine_still_has_what_the_patch_needs()
    {
        Assert.True(ConditionBars.Supported);
        Assert.NotNull(ConditionBars.Row("Clean", 1, "", "tip"));
    }

    [Fact]
    public void A_used_tub_reads_dirty_and_a_scrubbed_one_clean()
    {
        var hot = new BuildingHeat(Heat.Hot, 1, 1, 1, 2, 4, 10);

        var dirty = PanelBars.TubBars(hot, 1, 2, dirty: true).ToList();
        var clean = PanelBars.TubBars(hot, 0, 2, dirty: false).ToList();

        Assert.Equal(("Dirty", 0f), (dirty[0].Name, dirty[0].Fraction));
        Assert.Equal(("Clean", 1f), (clean[0].Name, clean[0].Fraction));
        Assert.Equal(("Water: hot", 1f, "1/2"), (dirty[1].Name, dirty[1].Fraction, dirty[1].Value));
    }

    [Fact]
    public void A_tea_piece_shows_its_boiler_and_water()
    {
        var bare = PanelBars.TeaBars(new BuildingHeat(Heat.NoBoiler, 0, 0, 0, 0, 0, 0)).ToList();
        var ready = PanelBars.TeaBars(new BuildingHeat(Heat.Hot, 1, 1, 1, 0, 4, 12)).ToList();

        Assert.Equal(("Steam boiler: none", 0f), (bare[0].Name, bare[0].Fraction));
        Assert.Equal(("No Water in tanks", 0f), (bare[1].Name, bare[1].Fraction));
        Assert.Equal(("Steam boiler: lit", 1f, "1/1"), (ready[0].Name, ready[0].Fraction, ready[0].Value));
        Assert.Equal(("Water in tanks", 1f, "12"), (ready[1].Name, ready[1].Fraction, ready[1].Value));
    }

    [Fact]
    public void A_cold_tea_bench_gets_back_what_it_drew_but_keeps_what_was_loaded()
    {
        var drawn = new InputSlot(new RecipeInput("tea", 5, 1)) { Count = 2, Level = 0.4f };
        var loaded = new InputSlot(new RecipeInput("teaware", 5, 1)) { Count = 4, Level = 0.9f };

        TeaWorks.GiveBack([drawn, loaded], [(3, 0.2f), (3, 1f)]);

        Assert.Equal((3, 0.2f), (drawn.Count, drawn.Level));
        Assert.Equal((4, 0.9f), (loaded.Count, loaded.Level));
    }

    [Fact]
    public void A_boiler_shows_the_hours_its_water_lasts()
    {
        var bars = PanelBars.BoilerBars(new BuildingHeat(Heat.Hot, 2, 2, 2, 6, 8, 6), waterPerDay: 2).ToList();

        Assert.Equal(("Water left: 36 h", 1f, "6"), (bars[0].Name, bars[0].Fraction, bars[0].Value));
        Assert.Equal(("Seats heated", 1f, "8/6"), (bars[1].Name, bars[1].Fraction, bars[1].Value));
    }
}
