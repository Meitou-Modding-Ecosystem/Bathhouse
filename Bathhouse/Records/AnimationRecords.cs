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
    /// The clip a bather kneels into the water with (1.3 s, played once), and the washing clip it loops after it (6.37 s, its last key the first
    /// pose, so it repeats without a seam).
    /// </summary>
    public const string BathEnterClip = "bathhouse_bath_enter", BathWashClip = "bathhouse_bath_wash";

    /// <summary>
    /// The lengths in seconds of <see cref="BathEnterClip"/> and <see cref="BathWashClip"/>.
    /// </summary>
    public const float BathEnterSeconds = 1.3f, BathWashSeconds = 6.3667f;

    /// <summary>
    /// Which bath clip plays <paramref name="seconds"/> after the bather sat down, and how far through it (0 to 1): the kneel once, then the wash
    /// looping until the soak ends.
    /// </summary>
    public static (bool Enter, float Progress) BathFrame(float seconds)
    {
        if (seconds < BathEnterSeconds) return (true, Math.Max(0, seconds) / BathEnterSeconds);

        return (false, (seconds - BathEnterSeconds) % BathWashSeconds / BathWashSeconds);
    }

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
    /// The clips' techniques and the animation file.
    /// </summary>
    public static IEnumerable<FcsRecord> Build()
    {
        yield return Technique(Ids.TeaSitMove, "Bathhouse tea sit", TeaSitClip);
        yield return Technique(Ids.BathEnterMove, "Bathhouse bath enter", BathEnterClip);
        yield return Technique(Ids.BathWashMove, "Bathhouse bath wash", BathWashClip);

        var file = RecordKit.New(Ids.Animations, FcsRecordType.AnimationFile, "Bathhouse animations");
        file.Filenames["male animation"] = MaleFile;
        file.Filenames["female animation"] = FemaleFile;
        yield return file;
    }

    /// <summary>
    /// A disabled technique that plays one clip.
    /// </summary>
    static FcsRecord Technique(string id, string name, string clip)
    {
        var technique = RecordKit.New(id, FcsRecordType.CombatTechnique, name);
        technique.Strings["anim name"] = clip;
        technique.Bools["disabled"] = true;
        return technique;
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
