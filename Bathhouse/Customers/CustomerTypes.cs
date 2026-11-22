namespace Bathhouse.Customers;

/// <summary>
/// A kind of customer (docs/bathhouse.md "Customers"): the squad template and faction a group is spawned from (empty for a kind no faction sends
/// yet), its price tolerance (times the suggested price it pays without a second thought; its demand on service rises with it), whether it is rowdy
/// (twice the fight chance) and the reputation the bathhouse needs before it comes.
/// </summary>
public sealed record CustomerType(string Key, string Name, string Template, string Faction, float Tolerance, bool Rowdy, int Reputation)
{
    /// <summary>
    /// Whether the mod can send a group of this kind now.
    /// </summary>
    public bool Spawnable => Template.Length > 0;

    /// <summary>
    /// The least visit quality this kind leaves without complaining: a quarter of its tolerance (drifters 0.2, nobles 0.5).
    /// </summary>
    public float Demand => Tolerance / 4;
}

/// <summary>
/// The customer kinds. Only factions not hostile to the player are sent until the guest truce lands; skeletons, crab raiders, nobles and cannibals
/// have lines and tolerances but no source until their factions are added.
/// </summary>
public static class CustomerTypes
{
    /// <summary>
    /// The bathhouse's reputation until reputation lands (milestone 5): drifters and traders come, Shek and bounty hunters wait for 2.
    /// </summary>
    public const int Reputation = 1;

    /// <summary>
    /// Every kind, the spawnable ones first.
    /// </summary>
    public static readonly IReadOnlyList<CustomerType> All =
    [
        new("drifters", "Drifters", "3950-gamedata.base", "51707-rebirth.mod", 0.8f, false, 1),
        new("traders", "Traders", "1161-gamedata.base", "1088-gamedata.base", 1.0f, false, 1),
        new("hunters", "Bounty hunters", "5337-gamedata.base", "14508-gamedata.base", 1.0f, false, 2),
        new("shek", "Shek", "51336-rebirth.mod", "11624-Dialogue (10).mod", 1.0f, true, 2),
        new("skeletons", "Skeletons", "", "", 1.2f, false, 3),
        new("crabs", "Crab raiders", "", "", 1.0f, true, 3),
        new("nobles", "Nobles", "", "", 2.0f, false, 3),
        new("cannibals", "Cannibals", "", "", 1.5f, true, 3),
    ];

    /// <summary>
    /// The kind with a key; null for none.
    /// </summary>
    public static CustomerType? Find(string key) => All.FirstOrDefault(t => t.Key == key);
}
