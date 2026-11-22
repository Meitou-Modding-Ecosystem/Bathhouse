using Bathhouse.Records;
using Meitou.Data.Gameplay;
using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// Keeps the massage beds out of the game's automatic bed searches (docs/bathhouse.md "Massage"): sleeping, "Sleep when injured", rescuing and
/// putting a body in the nearest bed, NPC visitors' sleep and rented beds all pass over a bed their faction cannot reach, so each bed is marked
/// unreachable for every faction (<c>FixtureRegistry.MarkUnreachable</c>). A player's order naming the bed and the mod's customers (<c>EnterBed</c>)
/// do not ask, so they still use it. The marks are not saved: they are laid again after a load, ending at the same hour.
/// </summary>
public sealed class PrivateBeds(FactionRelations relations)
{
    /// <summary>
    /// Game hours between renewals; each mark lasts until the end of the next period, so it never lapses between checks.
    /// </summary>
    public const double PeriodHours = 24;

    /// <summary>
    /// The beds marked in the current period.
    /// </summary>
    readonly HashSet<string> _marked = new(StringComparer.Ordinal);

    /// <summary>
    /// The period <see cref="_marked"/> belongs to; -1 before the first check.
    /// </summary>
    long _period = -1;

    /// <summary>
    /// Marks every massage bed not yet marked this period.
    /// </summary>
    public void Step(World world)
    {
        long period = (long)Math.Floor(world.GameHours / PeriodHours);
        if (period != _period)
        {
            _period = period;
            _marked.Clear();
        }

        foreach (var site in world.Buildings.All)
        {
            if (site.Record != Ids.MassageBed || site.Destroyed || _marked.Contains(site.Id)) continue;
            if (Massage.BedOf(world, site) is not { } bed) continue;

            double hours = (period + 2) * PeriodHours - world.GameHours;
            for (int faction = 0; faction < relations.Count; faction++) world.Fixtures.MarkUnreachable(world, bed, faction, hours);
            _marked.Add(site.Id);
        }
    }
}
