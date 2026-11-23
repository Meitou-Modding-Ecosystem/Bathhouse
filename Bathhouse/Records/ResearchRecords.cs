using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The research tree (docs/bathhouse.md "Research"): Bathhouse Basics, then Hot Baths, Massage, Tea Service and the Oil Bath after it, and the
/// Blood Bath after Hot Baths.
/// </summary>
static class ResearchRecords
{
    /// <summary>
    /// One project: its id, name, research bench level, hours, the projects it needs, what it unlocks, the armour it unlocks, and its text.
    /// </summary>
    sealed record Spec(string Id, string Name, int Level, int Hours, string[] Needs, string[] Buildings, string[] Armour, string Text);

    /// <summary>
    /// The research list the projects show in (the game's lists are Core, Crafting, Defence, Electrics, Farming, Industry, Smithing, Training).
    /// </summary>
    const string Category = "Crafting";

    /// <summary>
    /// The projects, in id order.
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.BasicsResearch, "Bathhouse Basics", 1, 6, [],
            [
                Ids.TowelLoom, Ids.SoapPress, Ids.WashingMachine, Ids.Boiler, Ids.WaterTank, Ids.SmallTub, Ids.Counter, Ids.Lockers, Ids.Hamper,
                Ids.TowelRack, Ids.RobeHamper
            ],
            [Ids.Robe], "Hot water, clean towels and a place to soak. Unlocks the towel loom, soap press, washing machine, steam boiler, water tank,"
            + " small tub,"
            + " check-in counter, lockers, laundry hamper, towel rack, robe hamper and the bathhouse robe."),
        new(Ids.HotBathsResearch, "Hot Baths", 2, 8, [Ids.BasicsResearch],
            [Ids.MediumTub, Ids.LargeTub, Ids.ReviewBoard, Ids.ZenGarden, Ids.WallArt, Ids.Tapestry, Ids.Statue, Ids.TowelHanger], [],
            "Bigger baths and a place worth relaxing in. Unlocks the medium tub, large bath, review board and decorations."),
        new(Ids.MassageResearch, "Massage", 2, 8, [Ids.BasicsResearch], [Ids.MassageBed, Ids.MassageStation], [],
            "Skilled hands for tired bodies. Unlocks the massage bed and massage station."),
        new(Ids.TeaResearch, "Tea Service", 2, 8, [Ids.BasicsResearch],
            [Ids.TeaDryer, Ids.Kiln, Ids.TeaBench, Ids.TeaTable, Ids.TeaShelf, Ids.TeaDisplay], [],
            "Greenfruit tea for bathers. Unlocks the tea dryer, pottery kiln, tea bench, tea table, tea shelf and tea set display."),
        new(Ids.OilBathResearch, "Oil Baths", 2, 8, [Ids.BasicsResearch], [Ids.OilBath], [],
            "A warm soak in machine oil, for skeleton guests. Unlocks the oil bath."),
        new(Ids.BloodBathResearch, "Blood Baths", 3, 12, [Ids.HotBathsResearch], [Ids.BloodBath], [],
            "For guests with darker tastes. Unlocks the blood bath, built with raw meat. Other bathers shun a building that has one."),
    ];

    /// <summary>
    /// Every project.
    /// </summary>
    public static IEnumerable<FcsRecord> Build() => Specs.Select(Project);

    /// <summary>
    /// One project: no blueprint, no cost, never repeated.
    /// </summary>
    static FcsRecord Project(Spec spec)
    {
        var record = RecordKit.New(spec.Id, FcsRecordType.Research, spec.Name);
        record.Ints["level"] = spec.Level;
        record.Ints["time"] = spec.Hours;
        record.Ints["money"] = 0;
        record.Ints["repeats"] = 0;
        record.Ints["power capacity increase"] = 0;
        record.Ints["power increase"] = 0;
        record.Floats["production mult"] = 1;
        record.Floats["repeat mult"] = 1;
        record.Bools["blueprint only"] = false;
        record.Bools["is level upgrade"] = false;
        record.Strings["category"] = Category;
        record.Strings["description"] = spec.Text;

        RecordKit.Links(record, "requirements", [.. spec.Needs.Select(n => (n, 0, 100))]);
        RecordKit.Links(record, "enable buildings", [.. spec.Buildings.Select(b => (b, 0, 0))]);
        if (spec.Armour.Length > 0) RecordKit.Links(record, "enable armour", [.. spec.Armour.Select(a => (a, 0, 0))]);
        return record;
    }
}
