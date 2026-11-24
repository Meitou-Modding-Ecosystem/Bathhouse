using System.Globalization;
using Bathhouse.Customers;
using Bathhouse.Records;
using Bathhouse.Staffing;
using Meitou.Simulation;
using Meitou.Simulation.Construction;

namespace Bathhouse.Bathing;

/// <summary>
/// One bar under a piece's Condition bar: its caption, how full it is (0 to 1), the number at its right end and its tooltip.
/// </summary>
public readonly record struct Bar(string Name, float Fraction, string Value, string Tip);

/// <summary>
/// The bars under each piece's Condition bar (docs/bathhouse.md "Panels"): for every piece that works only under conditions or with other pieces,
/// what it needs and what it is linked to. Worked out in the serial check from the world and the system's parts; <see cref="ConditionBars"/>
/// draws them.
/// </summary>
public sealed class PanelBars(Heating heating, TubCare care, Clientele clientele, TeaWorks tea)
{
    #region Building

    /// <summary>
    /// Every finished piece's bars by site id (pieces with none are left out).
    /// </summary>
    public Dictionary<string, Bar[]> Build(World world, IReadOnlyList<Tub> tubs, Soaks soaks, BathhouseSettings settings)
    {
        var bars = new Dictionary<string, Bar[]>(StringComparer.Ordinal);
        foreach (var tub in tubs)
            bars[tub.Site.Id] = [.. TubBars(world, tub, soaks.All.Count(s => s.Tub == tub.Site.Id))];

        foreach (var site in world.Buildings.All)
        {
            if (!site.Complete || site.Destroyed || PieceBars(world, site, settings) is not { } piece) continue;

            bars[site.Id] = [.. piece];
        }

        return bars;
    }

    /// <summary>
    /// A piece's bars by its record; null for a piece without any (tubs are done by <see cref="Build"/>).
    /// </summary>
    IEnumerable<Bar>? PieceBars(World world, BuildSite site, BathhouseSettings settings) => site.Record switch
    {
        Ids.Boiler => BoilerBars(heating.Of(site.Parent), settings.WaterPerDay),
        Ids.TeaBench => TeaBars(heating.Of(site.Parent)),
        Ids.UsedTeaRack => [.. TeaBars(heating.Of(site.Parent)), .. RackBars(world, site)],
        Ids.MassageBed => BedBars(world, site),
        Ids.MassageStation => StationBars(world, site),
        Ids.TeaTable => TableBars(world, site),
        _ => null,
    };

    #endregion

    #region Tubs and heat

    /// <summary>
    /// A tub's bars: Clean or Dirty, its water's heat with the seats taken, and who scrubs it.
    /// </summary>
    IEnumerable<Bar> TubBars(World world, Tub tub, int taken)
    {
        foreach (var bar in TubBars(heating.Of(tub.Building), taken, tub.Seats, care.IsDirty(tub.Site.Id))) yield return bar;

        yield return Scrubber(world, tub, taken);
    }

    /// <summary>
    /// Who scrubs the tub and what holds them up: scrubbing it now, waiting for its bathers to leave, or at another task.
    /// </summary>
    Bar Scrubber(World world, Tub tub, int taken)
    {
        string id = tub.Site.Id;
        if (StaffRoles.HolderAt(world, id, StaffRole.Scrubber) is not { } slot || world.Characters.Cold(slot) is not { } cold)
            return new Bar("Scrubber: none", 0, "", "Nobody holds the Tub scrubber job at this tub, so it stays dirty after each soak.");

        if (!care.IsDirty(id)) return new Bar($"Scrubber: {cold.Name}", 1, "", $"{cold.Name} holds the Tub scrubber job here.");

        var (value, why) = ScrubState(cold, id, taken);
        return new Bar($"Scrubber: {cold.Name}", 0.5f, value, why);
    }

    /// <summary>
    /// What a dirty tub's scrubber is doing about it, as a short value and its tip.
    /// </summary>
    static (string Value, string Tip) ScrubState(CharacterCold cold, string tub, int taken)
    {
        if (taken > 0) return ("waits", $"{cold.Name} scrubs it once the {taken} bather(s) in it leave.");

        var scrub = StaffRoles.TypeOf(StaffRole.Scrubber);
        var job = cold.Running is { Job: var k } && k >= 0 && k < cold.Jobs.Count ? cold.Jobs[k] : null;
        if (job is { } now && now.Type == scrub && now.Site == tub) return ("scrubbing", $"{cold.Name} is scrubbing it.");

        if (job is { } other && other.Type == scrub) return ("next", $"{cold.Name} is scrubbing another tub and comes here after.");

        return ("busy", $"{cold.Name} is at another task. Jobs higher in their jobs list, orders and needs come first: move this job up or free them.");
    }

