using Bathhouse.Bathing;
using Bathhouse.Customers;

namespace Bathhouse.Tests;

/// <summary>
/// The customers' rules that need no world (docs/bathhouse.md "Customers"): price acceptance, the price steps, quality and pay, the complaints'
/// daily cap, demand by kind and the barks' coverage.
/// </summary>
public sealed class CustomerTests
{
    [Theory]
    [InlineData(0.8, 0.8, 1.0)]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(0.5, 1.0, 1.0)]
    public void A_price_at_or_under_tolerance_is_always_taken(double ratio, double tolerance, double chance) =>
        Assert.Equal(chance, Pricing.AcceptChance(ratio, tolerance));

    [Fact]
    public void Overpricing_turns_most_customers_away_quickly()
    {
        double quarterOver = Pricing.AcceptChance(1.25, 1.0);
        double doubled = Pricing.AcceptChance(2.0, 1.0);

        Assert.InRange(quarterOver, 0.1, 0.2);
        Assert.True(doubled < 0.001);
    }

    [Fact]
    public void Price_steps_cycle_through_the_list()
    {
        Assert.Equal(125, Pricing.Next(100));
        Assert.Equal(50, Pricing.Next(200));
    }

    [Fact]
    public void A_visit_at_suggested_prices_costs_its_base_prices()
    {
        var (price, suggested) = new Pricing().Visit(TubKind.Medium);

        Assert.Equal(105, price);
        Assert.Equal(105, suggested);
    }

    [Fact]
    public void A_good_visit_pays_the_price_and_a_weak_one_less()
    {
        var hot = Quality.Of(soaked: 3, lukewarm: 0, waited: 0, cold: false);
        var lukewarm = Quality.Of(soaked: 3, lukewarm: 3, waited: 1, cold: false);

        Assert.Equal(100, hot.Pay(100));
        Assert.Equal(0.35f, lukewarm.Total, 3);
        Assert.Equal(44, lukewarm.Pay(100));
    }

    [Fact]
    public void A_visit_without_towel_or_soap_costs_only_the_bath()
    {
        var pricing = new Pricing();

        Assert.Equal(60, pricing.Visit(TubKind.Medium, towel: false, soap: false).Price);
        Assert.Equal(87, pricing.Visit(TubKind.Medium, towel: true, soap: false).Price);
        Assert.Equal(78, pricing.Visit(TubKind.Medium, towel: false, soap: true).Price);
    }

    [Theory]
    [InlineData(true, true, 1.0f)]
    [InlineData(true, false, 0.9f)]
    [InlineData(false, true, 0.9f)]
    [InlineData(false, false, 0.8f)]
    public void A_towel_and_soap_each_raise_the_quality(bool towel, bool soap, float total) =>
        Assert.Equal(total, Quality.Of(soaked: 3, lukewarm: 0, waited: 0, cold: false, towel: towel, soap: soap).Total, 3);

    [Fact]
    public void A_robe_raises_the_quality() =>
        Assert.Equal(Quality.RobeWeight, Quality.Of(soaked: 3, lukewarm: 0, waited: 0, cold: false, robe: false).NoRobe);

    [Fact]
    public void Waiting_takes_off_at_most_its_cap() =>
        Assert.Equal(Quality.MostWait, Quality.Of(soaked: 2, lukewarm: 0, waited: 10, cold: false).Wait);

    [Theory]
    [InlineData(0, 1, 5, 1)]
    [InlineData(4, 1, 5, 1)]
    [InlineData(5, 1, 5, 0)]
    [InlineData(4, 3, 5, 1)]
    [InlineData(0, 0, 5, 0)]
    public void Complaints_cost_no_more_than_the_daily_cap(int lostToday, int perComplaint, int cap, int loss) =>
        Assert.Equal(loss, Complaints.Loss(lostToday, perComplaint, cap));

    [Fact]
    public void Nobles_demand_more_than_drifters() =>
        Assert.True(CustomerTypes.Find("nobles")!.Demand > CustomerTypes.Find("drifters")!.Demand);

    [Fact]
    public void Every_kind_has_a_line_for_every_event() => Assert.True(Barks.Complete());
}
