using Bathhouse.Records;
using Meitou.Modding;

namespace Bathhouse;

/// <summary>
/// The mod's entry, named by "entry" in meitou.json (docs/bathhouse.md): adds the records in the data phase, then registers the settings.
/// </summary>
public sealed class BathhouseMod : IMod, IModDataPhase
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "bathhouse";

    /// <summary>
    /// Adds the mod's items, machines, furniture and research to the merged game data.
    /// </summary>
    public void EditData(ModDataContext context)
    {
        foreach (var record in BathhouseData.Records(context.Data)) context.Apply(record);
    }

    /// <summary>
    /// Registers the settings.
    /// </summary>
    public void Initialize(ModContext context)
    {
        context.AddSettings(BathhouseSettings.Current);
    }
}
