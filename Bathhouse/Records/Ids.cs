namespace Bathhouse.Records;

/// <summary>
/// The string ids of the records in Bathhouse.mod (docs/bathhouse.md "Data file"). Saved games and other mods name them, so a number is never
/// reused for another record.
/// </summary>
public static class Ids
{
    /// <summary>
    /// The data file's name; every id ends with it.
    /// </summary>
    public const string FileName = "Bathhouse.mod";

    #region Items

    /// <summary>
    /// A clean towel: what a bather dries with (<c>ITEM</c>).
    /// </summary>
    public const string Towel = "1-" + FileName;

    /// <summary>
    /// A used towel, washed back into a clean one.
    /// </summary>
    public const string DirtyTowel = "2-" + FileName;

    /// <summary>
    /// A bar of hemp soap: a bather's, and the washing machine's second input.
    /// </summary>
    public const string Soap = "3-" + FileName;

    /// <summary>
    /// Dried greenfruit, brewed with teaware into a tea set.
    /// </summary>
    public const string GreenfruitTea = "4-" + FileName;

    /// <summary>
    /// Empty cups and pot: fired at the kiln, and rinsed out of a used tea set.
    /// </summary>
    public const string Teaware = "5-" + FileName;

    /// <summary>
    /// A tray of filled teaware, served to bathers.
    /// </summary>
    public const string TeaSet = "6-" + FileName;

    /// <summary>
    /// The tray of empty, stained teaware a drunk tea set leaves on its table.
    /// </summary>
    public const string UsedTeaSet = "8-" + FileName;

    /// <summary>
    /// The staff's robe (<c>ARMOUR</c>).
    /// </summary>
    public const string Robe = "7-" + FileName;

    #endregion

    #region Machines

    /// <summary>
    /// The machines' functionalities (<c>BUILDING_FUNCTIONALITY</c>), in the order of <see cref="TowelLoom"/> and the machines after it.
    /// </summary>
    public const string TowelLoomWork = "20-" + FileName, SoapPressWork = "21-" + FileName, WasherWork = "22-" + FileName,
        TeaDryerWork = "23-" + FileName, KilnWork = "24-" + FileName, TeaBenchWork = "25-" + FileName, MassageWork = "26-" + FileName;

    /// <summary>
    /// The production machines and the tea bench (<c>BUILDING</c>).
    /// </summary>
    public const string TowelLoom = "30-" + FileName, SoapPress = "31-" + FileName, WashingMachine = "32-" + FileName, TeaDryer = "33-" + FileName,
        Kiln = "34-" + FileName, TeaBench = "35-" + FileName;

    /// <summary>
    /// The steam boiler that heats a building's tubs.
    /// </summary>
    public const string Boiler = "36-" + FileName;

    /// <summary>
    /// The water tank the boilers draw from: storage for Water only.
    /// </summary>
    public const string WaterTank = "37-" + FileName;

    #endregion

    #region Furniture

    /// <summary>
    /// The tubs: small (one bather), medium (two), large (four), the oil bath and the blood bath.
    /// </summary>
    public const string SmallTub = "40-" + FileName, MediumTub = "41-" + FileName, LargeTub = "42-" + FileName, OilBath = "43-" + FileName,
        BloodBath = "44-" + FileName;

    /// <summary>
    /// The service furniture.
    /// </summary>
    public const string MassageBed = "50-" + FileName, MassageStation = "51-" + FileName, Counter = "52-" + FileName, Lockers = "53-" + FileName,
        Hamper = "54-" + FileName, TowelRack = "55-" + FileName, TeaTable = "56-" + FileName, ReviewBoard = "57-" + FileName,
        TeaShelf = "58-" + FileName;

    /// <summary>
    /// The decorations.
    /// </summary>
    public const string ZenGarden = "60-" + FileName, WallArt = "61-" + FileName, Tapestry = "62-" + FileName, Statue = "63-" + FileName,
        TowelHanger = "64-" + FileName, TeaDisplay = "65-" + FileName;

    #endregion

    #region Models

    /// <summary>
    /// The pieces' own models: the buildings the structure import makes from <c>Structures/&lt;piece&gt;.glb</c> (block b in
    /// <c>Structures/ids.json</c> is building 900000 + 100 b). Each piece copies its look when the deployed data has it.
    /// </summary>
    public const string SmallTubLook = "900000-" + FileName, BloodBathLook = "900100-" + FileName, LargeTubLook = "900200-" + FileName,
        MassageBedLook = "900300-" + FileName, MediumTubLook = "900400-" + FileName, OilBathLook = "900500-" + FileName,
        BoilerLook = "900600-" + FileName, MassageStationLook = "900700-" + FileName, CounterLook = "900800-" + FileName,
        HamperLook = "900900-" + FileName, SoapPressLook = "901000-" + FileName, TeaBenchLook = "901100-" + FileName,
        TeaTableLook = "901200-" + FileName, TowelLoomLook = "901300-" + FileName, WashingMachineLook = "901400-" + FileName,
        LockersLook = "901500-" + FileName, TowelRackLook = "901600-" + FileName, TeaDryerLook = "901700-" + FileName,
        KilnLook = "901800-" + FileName, ReviewBoardLook = "902100-" + FileName, StatueLook = "902200-" + FileName,
        ZenGardenLook = "902300-" + FileName, TeaDisplayLook = "902400-" + FileName, TeaShelfLook = "902500-" + FileName;

    #endregion

    #region Goals

    /// <summary>
    /// The bath goal's <c>AI_TASK</c>, listed in every race's and the player faction's <c>AI Goals</c> so anyone can be sent to soak.
    /// </summary>
    public const string BathGoal = "90-" + FileName;

    /// <summary>
    /// The bath goal's task type (its <c>enum</c>), also the row its walk and soak run on. Claimed by the mod; Bathhouse keeps 4401 to 4409.
    /// </summary>
    public const int BathGoalType = 4401;

    /// <summary>
    /// The visit goal's <c>AI_TASK</c>: a customer walks to the check-in counter and waits there. Listed with the bath goal.
    /// </summary>
    public const string VisitGoal = "91-" + FileName;

    /// <summary>
    /// The visit goal's task type, also its walk's row.
    /// </summary>
    public const int VisitGoalType = 4402;

    /// <summary>
    /// The staff job types (docs/bathhouse.md "Staff"): clerk, scrubber, laundry runner and tea server, each also its errands' row.
    /// </summary>
    public const int ClerkJob = 4403, ScrubberJob = 4404, LaundryJob = 4405, TeaJob = 4406;

    #endregion

    #region Research

    /// <summary>
    /// The research projects (<c>RESEARCH</c>).
    /// </summary>
    public const string BasicsResearch = "80-" + FileName, HotBathsResearch = "81-" + FileName, MassageResearch = "82-" + FileName,
        TeaResearch = "83-" + FileName, OilBathResearch = "84-" + FileName, BloodBathResearch = "85-" + FileName;

    #endregion
}
