using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The items (docs/bathhouse.md "Items"). Each is a copy of a game item for its look (mesh, icon, size) until the mod's own models land, with
/// its own name, value and text.
/// </summary>
static class ItemRecords
{
    /// <summary>
    /// One item: its id and name, the game item it looks like (string id, then name), its value in cats, how many stack, and its text.
    /// </summary>
    sealed record Spec(string Id, string Name, string ModelId, string ModelName, int Value, int Stack, string Text, string Icon);

    /// <summary>
    /// The game's item functions used: 0, none (a plain trade good).
    /// </summary>
    const int NoFunction = 0;

    /// <summary>
    /// The mod's folder under the game's <c>mods</c> folder.
    /// </summary>
    const string ModFolder = "Bathhouse";

    /// <summary>
    /// The items, in id order.
    /// </summary>
    static readonly Spec[] Specs =
    [
        new(Ids.Towel, "Towel", "584-gamedata.base", "Fabrics", 40, 10,
            "A thick cotton towel. Bathers dry off with one; most come back dirty, some are ruined.", "towel"),
        new(Ids.DirtyTowel, "Dirty Towel", "584-gamedata.base", "Fabrics", 5, 10,
            "A used towel. A washing machine and a little soap make it clean again.", "dirtytowel"),
        new(Ids.Soap, "Soap", "43959-rebirth.mod", "Foodcube", 30, 20, "A rough bar of hemp soap, for bathers and for the laundry.", "soap"),
        new(Ids.GreenfruitTea, "Greenfruit Tea", "42309-changes_otto.mod", "Greenfruit", 25, 20,
            "Sliced and dried greenfruit. Brewed in teaware it makes a sweet, pale tea.", "greenfruittea"),
        new(Ids.Teaware, "Teaware", "42319-changes_otto.mod", "Cup", 60, 5,
            "A pot and cups of fired stone. Fill it with greenfruit tea at a tea bench to make a tea set.", "teaware"),
        new(Ids.TeaSet, "Tea Set", "42316-changes_otto.mod", "Ceramic Bowl", 120, 5,
            "A tray of hot greenfruit tea, ready to serve. Drunk, it leaves its teaware behind.", "teaset"),
    ];

    /// <summary>
    /// The items, then the staff robe.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) => [.. Specs.Select(s => Item(game, s)), Robe(game)];

    /// <summary>
    /// One item: the model's fields and look, without its recipe, as a plain trade good. A tea set is brewed from teaware and greenfruit tea.
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

        if (spec.Id == Ids.TeaSet) RecordKit.Links(record, "ingredients", (Ids.Teaware, 100, 0), (Ids.GreenfruitTea, 100, 0));
        return record;
    }

    /// <summary>
    /// The staff robe: a copy of the game's Monk Robe (its look, slot and cover) under its own name.
    /// </summary>
    static FcsRecord Robe(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.Armour, "2189-gamedata.base", "Monk Robe");
        var record = RecordKit.Copy(model, Ids.Robe, "Bathhouse Robe");

        record.Strings["description"] = "A plain cotton robe. Bathhouse staff who wear one give better service.";
        SetIcon(record, "robe");
        return record;
    }

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
