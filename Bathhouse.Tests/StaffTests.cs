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
    [InlineData(0f, 0.5)]
    [InlineData(50f, 1.0)]
    [InlineData(100f, 1.5)]
    [InlineData(150f, 1.5)]
    [InlineData(-10f, 0.5)]
    public void Massage_pay_scales_with_dexterity_from_half_to_one_and_a_half(float dexterity, double factor) =>
        Assert.Equal(factor, Massage.Skill(dexterity), 6);

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
        Assert.Equal(40, prices.Price(Service.Tea, TubKind.Medium));
        Assert.Equal(15, prices.Price(Service.Massage, TubKind.Medium));
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
        Assert.Null(StaffRoles.OfferedBy(Ids.Boiler));
    }
}
