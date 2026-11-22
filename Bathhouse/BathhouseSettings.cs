using Meitou.Modding;

namespace Bathhouse;

/// <summary>
/// The player's settings (docs/bathhouse.md "Settings"), shown under the mod's row on the Settings screen. Systems read <see cref="Current"/>.
/// </summary>
public sealed class BathhouseSettings
{
    /// <summary>
    /// The settings in force: the registered object once the mod has loaded, defaults before (and in tests).
    /// </summary>
    public static BathhouseSettings Current { get; internal set; } = new();

    #region Heating

    /// <summary>
    /// The default of <see cref="SeatsPerBoiler"/>, also quoted in the boiler's build-menu text.
    /// </summary>
    public const int DefaultSeatsPerBoiler = 4;

    /// <summary>
    /// How many bathers' seats one boiler keeps hot; past that every tub in the building is lukewarm.
    /// </summary>
    [Setting("Seats per boiler", Hint = "How many bathers one steam boiler keeps hot. Past that, every tub in the building is lukewarm.",
        Min = 1, Max = 16, Step = 1)]
    public int SeatsPerBoiler { get; set; } = DefaultSeatsPerBoiler;

    /// <summary>
    /// Water one lit boiler uses a game day, taken from storage boxes in its building.
    /// </summary>
    [Setting("Water per boiler a day", Hint = "Water each lit boiler takes from boxes in its building each day.", Min = 0, Max = 20, Step = 1)]
    public int WaterPerDay { get; set; } = 2;

    #endregion

    #region Bathing

    /// <summary>
    /// Game hours a soak in a small tub lasts.
    /// </summary>
    [Setting("Small tub (hours)", Hint = "How long a soak in a small tub lasts.", Min = 1, Max = 12, Step = 1, SubGroup = "Soak length")]
    public int SmallSoakHours { get; set; } = 2;

    /// <summary>
    /// Game hours a soak in a medium tub lasts.
    /// </summary>
    [Setting("Medium tub (hours)", Hint = "How long a soak in a medium tub lasts.", Min = 1, Max = 12, Step = 1, SubGroup = "Soak length")]
    public int MediumSoakHours { get; set; } = 3;

    /// <summary>
    /// Game hours a soak in a large bath lasts.
    /// </summary>
    [Setting("Large bath (hours)", Hint = "How long a soak in a large bath lasts.", Min = 1, Max = 12, Step = 1, SubGroup = "Soak length")]
    public int LargeSoakHours { get; set; } = 4;

    /// <summary>
    /// Game hours a soak in the oil or blood bath lasts.
    /// </summary>
    [Setting("Oil and blood baths (hours)", Hint = "How long a soak in the oil bath or blood bath lasts.", Min = 1, Max = 12, Step = 1,
        SubGroup = "Soak length")]
    public int SpecialSoakHours { get; set; } = 3;

    /// <summary>
    /// Game days the Soothed buff lasts after a soak.
    /// </summary>
    [Setting("Soothed buff (days)", Hint = "How long a soak's Soothed buff lasts.", Min = 1, Max = 10, Step = 1)]
    public int BuffDays { get; set; } = 3;

    /// <summary>
    /// Whether a soak clears status effects marked bad (those switched off on a boiler's panel stay).
    /// </summary>
    [Setting("Soaks clear debuffs", Hint = "A soak removes bad status effects, except those switched off on a steam boiler's panel.")]
    public bool ClearDebuffs { get; set; } = true;

    /// <summary>
    /// The chance a used towel comes back dirty, in percent; otherwise it is ruined.
    /// </summary>
    [Setting("Towels kept (%)", Hint = "A used towel comes back dirty this often; otherwise it is ruined.", Min = 0, Max = 100, Step = 5)]
    public int DirtyChance { get; set; } = 80;

    #endregion

    #region Customers

    /// <summary>
    /// Game hours between customer groups at an open bathhouse (0: none).
    /// </summary>
    [Setting("Hours between customers", Hint = "How often a group of bathers comes to an open bathhouse (0 for none).", Min = 0, Max = 48, Step = 1)]
    public int HoursBetweenGroups { get; set; } = 6;

    /// <summary>
    /// Faction relation lost per complaint.
    /// </summary>
    [Setting("Relation lost per complaint", Min = 0, Max = 10, Step = 1)]
    public int RelationPerComplaint { get; set; } = 1;

    /// <summary>
    /// The most relation one faction loses to complaints in a game day.
    /// </summary>
    [Setting("Most relation lost a day", Hint = "Caps what complaints cost with one faction each day.", Min = 0, Max = 20, Step = 1)]
    public int RelationCapPerDay { get; set; } = 5;

    /// <summary>
    /// The chance an unhappy bather picks a fight, in percent (rowdy kinds double it). Only their own squad turns hostile.
    /// </summary>
    [Setting("Fight chance (%)", Hint = "How often an unhappy bather picks a fight. Only their own squad turns hostile.", Min = 0, Max = 50,
        Step = 1)]
    public int FightChance { get; set; } = 5;

    #endregion
}
