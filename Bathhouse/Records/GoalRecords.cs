using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// The bath goal in the data (docs/bathhouse.md "Bath goal"): its <c>AI_TASK</c>, and that task added to the <c>AI Goals</c> of every race and of the
/// player's faction, the lists the chooser weighs for any character. Its desire is the mod's: 0 for everyone but the bathers it sends.
/// </summary>
static class GoalRecords
{
    /// <summary>
    /// The player's faction, the only base faction with <c>AI Goals</c> (Nameless).
    /// </summary>
    const string PlayerFaction = "204-gamedata.base";

    /// <summary>
    /// The tier the chooser scores the goal in: urgent (3), so a bather sent to a tub goes before jobs and town routines; Self preservation is
    /// urgent too and outscores it in a fight.
    /// </summary>
    const int Urgent = 3;

    /// <summary>
    /// The goal's weight in a list, in hundredths.
    /// </summary>
    const int Weight = 100;

    /// <summary>
    /// The goal's record, then one change per race and the player's faction listing it.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) => [Goal(), .. Lists(game)];

    /// <summary>
    /// The bath goal: an urgent task of the mod's own type, aimed at the character itself.
    /// </summary>
    static FcsRecord Goal()
    {
        var record = RecordKit.New(Ids.BathGoal, FcsRecordType.AiTask, "Taking a bath");
        record.Ints["enum"] = Ids.BathGoalType;
        record.Ints["classification"] = Urgent;
        record.Ints["targeting"] = 1;
        record.Ints["ending"] = 0;
        return record;
    }

    /// <summary>
    /// A change of every race and of the player's faction (when the data has it) that adds the goal to its <c>AI Goals</c>.
    /// </summary>
    static IEnumerable<FcsRecord> Lists(GameDatabase game)
    {
        var owners = game.OfType(FcsRecordType.Race)
            .Concat(game.Find(PlayerFaction) is { } faction ? [faction] : [])
            .OrderBy(r => r.StringId, StringComparer.Ordinal);
        foreach (var owner in owners) yield return Listing(owner);
    }

    /// <summary>
    /// A change of <paramref name="owner"/> adding the goal to its list (a data file's change: the record's id with flag 1, as
    /// <c>ModDataContext.Edit</c> makes it).
    /// </summary>
    static FcsRecord Listing(GameRecord owner)
    {
        var change = new FcsRecord { StringId = owner.StringId, RecordType = owner.Type, Name = owner.Name, Flags = 1 };
        RecordKit.Links(change, "AI Goals", (Ids.BathGoal, 0, Weight));
        return change;
    }
}
