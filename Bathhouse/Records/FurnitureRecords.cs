using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The tubs, service furniture and decorations (docs/bathhouse.md "Furniture"). Each is a copy of a game building for its look until the
/// mod's own models land. Tubs have no functionality, so no game or mod AI (a town bar's, Fiefdom's) sits in them: bathing is the mod's code.
/// </summary>
static class FurnitureRecords
{
    /// <summary>
    /// What a piece does in the game's own rules.
    /// </summary>
    enum Use
    {
        /// <summary>
        /// Nothing: a tub, a decoration, the review board.
        /// </summary>
        None,

        /// <summary>
        /// A storage box (the game's General storage).
        /// </summary>
        Storage,

        /// <summary>
        /// A lockable storage box.
        /// </summary>
        LockedStorage,

        /// <summary>
        /// Keeps the model's own functionality (the bed, the shop counter).
        /// </summary>
        Model,

        /// <summary>
        /// The massage station's functionality (<see cref="Ids.MassageWork"/>).
        /// </summary>
        Massage,
    }

    /// <summary>
    /// One piece: its id and name, the game building it copies (string id, then name), what it does, its build-menu list and its text.
    /// </summary>
    sealed record Spec(string Id, string Name, string ModelId, string ModelName, Use Use, string Category, string Text);

    /// <summary>
    /// The game's General storage functionality: any item, worked by nobody.
    /// </summary>
    const string GeneralStorage = "2950-gamedata.base";

    /// <summary>
    /// The storage grid of the hamper, towel rack, tea table and lockers, in inventory cells.
    /// </summary>
    const int StorageWidth = 8, StorageHeight = 8;

    /// <summary>
    /// The game's Raw Meat, the blood bath's extra build cost.
    /// </summary>
    const string RawMeat = "42243-rebirth.mod";

    /// <summary>
    /// How much raw meat the blood bath costs to build.
    /// </summary>
    const int BloodBathMeat = 10;

