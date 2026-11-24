using System.Collections;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace Bathhouse.Bathing;

/// <summary>
/// A tub's, tea bench's and used tea rack's bars under the Condition bar in the selected building's panel (docs/bathhouse.md "Panels"): a tub's
/// cleanliness and water heat with the seats taken; a tea piece's steam boiler and Water, which it needs to work. The button column keeps only
/// the buttons. The panel's bars come from an engine method a mod cannot name
/// (<c>BuildingSelection.BuildingRows</c>), so a postfix adds them to the list it returns; display only, on the host's thread, from the system's
/// published views.
/// </summary>
[HarmonyPatch]
public static class ConditionBars
{
    #region Engine

    /// <summary>
    /// The selected building's panel class.
    /// </summary>
    static readonly Type? SelectionType = AccessTools.TypeByName("Meitou.Game.Building.BuildingSelection");

    /// <summary>
    /// The method patched: the medical column's rows of a selected building; null in a game without it.
    /// </summary>
    static readonly MethodInfo? RowsMethod = SelectionType is null ? null : AccessTools.Method(SelectionType, "BuildingRows");

    /// <summary>
    /// One row of that column (name, green, yellow, red, value, arrows, robot, tooltip).
    /// </summary>
    static readonly Type? RowType = AccessTools.TypeByName("Meitou.Game.PlayerInterface+MedicalRow");

    /// <summary>
    /// The selected building's site id (<c>SelectedBuilding.SiteId</c>).
    /// </summary>
    static readonly Type? FocusType = AccessTools.TypeByName("Meitou.Game.Building.BuildingSelection+SelectedBuilding");

    /// <summary>
    /// Whether the game has what the patch needs; without it the tub's water stays a line in the button column.
    /// </summary>
    public static bool Active { get; private set; }

    /// <summary>
    /// The system of the latest world (set by the mod's entry).
    /// </summary>
    public static Func<BathhouseSystem?> System { get; set; } = () => null;

    /// <summary>
    /// Whether the game has the method, its <c>focus</c> parameter and the row and selection types the patch needs.
    /// </summary>
    public static bool Supported =>
        RowsMethod?.GetParameters() is [{ Name: "focus" }] && RowsMethod.ReturnType.IsAssignableTo(typeof(IList)) && RowType is not null
        && FocusType is not null;

    /// <summary>
    /// Whether to patch: only a game that has what the patch needs.
    /// </summary>
    static bool Prepare() => Active = Supported;

    /// <summary>
    /// The method patched.
    /// </summary>
    static MethodBase TargetMethod() => RowsMethod!;

#pragma warning disable IDE1006 // Harmony passes the return value only to a parameter named __result.
    /// <summary>
    /// Adds the tub's bars after the building's own (Condition, then a farm's crops).
    /// </summary>
    static void Postfix(object focus, object __result)
#pragma warning restore IDE1006
    {
        if (__result is not IList rows || Traverse.Create(focus).Property("SiteId").GetValue<string?>() is not { } site) return;
        if (System() is not { } bathhouse || bathhouse.Piece(site) is not { } view) return;

        var bars = view.Seats > 0 ? Bars(view)
            : view.Tea ? TeaBars(view.Heat)
            : [];
        foreach (var (name, fraction, value, tip) in bars) rows.Add(Row(name, fraction, value, tip));
    }

    /// <summary>
    /// A bar drawn as the Condition bar is: its fraction as the yellow layer.
    /// </summary>
    public static object Row(string name, float fraction, string value, string tip) =>
        Activator.CreateInstance(RowType!, name, 0f, fraction, 0f, value, "", false, tip)!;

    #endregion

    #region Bars

    /// <summary>
    /// A tub's bars: Clean or Dirty, then its water's heat with the seats taken.
    /// </summary>
    public static IEnumerable<(string Name, float Fraction, string Value, string Tip)> Bars(PieceView view)
    {
        yield return view.Dirty
            ? ("Dirty", 0, "", "Used since it was last scrubbed. A Tub scrubber cleans it; a dirty tub makes a worse soak.")
            : ("Clean", 1, "", "Scrubbed since it was last used.");

        float heat = view.Heat.Heat switch
        {
            Heat.Hot => 1,
            Heat.Lukewarm => 0.5f,
            _ => 0,
        };
        string water = view.Heat.Warm ? view.Heat.Heat == Heat.Hot ? "Water: hot" : "Water: lukewarm" : "Water: cold";
        yield return (water, heat, $"{view.Taken}/{view.Seats}", BathhouseSystem.Describe(view.Heat.Heat) + " Seats taken over seats.");
    }

    /// <summary>
    /// A tea bench's or used tea rack's bars: its building's steam boiler, then the Water in its stores. It works only while both are there
    /// (docs/bathhouse.md "Tea works").
    /// </summary>
    public static IEnumerable<(string Name, float Fraction, string Value, string Tip)> TeaBars(BuildingHeat heat)
    {
        yield return Boiler(heat);
        yield return Water(heat);
    }

    /// <summary>
    /// The building's steam boiler: lit, or why not.
    /// </summary>
    static (string, float, string, string) Boiler(BuildingHeat heat)
    {
        const string tip = "Tea needs hot water: a lit steam boiler in the same building, with Water in its tanks.";
        string state = heat.Boilers == 0 ? "none"
            : heat.Lit > 0 ? "lit"
            : heat.On == 0 ? "switched off"
            : "no power";
        string count = heat.Boilers > 0 ? $"{heat.Lit}/{heat.Boilers}" : "";
        return ($"Steam boiler: {state}", heat.Lit > 0 ? 1 : 0, count, tip);
    }

    /// <summary>
    /// The Water in the building's stores.
    /// </summary>
    static (string, float, string, string) Water(BuildingHeat heat)
    {
        const string tip = "Water in this building's stores (a Bathhouse Water Tank). The boiler drinks it to make hot water.";
        return heat.Water > 0 ? ("Water in tanks", 1, heat.Water.ToString(CultureInfo.InvariantCulture), tip) : ("No Water in tanks", 0, "", tip);
    }

    /// <summary>
    /// Hot water for a tea piece in one line (a game without the bars): yes, or the first thing missing.
    /// </summary>
    public static (string Name, float Fraction, string Value, string Tip) HotWater(BuildingHeat heat) =>
        heat.Warm ? ("Hot water: yes", 1, "", "A lit steam boiler and Water in this building.")
        : heat.Heat == Heat.Outdoors ? ("Hot water: no", 0, "", "Tea needs hot water: build it inside a building with a steam boiler.")
        : ("Hot water: no", 0, "", $"Tea needs hot water: {BathhouseSystem.Describe(heat.Heat)}.");

    #endregion
}