    /// <summary>
    /// A tub's state bars: Clean or Dirty, then its water's heat with the seats taken.
    /// </summary>
    public static IEnumerable<Bar> TubBars(BuildingHeat heat, int taken, int seats, bool dirty)
    {
        yield return dirty
            ? new Bar("Dirty", 0, "", "Used since it was last scrubbed. A Tub scrubber cleans it; a dirty tub makes a worse soak.")
            : new Bar("Clean", 1, "", "Scrubbed since it was last used.");

        float warmth = heat.Heat switch
        {
            Heat.Hot => 1,
            Heat.Lukewarm => 0.5f,
            _ => 0,
        };
        string water = heat.Warm ? heat.Heat == Heat.Hot ? "Water: hot" : "Water: lukewarm" : "Water: cold";
        string why = BathhouseSystem.Describe(heat.Heat);
        yield return new Bar(water, warmth, $"{taken}/{seats}", char.ToUpperInvariant(why[0]) + why[1..] + ". Seats taken over seats.");
    }

    /// <summary>
    /// A boiler's bars: the hours its building's Water lasts the lit boilers, and the seats they heat.
    /// </summary>
    public static IEnumerable<Bar> BoilerBars(BuildingHeat heat, int waterPerDay)
    {
        const string waterTip = "Hours the Water in this building's stores lasts its lit boilers. Each lit boiler drinks Water every day.";
        if (heat.Lit == 0 || waterPerDay <= 0) yield return new Bar("Water: not used", heat.Water > 0 ? 1 : 0, Count(heat.Water), waterTip);
        else
        {
            double hours = heat.Water * 24.0 / (heat.Lit * waterPerDay);
            yield return new Bar($"Water left: {hours:0} h", (float)Math.Min(1, hours / 24), Count(heat.Water), waterTip);
        }

        float share = heat.Seats == 0 ? 1 : Math.Min(1, (float)heat.Capacity / heat.Seats);
        string tip = $"{heat.Lit} of {heat.Boilers} boilers lit: they heat {heat.Capacity} seats for {heat.Seats} in this building's tubs.";
        yield return new Bar("Seats heated", share, $"{heat.Capacity}/{heat.Seats}", tip);
    }

    /// <summary>
    /// A tea bench's or used tea rack's bars: its building's steam boiler, then the Water in its stores. It works only while both are there
    /// (docs/bathhouse.md "Tea works").
    /// </summary>
    public static IEnumerable<Bar> TeaBars(BuildingHeat heat)
    {
        const string boilerTip = "Tea needs hot water: a lit steam boiler in the same building, with Water in its tanks.";
        string state = heat.Boilers == 0 ? "none"
            : heat.Lit > 0 ? "lit"
            : heat.On == 0 ? "switched off"
            : "no power";
        string lit = heat.Boilers > 0 ? $"{heat.Lit}/{heat.Boilers}" : "";
        yield return new Bar($"Steam boiler: {state}", heat.Lit > 0 ? 1 : 0, lit, boilerTip);

        const string waterTip = "Water in this building's stores (a Bathhouse Water Tank). The boiler drinks it to make hot water.";
        yield return heat.Water > 0 ? new Bar("Water in tanks", 1, Count(heat.Water), waterTip) : new Bar("No Water in tanks", 0, "", waterTip);
    }

    #endregion

    #region Tea

    /// <summary>
    /// A used tea rack's own bars: its rinse under way with the used sets waiting, and whether a teaware shelf in the building has room.
    /// </summary>
    IEnumerable<Bar> RackBars(World world, BuildSite rack)
    {
        int used = Goods.Count(Goods.StoreOf(world, rack), Ids.UsedTeaSet);
        const string rinseTip = "Rinses one Used Tea Set an hour into Teaware while the building has hot water. Used sets waiting at the right.";
        yield return new Bar("Rinsing", (float)tea.Rinsed(rack.Id), Count(used), rinseTip);

        bool any = Goods.PiecesIn(world, rack.Parent, Ids.TeawareShelf).Any();
        bool room = TeaWorks.Shelf(world, rack.Parent) is not null;
        string shelf = room ? "Teaware shelf: room"
            : any ? "Teaware shelf: full"
            : "Teaware shelf: none";
        yield return new Bar(shelf, room ? 1 : 0, "", "Rinsed Teaware goes onto a teaware shelf in the same building with room; without one it waits.");
    }

