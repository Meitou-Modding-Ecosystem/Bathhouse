using Bathhouse.Records;
using Meitou.Game.Ui;

namespace Bathhouse.Bathing;

/// <summary>
/// The bathhouse's panel lines (docs/bathhouse.md "Panels"): a tub shows its water and seats, a boiler what it heats and the switches for the bad
/// effects soaks clear. The sources run on the host's thread and read only the system's published views.
/// </summary>
public sealed class BathhouseUi(Func<BathhouseSystem?> system) : IUiExtension
{
    /// <summary>
    /// The most debuff switches a boiler's panel shows (the panel holds about six lines).
    /// </summary>
    public const int MaxDebuffLines = 4;

    public string Id => BathhouseMod.Id;

    public void Register(UiRegistration registration) => registration.AddBuildingActions(Lines);

    /// <summary>
    /// The lines of the selected piece: none for a piece that is not the bathhouse's or not the player's.
    /// </summary>
    IEnumerable<UiBuildingAction> Lines(UiBuildingContext context)
    {
        var building = context.Building;
        if (!building.Owned || system() is not { } bathhouse || bathhouse.Piece(building.SiteId) is not { } view) return [];

        if (building.Record == Ids.Boiler) return [BoilerLine(view), .. DebuffLines(context, bathhouse)];
        return [TubLine(view)];
    }

    /// <summary>
    /// A tub's line: its water and its seats taken.
    /// </summary>
    static UiBuildingAction TubLine(PieceView view)
    {
        string water = view.Heat.Warm ? view.Heat.Heat == Heat.Hot ? "hot" : "lukewarm" : "cold";
        return new UiBuildingAction($"Water: {water}, {view.Taken} of {view.Seats} seats taken", BathhouseSystem.Describe(view.Heat.Heat), Nothing);
    }

    /// <summary>
    /// A boiler's line: its building's heat and how many seats the lit boilers heat.
    /// </summary>
    static UiBuildingAction BoilerLine(PieceView view)
    {
        var heat = view.Heat;
        string caption = $"Tubs: {BathhouseSystem.Describe(heat.Heat)}";
        string tooltip = $"{heat.Lit} of {heat.Boilers} boilers lit, heating {heat.Capacity} seats for {heat.Seats} in tubs; "
            + $"{heat.Water} Water in store.";
        return new UiBuildingAction(caption, tooltip, Nothing);
    }

    /// <summary>
    /// One switch per bad effect of any mod (the first <see cref="MaxDebuffLines"/>): whether a soak clears it.
    /// </summary>
    static IEnumerable<UiBuildingAction> DebuffLines(UiBuildingContext context, BathhouseSystem bathhouse)
    {
        foreach (var effect in Soothing.Debuffs.Take(MaxDebuffLines))
        {
            bool kept = bathhouse.Keeps(effect.Id);
            string caption = $"Soaks clear {effect.Name}: {(kept ? "off" : "on")}";
            yield return new UiBuildingAction(caption, effect.Description, Switch(context, effect.Id, !kept));
        }
    }

    /// <summary>
    /// The press of a debuff switch: queues the command that keeps the effect, or clears it again.
    /// </summary>
    static Action Switch(UiBuildingContext context, string effect, bool keep) =>
        () => context.Enqueue(new KeepDebuffCommand(effect, keep) { Tick = context.Tick });

    /// <summary>
    /// A press that does nothing: a line that only tells.
    /// </summary>
    static void Nothing()
    {
    }
}
