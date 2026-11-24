using Bathhouse.Records;
using Bathhouse.Staffing;
using Meitou.Simulation;
using Meitou.Simulation.Construction;

namespace Bathhouse.Bathing;

/// <summary>
/// The tea pieces that need hot water (docs/bathhouse.md "Tea works"): a used tea rack rinses its Used Tea Sets into Teaware on a teaware shelf, and
/// a tea bench makes progress and uses its ingredients, only while its building is warm (a lit boiler and Water, <see cref="Heating"/>). Neither
/// state is saved: a load starts a rinse over and holds a bench at its loaded progress and ingredients.
/// </summary>
public sealed class TeaWorks(Heating heating)
{
    /// <summary>
    /// Game hours a used tea rack takes to rinse one set.
    /// </summary>
    public const double RinseHours = 1;

    /// <summary>
    /// Each rack's hours towards its next rinsed set.
    /// </summary>
    readonly Dictionary<string, double> _rinsing = new(StringComparer.Ordinal);

    /// <summary>
    /// Each tea bench's progress on its first craft at the last check, held there while its building is cold.
    /// </summary>
    readonly Dictionary<string, float> _held = new(StringComparer.Ordinal);

    /// <summary>
    /// Each tea bench's ingredient sections (items held, level drawn) at the last check, given back while its building is cold.
    /// </summary>
    readonly Dictionary<string, (int Count, float Level)[]> _stock = new(StringComparer.Ordinal);

    /// <summary>
    /// One check over <paramref name="hours"/> game hours: racks rinse, benches in cold buildings stay where they were.
    /// </summary>
    public void Step(World world, double hours)
    {
        foreach (var site in world.Buildings.All)
        {
            if (!site.Complete || site.Destroyed) continue;

            if (site.Record == Ids.UsedTeaRack) Rinse(world, site, hours);
            else if (site.Record == Ids.TeaBench) Hold(world, site);
        }
    }

    /// <summary>
    /// Whether a piece's building is warm: a lit boiler and Water in it.
    /// </summary>
    public bool Hot(BuildSite site) => heating.Of(site.Parent).Warm;

    /// <summary>
    /// A rack in a warm building holding a Used Tea Set works towards a rinse; each <see cref="RinseHours"/> turns one into Teaware on the
    /// building's first teaware shelf with room (it waits for room).
    /// </summary>
    void Rinse(World world, BuildSite rack, double hours)
    {
        var store = Goods.StoreOf(world, rack);
        if (!Hot(rack) || Goods.Count(store, Ids.UsedTeaSet) == 0)
        {
            _rinsing.Remove(rack.Id);
            return;
        }

        double done = _rinsing.GetValueOrDefault(rack.Id) + hours;
        if (done >= RinseHours && Shelf(world, rack.Parent) is { } shelf && Goods.Make(world, Ids.Teaware) is { } clean)
        {
            Goods.Take(store!, Ids.UsedTeaSet);
            shelf.Add(clean);
            done = 0;
        }

        _rinsing[rack.Id] = Math.Min(done, RinseHours);
    }

    /// <summary>
    /// The first teaware shelf in the building with room for a Teaware; null for none.
    /// </summary>
    static Meitou.Simulation.Items.Inventory? Shelf(World world, string? building)
    {
        var unit = Goods.Make(world, Ids.Teaware);
        return Goods.PiecesIn(world, building, Ids.TeawareShelf)
            .Select(s => Goods.StoreOf(world, s))
            .FirstOrDefault(s => s is not null && unit is not null && s.HasRoomFor(unit));
    }

    /// <summary>
    /// A tea bench's first craft keeps the progress it had at the last check while its building is cold, so it brews only with hot water. The
    /// engine draws a bench's ingredients as its operator works, so a cold bench also gets back what it drew since then.
    /// </summary>
    void Hold(World world, BuildSite bench)
    {
        if (world.Machines.Find(bench.Id) is not { Craft.Queue: [var job, ..] } machine)
        {
            _held.Remove(bench.Id);
            _stock.Remove(bench.Id);
            return;
        }

        if (!Hot(bench))
        {
            if (_held.TryGetValue(bench.Id, out float was) && job.Progress > was) job.Progress = was;
            if (_stock.TryGetValue(bench.Id, out var stock)) GiveBack(machine.Inputs, stock);
        }

        _held[bench.Id] = job.Progress;
        _stock[bench.Id] = [.. machine.Inputs.Select(i => (i.Count, i.Level))];
    }

    /// <summary>
    /// Puts each section back to its last check when it has less now: a section the operator loaded since (more items) keeps what it has.
    /// </summary>
    public static void GiveBack(Meitou.Simulation.Production.InputSlot[] inputs, (int Count, float Level)[] stock)
    {
        if (inputs.Length != stock.Length) return;

        for (int i = 0; i < inputs.Length; i++)
        {
            var (count, level) = stock[i];
            if (inputs[i].Count > count || inputs[i].Count + inputs[i].Level >= count + level) continue;

            inputs[i].Count = count;
            inputs[i].Level = level;
        }
    }
}