    /// <summary>
    /// A tea table's bars: the set on it, and who serves it.
    /// </summary>
    static IEnumerable<Bar> TableBars(World world, BuildSite table)
    {
        var store = Goods.StoreOf(world, table);
        var set = Goods.Count(store, Ids.TeaSet) > 0 ? new Bar("Tea Set ready", 1, "", "An arriving customer can have tea here before the bath.")
            : Goods.Count(store, Ids.UsedTeaSet) > 0 ? new Bar("Used Tea Set to clear", 0, "", "The tea server takes it to a used tea rack.")
            : new Bar("No Tea Set", 0, "", "The tea server brings one from a tea set cabinet.");
        yield return set;
        yield return Holder(world, table.Id, StaffRole.Tea, "Tea server", "Nobody holds the Tea server job at this table.");
    }

    #endregion

    #region Massage

    /// <summary>
    /// A massage bed's bars: its customer's massage time left, the attendant at its station with their Dexterity, and the station's towels.
    /// </summary>
    IEnumerable<Bar> BedBars(World world, BuildSite bed)
    {
        var visit = clientele.Visits.FirstOrDefault(v => v.Phase == VisitPhase.Massage && v.Bed == bed.Id);
        yield return Customer(visit);

        if (Massage.Station(world, bed) is not { } station)
        {
            yield return new Bar("No massage station", 0, "", "A massage bed needs a massage station within 5 m in the same building.");
            yield break;
        }

        yield return Attendant(world, station);
        yield return Towels(world, station);
    }

    /// <summary>
    /// A massage station's bars: whether its bed is in use, its attendant with their Dexterity, and its towels.
    /// </summary>
    IEnumerable<Bar> StationBars(World world, BuildSite station)
    {
        bool paired = world.Buildings.All.Any(s => s.Record == Ids.MassageBed && s.Complete && Massage.Station(world, s)?.Id == station.Id);
        var bed = !paired ? new Bar("No massage bed", 0, "", "A massage station serves the beds within 5 m in the same building.")
            : Massage.Occupied(world, station) ? new Bar("Bed in use", 1, "", "Someone lies on a bed this station serves.")
            : new Bar("Bed free", 0, "", "Nobody lies on the beds this station serves.");
        yield return bed;
        yield return Attendant(world, station);
        yield return Towels(world, station);
    }

    /// <summary>
    /// The massage customer on a bed: the hours of massage left, or none.
    /// </summary>
    static Bar Customer(Visit? visit)
    {
        const string tip = "A customer stays for an hour of massage; the time runs only while the attendant works. Nobody coming within an hour, "
            + "they leave.";
        if (visit is null) return new Bar("No customer", 0, "", tip);

        double left = Math.Max(0, Massage.Hours - visit.Massaged);
        return new Bar($"Customer: {left:0.0} h left", (float)(left / Massage.Hours), "", tip);
    }

    /// <summary>
    /// Who holds the station's Massage attendant job, with their Dexterity (a massage's worth grows with it).
    /// </summary>
    static Bar Attendant(World world, BuildSite station)
    {
        const string tip = "The Massage attendant at this station. Dexterity sets what a massage is worth: a tenth at 0, all of it at 100.";
        if (StaffRoles.HolderAt(world, station.Id, StaffRole.Massage) is not { } slot || world.Characters.Cold(slot) is not { } cold)
            return new Bar("No attendant", 0, "", tip);

        float dexterity = cold.Stats?.Dexterity ?? 0;
        return new Bar($"Attendant: {cold.Name}", dexterity / 100, $"Dex {dexterity:0}", tip);
    }

    /// <summary>
    /// The towels in a massage station; a massage uses one.
    /// </summary>
    static Bar Towels(World world, BuildSite station)
    {
        int towels = Massage.TowelsIn(world, station);
        int most = Math.Max(1, FurnitureRecords.CapacityOf(Ids.MassageStation));
        return new Bar("Towels", (float)towels / most, $"{towels}/{most}", "A massage uses one Towel from the station; haulers keep it stocked.");
    }

    #endregion

    #region Shared

    /// <summary>
    /// Who holds the role's job at the piece, or nobody.
    /// </summary>
    static Bar Holder(World world, string piece, StaffRole role, string caption, string none)
    {
        if (StaffRoles.HolderAt(world, piece, role) is not { } slot || world.Characters.Cold(slot) is not { } cold)
            return new Bar($"{caption}: none", 0, "", none);

        return new Bar($"{caption}: {cold.Name}", 1, "", $"{cold.Name} holds the {StaffRoles.Name(role)} job here.");
    }

    /// <summary>
    /// A count as the bar's number.
    /// </summary>
    static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    #endregion
}
