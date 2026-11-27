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
    /// The bathhouse robe (<c>ARMOUR</c>): staff wear it, and bathers change into one from the lockers.
    /// </summary>
    public const string Robe = "7-" + FileName;

    /// <summary>
    /// A robe a bather has worn, washed back into a clean one.
    /// </summary>
    public const string DirtyRobe = "9-" + FileName;

    #endregion

    #region Machines

    /// <summary>
    /// The machines' functionalities (<c>BUILDING_FUNCTIONALITY</c>), in the order of <see cref="TowelLoom"/> and the machines after it.
    /// </summary>
    public const string TowelLoomWork = "20-" + FileName, SoapPressWork = "21-" + FileName, WasherWork = "22-" + FileName,
        TeaDryerWork = "23-" + FileName, KilnWork = "24-" + FileName, TeaBenchWork = "25-" + FileName, CisternWork = "26-" + FileName;

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

    /// <summary>
    /// The water cistern: a machine whose output holds Water, so haulers draw from it as from a well; its Water carrier fills it from wells.
    /// </summary>
    public const string Cistern = "38-" + FileName;

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
        TeaShelf = "58-" + FileName, RobeHamper = "59-" + FileName;

    /// <summary>
    /// The decorations.
    /// </summary>
    public const string ZenGarden = "60-" + FileName, WallArt = "61-" + FileName, Tapestry = "62-" + FileName, Statue = "63-" + FileName,
        TowelHanger = "64-" + FileName, TeaDisplay = "65-" + FileName;

    /// <summary>
    /// The soap shelf: service furniture, numbered after the decorations because 50 to 59 were taken.
    /// </summary>
    public const string SoapShelf = "66-" + FileName;

    /// <summary>
    /// The tea stores, one for each tea item the brewing makes or leaves: teaware, tea sets and used tea sets (service furniture).
    /// </summary>
    public const string TeawareShelf = "67-" + FileName, TeaSetCabinet = "68-" + FileName, UsedTeaRack = "69-" + FileName;

    #endregion

    #region Effects

    /// <summary>
    /// The steam effects (<c>EFFECT</c>, Records/EffectRecords.cs): one over each kind of tub's water, and the boiler's plume.
    /// </summary>
    public const string SmallTubSteam = "70-" + FileName, MediumTubSteam = "71-" + FileName, LargeTubSteam = "72-" + FileName,
        OilBathSteam = "73-" + FileName, BloodBathSteam = "74-" + FileName, BoilerSteam = "75-" + FileName;

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
        ZenGardenLook = "902300-" + FileName, TeaDisplayLook = "902400-" + FileName, TeaShelfLook = "902500-" + FileName,
        RobeHamperLook = "902600-" + FileName, SoapShelfLook = "902700-" + FileName, TeawareShelfLook = "902800-" + FileName,
        TeaSetCabinetLook = "902900-" + FileName, UsedTeaRackLook = "903000-" + FileName,
        CisternLook = "903100-" + FileName;

    #endregion

    #region Goals

    /// <summary>
    /// The bath goal's <c>AI_TASK</c>, listed in every race's and the player faction's <c>AI Goals</c> so anyone can be sent to soak.
    /// </summary>
    public const string BathGoal = "90-" + FileName;

    /// <summary>
    /// The bath goal's task type (its <c>enum</c>), also the row its walk and soak run on. Claimed by the mod inside its number block 4400 to 4499 (README "Numbers").
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
    /// The staff job types (docs/bathhouse.md "Staff"): clerk, scrubber, laundry runner, tea server, massage attendant and water carrier, each also
    /// its errands' row.
    /// </summary>
    public const int ClerkJob = 4403, ScrubberJob = 4404, LaundryJob = 4405, TeaJob = 4406, MassageJob = 4407, WaterJob = 4408;

    #endregion

    #region Research

    /// <summary>
    /// The research projects (<c>RESEARCH</c>).
    /// </summary>
    public const string BasicsResearch = "80-" + FileName, HotBathsResearch = "81-" + FileName, MassageResearch = "82-" + FileName,
        TeaResearch = "83-" + FileName, OilBathResearch = "84-" + FileName, BloodBathResearch = "85-" + FileName;

    #endregion

    #region Animations

    /// <summary>
    /// The tea table sit's <c>COMBAT_TECHNIQUE</c> and the mod's <c>ANIMATION_FILE</c> (<see cref="AnimationRecords"/>).
    /// </summary>
    public const string TeaSitMove = "95-" + FileName, Animations = "96-" + FileName;

    /// <summary>
    /// The bath's <c>COMBAT_TECHNIQUE</c>s: kneeling into the water, then washing (<see cref="AnimationRecords"/>).
    /// </summary>
    public const string BathEnterMove = "97-" + FileName, BathWashMove = "98-" + FileName;

    #endregion
}
