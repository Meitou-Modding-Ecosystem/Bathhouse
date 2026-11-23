using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The machines (docs/bathhouse.md "Machines"): production machines and the tea bench, each a copy of a game machine whose functionality is
/// copied and given Bathhouse's inputs and outputs, and the steam boiler.
/// </summary>
static class MachineRecords
{
    /// <summary>
    /// One input or output: the item, the most its section holds, and (inputs) the rate, in hundredths of an item per item made.
    /// </summary>
    sealed record Flow(string Item, int Holds, int Rate = 0);

    /// <summary>
    /// One machine: its building and functionality ids, name, the game building it copies (string id, then name), that building's
    /// functionality (string id, then name), its inputs and outputs, and its text.
    /// </summary>
    sealed record Spec(string Id, string WorkId, string Name, string ModelId, string ModelName, string WorkModelId, string WorkModelName,
        Flow[] Consumes, Flow[] Produces, string Text);

    /// <summary>
    /// The production machines and the tea bench. Rates follow the game's own: its hemp loom takes 3 hemp a fabric (rate 300).
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.TowelLoom, Ids.TowelLoomWork, "Towel Loom", "2016-gamedata.base", "Fabric Loom (hemp)", "2018-gamedata.base", "Textiles hemp",
            [new("584-gamedata.base", 10, 50)], [new(Ids.Towel, 10)],
            "Weaves fabric into thick towels, two from each bolt. Give one of your people its operate job and keep it in fabric."),
        new(Ids.SoapPress, Ids.SoapPressWork, "Soap Press", "52163-rebirth.mod", "Hemp Processor", "52158-rebirth.mod", "drugs",
            [new("1965-gamedata.base", 10, 100)], [new(Ids.Soap, 10)],
            "Presses hemp into bars of soap, one hemp a bar. Give one of your people its operate job and keep it in hemp."),
        new(Ids.TeaDryer, Ids.TeaDryerWork, "Tea Dryer", "50922-rebirth.mod", "Biofuel Distillery (Hemp)", "50921-rebirth.mod", "hemp to fuel",
            [new("42309-changes_otto.mod", 10, 100)], [new(Ids.GreenfruitTea, 10)],
            "Slices and dries greenfruit into greenfruit tea. Give one of your people its operate job and keep it in greenfruit."),
        new(Ids.Kiln, Ids.KilnWork, "Pottery Kiln", "1944-gamedata.base", "Cooking Stove", "2018-gamedata.base", "Textiles hemp",
            [new("1515-gamedata.base", 10, 100)], [new(Ids.Teaware, 5)],
            "Fires raw stone into teaware. Give one of your people its operate job and keep it in raw stone."),
    ];

    /// <summary>
    /// The production machines, the tea bench and the steam boiler, functionalities first.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) =>
    [
        .. Specs.Select(s => Work(game, s)),
        TeaBenchWork(game),
        WasherWork(game),
        .. Specs.Select(s => Building(game, s, s.WorkId)),
        TeaBench(game),
        Washer(game),
        Boiler(game),
    ];

    #region Production machines

    /// <summary>
    /// The game's production function (a refinery: inputs to outputs while worked).
    /// </summary>
    const int RefineryFunction = 4;

    /// <summary>
    /// A machine's functionality: the model's, with its inputs and outputs replaced.
    /// </summary>
    static FcsRecord Work(GameDatabase game, Spec spec)
    {
        var model = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, spec.WorkModelId, spec.WorkModelName);
        var record = RecordKit.Copy(model, spec.WorkId, spec.Name, "consumes", "produces");

        record.Ints["function"] = RefineryFunction;
        record.Bools["is production building"] = true;
        record.Bools["output per day"] = true;
        record.Floats["use range"] = WorkSpots.OperatorReach;
        RecordKit.Links(record, "consumes", [.. spec.Consumes.Select(f => (f.Item, f.Holds, f.Rate))]);
        RecordKit.Links(record, "produces", [.. spec.Produces.Select(f => (f.Item, f.Holds, 0))]);
        return record;
    }

    /// <summary>
    /// A machine: the model building's look, size and build cost with the given functionality, in the given build-menu list. On its own look its
    /// operator stands at a use node in front of it (<see cref="WorkSpots"/>), not wherever the walk to its centre ends.
    /// </summary>
    static FcsRecord Building(GameDatabase game, Spec spec, string work)
    {
        var standIn = RecordKit.Model(game, FcsRecordType.Building, spec.ModelId, spec.ModelName);
        var model = FurnitureRecords.OwnLook(game, spec.Id) ?? standIn;
        var record = RecordKit.Copy(model, spec.Id, spec.Name, "functionality", "upgrades to");

        if (model != standIn) KeepCost(standIn, record);
        if (model != standIn) FurnitureRecords.AddUseNode(record, WorkSpots.Local(spec.Id), WorkSpots.NodeYaw);

        RecordKit.Links(record, "functionality", (work, 0, 0));
        record.Strings["building category"] = RecordKit.BuildCategory;
        record.Strings["Description"] = spec.Text;
        record.Bools["creates player town"] = true;
        return record;
    }

    /// <summary>
    /// A machine on its own look keeps the stand-in's build cost and power draw, so only its look changes.
    /// </summary>
    static void KeepCost(GameRecord standIn, FcsRecord record)
    {
        RecordKit.CopyLinks(standIn, record, "construction");
        if (standIn.Ints.TryGetValue("power output", out int power)) record.Ints["power output"] = power;
        else record.Ints.Remove("power output");
    }

    #endregion

    #region Tea bench, washer and boiler

    /// <summary>
    /// The steam boiler's draw from the town's grid, in the game's power units (a weapon smith draws 8, a small generator gives 40).
    /// </summary>
    const int BoilerDraw = 10;

    /// <summary>
    /// The steam boiler's build cost: iron plates, as many as the cooking stove's.
    /// </summary>
    const int BoilerPlates = 2;

    /// <summary>
    /// The washer's <c>production mult</c>: about a game hour a wash for an unskilled operator (the stove's 1.5 took over three; headless, Nameless).
    /// </summary>
    const float WashSpeed = 5;

    /// <summary>
    /// The tea bench's functionality: the cooking stove's, crafting only tea sets (from their ingredients, teaware and greenfruit tea).
    /// </summary>
    static FcsRecord TeaBenchWork(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, "43954-rebirth.mod", "cooking general");
        var record = RecordKit.Copy(model, Ids.TeaBenchWork, "tea bench", "item crafts");

        RecordKit.Links(record, "item crafts", (Ids.TeaSet, 5, 0));
        record.Floats["use range"] = WorkSpots.OperatorReach;
        return record;
    }

    /// <summary>
    /// The washing machine's functionality: the cooking stove's crafting, with a queue of towel, robe and teaware washes (each from its
    /// <c>ingredients</c>, a dirty one and a quarter of a soap). The output holds 10 towels, one robe (armour never stacks) and 5 teaware.
    /// </summary>
    static FcsRecord WasherWork(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, "43954-rebirth.mod", "cooking general");
        var record = RecordKit.Copy(model, Ids.WasherWork, "Washing Machine", "item crafts");

        RecordKit.Links(record, "item crafts", (Ids.Towel, 10, 0), (Ids.Robe, 1, 0), (Ids.Teaware, 5, 0));
        record.Floats["production mult"] = WashSpeed;
        record.Floats["use range"] = WorkSpots.OperatorReach;
        return record;
    }

    /// <summary>
    /// The washing machine: the Sake Still's look and build cost with the washer functionality.
    /// </summary>
    static FcsRecord Washer(GameDatabase game)
    {
        var spec = new Spec(Ids.WashingMachine, Ids.WasherWork, "Washing Machine", "52162-rebirth.mod", "Sake Still", "", "", [], [],
            "Washes dirty towels, robes and used tea sets clean, one bar of soap for four. Queue Towel, Robe and Teaware washes at it like a"
            + " crafting bench (Repeat Queue keeps it going) and give one of your people its operate job. A Laundry runner (panel button)"
            + " fetches dirty goods from the hampers and the used tea rack for the queued washes, keeps it in soap and takes towels to the"
            + " racks, robes to the lockers and teaware to the teaware shelf.");
        return Building(game, spec, Ids.WasherWork);
    }

    /// <summary>
    /// The tea bench: the cooking stove's look and power with the tea bench functionality.
    /// </summary>
    static FcsRecord TeaBench(GameDatabase game)
    {
        var spec = new Spec(Ids.TeaBench, Ids.TeaBenchWork, "Tea Bench", "1944-gamedata.base", "Cooking Stove", "", "", [], [],
            "Brews greenfruit tea in teaware into tea sets for the tea tables. Craft at it like a cooking stove and store the sets on a tea shelf.");
        return Building(game, spec, Ids.TeaBenchWork);
    }

    /// <summary>
    /// The steam boiler: the game's Boiler Tank prop made a power consumer. Its functionality is the game's lights' (a consumer that needs no
    /// operator), so the selection panel shows the game's Power switch; indoors it always draws. What it heats is the mod's code.
    /// </summary>
    static FcsRecord Boiler(GameDatabase game)
    {
        var model = FurnitureRecords.OwnLook(game, Ids.Boiler)
            ?? RecordKit.Model(game, FcsRecordType.Building, "5369-furniture_otto.mod", "Boiler Tank");
        var lights = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, "5358-gamedata.base", "Lights");
        var record = RecordKit.Copy(model, Ids.Boiler, "Steam Boiler", "functionality");

        RecordKit.Links(record, "functionality", (lights.StringId, 0, 0));
        record.Ints["power output"] = -BoilerDraw;
        RecordKit.Links(record, "construction", ("42159-gamedata.base", BoilerPlates, 0));
        record.Strings["building category"] = RecordKit.BuildCategory;
        record.Strings["Description"] = "Heats the tubs in its building while it is switched on and powered. It drinks Water every day"
            + " from the building's stores, so build a bathhouse water tank beside it."
            + $" Each boiler heats {BathhouseSettings.DefaultSeatsPerBoiler} bathers' seats.";
        record.Bools["creates player town"] = true;
        EffectRecords.Attach(record, Ids.Boiler);
        return record;
    }

    #endregion
}
