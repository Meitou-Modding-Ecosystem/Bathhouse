using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The items (docs/bathhouse.md "Items"). Each is a copy of a game item for its fields, with its own name, value, text, icon and model (from
/// <c>Bathhouse/ItemModels</c>, when the import made it; else the copied item's look).
/// </summary>
static class ItemRecords
{
    /// <summary>
    /// One item: its id and name, the game item it is copied from (string id, then name), its value in cats, how many stack, its text, its icon
    /// (<c>Bathhouse/items/icons/bathhouse-&lt;icon&gt;.png</c>) and its own model (<c>ItemModels/&lt;model&gt;.glb</c>, block from
    /// <c>ItemModels/ids.json</c>).
    /// </summary>
    sealed record Spec(string Id, string Name, string ModelId, string ModelName, int Value, int Stack, string Text, string Icon, string Model,
        int Block);

    /// <summary>
    /// The first MATERIAL_SPECS_CLOTHING number the structure import gives item models (block 100 x n from it).
    /// </summary>
    const int ItemMaterialBase = 950000;

    /// <summary>
    /// The game's item functions used: 0, none (a plain trade good).
    /// </summary>
    const int NoFunction = 0;

    /// <summary>
    /// The armour slot ATTACH_BODY (the worn <c>armour</c> section): the robe goes on over the body as other robes do, in place of the bather's
    /// body armour. The Monk Robe it copies sits in ATTACH_BELT (14) in the merged test-install data, under body armour.
    /// </summary>
    const int BodySlot = 5;

    /// <summary>
    /// The soap a wash draws, in hundredths: a bar washes four towels or robes (the washing machine crafts them from their <c>ingredients</c>).
    /// </summary>
    const int WashSoap = 25;

    /// <summary>
    /// A wash's <c>production time</c> in game hours (an item craft's hours; the field's default 4 is a cooked meal's).
    /// </summary>
    const int WashHours = 1;

    /// <summary>
    /// The mod's folder under the game's <c>mods</c> folder.
    /// </summary>
    const string ModFolder = "Bathhouse";

    /// <summary>
    /// What an hour's work adds to a made item's worth (Matthew's rule: its ingredients' worth plus 2% per hour worked). The game prices an item
    /// with <c>ingredients</c> as their worth times 1 + its <c>profitability</c>, so a crafted item's profitability is this times its
    /// <c>production time</c>.
    /// </summary>
    const float WorkMarkup = 0.02f;

    /// <summary>
    /// The robe's <c>part coverage</c> on each part, the chest's counting 1.5 times: 20 + 20 + 20 + 27 x 1.5 = 100, so the clothing bench (which
    /// draws an armour's total coverage / 100 Fabrics) makes it from one Fabrics. The robe has no protection, so its coverage changes nothing else.
    /// </summary>
    const int RobeCoverage = 20, RobeChestCoverage = 27;

    /// <summary>
    /// The robe's <c>relative price mult</c>: worth one Fabrics (56 cats) plus 2% per hour of its 3 h craft at quality 50 (the Monk Robe is 548
    /// there).
    /// </summary>
    const float RobePriceMult = 0.11f;

    /// <summary>
    /// The game's chest body part (<c>part coverage</c> counts it 1.5 times).
    /// </summary>
    const string Chest = "101-gamedata.quack";

    /// <summary>
    /// The items, in id order. Each value gives the trade worth (value x the category mult: 0.6, greenfruit tea 0.45) of its ingredients plus
    /// 2% per hour worked, from the base game's hemp (17 cats), Fabrics (56), greenfruit (27) and raw stone (36) and each machine's hours an item
    /// (docs/bathhouse.md "Values").
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.Towel, "Towel", "584-gamedata.base", "Fabrics", 47, 10,
            "A thick cotton towel. Bathers dry off with one; most come back dirty, some are ruined.", "towel", "towel", 6),
        new(Ids.DirtyTowel, "Dirty Towel", "584-gamedata.base", "Fabrics", 37, 10,
            "A used towel. A washing machine and a little soap make it clean again.", "dirtytowel", "dirty-towel", 1),
        new(Ids.Soap, "Soap", "43959-rebirth.mod", "Foodcube", 30, 20, "A rough bar of hemp soap, for bathers and for the laundry.", "soap",
            "soap", 3),
        new(Ids.GreenfruitTea, "Greenfruit Tea", "42309-changes_otto.mod", "Greenfruit", 61, 20,
            "Sliced and dried greenfruit. Brewed in teaware it makes a sweet, pale tea.", "greenfruittea", "greenfruit-tea", 2),
        new(Ids.Teaware, "Teaware", "42319-changes_otto.mod", "Cup", 61, 5,
            "A pot and cups of fired stone. Brew greenfruit tea in it with Water at a tea bench to make a tea set.", "teaware", "teaware", 5),
        new(Ids.TeaSet, "Tea Set", "42316-changes_otto.mod", "Ceramic Bowl", 115, 5,
            "A tray of hot greenfruit tea, ready to serve. Drunk, it leaves a used tea set behind.", "teaset", "teaset", 4),
        new(Ids.UsedTeaSet, "Used Tea Set", "42316-changes_otto.mod", "Ceramic Bowl", 53, 5,
            "A tray of empty, stained cups. A used tea rack rinses it back into teaware with hot water.", "usedteaset", "used-teaset", 7),
        new(Ids.DirtyRobe, "Dirty Robe", "584-gamedata.base", "Fabrics", 90, 5,
            "A robe a bather has worn. A washing machine and a little soap make it clean again.", "dirtyrobe", "dirty-robe", 0),
    ];

    /// <summary>
    /// The items, then the staff robe.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) => [.. Specs.Select(s => Item(game, s)), Robe(game)];

    /// <summary>
    /// One item: the model's fields and look, without its recipe, as a plain trade good. A tea set is brewed from teaware and greenfruit tea, and
    /// a towel washed from a dirty towel and soap.
    /// </summary>
    static FcsRecord Item(GameDatabase game, Spec spec)
    {
        var model = RecordKit.Model(game, FcsRecordType.Item, spec.ModelId, spec.ModelName);
        var record = RecordKit.Copy(model, spec.Id, spec.Name, "ingredients");

        record.Ints["value"] = spec.Value;
        record.Ints["stackable"] = spec.Stack;
        record.Ints["item function"] = NoFunction;
        record.Strings["description"] = spec.Text;
        SetIcon(record, spec.Icon);
        WearOwnModel(game, record, spec);

        if (spec.Id == Ids.TeaSet) Brewed(record);
        if (spec.Id == Ids.Towel) Washed(record, Ids.DirtyTowel);
        if (record.References.ContainsKey("ingredients")) Markup(record);
        return record;
    }

    /// <summary>
    /// Points the item at its own mesh and material when the deployed data has the material (tests and tools that read the game without the
    /// deployed mod keep the copied item's look). An item's own texture map would win over its material, so the copied one is cleared.
    /// </summary>
    static void WearOwnModel(GameDatabase game, FcsRecord record, Spec spec)
    {
        string material = $"{ItemMaterialBase + 100 * spec.Block}-{Ids.FileName}";
        if (game.Find(material) is null) return;

        string mesh = $"bathhouse_{spec.Model.Replace('-', '_')}.mesh";
        record.Filenames["mesh"] = mesh;
        record.Filenames["ground mesh"] = mesh;
        record.Filenames["texture map"] = "";
        record.Filenames["normal map"] = "";
        record.References["material"] = [new FcsReference(material, 0, 0, 0)];
    }

    /// <summary>
    /// A tea set's recipe: teaware, greenfruit tea and the game's Water.
    /// </summary>
    static void Brewed(FcsRecord record) =>
        RecordKit.Links(record, "ingredients", (Ids.Teaware, 100, 0), (Ids.GreenfruitTea, 100, 0), (FurnitureRecords.Water, 100, 0));

    /// <summary>
    /// Prices a crafted item at its ingredients' worth plus <see cref="WorkMarkup"/> per hour of its <c>production time</c>.
    /// </summary>
    static void Markup(FcsRecord record) => record.Floats["profitability"] = WorkMarkup * record.Ints.GetValueOrDefault("production time", 4);

    /// <summary>
    /// Makes a clean item the washing machine's craft: from one of its dirty record and a quarter of a soap, in <see cref="WashHours"/>.
    /// </summary>
    static void Washed(FcsRecord record, string dirty)
    {
        RecordKit.Links(record, "ingredients", (dirty, 100, 0), (Ids.Soap, WashSoap, 0));
        record.Ints["production time"] = WashHours;
    }

    /// <summary>
    /// The bathhouse robe: a copy of the game's Monk Robe (its look and cover) under its own name, worn in the body slot.
    /// </summary>
    static FcsRecord Robe(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.Armour, "2189-gamedata.base", "Monk Robe");
        var record = RecordKit.Copy(model, Ids.Robe, "Bathhouse Robe");
        record.Ints["slot"] = BodySlot;
        record.Floats["relative price mult"] = RobePriceMult;
        var parts = model.GetReferences("part coverage").Select(p => (p.TargetStringId, Coverage(p.TargetStringId), 0));
        RecordKit.Links(record, "part coverage", [.. parts]);
        Washed(record, Ids.DirtyRobe);

        record.Strings["description"] = "A plain cotton robe. Bathers change into one from the bathhouse lockers; staff who wear one give better"
            + " service.";
        SetIcon(record, "robe");
        return record;
    }

    /// <summary>
    /// The robe's coverage of one body part.
    /// </summary>
    static int Coverage(string part) => part == Chest ? RobeChestCoverage : RobeCoverage;

    /// <summary>
    /// Shows the mod's own icon (made by tools/make-icons.py) instead of a picture drawn from the model's mesh: the game takes an <c>icon</c> file by
    /// its bare name from its Icons group, which a mod's <c>items/icons</c> folder joins.
    /// </summary>
    static void SetIcon(FcsRecord record, string icon)
    {
        record.Filenames["icon"] = $@".\mods\{ModFolder}\items\icons\bathhouse-{icon}.png";
        record.Bools["auto icon image"] = false;
    }
}
