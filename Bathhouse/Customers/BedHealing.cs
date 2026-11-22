using Bathhouse.Records;
using Meitou.Simulation;
using Meitou.Simulation.Locks;

namespace Bathhouse.Customers;

/// <summary>
/// A massage bed heals fast only while someone works its station (docs/bathhouse.md "Private beds"). The bed itself heals as the game's Bed (its
/// stand-in's <c>output rate</c>); each check the mod adds <see cref="Boost"/> times what the game healed of each part of whoever lies in an attended
/// bed since the last check, taken from the part's treated damage as the game's healing is. Not saved: after a load the first check only notes
/// the body.
/// </summary>
public sealed class BedHealing
{
    /// <summary>
    /// The extra healing per unit the game heals in an attended bed: 3, so it heals 4 times as fast as the game's Bed.
    /// </summary>
    public const float Boost = 3;

    /// <summary>
    /// Each attended sleeper's part flesh at the last check.
    /// </summary>
    readonly Dictionary<CharacterId, float[]> _last = [];

    /// <summary>
    /// Boosts the healing of everyone lying in an attended massage bed and forgets the others.
    /// </summary>
    public void Step(World world)
    {
        var seen = new HashSet<CharacterId>();
        foreach (var site in world.Buildings.All)
        {
            if (site.Record != Ids.MassageBed || !site.Complete || site.Destroyed) continue;
            if (Massage.BedOf(world, site) is not { } bed || FixtureRegistry.IsFree(world.Characters, bed)) continue;
            if (Massage.Station(world, site) is not { } station || Massage.Attendant(world, station) is null) continue;

            Heal(world, bed.Occupant);
            seen.Add(bed.Occupant);
        }

        foreach (var who in _last.Keys.Where(k => !seen.Contains(k)).ToList()) _last.Remove(who);
    }

    /// <summary>
    /// Adds the boost to each part that healed since the last check, then notes the parts' flesh.
    /// </summary>
    void Heal(World world, CharacterId who)
    {
        if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot)?.Medical is not { Dead: false } medical) return;

        var parts = medical.Parts;
        if (_last.TryGetValue(who, out var last) && last.Length == parts.Count)
        {
            for (int i = 0; i < parts.Count; i++) Boosted(parts[i], parts[i].Flesh - last[i]);
        }

        _last[who] = parts.Select(p => p.Flesh).ToArray();
    }

    /// <summary>
    /// Heals a part <see cref="Boost"/> times <paramref name="healed"/> more, out of its treated damage and up to its HP.
    /// </summary>
    static void Boosted(Meitou.Simulation.Bodies.HealthPart part, float healed)
    {
        if (healed <= 0 || part.Bandage <= 0) return;

        float extra = MathF.Min(MathF.Min(healed * Boost, part.Bandage), MathF.Max(0, part.MaxHp - part.Flesh));
        part.Bandage -= extra;
        part.Flesh += extra;
    }
}
