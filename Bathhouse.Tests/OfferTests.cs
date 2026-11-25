using Bathhouse.Bathing;
using Bathhouse.Customers;
using Bathhouse.Staffing;
using Meitou.Simulation.Production;

namespace Bathhouse.Tests;

/// <summary>
/// The staff jobs' offers (engine <c>AddJobOffer</c>) pass the engine's own checks, and every number the mod claims is in the README's list.
/// </summary>
public sealed class OfferTests
{
    [Fact]
    public void Each_role_offers_its_job_to_people_on_its_pieces()
    {
        foreach (var role in StaffRoles.All)
        {
            var offer = StaffRoles.Offer(role);

            Assert.Null(offer.Refusal());
            Assert.Equal(StaffRoles.TypeOf(role), offer.Type);
            Assert.Equal(ModJobTakers.People, offer.Takers);
            Assert.All(offer.Buildings, b => Assert.Equal(role, StaffRoles.OfferedBy(b)));
        }
    }

    [Fact]
    public void Every_claimed_number_is_listed_in_the_readme()
    {
        var listed = NumbersTests.Listed();
        int[] claimed = [(int)Soaks.Row, (int)Clientele.Row, .. StaffRoles.All.Select(r => (int)StaffRoles.TypeOf(r))];

        Assert.All(claimed, n => Assert.Contains(n, listed));
    }
}
