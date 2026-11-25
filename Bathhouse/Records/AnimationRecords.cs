using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Modding;

namespace Bathhouse.Records;

/// <summary>
/// The mod's animations (docs/bathhouse.md "Animations"): an ANIMATION_FILE naming the clips' files in items/animations (one per body skeleton),
/// linked to every race built on the game's human skeletons, and a disabled COMBAT_TECHNIQUE per clip, which the mod plays as a combat move
/// (disabled, so no fight ever picks it).
/// </summary>
public static class AnimationRecords
{
    /// <summary>
    /// The clip a customer sits at a tea table in (cross-legged on the floor; a 2.5 s loop).
    /// </summary>
    public const string TeaSitClip = "bathhouse_tea_sit";

    /// <summary>
    /// The clip files for male and female bodies (male_skeleton and female_skeleton bones); the game links them by bare name.
    /// </summary>
    const string MaleFile = @".\mods\Bathhouse\items\animations\bathhouse_male.skeleton",
        FemaleFile = @".\mods\Bathhouse\items\animations\bathhouse_female.skeleton";

    /// <summary>
    /// The folder of the game's human body meshes (humans, shek, hivers, skeletons, fishmen): every mesh in it uses male_skeleton or
    /// female_skeleton, except the Hive Queen's.
    /// </summary>
    const string HumanoidMeshes = "data/character/meshes/";

    /// <summary>
    /// The one mesh in <see cref="HumanoidMeshes"/> with a skeleton of its own.
    /// </summary>
    const string QueenMesh = "hive_queen.mesh";

    /// <summary>
    /// The tea sit's technique and the animation file.
    /// </summary>
    public static IEnumerable<FcsRecord> Build()
    {
        var sit = RecordKit.New(Ids.TeaSitMove, FcsRecordType.CombatTechnique, "Bathhouse tea sit");
        sit.Strings["anim name"] = TeaSitClip;
        sit.Bools["disabled"] = true;
        yield return sit;

        var file = RecordKit.New(Ids.Animations, FcsRecordType.AnimationFile, "Bathhouse animations");
        file.Filenames["male animation"] = MaleFile;
        file.Filenames["female animation"] = FemaleFile;
        yield return file;
    }

    /// <summary>
    /// Adds the animation file to the <c>animation files</c> of every humanoid race (<see cref="Humanoid"/>).
    /// </summary>
    public static void LinkRaces(ModDataContext context)
    {
        foreach (var race in context.Data.OfType(FcsRecordType.Race).Where(Humanoid))
            context.Edit(race.StringId).References["animation files"] = [new FcsReference(Ids.Animations, 0, 0, 0)];
    }

    /// <summary>
    /// Whether a race's body is one of the game's human meshes, so the clips' bones fit it.
    /// </summary>
    static bool Humanoid(GameRecord race)
    {
        string mesh = race.GetPath("male mesh").Replace('\\', '/').TrimStart('.', '/').ToLowerInvariant();
        return mesh.StartsWith(HumanoidMeshes, StringComparison.Ordinal) && !mesh.EndsWith(QueenMesh, StringComparison.Ordinal);
    }
}
