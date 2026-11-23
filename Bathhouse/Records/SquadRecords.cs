using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The bather groups' squad templates (docs/bathhouse.md "Customers"): for each customer kind's game template, one copy per group size from 1 to
/// <see cref="MostMembers"/>, with its leader and the size less one of its first squad member, no animals, and <c>dont multiply</c>, so the
/// world's Squad size setting leaves the size alone.
/// </summary>
public static class SquadRecords
{
    /// <summary>
    /// The largest bather group a template exists for.
    /// </summary>
    public const int MostMembers = 12;

    /// <summary>
    /// The game templates the spawnable customer kinds come from (drifters, traders, bounty hunters, Shek duelists), in id order.
    /// </summary>
    static readonly string[] Sources = ["3950-gamedata.base", "1161-gamedata.base", "5337-gamedata.base", "51336-rebirth.mod"];

    /// <summary>
    /// The member lists a bather template drops: everyone but the leader and the sized squad.
    /// </summary>
    static readonly string[] Dropped = ["squad", "squad2", "animals", "animals2", "slaves", "prisoners", "choosefrom list"];

    /// <summary>
    /// The template of a group of <paramref name="size"/> (capped at <see cref="MostMembers"/>) made from the game template
    /// <paramref name="source"/>; null for a template without bather copies.
    /// </summary>
    public static string? Id(string source, int size)
    {
        int kind = Array.IndexOf(Sources, source);
        if (kind < 0) return null;

        return $"{1000 + 100 * kind + Math.Clamp(size, 1, MostMembers)}-{Ids.FileName}";
    }

    /// <summary>
    /// Every bather template.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) =>
        Sources.SelectMany(source => Enumerable.Range(1, MostMembers).Select(size => Squad(game, source, size)));

    /// <summary>
    /// One bather template: the game template's leader, AI packages, faction and dialogue, plus <paramref name="size"/> - 1 of its first squad
    /// member (its leader again when it has none).
    /// </summary>
    static FcsRecord Squad(GameDatabase game, string source, int size)
    {
        var model = game.Find(source) is { Type: FcsRecordType.SquadTemplate } found
            ? found
            : throw new InvalidOperationException($"The game data has no squad template {source}.");
        var record = RecordKit.Copy(model, Id(source, size)!, $"Bathhouse bathers: {model.Name} ({size})", Dropped);

        record.Bools["dont multiply"] = true;
        record.Ints["num random chars"] = 0;
        if (size > 1) RecordKit.Links(record, "squad", (Member(model), size - 1, 0));
        return record;
    }

    /// <summary>
    /// The member a bather template repeats: the game template's first squad member, else its leader.
    /// </summary>
    static string Member(GameRecord model) =>
        model.GetReferences("squad") is [var first, ..] ? first.TargetStringId : model.GetReferences("leader")[0].TargetStringId;
}
