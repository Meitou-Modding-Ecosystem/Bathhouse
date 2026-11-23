using Bathhouse.Bathing;
using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// One guest on the ledger's guest list: name, kind, what it is doing, the time it has left there, its quality so far (NaN before it soaks or
/// waits) and what is costing it quality.
/// </summary>
public readonly record struct GuestView(string Name, string Kind, string Status, string Left, float Quality, string Issues);

/// <summary>
/// The ledger's guest list (docs/bathhouse.md "Guest book"): the groups still on their way, then every guest whose visit runs, with its quality so
/// far by the payment rule (<see cref="Quality.Of"/>). Built in the system's serial step for the host thread to read.
/// </summary>
public static class GuestList
{
    /// <summary>
    /// The groups on their way, as lines.
    /// </summary>
    public static string[] OnTheWay(World world, Clientele clientele) =>
    [
        .. clientele.Groups.Where(g => !g.Enlisted).Select(g =>
        {
            string kind = CustomerTypes.Find(g.Kind)?.Name ?? g.Kind;
            double gone = world.GameHours - g.SpawnedAt;
            return $"{kind} on their way: set out {gone:0.0} h ago, give up after {Clientele.GroupHours - gone:0.0} h more.";
        }),
    ];

    /// <summary>
    /// The guests whose visits run, in lasting-id order.
    /// </summary>
    public static GuestView[] Of(World world, Clientele clientele, Soaks soaks, IReadOnlyList<Tub> tubs, Func<TubKind, double> length)
    {
        var views = new List<GuestView>();
        foreach (var visit in clientele.Visits.Where(v => v.Phase != VisitPhase.Done))
        {
            var who = world.LastingIds.Find(visit.Customer);
            if (who.IsNone) continue;

            string name = world.Characters.NameOf(who);
            string kind = CustomerTypes.Find(visit.Kind)?.Name ?? visit.Kind;
            views.Add(visit.Phase switch
            {
                VisitPhase.Arriving => Arriving(name, kind, visit),
                VisitPhase.Soaking => Soaking(name, kind, visit, soaks.Of(who), tubs, length),
                _ => Massaged(name, kind, visit),
            });
        }

        return [.. views];
    }

    /// <summary>
    /// A guest walking to the counter or waiting there.
    /// </summary>
    static GuestView Arriving(string name, string kind, Visit visit)
    {
        if (double.IsNaN(visit.ArrivedAt)) return new GuestView(name, kind, "walking to the counter", "", float.NaN, "");

        var quality = Quality.Of(0, 0, visit.Waited, cold: false);
        string issues = quality.Wait > 0 ? "waiting" : "";
        return new GuestView(name, kind, "waiting at the counter", Hours(Clientele.MostWait - visit.Waited), quality.Total, issues);
    }

    /// <summary>
    /// A guest checked in: walking to its tub or soaking.
    /// </summary>
    static GuestView Soaking(string name, string kind, Visit visit, Soak? soak, IReadOnlyList<Tub> tubs, Func<TubKind, double> length)
    {
        if (soak is null) return new GuestView(name, kind, "going to the tub", "", float.NaN, "");

        var tub = tubs.FirstOrDefault(t => t.Site.Id == soak.Tub);
        double hours = tub is null ? 0 : length(tub.Kind);
        var quality = Quality.Of(soak.Soaked, soak.Lukewarm, visit.Waited, false, visit.DirtyTub, visit.Towel, visit.Soap, soak.Robed != false);
        string where = tub is null ? "tub" : $"{tub.Kind.ToString().ToLowerInvariant()} tub";
        string status = soak.Seated ? $"soaking in the {where}" : "going to the tub";
        return new GuestView(name, kind, status, Hours(hours - soak.Soaked), quality.Total, Issues(quality));
    }

    /// <summary>
    /// A guest on a massage bed.
    /// </summary>
    static GuestView Massaged(string name, string kind, Visit visit)
    {
        string status = visit.Lying ? visit.BedWait > 0 && visit.Massaged == 0 ? "waiting for the attendant" : "having a massage" : "going to the bed";
        string issues = visit.BedWait > 0 ? $"waited {visit.BedWait:0.0} h for the attendant" : "";
        return new GuestView(name, kind, status, Hours(Massage.Hours - visit.Massaged), visit.Quality, issues);
    }

    /// <summary>
    /// What is costing a soak quality, in a few words each.
    /// </summary>
    static string Issues(Quality quality)
    {
        var issues = new List<string>();
        if (quality.Heat > 0) issues.Add("lukewarm water");
        if (quality.Wait > 0) issues.Add("waited");
        if (quality.Dirt > 0) issues.Add("dirty tub");
        if (quality.NoTowel > 0) issues.Add("no towel");
        if (quality.NoSoap > 0) issues.Add("no soap");
        if (quality.NoRobe > 0) issues.Add("no robe");
        return string.Join(", ", issues);
    }

    /// <summary>
    /// Game hours left, as the list shows them.
    /// </summary>
    static string Hours(double hours) => $"{Math.Max(0, hours):0.0} h";
}
