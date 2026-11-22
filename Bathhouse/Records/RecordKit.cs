using Meitou.Data;
using Meitou.Data.Fcs;

namespace Bathhouse.Records;

/// <summary>
/// Shared steps for building Bathhouse.mod from the installed game: finding a stand-in record, copying it, and linking records.
/// </summary>
static class RecordKit
{
    /// <summary>
    /// The build menu category every bathhouse building is listed under (docs/bathhouse.md "Build menu").
    /// </summary>
    public const string BuildCategory = "BATH HOUSE";

    /// <summary>
    /// A new record, as a mod file adds one.
    /// </summary>
    public static FcsRecord New(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = name, RecordType = type };

    /// <summary>
    /// The game's record of <paramref name="type"/> with string id <paramref name="id"/>, else the first by string id named
    /// <paramref name="name"/>. Throws when the game has neither, so a missing stand-in fails the generator instead of writing a broken file.
    /// </summary>
    public static GameRecord Model(GameDatabase game, FcsRecordType type, string id, string name) =>
        game.Find(id) is { } byId && byId.Type == type
            ? byId
            : game.OfType(type)
                .Where(r => r.Name == name)
                .OrderBy(r => r.StringId, StringComparer.Ordinal)
                .FirstOrDefault()
            ?? throw new InvalidOperationException($"The game data has no {type} '{name}' ({id}).");

    /// <summary>
    /// A copy of <paramref name="model"/> under a new id and name: every field, and every link but those named in <paramref name="skip"/>.
    /// </summary>
    public static FcsRecord Copy(GameRecord model, string id, string name, params string[] skip)
    {
        var record = New(id, model.Type, name);
        CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (skip.Contains(list)) continue;

            record.References[list] = [.. model.GetReferences(list).Select(Link)];
        }

        return record;
    }

    /// <summary>
    /// Sets <paramref name="list"/> of <paramref name="record"/> to a copy of <paramref name="from"/>'s links in that list.
    /// </summary>
    public static void CopyLinks(GameRecord from, FcsRecord record, string list) =>
        record.References[list] = [.. from.GetReferences(list).Select(Link)];

    /// <summary>
    /// Sets <paramref name="list"/> to links to <paramref name="targets"/>, each with the values (v0, v1).
    /// </summary>
    public static void Links(FcsRecord record, string list, params (string Target, int V0, int V1)[] targets) =>
        record.References[list] = [.. targets.Select(t => new FcsReference(t.Target, t.V0, t.V1, 0))];

    /// <summary>
    /// Copies every field of a merged record but its links.
    /// </summary>
    static void CopyFields(GameRecord from, FcsRecord to)
    {
        foreach (var (key, value) in from.Bools) to.Bools[key] = value;
        foreach (var (key, value) in from.Floats) to.Floats[key] = value;
        foreach (var (key, value) in from.Ints) to.Ints[key] = value;
        foreach (var (key, value) in from.Vector3S) to.Vector3S[key] = value;
        foreach (var (key, value) in from.Vector4S) to.Vector4S[key] = value;
        foreach (var (key, value) in from.Strings) to.Strings[key] = value;
        foreach (var (key, value) in from.Filenames) to.Filenames[key] = value;
    }

    /// <summary>
    /// A merged link as a file's.
    /// </summary>
    static FcsReference Link(GameReference link) => new(link.TargetStringId, link.Values.Value0, link.Values.Value1, link.Values.Value2);
}
