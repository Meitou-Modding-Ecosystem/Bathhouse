using Bathhouse.Bathing;
using Bathhouse.Records;
using Meitou.Modding;
using Meitou.Simulation;

namespace Bathhouse;

/// <summary>
/// The mod's entry, named by "entry" in meitou.json (docs/bathhouse.md): adds the records in the data phase, then registers the settings, the bath
/// goal, the tubs' bath verb, the towel's use, the Soothed buff, the system, the panels and the debug row.
/// </summary>
public sealed class BathhouseMod : IMod, IModDataPhase
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "bathhouse";

    /// <summary>
    /// The system of the latest world, for the panels and the debug row.
    /// </summary>
    BathhouseSystem? _latest;

    /// <summary>
    /// Adds the mod's items, machines, furniture, research and bath goal to the merged game data.
    /// </summary>
    public void EditData(ModDataContext context)
    {
        foreach (var record in BathhouseData.Records(context.Data)) context.Apply(record);
    }

    /// <summary>
    /// Registers everything the mod runs.
    /// </summary>
    public void Initialize(ModContext context)
    {
        context.AddSettings(BathhouseSettings.Current);
        context.AddGoalType(Soaks.Row, "Bathhouse: taking a bath");
        context.AddStatusEffect(Soothing.Soothed);

        var purpose = context.RngPurpose;
        context.AddItemUse(Ids.Towel, use => Towels.Use(use, purpose));
        context.AddSystem("bathhouse", _ => _latest = new BathhouseSystem(purpose), SystemAnchor.End);
        context.AddUi(new BathhouseUi(() => _latest));
        context.AddDebugCommand("Bathhouse: tubs and bathers", _ => Told(context, _latest?.View ?? "no world yet"));
    }

    /// <summary>
    /// Logs a debug answer too (the status line shows only its start).
    /// </summary>
    static string Told(ModContext context, string answer)
    {
        context.Log(answer);
        return answer;
    }
}
