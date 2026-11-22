using Bathhouse.Bathing;

namespace Bathhouse.Staffing;

/// <summary>
/// Dirty tubs (docs/bathhouse.md "Staff"): a tub gets dirty when a soak in it ends and stays so until a scrubber scrubs it; the blood bath takes
/// longer to mop. A customer put in a dirty tub has a worse visit. Saved and hashed.
/// </summary>
public sealed class TubCare
{
    /// <summary>
    /// Game hours a scrub takes.
    /// </summary>
    public const double ScrubHours = 0.5;

    /// <summary>
    /// Game hours mopping the blood bath takes.
    /// </summary>
    public const double MopHours = 1.5;

    /// <summary>
    /// The dirty tubs' site ids.
    /// </summary>
    readonly SortedSet<string> _dirty = new(StringComparer.Ordinal);

    /// <summary>
    /// The dirty tubs, for saving, hashing and the views.
    /// </summary>
    public IReadOnlyCollection<string> Dirty => _dirty;

    /// <summary>
    /// Whether a tub is dirty.
    /// </summary>
    public bool IsDirty(string tub) => _dirty.Contains(tub);

    /// <summary>
    /// A soak in the tub ended.
    /// </summary>
    public void Used(string tub) => _dirty.Add(tub);

    /// <summary>
    /// A scrubber finished the tub.
    /// </summary>
    public void Scrubbed(string tub) => _dirty.Remove(tub);

    /// <summary>
    /// Puts back the dirty tubs read from a save.
    /// </summary>
    public void Restore(IEnumerable<string> tubs)
    {
        _dirty.Clear();
        _dirty.UnionWith(tubs);
    }

    /// <summary>
    /// How long scrubbing a kind of tub takes.
    /// </summary>
    public static double HoursFor(TubKind kind) => kind == TubKind.Blood ? MopHours : ScrubHours;
}
