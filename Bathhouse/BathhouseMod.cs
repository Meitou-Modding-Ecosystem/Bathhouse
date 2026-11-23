using Bathhouse.Bathing;
using Bathhouse.Customers;
using Bathhouse.Records;
using Bathhouse.Staffing;
using Meitou.Data.Gameplay.Combat;
using Meitou.Modding;
using Meitou.Simulation;

namespace Bathhouse;

/// <summary>
/// The mod's entry, named by "entry" in meitou.json (docs/bathhouse.md): adds the records in the data phase, then registers the settings, the bath
/// and visit goals, the towel's use, the Soothed buff, the system, the panels and the debug rows.
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
    /// The tea sit's COMBAT_TECHNIQUE (<see cref="AnimationRecords"/>); null when the data phase did not add it (customers then sit as the game
    /// rests).
    /// </summary>
    CombatTechnique? _teaSit;

    /// <summary>
    /// The tea sit clip's length, in seconds: it loops.
    /// </summary>
    const float TeaSitSeconds = 2.5f;

    /// <summary>
    /// Adds the mod's items, machines, furniture, research and bath goal to the merged game data.
    /// </summary>
    public void EditData(ModDataContext context)
    {
        foreach (var record in BathhouseData.Records(context.Data)) context.Apply(record);
        AnimationRecords.LinkRaces(context);
    }

    /// <summary>
    /// Registers everything the mod runs.
    /// </summary>
    public void Initialize(ModContext context)
    {
        context.AddSettings(BathhouseSettings.Current);
        context.AddGoalType(Soaks.Row, "Bathhouse: taking a bath");
        context.AddGoalType(Clientele.Row, "Bathhouse: visiting the bathhouse");
        foreach (var role in StaffRoles.All)
        {
            context.AddJobType(StaffRoles.TypeOf(role), StaffRoles.Name(role));
            context.AddJobOffer(StaffRoles.Offer(role));
        }
        context.AddStatusEffect(Soothing.Soothed);

        var purpose = context.RngPurpose;
        context.AddItemUse(Ids.Towel, use => Towels.Use(use, purpose));
        _teaSit = context.Data.Find(Ids.TeaSitMove) is { } sit ? CombatTechnique.From(sit) : null;
        context.AddCombatMove(TeaSitMove);
        context.AddSystem("bathhouse", build => _latest = new BathhouseSystem(purpose, build.Data), SystemAnchor.End);
        context.AddUi(new BathhouseUi(() => _latest, new CounterLedger(() => _latest, context.Enqueue, () => context.Tick)));
        context.AddDebugCommand("Bathhouse: tubs and bathers", _ => Told(context, _latest?.View ?? "no world yet"));
        context.AddDebugCommand("Bathhouse: send customers (kind)", SendCustomers, hasField: true);
    }

    /// <summary>
    /// The tea sit, looping, for a customer sitting at a tea table (a mod combat move: read-only, from the last serial step's view).
    /// </summary>
    CombatMove? TeaSitMove(World world, CharacterId id, long tick)
    {
        if (_teaSit is null || _latest?.SitsAtTea(id) != true) return null;

        float seconds = tick * world.TickSeconds;
        return new CombatMove(_teaSit, seconds % TeaSitSeconds / TeaSitSeconds);
    }

    /// <summary>
    /// The debug row that sends a group of the typed customer kind now (drifters, traders, hunters or shek).
    /// </summary>
    static string SendCustomers(DebugCommandContext debug)
    {
        string kind = debug.Field.Trim().ToLowerInvariant();
        if (CustomerTypes.Find(kind) is not { Spawnable: true })
            return $"Kinds: {string.Join(", ", CustomerTypes.All.Where(k => k.Spawnable).Select(k => k.Key))}";

        debug.Enqueue(new SendCustomersCommand(kind) { Tick = debug.Tick });
        return $"Sending {kind}.";
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
