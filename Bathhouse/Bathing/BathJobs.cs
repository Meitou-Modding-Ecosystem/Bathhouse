using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Production;

namespace Bathhouse.Bathing;

/// <summary>
/// The "Take a bath" verb on tubs (docs/bathhouse.md "Player bathers"): vanilla has no Sit verb, so each tub record gets a mod job its context menu
/// offers to the player's people. The system turns a held job into a soak at once and takes the job off again: it is a one-time order, not a
/// standing job.
/// </summary>
public static class BathJobs
{
    /// <summary>
    /// The job kind's id prefix; the tub record's number follows.
    /// </summary>
    const string Prefix = "bathhouse.bathe.";

    /// <summary>
    /// One job kind per tub record.
    /// </summary>
    public static IEnumerable<ModJobKind> Kinds =>
        new[] { Ids.SmallTub, Ids.MediumTub, Ids.LargeTub, Ids.OilBath, Ids.BloodBath }
            .Select(tub => new ModJobKind(Prefix + tub, tub, "Take a bath", "Bathing", ModJobTakers.People));

    /// <summary>
    /// Whether a mod job is one of the bath verbs.
    /// </summary>
    public static bool IsBath(ModJob job) => job.Kind.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// The player's characters holding a bath job, each with the tub site it names, in slot order; the jobs are taken off.
    /// </summary>
    public static List<(int Slot, string Tub)> Take(World world)
    {
        var taken = new List<(int, string)>();
        var table = world.Characters;
        for (int slot = 0; slot < table.HighWater; slot++)
        {
            if (!table.Next[slot].Alive || table.Cold(slot) is not { IsPlayer: true } cold || cold.ModJobs.Find(IsBath) is not { } job) continue;

            cold.ModJobs.RemoveAll(IsBath);
            taken.Add((slot, job.Site));
        }

        return taken;
    }
}
