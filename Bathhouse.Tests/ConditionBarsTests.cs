using Bathhouse.Bathing;

namespace Bathhouse.Tests;

/// <summary>
/// The tub's Condition-column bars: the engine members the postfix reaches by name still exist (a rename would silently drop the bars), and
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

        var dirty = ConditionBars.Bars(new PieceView(hot, 1, 2, Dirty: true)).ToList();
        var clean = ConditionBars.Bars(new PieceView(hot, 0, 2)).ToList();

        Assert.Equal(("Dirty", 0f), (dirty[0].Name, dirty[0].Fraction));
        Assert.Equal(("Clean", 1f), (clean[0].Name, clean[0].Fraction));
        Assert.Equal(("Water: hot", 1f, "1/2"), (dirty[1].Name, dirty[1].Fraction, dirty[1].Value));
    }
}
