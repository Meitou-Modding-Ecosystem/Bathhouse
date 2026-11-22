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
    /// functionality (string id, then name), its build-menu list, its inputs and outputs, and its text.
    /// </summary>
    sealed record Spec(string Id, string WorkId, string Name, string ModelId, string ModelName, string WorkModelId, string WorkModelName,
        string Category, Flow[] Consumes, Flow[] Produces, string Text);

    /// <summary>
    /// The production machines and the tea bench. Rates follow the game's own: its hemp loom takes 3 hemp a fabric (rate 300).
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.TowelLoom, Ids.TowelLoomWork, "Towel Loom", "2016-gamedata.base", "Fabric Loom (hemp)", "2018-gamedata.base", "Textiles hemp",
            "CRAFTING", [new("584-gamedata.base", 10, 50)], [new(Ids.Towel, 10)],
            "Weaves fabrics into thick towels, two from each bolt."),
        new(Ids.SoapPress, Ids.SoapPressWork, "Soap Press", "52163-rebirth.mod", "Hemp Processor", "52158-rebirth.mod", "drugs",
            "CRAFTING", [new("1965-gamedata.base", 10, 200)], [new(Ids.Soap, 10)], "Presses hemp into bars of soap, two hemp a bar."),
        new(Ids.WashingMachine, Ids.WasherWork, "Washing Machine", "52162-rebirth.mod", "Sake Still", "52157-rebirth.mod", "Sake",
            "CRAFTING", [new(Ids.DirtyTowel, 10, 100), new(Ids.Soap, 10, 25)], [new(Ids.Towel, 10)],
            "Washes dirty towels clean. One bar of soap does four towels."),
        new(Ids.TeaDryer, Ids.TeaDryerWork, "Tea Dryer", "50922-rebirth.mod", "Biofuel Distillery (Hemp)", "50921-rebirth.mod", "hemp to fuel",
            "FOOD", [new("42309-changes_otto.mod", 10, 100)], [new(Ids.GreenfruitTea, 10)], "Slices and dries greenfruit into tea."),
        new(Ids.Kiln, Ids.KilnWork, "Pottery Kiln", "1944-gamedata.base", "Cooking Stove", "2018-gamedata.base", "Textiles hemp",
            "CRAFTING", [new("1515-gamedata.base", 10, 100)], [new(Ids.Teaware, 5)], "Fires raw stone into teaware."),
    ];

    /// <summary>
    /// The production machines, the tea bench and the steam boiler, functionalities first.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) =>
    [
        .. Specs.Select(s => Work(game, s)),
        TeaBenchWork(game),
        .. Specs.Select(s => Building(game, s, s.WorkId)),
        TeaBench(game),
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
        RecordKit.Links(record, "consumes", [.. spec.Consumes.Select(f => (f.Item, f.Holds, f.Rate))]);
        RecordKit.Links(record, "produces", [.. spec.Produces.Select(f => (f.Item, f.Holds, 0))]);
        return record;
    }

    /// <summary>
    /// A machine: the model building's look, size and build cost with the given functionality, in the given build-menu list.
    /// </summary>
    static FcsRecord Building(GameDatabase game, Spec spec, string work)
    {
        var model = RecordKit.Model(game, FcsRecordType.Building, spec.ModelId, spec.ModelName);
        var record = RecordKit.Copy(model, spec.Id, spec.Name, "functionality", "upgrades to");

        RecordKit.Links(record, "functionality", (work, 0, 0));
        record.Strings["building category"] = spec.Category;
        record.Strings["Description"] = spec.Text;
        record.Bools["creates player town"] = true;
        return record;
    }

    #endregion

    #region Tea bench and boiler

    /// <summary>
    /// The steam boiler's draw from the town's grid, in the game's power units (a weapon smith draws 8, a small generator gives 40).
    /// </summary>
    const int BoilerDraw = 10;

    /// <summary>
    /// The steam boiler's build cost: iron plates, as many as the cooking stove's.
    /// </summary>
    const int BoilerPlates = 2;

    /// <summary>
    /// The tea bench's functionality: the cooking stove's, crafting only tea sets (from their ingredients, teaware and greenfruit tea).
    /// </summary>
    static FcsRecord TeaBenchWork(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, "43954-rebirth.mod", "cooking general");
        var record = RecordKit.Copy(model, Ids.TeaBenchWork, "tea bench", "item crafts");

        RecordKit.Links(record, "item crafts", (Ids.TeaSet, 5, 0));
        return record;
    }

    /// <summary>
    /// The tea bench: the cooking stove's look and power with the tea bench functionality.
    /// </summary>
    static FcsRecord TeaBench(GameDatabase game)
    {
        var spec = new Spec(Ids.TeaBench, Ids.TeaBenchWork, "Tea Bench", "1944-gamedata.base", "Cooking Stove", "", "", "FOOD", [], [],
            "Brews greenfruit tea in teaware to make tea sets for the bathhouse's tea tables.");
        return Building(game, spec, Ids.TeaBenchWork);
    }

    /// <summary>
    /// The steam boiler: the game's Boiler Tank prop made a power consumer. Its functionality is the game's lights' (a consumer that needs no
    /// operator), so the selection panel shows the game's Power switch; indoors it always draws. What it heats is the mod's code.
    /// </summary>
    static FcsRecord Boiler(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.Building, "5369-furniture_otto.mod", "Boiler Tank");
        var lights = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, "5358-gamedata.base", "Lights");
        var record = RecordKit.Copy(model, Ids.Boiler, "Steam Boiler", "functionality");

        RecordKit.Links(record, "functionality", (lights.StringId, 0, 0));
        record.Ints["power output"] = -BoilerDraw;
        RecordKit.Links(record, "construction", ("42159-gamedata.base", BoilerPlates, 0));
        record.Strings["building category"] = "POWER";
        record.Strings["Description"] = "Heats every tub in its building while it is switched on, powered and has Water in a box nearby."
            + $" Each boiler heats {BathhouseSettings.DefaultSeatsPerBoiler} bathers' seats.";
        record.Bools["creates player town"] = true;
        return record;
    }

    #endregion
}
