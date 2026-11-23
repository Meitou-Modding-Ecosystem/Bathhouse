using Bathhouse.Bathing;
using Bathhouse.Customers;
using Bathhouse.Records;
using Bathhouse.Staffing;

namespace Bathhouse.Tests;

/// <summary>
/// Milestone 4's rules that need no world (docs/bathhouse.md "Staff", "Customers"): dirty tubs, the massage skill, the dirty tub's quality part,
/// the check-in price leaving tea and massage out, and the staff roles' types and pieces.
/// </summary>
public sealed class StaffTests
{
    [Fact]
    public void A_tub_is_dirty_from_its_soak_until_scrubbed()
    {
        var care = new TubCare();
        care.Used("tub-1");
        Assert.True(care.IsDirty("tub-1"));
        Assert.False(care.IsDirty("tub-2"));

        care.Scrubbed("tub-1");
        Assert.Empty(care.Dirty);
    }

    [Fact]
    public void The_blood_bath_takes_longer_to_clean()
    {
        Assert.Equal(TubCare.ScrubHours, TubCare.HoursFor(TubKind.Large));
        Assert.Equal(TubCare.MopHours, TubCare.HoursFor(TubKind.Blood));
    }

    [Theory]
    [InlineData(0f, 0.1)]
    [InlineData(50f, 0.55)]
    [InlineData(100f, 1.0)]
    [InlineData(150f, 1.0)]
    [InlineData(-10f, 0.1)]
    public void Massage_pay_scales_with_dexterity_from_a_tenth_to_all_of_the_price(float level, double factor) =>
        Assert.Equal(factor, Massage.Pay(level), 6);

    [Fact]
    public void A_robed_attendant_earns_more_but_never_past_the_price()
    {
        Assert.Equal(0.66, Massage.Pay(50f, robed: true), 6);
        Assert.Equal(1.0, Massage.Pay(90f, robed: true), 6);
    }

    [Fact]
    public void An_attendant_with_dexterity_100_earns_300_an_hour_at_the_suggested_price() =>
        Assert.Equal(300, new Pricing().Price(Service.Massage, TubKind.Small) * Massage.Pay(100f));

    [Fact]
    public void Massage_trains_dexterity_a_little()
    {
        var stats = new Meitou.Simulation.Bodies.CharacterStats();
        stats[Meitou.Data.Gameplay.Bodies.StatsEnumerated.Dexterity] = 20;
        for (int hour = 0; hour < 10; hour++) stats.Gain(Meitou.Data.Gameplay.Bodies.StatsEnumerated.Dexterity, Massage.TrainingPerHour);

        Assert.InRange(stats.Dexterity, 22, 24);
    }

    [Fact]
    public void Every_work_spot_stands_clear_of_its_piece()
    {
        foreach (string piece in (string[])[Ids.WashingMachine, Ids.TowelLoom, Ids.SmallTub, Ids.LargeTub, Ids.MassageStation, Ids.Hamper])
            Assert.True(WorkSpots.Out(piece) >= WorkSpots.Clearance + 2.5f, piece);
    }

    [Fact]
    public void A_dirty_tub_takes_a_fifth_off_quality()
    {
        var clean = Quality.Of(2, 0, 0, cold: false);
        var dirty = Quality.Of(2, 0, 0, cold: false, dirty: true);

        Assert.Equal(1f, clean.Total);
        Assert.Equal(1f - Quality.DirtWeight, dirty.Total, 5);
    }

    [Fact]
    public void Tea_and_massage_prices_leave_the_check_in_price_alone()
    {
        var prices = new Pricing();
        var before = prices.Visit(TubKind.Medium);
        prices.Set(Service.Tea, 200);
        prices.Set(Service.Massage, 50);

        Assert.Equal(before, prices.Visit(TubKind.Medium));
        Assert.Equal(138, prices.Price(Service.Tea, TubKind.Medium));
        Assert.Equal(150, prices.Price(Service.Massage, TubKind.Medium));
    }

    [Fact]
    public void Each_role_has_its_own_job_type_and_name()
    {
        foreach (var role in StaffRoles.All) Assert.Equal(role, StaffRoles.RoleOf(StaffRoles.TypeOf(role)));

        Assert.Equal(StaffRoles.All.Length, StaffRoles.All.Select(StaffRoles.TypeOf).Distinct().Count());
        Assert.Equal(StaffRoles.All.Length, StaffRoles.All.Select(StaffRoles.Name).Distinct().Count());
        Assert.Null(StaffRoles.RoleOf((Meitou.Data.Gameplay.Ai.TaskType)Ids.VisitGoalType));
    }

    [Fact]
    public void The_pieces_offer_their_jobs()
    {
        Assert.Equal(StaffRole.Clerk, StaffRoles.OfferedBy(Ids.Counter));
        Assert.Equal(StaffRole.Scrubber, StaffRoles.OfferedBy(Ids.BloodBath));
        Assert.Equal(StaffRole.Laundry, StaffRoles.OfferedBy(Ids.WashingMachine));
        Assert.Equal(StaffRole.Tea, StaffRoles.OfferedBy(Ids.TeaTable));
        Assert.Equal(StaffRole.Massage, StaffRoles.OfferedBy(Ids.MassageStation));
        Assert.Null(StaffRoles.OfferedBy(Ids.Boiler));
    }
}
