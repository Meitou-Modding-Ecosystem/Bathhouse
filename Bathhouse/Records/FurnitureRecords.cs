using System.Numerics;
using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The tubs, service furniture and decorations (docs/bathhouse.md "Furniture"). Each is a copy of a game building for its look until the
/// mod's own models land. Tubs are storages that take nothing (<see cref="Use.JobPost"/>), so no game or mod AI (a town bar's, Fiefdom's) sits in
/// them and haulers never fill them: bathing is the mod's code.
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
        /// Storage for its own goods only (the game's Resource storage with a <c>limit inventory</c>, <see cref="Limits"/>), so haulers never fill
        /// it with other things.
        /// </summary>
        Limited,

        /// <summary>
        /// A <see cref="Limited"/> store that locks.
        /// </summary>
        LockedLimited,

        /// <summary>
        /// Keeps the model's own functionality (the bed, the shop counter).
        /// </summary>
        Model,

        /// <summary>
        /// The massage station's functionality (<see cref="Ids.MassageWork"/>).
        /// </summary>
        Massage,

        /// <summary>
        /// A storage that takes nothing (the game's Resource storage with an <c>itemtype limit</c> no item has): only so that a Shift + right click
        /// gives the selection a job there, which the mod turns into its own (a tub's Tub scrubber).
        /// </summary>
        JobPost,

        /// <summary>
        /// Storage for Water only (the game's Resource storage, limited as the Water Tower is): the boilers' supply.
        /// </summary>
        Water,
    }

    /// <summary>
    /// One piece: its id and name, the game building it copies (string id, then name), what it does and its text.
    /// </summary>
    sealed record Spec(string Id, string Name, string ModelId, string ModelName, Use Use, string Text);

    /// <summary>
    /// The game's General storage functionality: any item, worked by nobody.
    /// </summary>
    const string GeneralStorage = "2950-gamedata.base";

    /// <summary>
    /// The game's Resource storage functionality, which the Water Tower limits to Water.
    /// </summary>
    const string ResourceStorage = "1518-gamedata.base";

    /// <summary>
    /// The game's Water item.
    /// </summary>
    public const string Water = "1914-gamedata.base";

    /// <summary>
    /// The storage grid of the hamper, towel rack, soap shelf, tea table and lockers, in inventory cells.
    /// </summary>
    const int StorageWidth = 8, StorageHeight = 8;

    /// <summary>
    /// The check-in counter's storage grid, in inventory cells: room for its towels and soap. The game's Shop Counter's grid is a trader's, and its
    /// window ran off a 1920 x 1200 screen (Matthew's game).
    /// </summary>
    const int CounterWidth = 12, CounterHeight = 10;

    /// <summary>
    /// The only items each <see cref="Use.Limited"/> and <see cref="Use.LockedLimited"/> piece takes.
    /// </summary>
    static readonly Dictionary<string, string[]> Limits = new()
    {
        [Ids.Hamper] = [Ids.DirtyTowel],
        [Ids.Counter] = [Ids.Towel, Ids.Soap],
        [Ids.RobeHamper] = [Ids.DirtyRobe],
        // Two items, so the grid is the storage size rather than one robe's footprint; haulers store only the robe (itemtype limit ARMOUR).
        [Ids.Lockers] = [Ids.Robe, Ids.DirtyRobe],
        [Ids.TowelRack] = [Ids.Towel],
        [Ids.SoapShelf] = [Ids.Soap],
        // Two items in a grid of one tea set's footprint: the table holds one set, fresh or used.
        [Ids.TeaTable] = [Ids.TeaSet, Ids.UsedTeaSet],
        [Ids.TeaShelf] = [Ids.GreenfruitTea],
        [Ids.TeawareShelf] = [Ids.Teaware],
        [Ids.TeaSetCabinet] = [Ids.TeaSet],
        [Ids.UsedTeaRack] = [Ids.UsedTeaSet],
    };

    /// <summary>
    /// How many units each piece limited to one item holds, its <c>stackable bonus mult</c>: the game makes such a storage one cell of the item's
    /// footprint, whose stack starts from 1 in a limited storage and is multiplied by the mult (engine <c>Inventory.LimitOf</c>), as the game's
    /// own stores (Storage: Hemp 250, Water Tank 100).
    /// </summary>
    static readonly Dictionary<string, int> Capacity = new()
    {
        [Ids.Hamper] = 50,
        [Ids.RobeHamper] = 20,
        [Ids.TowelRack] = 50,
        [Ids.SoapShelf] = 100,
        [Ids.TeaShelf] = 50,
        [Ids.TeawareShelf] = 20,
        [Ids.TeaSetCabinet] = 20,
        [Ids.UsedTeaRack] = 20,
    };

    /// <summary>
    /// The tea table's grid, in inventory cells: one tea set's footprint, so it holds one set.
    /// </summary>
    const int TeaTableCells = 2;

    /// <summary>
    /// The game's Raw Meat, the blood bath's extra build cost.
    /// </summary>
    const string RawMeat = "42243-rebirth.mod";

    /// <summary>
    /// How much raw meat the blood bath costs to build.
    /// </summary>
    const int BloodBathMeat = 10;

    /// <summary>
    /// How to use a tub, the end of every tub's description.
    /// </summary>
    const string TubUse = "Build it in a building with a steam boiler that is switched on, powered and has Water. Select your people and press"
        + " Take a bath on its panel, or let checked-in customers use it. A Tub scrubber (panel button) cleans it after each soak.";

    /// <summary>
    /// Every piece, in id order.
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.WaterTank, "Bathhouse Water Tank", "12839-nodes_otto.mod", "Small Water Tank", Use.Water,
            "Holds only Water, which the steam boilers in the same building drink every day. Without Water the tubs stay cold."),
        new(Ids.SmallTub, "Small Tub", "5351-furniture_otto.mod", "Wooden Barrel", Use.JobPost,
            "A soaking tub for one. " + TubUse),
        new(Ids.MediumTub, "Medium Tub", "896-gamedata.base", "Large Barrels", Use.JobPost,
            "A soaking tub for two. " + TubUse),
        new(Ids.LargeTub, "Large Bath", "895-gamedata.base", "Booze Barrels", Use.JobPost,
            "A communal bath for four. " + TubUse),
        new(Ids.OilBath, "Oil Bath", "49382-rebirth.mod", "Booze Barrel", Use.JobPost,
            "A vat of warm machine oil for skeleton guests. " + TubUse),
        new(Ids.BloodBath, "Blood Bath", "2642-Cannibals.mod", "Cannibal Butcher Table", Use.JobPost,
            "A grim soak for grim guests. Other bathers refuse a building that has one, and the Holy Nation and United Cities disapprove. " + TubUse),
        new(Ids.MassageBed, "Massage Bed", "3460-otto.mod", "Bed", Use.Model,
            "Customers lie here for a massage. Build a massage station in the same building: each bed pairs with the nearest one. Heals bandaged"
            + " wounds four times as fast while someone works the station. Your own people only use it when ordered to."),
        new(Ids.MassageStation, "Massage Station", "3471-otto.mod", "Small Table", Use.Massage,
            "Give one of your people its operate job and keep towels in it: the attendant only works while it has some, using one an hour."
            + " While a customer lies on the nearest massage bed in the same building, each hour pays by the worker's Dexterity (up to 300"
            + " an hour at 100), which the work trains a little. Set the massage"
            + " price on its panel."),
        new(Ids.Counter, "Check-in Counter", "3469-otto.mod", "Shop Counter", Use.Limited,
            "The heart of the bathhouse: customers come to it from 6:00 to 22:00 while it stands in a building with a warm tub. Keep towels"
            + " and soap in it or in the building's stores, have someone stand by it (Bathhouse clerk: panel button, or Shift + right click the counter), and set prices and open it"
            + " on its panel."),
        new(Ids.Lockers, "Lockers", "1532622-rebirth.mod", "Crossbow Locker", Use.LockedLimited,
            "Holds only bathhouse robes, and locks. Bathers in the same building take a robe here and keep the armour it replaces in their"
            + " backpack while they soak; the worn robe goes to a robe hamper."),
        new(Ids.Hamper, "Laundry Hamper", "5355-furniture_otto.mod", "Small Barrel", Use.Limited,
            "Holds only dirty towels. Used towels land here, and a Laundry runner takes them to the washing machine."),
        new(Ids.TowelRack, "Towel Rack", "3470-otto.mod", "Shelf", Use.Limited,
            "Holds only clean towels, up to 50. Check-ins and soaks take towels from it, and a Laundry runner refills it from the washing machine."),
        new(Ids.TeaTable, "Tea Table", "3471-otto.mod", "Small Table", Use.Limited,
            "Holds one tea set. Bathers in the same building buy tea here after a soak and leave the used set on it. A Tea server (panel button)"
            + " brings a tea set from a tea set cabinet and takes the used one to a used tea rack. Set the tea price on its panel."),
        new(Ids.TeaShelf, "Tea Shelf", "3470-otto.mod", "Shelf", Use.Limited,
            "Holds only greenfruit tea, up to 50. Haulers bring it from the tea dryer; the tea bench's worker takes it from here."),
        new(Ids.RobeHamper, "Robe Hamper", "5355-furniture_otto.mod", "Small Barrel", Use.Limited,
            "Holds only dirty robes. Bathers drop their robe here after a soak (on the floor without one)."),
        new(Ids.ReviewBoard, "Review Board", "54552-Newwworld.mod", "banner_traders", Use.None,
            "Decoration: a notice board for guests' reviews."),
        new(Ids.ZenGarden, "Zen Garden", "50606-Newwworld.mod", "Carpet: Rusty Rug", Use.None, "Decoration: raked gravel and three stones."),
        new(Ids.WallArt, "Wall Art", "43324-changes_otto.mod", "banner_empire", Use.None, "Decoration: an ink painting of the desert."),
        new(Ids.Tapestry, "Tapestry", "43322-changes_otto.mod", "banner_holy", Use.None, "Decoration: a woven hanging of cranes in flight."),
        new(Ids.Statue, "Statue", "42325-changes_otto.mod", "Emperor Statue", Use.None, "Decoration: a weathered stone figure, seated in thought."),
        new(Ids.TowelHanger, "Towel Hanger", "5360-furniture_otto.mod", "Wall Shelf 1", Use.None, "Decoration: a peg rail with towels."),
        new(Ids.TeaDisplay, "Tea Set Display", "12618-nodes_otto.mod", "Wall Shelf 2", Use.None, "Decoration: a shelf of teaware on show."),
        new(Ids.SoapShelf, "Soap Shelf", "3470-otto.mod", "Shelf", Use.Limited,
            "Holds only soap, up to 100 bars. Haulers bring it from the soap press; check-ins and the Laundry runner take soap from it."),
        new(Ids.TeawareShelf, "Teaware Shelf", "3470-otto.mod", "Shelf", Use.Limited,
            "Holds only clean teaware, up to 20. Haulers bring it from the pottery kiln and the washing machine; the tea bench's worker takes"
            + " it from here."),
        new(Ids.TeaSetCabinet, "Tea Set Cabinet", "3470-otto.mod", "Shelf", Use.Limited,
            "Holds only tea sets, up to 20. Haulers bring them from the tea bench; the Tea server takes them to the tea tables."),
        new(Ids.UsedTeaRack, "Used Tea Rack", "3470-otto.mod", "Shelf", Use.Limited,
            "Holds only used tea sets, up to 20. The Tea server clears the tea tables into it; the washing machine's worker takes them to wash."),
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
        record.Floats["use range"] = WorkSpots.OperatorReach;
        RecordKit.Links(record, "consumes", (Ids.Towel, 5, 100));
        RecordKit.Links(record, "produces", (Ids.DirtyTowel, 5, 0));
        return record;
    }

    /// <summary>
    /// One piece: the model's look and size, the functionality its use asks for, in the bathhouse build-menu list with its text.
    /// </summary>
    static FcsRecord Piece(GameDatabase game, Spec spec)
    {
        var standIn = RecordKit.Model(game, FcsRecordType.Building, spec.ModelId, spec.ModelName);
        var model = OwnLook(game, spec.Id) ?? standIn;
        string[] skip = spec.Use == Use.Model ? ["upgrades to", "produces"] : ["functionality", "upgrades to", "produces"];
        var record = RecordKit.Copy(model, spec.Id, spec.Name, skip);

        if (spec.Use == Use.Model && model != standIn) KeepWork(standIn, record);
        if (spec.Id == Ids.MassageBed && model != standIn) KeepUseNodes(standIn, record);
        if (model != standIn && WorkSpots.Measured(spec.Id)) AddUseNode(record, WorkSpots.Local(spec.Id), WorkSpots.NodeYaw);

        SetUse(record, spec);
        record.Strings["building category"] = RecordKit.BuildCategory;
        record.Strings["Description"] = spec.Text;
        record.Bools["creates player town"] = true;

        if (spec.Id == Ids.BloodBath) AddMeat(record);
        EffectRecords.Attach(record, spec.Id);
        if (spec.Id == Ids.Counter) SetGrid(record, CounterWidth, CounterHeight);
        if (spec.Id == Ids.TeaTable) SetGrid(record, TeaTableCells, TeaTableCells);
        return record;
    }

    /// <summary>
    /// The building the structure import made from the piece's own model (docs/structures.md), when the data has it; null to copy the game's
    /// stand-in (tests and tools that read the game without the deployed mod).
    /// </summary>
    internal static GameRecord? OwnLook(GameDatabase game, string piece)
    {
        string? look = piece switch
        {
            Ids.SmallTub => Ids.SmallTubLook,
            Ids.MediumTub => Ids.MediumTubLook,
            Ids.LargeTub => Ids.LargeTubLook,
            Ids.OilBath => Ids.OilBathLook,
            Ids.BloodBath => Ids.BloodBathLook,
            Ids.MassageBed => Ids.MassageBedLook,
            Ids.MassageStation => Ids.MassageStationLook,
            Ids.Boiler => Ids.BoilerLook,
            Ids.Counter => Ids.CounterLook,
            Ids.Hamper => Ids.HamperLook,
            Ids.Lockers => Ids.LockersLook,
            Ids.TowelRack => Ids.TowelRackLook,
            Ids.TeaTable => Ids.TeaTableLook,
            Ids.SoapPress => Ids.SoapPressLook,
            Ids.TeaBench => Ids.TeaBenchLook,
            Ids.TowelLoom => Ids.TowelLoomLook,
            Ids.WashingMachine => Ids.WashingMachineLook,
            Ids.TeaDryer => Ids.TeaDryerLook,
            Ids.Kiln => Ids.KilnLook,
            Ids.ReviewBoard => Ids.ReviewBoardLook,
            Ids.Statue => Ids.StatueLook,
            Ids.ZenGarden => Ids.ZenGardenLook,
            Ids.TeaDisplay => Ids.TeaDisplayLook,
            Ids.TeaShelf => Ids.TeaShelfLook,
            Ids.RobeHamper => Ids.RobeHamperLook,
            Ids.SoapShelf => Ids.SoapShelfLook,
            _ => null,
        };

        return look is not null && game.Find(look) is { Type: FcsRecordType.Building } building ? building : null;
    }

    /// <summary>
    /// A piece that works as its stand-in (the massage bed lies a customer down as the game's bed, the counter sells as the game's shop counter)
    /// keeps the stand-in's functionality, inventory and <c>output rate</c> (a bed's healing factor) on its own look. With no use node a sleeper lies
    /// at the piece's origin, facing its way.
    /// </summary>
    static void KeepWork(GameRecord standIn, FcsRecord record)
    {
        RecordKit.CopyLinks(standIn, record, "functionality");
        RecordKit.CopyLinks(standIn, record, "limit inventory");
        if (standIn.Floats.TryGetValue("output rate", out float rate)) record.Floats["output rate"] = rate;
        record.Bools["has inventory"] = standIn.Bools.GetValueOrDefault("has inventory");
        foreach (string size in (string[])["storage size width", "storage size height"])
        {
            if (standIn.Ints.TryGetValue(size, out int value)) record.Ints[size] = value;
            else record.Ints.Remove(size);
        }
    }

    /// <summary>
    /// The game's use node (NODE_USE, node type 4): where a sleeper lies in a bed and where a machine's operator stands.
    /// </summary>
    const string UseNodeRecord = "1183-gamedata.base";

    /// <summary>
    /// Gives the massage bed on its own model one of the game Bed's use nodes, scaled as the Bed scales it. The Bed lies along local X with a node
    /// beside each long side (z +11.18 and -11.05, the second turned 180 degrees, so they face opposite ways), and the side a sleeper comes from
    /// decides where its head goes. The model lies along X (its .json <c>yaw</c> 0) with its head rest at -X and keeps only the -Z node, so every
    /// sleeper lies the same way (Forgotten Buildings' Bed with Pillow keeps the same one).
    /// </summary>
    static void KeepUseNodes(GameRecord standIn, FcsRecord record)
    {
        float scale = standIn.GetFloat("scale", 1) / record.Floats.GetValueOrDefault("scale", 1);
        var node = standIn.Instances.Values
            .Where(i => i.Target == UseNodeRecord)
            .OrderBy(i => i.Position.Z)
            .FirstOrDefault();
        if (node is null) return;

        record.Instances.Add(new FcsInstance { Id = node.Id, Target = UseNodeRecord, Position = node.Position * scale, Rotation = node.Rotation });
    }

    /// <summary>
    /// Adds a use node at <paramref name="at"/> (building units), turned <paramref name="yaw"/> radians about Y.
    /// </summary>
    internal static void AddUseNode(FcsRecord record, Vector3 at, float yaw)
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
        record.Instances.Add(new FcsInstance { Id = $"usenode{record.Instances.Count + 1}", Target = UseNodeRecord, Position = at, Rotation = turn });
    }

    /// <summary>
    /// Sets the piece's storage grid to <paramref name="width"/> by <paramref name="height"/> cells.
    /// </summary>
    static void SetGrid(FcsRecord record, int width, int height)
    {
        record.Ints["storage size width"] = width;
        record.Ints["storage size height"] = height;
    }

    /// <summary>
    /// Gives the piece the functionality, inventory and lock its use asks for.
    /// </summary>
    static void SetUse(FcsRecord record, Spec spec)
    {
        var use = spec.Use;
        bool storage = use is Use.Storage or Use.LockedStorage;
        record.Bools["has inventory"] = storage || use == Use.Model && record.Bools.GetValueOrDefault("has inventory");
        record.Bools["has lock"] = use is Use.LockedStorage or Use.LockedLimited;

        if (storage)
        {
            RecordKit.Links(record, "functionality", (GeneralStorage, 0, 0));
            record.Ints["storage size width"] = StorageWidth;
            record.Ints["storage size height"] = StorageHeight;
        }

        if (use == Use.Massage) RecordKit.Links(record, "functionality", (Ids.MassageWork, 0, 0));
        if (use == Use.Water) SetWater(record);
        if (use == Use.JobPost) SetJobPost(record);
        if (use is Use.Limited or Use.LockedLimited) SetLimited(record, Limits[spec.Id], Capacity.GetValueOrDefault(spec.Id, 1));
        if (spec.Id == Ids.Lockers) record.Ints["itemtype limit"] = (int)FcsRecordType.Armour;
    }

    /// <summary>
    /// Makes the piece a store for its own goods only; one limited to a single item holds <paramref name="capacity"/> of it.
    /// </summary>
    static void SetLimited(FcsRecord record, string[] items, int capacity)
    {
        record.Bools["has inventory"] = true;
        RecordKit.Links(record, "functionality", (ResourceStorage, 0, 0));
        RecordKit.Links(record, "limit inventory", items.Select(i => (i, 0, 100)).ToArray());
        record.Ints["storage size width"] = StorageWidth;
        record.Ints["storage size height"] = StorageHeight;
        if (items.Length == 1) record.Floats["stackable bonus mult"] = capacity;
    }

    /// <summary>
    /// Makes the piece a store that takes nothing: a Resource storage whose <c>itemtype limit</c> is BUILDING, a type no item has, so the hauling AI
    /// finds nothing to put there (<c>MachineData.Accepts</c>). A one-cell grid.
    /// </summary>
    static void SetJobPost(FcsRecord record)
    {
        record.Bools["has inventory"] = true;
        RecordKit.Links(record, "functionality", (ResourceStorage, 0, 0));
        record.References.Remove("limit inventory");
        record.Ints["itemtype limit"] = (int)FcsRecordType.Building;
        SetGrid(record, 1, 1);
    }

    /// <summary>
    /// Makes the piece a Water store, as the game's Water Tower is.
    /// </summary>
    static void SetWater(FcsRecord record)
    {
        record.Bools["has inventory"] = true;
        RecordKit.Links(record, "functionality", (ResourceStorage, 0, 0));
        RecordKit.Links(record, "limit inventory", (Water, 0, 100));
        record.Ints["storage size width"] = StorageWidth;
        record.Ints["storage size height"] = StorageHeight;
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
