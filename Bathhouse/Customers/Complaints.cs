using System.Numerics;
using Meitou.Data.Gameplay;
using Meitou.Simulation;
using Meitou.Simulation.Dialogue;
using Meitou.Simulation.Factions;

namespace Bathhouse.Customers;

/// <summary>
/// Complaints (docs/bathhouse.md "Complaints"): the customer's faction loses relation with the player's, capped per faction per game day so
/// complaints never turn a faction hostile, and now and then the customer picks a fight, which turns only its own squad against the player's people
/// near it (a squad TEMP_ENEMY tag), never the faction. The day's losses are saved and hashed.
/// </summary>
public sealed class Complaints(FactionRelations relations)
{
    /// <summary>
    /// How near a player's character must stand to be in a fight a customer picks.
    /// </summary>
    public const float FightReach = 300;

    /// <summary>
    /// The roll's counter salt.
    /// </summary>
    const ulong Salt = 0xF1_6475;

    /// <summary>
    /// Relation lost today by faction string id.
    /// </summary>
    readonly SortedDictionary<string, int> _lost = new(StringComparer.Ordinal);

    /// <summary>
    /// The game day <see cref="_lost"/> counts.
    /// </summary>
    public int Day { get; set; } = -1;

    /// <summary>
    /// Relation lost today by faction, for saving and hashing.
    /// </summary>
    public SortedDictionary<string, int> Lost => _lost;

    /// <summary>
    /// A customer of <paramref name="kind"/> complains: its faction loses relation (within today's cap), and it may pick a fight. True for a fight.
    /// </summary>
    public bool Complain(World world, int slot, CustomerType kind, RngPurpose purpose, BathhouseSettings settings)
    {
        var cold = world.Characters.Cold(slot);
        if (cold is null) return false;

        LoseRelation(world, slot, cold.Faction, settings);
        if (!Fights(world, slot, kind, purpose, settings.FightChance)) return false;

        Fight(world, slot, cold.SquadId);
        return true;
    }

    /// <summary>
    /// The relation a complaint costs: <see cref="BathhouseSettings.RelationPerComplaint"/>, no more than what is left of today's cap.
    /// </summary>
    public static int Loss(int lostToday, int perComplaint, int capPerDay) =>
        Math.Clamp(Math.Min(perComplaint, capPerDay - lostToday), 0, perComplaint);

    /// <summary>
    /// Takes the complaint's relation off the customer's faction towards the player's.
    /// </summary>
    void LoseRelation(World world, int slot, int faction, BathhouseSettings settings)
    {
        int day = (int)Math.Floor(world.GameHours / 24);
        if (day != Day)
        {
            _lost.Clear();
            Day = day;
        }

        if (!relations.Contains(faction) || world.Player.Faction < 0 || relations.Factions[faction].Id is not { } id) return;

        int lost = _lost.GetValueOrDefault(id);
        int loss = Loss(lost, settings.RelationPerComplaint, settings.RelationCapPerDay);
        if (loss == 0) return;

        _lost[id] = lost + loss;
        var cause = new RelationCause(RelationCauseType.Dialogue, world.Characters.IdOf(slot));
        PlayerRelations.Add(world, relations, faction, world.Player.Faction, -loss, cause, "a bathhouse complaint");
    }

    /// <summary>
    /// Whether the customer picks a fight: <see cref="BathhouseSettings.FightChance"/> percent, twice that for a rowdy kind.
    /// </summary>
    static bool Fights(World world, int slot, CustomerType kind, RngPurpose purpose, int chance)
    {
        int percent = kind.Rowdy ? chance * 2 : chance;
        var who = world.Characters.IdOf(slot);
        return Rng.Float(Rng.Hash(world.Seed, Rng.Key(who), purpose, Salt ^ (ulong)world.Tick)) * 100 < percent;
    }

    /// <summary>
    /// Turns the customer's squad against each of the player's people near it.
    /// </summary>
    static void Fight(World world, int slot, int squad)
    {
        if (squad < 0 || world.Systems.OfType<DialogueSystem>().FirstOrDefault() is not { } dialogue) return;

        var table = world.Characters;
        var at = table.Next[slot].Position;
        for (int other = 0; other < table.HighWater; other++)
        {
            if (!table.Next[other].Alive || table.Cold(other) is not { IsPlayer: true }) continue;
            if (Vector3.Distance(table.Next[other].Position, at) > FightReach) continue;

            dialogue.Memory.Flag(world, squad, table.IdOf(other), MemoryTag.TempEnemy);
        }
    }
}
