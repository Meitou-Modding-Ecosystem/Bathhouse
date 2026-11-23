using System.Numerics;
using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// Steam (docs/bathhouse.md "Steam"): EFFECT records naming the mod's particle systems (<c>items/particles/bathhouse_steam.pu</c>; the game reads
/// every <c>.pu</c> in a mod's folders), placed as instances on the tubs and the boiler. The game runs a building's effects while it is
/// complete (engine docs/formats/lights.md "Effects on buildings"), so the steam shows on every finished tub, warm or cold.
/// </summary>
static class EffectRecords
{
    /// <summary>
    /// The game's furniture smoke effect (<c>small- smoke3_small</c>), copied for its fields: type NONE (an effect on an object), no wind, no
    /// damage.
    /// </summary>
    const string SmokeModel = "5462-nodes_furniture1_otto.mod";

    /// <summary>
    /// Each effect: its id, name, particle system, the piece it sits on and where on it (building units from the piece's origin, which the structure
    /// import puts at the model's lowest point, centred): just under each tub's rim, the boiler's top.
    /// </summary>
    internal static readonly (string Id, string Name, string System, string Piece, Vector3 At)[] Steams =
    [
        (Ids.SmallTubSteam, "Bathhouse steam (small tub)", "bathhouse_steam_small", Ids.SmallTub, new(0, 6, 0)),
        (Ids.MediumTubSteam, "Bathhouse steam (medium tub)", "bathhouse_steam_medium", Ids.MediumTub, new(0, 9.5f, 0)),
        (Ids.LargeTubSteam, "Bathhouse steam (large bath)", "bathhouse_steam_large", Ids.LargeTub, new(0, 5, 0)),
        (Ids.OilBathSteam, "Bathhouse steam (oil bath)", "bathhouse_steam_oil", Ids.OilBath, new(0, 10, 0)),
        (Ids.BloodBathSteam, "Bathhouse steam (blood bath)", "bathhouse_steam_blood", Ids.BloodBath, new(0, 15, 0)),
        (Ids.BoilerSteam, "Bathhouse steam (boiler)", "bathhouse_steam_boiler", Ids.Boiler, new(0, 26, 0)),
    ];

    /// <summary>
    /// The steam effects.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game)
    {
        var model = RecordKit.Model(game, FcsRecordType.Effect, SmokeModel, "small- smoke3_small");
        foreach (var steam in Steams)
        {
            var record = RecordKit.Copy(model, steam.Id, steam.Name);
            record.Strings["particle system"] = steam.System;
            yield return record;
        }
    }

    /// <summary>
    /// Places the piece's steam on its building record; a piece without steam is left alone.
    /// </summary>
    public static void Attach(FcsRecord building, string piece)
    {
        foreach (var steam in Steams.Where(s => s.Piece == piece))
            building.Instances.Add(new FcsInstance { Id = "steam", Target = steam.Id, Position = steam.At, Rotation = Quaternion.Identity });
    }
}
