using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Bathhouse.Bathing;

/// <summary>
/// The pieces' bars under the Condition bar in the selected building's panel (docs/bathhouse.md "Panels"), as <see cref="PanelBars"/> works them
/// out: what each piece needs and what it is linked to. The button column keeps only the buttons. The panel's bars come from an engine method a mod cannot name
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
    /// Adds the piece's bars after the building's own (Condition, then a farm's crops).
    /// </summary>
    static void Postfix(object focus, object __result)
#pragma warning restore IDE1006
    {
        if (__result is not IList rows || Traverse.Create(focus).Property("SiteId").GetValue<string?>() is not { } site) return;
        if (System() is not { } bathhouse || bathhouse.Bars(site) is not { } bars) return;

        foreach (var (name, fraction, value, tip) in bars) rows.Add(Row(name, fraction, value, tip));
    }

    /// <summary>
    /// A bar drawn as the Condition bar is: its fraction as the yellow layer.
    /// </summary>
    public static object Row(string name, float fraction, string value, string tip) =>
        Activator.CreateInstance(RowType!, name, 0f, fraction, 0f, value, "", false, tip)!;

    #endregion

    #region Fallback

    /// <summary>
    /// Hot water for a tea piece in one line (a game without the bars): yes, or the first thing missing.
    /// </summary>
    public static (string Name, float Fraction, string Value, string Tip) HotWater(BuildingHeat heat) =>
        heat.Warm ? ("Hot water: yes", 1, "", "A lit steam boiler and Water in this building.")
        : heat.Heat == Heat.Outdoors ? ("Hot water: no", 0, "", "Tea needs hot water: build it inside a building with a steam boiler.")
        : ("Hot water: no", 0, "", $"Tea needs hot water: {BathhouseSystem.Describe(heat.Heat)}.");

    #endregion
}
