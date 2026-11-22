using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;

namespace Bathhouse.Bathing;

/// <summary>
/// What a soak does for the bather (docs/bathhouse.md "Soothed and debuffs"): the Soothed buff, and every Bad status effect of any mod cleared but
/// those the player keeps. Vanilla has no status effects (injuries and hunger are not effects), so this clears only mods' effects.
/// </summary>
public static class Soothing
{
    /// <summary>
    /// The Soothed buff: a little tougher and fitter for a few days. A fresh soak starts its time again.
    /// </summary>
    public static readonly StatusEffect Soothed = new("bathhouse.soothed", "Soothed")
    {
        Description = "Fresh from a hot bath: a little tougher and lighter on the feet.",
        Tone = StatusEffectTone.Good,
        Stacking = StatusEffectStacking.Refresh,
        Modifiers = [new StatusEffectModifier(StatsEnumerated.Toughness, 5), new StatusEffectModifier(StatsEnumerated.Athletics, 5)],
    };

    /// <summary>
    /// The registered effects whose tone is Bad, in registration order: what a soak clears and the debuff list offers.
    /// </summary>
    public static IEnumerable<StatusEffect> Debuffs => StatusEffects.All.Where(e => e.Tone == StatusEffectTone.Bad);

    /// <summary>
    /// Gives the bather the Soothed buff for <paramref name="days"/> game days.
    /// </summary>
    public static void Soothe(World world, CharacterId who, int days) => Soothed.Apply(world, who, days * 24.0);

    /// <summary>
    /// Removes the bather's Bad effects but those in <paramref name="kept"/>; returns how many it removed.
    /// </summary>
    public static int Clear(World world, CharacterId who, IReadOnlySet<string> kept)
    {
        if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot)?.Stats is not { } stats) return 0;

        var clear = Cleared(stats.EffectsOrNull?.All.Select(e => e.Id) ?? [], kept);
        foreach (var effect in clear) effect.Remove(world, who);
        return clear.Count;
    }

    /// <summary>
    /// The registered Bad effects among <paramref name="held"/> that are not kept, in the order held.
    /// </summary>
    public static List<StatusEffect> Cleared(IEnumerable<string> held, IReadOnlySet<string> kept) =>
        [.. held.Select(StatusEffects.Find).OfType<StatusEffect>().Where(e => e.Tone == StatusEffectTone.Bad && !kept.Contains(e.Id))];
}