    /// <summary>
    /// Every piece, in id order.
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.SmallTub, "Small Tub", "5351-furniture_otto.mod", "Wooden Barrel", Use.None, "INTERIOR",
            "A one-person soaking tub. Needs a lit steam boiler in the same building."),
        new(Ids.MediumTub, "Medium Tub", "896-gamedata.base", "Large Barrels", Use.None, "INTERIOR",
            "A soaking tub for two. Needs a lit steam boiler in the same building."),
        new(Ids.LargeTub, "Large Bath", "895-gamedata.base", "Booze Barrels", Use.None, "INTERIOR",
            "A communal bath for four. Needs a lit steam boiler in the same building."),
        new(Ids.OilBath, "Oil Bath", "49382-rebirth.mod", "Booze Barrel", Use.None, "INTERIOR",
            "A vat of warm machine oil for skeletons. Needs a lit steam boiler in the same building."),
        new(Ids.BloodBath, "Blood Bath", "2642-Cannibals.mod", "Cannibal Butcher Table", Use.None, "INTERIOR",
            "A grim soak for grim guests. Other bathers refuse a building that has one, and the Holy Nation and United Cities disapprove."),
        new(Ids.MassageBed, "Massage Bed", "3460-otto.mod", "Bed", Use.Model, "INTERIOR",
            "A customer lies here for a massage while an attendant works the massage station beside it."),
        new(Ids.MassageStation, "Massage Station", "3471-otto.mod", "Small Table", Use.Massage, "INTERIOR",
            "Oils, stones and towels. The attendant works here while a customer lies on the nearest massage bed."),
        new(Ids.Counter, "Check-in Counter", "3469-otto.mod", "Shop Counter", Use.Model, "STORAGE",
            "Bathers check in here and take a towel and soap from its stock."),
        new(Ids.Lockers, "Lockers", "1532622-rebirth.mod", "Crossbow Locker", Use.LockedStorage, "STORAGE",
            "Bathers lock their gear away while they soak."),
        new(Ids.Hamper, "Laundry Hamper", "5355-furniture_otto.mod", "Small Barrel", Use.Storage, "STORAGE",
            "Bathers drop their dirty towels here."),
        new(Ids.TowelRack, "Towel Rack", "3470-otto.mod", "Shelf", Use.Storage, "STORAGE", "Holds clean towels for the counter and the tubs."),
        new(Ids.TeaTable, "Tea Table", "3471-otto.mod", "Small Table", Use.Storage, "INTERIOR",
            "Keep tea sets here. Bathers nearby buy tea."),
        new(Ids.ReviewBoard, "Review Board", "54552-Newwworld.mod", "banner_traders", Use.None, "INTERIOR",
            "Shows the bathhouse's rating and its latest reviews."),
        new(Ids.ZenGarden, "Zen Garden", "50606-Newwworld.mod", "Carpet: Rusty Rug", Use.None, "INTERIOR", "Raked gravel and three stones."),
        new(Ids.WallArt, "Wall Art", "43324-changes_otto.mod", "banner_empire", Use.None, "INTERIOR", "An ink painting of the desert."),
        new(Ids.Tapestry, "Tapestry", "43322-changes_otto.mod", "banner_holy", Use.None, "INTERIOR", "A woven hanging of cranes in flight."),
        new(Ids.Statue, "Statue", "42325-changes_otto.mod", "Emperor Statue", Use.None, "INTERIOR", "A weathered stone figure, seated in thought."),
        new(Ids.TowelHanger, "Towel Hanger", "5360-furniture_otto.mod", "Wall Shelf 1", Use.None, "INTERIOR", "A peg rail with towels."),
        new(Ids.TeaDisplay, "Tea Set Display", "12618-nodes_otto.mod", "Wall Shelf 2", Use.None, "INTERIOR", "A shelf of teaware on show."),
    ];

    /// <summary>
    /// The massage station's functionality, then every piece.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) => [MassageWork(game), .. Specs.Select(s => Piece(game, s))];

    /// <summary>
    /// The massage station's functionality: a production machine for one attendant that turns towels into dirty towels (one an hour's work), so
    /// characters and Fiefdom residents work it. What it earns is the mod's code.
    /// </summary>
    static FcsRecord MassageWork(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.BuildingFunctionality, "2018-gamedata.base", "Textiles hemp");
        var record = RecordKit.Copy(model, Ids.MassageWork, "massage", "consumes", "produces");

        record.Ints["max operators"] = 1;
        RecordKit.Links(record, "consumes", (Ids.Towel, 5, 100));
        RecordKit.Links(record, "produces", (Ids.DirtyTowel, 5, 0));
        return record;
    }

    /// <summary>
    /// One piece: the model's look and size, the functionality its use asks for, in its build-menu list with its text.
    /// </summary>
    static FcsRecord Piece(GameDatabase game, Spec spec)
    {
        var model = RecordKit.Model(game, FcsRecordType.Building, spec.ModelId, spec.ModelName);
        string[] skip = spec.Use == Use.Model ? ["upgrades to", "produces"] : ["functionality", "upgrades to", "produces"];
        var record = RecordKit.Copy(model, spec.Id, spec.Name, skip);

        SetUse(record, spec.Use);
        record.Strings["building category"] = spec.Category;
        record.Strings["Description"] = spec.Text;
        record.Bools["creates player town"] = true;

        if (spec.Id == Ids.BloodBath) AddMeat(record);
        return record;
    }

    /// <summary>
    /// Gives the piece the functionality, inventory and lock its use asks for.
    /// </summary>
    static void SetUse(FcsRecord record, Use use)
    {
        bool storage = use is Use.Storage or Use.LockedStorage;
        record.Bools["has inventory"] = storage || use == Use.Model && record.Bools.GetValueOrDefault("has inventory");
        record.Bools["has lock"] = use == Use.LockedStorage;

        if (storage)
        {
            RecordKit.Links(record, "functionality", (GeneralStorage, 0, 0));
            record.Ints["storage size width"] = StorageWidth;
            record.Ints["storage size height"] = StorageHeight;
        }

        if (use == Use.Massage) RecordKit.Links(record, "functionality", (Ids.MassageWork, 0, 0));
    }

    /// <summary>
    /// Adds raw meat to the blood bath's build cost.
    /// </summary>
    static void AddMeat(FcsRecord record)
    {
        var cost = record.References.GetValueOrDefault("construction") ?? [];
        record.References["construction"] = [.. cost, new FcsReference(RawMeat, BloodBathMeat, 0, 0)];
    }
}
