using Bathhouse.Bathing;

namespace Bathhouse.Customers;

/// <summary>
/// What a customer pays for (docs/bathhouse.md "Prices").
/// </summary>
public enum Service
{
    /// <summary>
    /// The soak; its base price depends on the tub.
    /// </summary>
    Bath,

    /// <summary>
    /// The towel taken at check-in.
    /// </summary>
    Towel,

    /// <summary>
    /// The soap taken at check-in.
    /// </summary>
    Soap,

    /// <summary>
    /// A Tea Set bought from a tea table after the soak.
    /// </summary>
    Tea,

    /// <summary>
    /// An hour of massage, times the attendant's skill.
    /// </summary>
    Massage,
}

/// <summary>
/// The price list (docs/bathhouse.md "Prices"): each service's price is a percent of its suggested price, which is its base price times the
/// reputation factor. The counter's panel steps each percent through <see cref="Steps"/>. Saved and hashed.
/// </summary>
public sealed class Pricing
{
    /// <summary>
    /// The percents a counter's price line steps through.
    /// </summary>
    public static readonly int[] Steps = [50, 75, 100, 125, 150, 200];

    /// <summary>
    /// How fast the chance of taking a price above a customer's tolerance falls: e^(-k (ratio / tolerance - 1)), 13% at a quarter over.
    /// </summary>
    const double Falloff = 8;

    /// <summary>
    /// Each service's percent of its suggested price, in <see cref="Service"/> order.
    /// </summary>
    readonly int[] _percents = [100, 100, 100, 100, 100];

    /// <summary>
    /// A service's percent.
    /// </summary>
    public int Percent(Service service) => _percents[(int)service];

    /// <summary>
    /// Sets a service's percent (clamped to the steps' range).
    /// </summary>
    public void Set(Service service, int percent) => _percents[(int)service] = Math.Clamp(percent, Steps[0], Steps[^1]);

    /// <summary>
    /// The step after a percent, wrapping to the first.
    /// </summary>
    public static int Next(int percent) => Steps.FirstOrDefault(s => s > percent, Steps[0]);

    /// <summary>
    /// The step below <paramref name="percent"/> (the lowest stays).
    /// </summary>
    public static int Previous(int percent) => Steps.LastOrDefault(s => s < percent, Steps[0]);

    /// <summary>
    /// The step above <paramref name="percent"/> (the highest stays).
    /// </summary>
    public static int Higher(int percent) => Steps.FirstOrDefault(s => s > percent, Steps[^1]);

    /// <summary>
    /// The reputation factor of the suggested prices: 1 until reputation lands (milestone 5).
    /// </summary>
    public static double ReputationFactor => 1;

    /// <summary>
    /// A service's base price in cats; a bath's by its tub. The towel, soap and tea are the trade worth of the item the customer takes (a Towel, a
    /// Soap, a Tea Set: ingredients plus 2% per hour worked, <c>ItemRecords</c>); the bath uses no goods and is 1.5 times what it was before the
    /// goods were priced so. The massage's is an hour by an attendant with Dexterity 100: a less skilled attendant earns less (<c>Massage.Pay</c>).
    /// </summary>
    public static int Base(Service service, TubKind tub) => service switch
    {
        Service.Towel => 27,
        Service.Soap => 18,
        Service.Tea => 69,
        Service.Massage => 300,
        _ => tub switch
        {
            TubKind.Small => 45,
            TubKind.Medium => 60,
            TubKind.Large => 75,
            TubKind.Oil => 90,
            _ => 120,
        },
    };

    /// <summary>
    /// A service's suggested price.
    /// </summary>
    public static double Suggested(Service service, TubKind tub) => Base(service, tub) * ReputationFactor;

    /// <summary>
    /// A service's price as set.
    /// </summary>
    public long Price(Service service, TubKind tub) => (long)Math.Round(Suggested(service, tub) * Percent(service) / 100);

    /// <summary>
    /// A check-in's price (the bath, and the towel and soap it gets) and its suggested price.
    /// </summary>
    public (long Price, double Suggested) Visit(TubKind tub, bool towel = true, bool soap = true)
    {
        Service[] services = [Service.Bath, .. towel ? [Service.Towel] : Array.Empty<Service>(), .. soap ? [Service.Soap] : Array.Empty<Service>()];
        return (services.Sum(s => Price(s, tub)), services.Sum(s => Suggested(s, tub)));
    }

    /// <summary>
    /// The chance a customer takes a price: 1 at or under its tolerance (price over suggested), then falling off fast.
    /// </summary>
    public static double AcceptChance(double ratio, double tolerance) =>
        ratio <= tolerance ? 1 : Math.Exp(-Falloff * (ratio / tolerance - 1));
}
