using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The mod's records (docs/bathhouse.md "Records"), made in the data phase from the merged game data: every record copies a game record for its
/// look and links the game's items by id, so the mod ships no game content of its own.
/// </summary>
public static class BathhouseData
{
    /// <summary>
    /// The data file's author and description.
    /// </summary>
    const string Author = "Meitou-Modding-Ecosystem",
        Description = "Bathhouse: towels, soap, tea, heated tubs and bathers. Generated; see docs/bathhouse.md.";

    /// <summary>
    /// Every record, in id order. Throws when the game lacks a record one copies.
    /// </summary>
    public static IReadOnlyList<FcsRecord> Records(GameDatabase game) =>
    [
        .. ItemRecords.Build(game), .. MachineRecords.Build(game), .. FurnitureRecords.Build(game), .. ResearchRecords.Build(),
        .. GoalRecords.Build(game), .. EffectRecords.Build(game)
    ];

    /// <summary>
    /// The records as a data file, for tools and tests that apply them as a .mod file would be.
    /// </summary>
    public static FcsFile File(GameDatabase game)
    {
        var file = new FcsFile { Author = Author, Description = Description };
        file.Records.AddRange(Records(game));
        for (int k = 0; k < file.Records.Count; k++) file.Records[k].Id = k + 1;

        file.NextId = file.Records.Count + 1;
        return file;
    }
}
