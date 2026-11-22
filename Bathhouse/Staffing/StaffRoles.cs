using Bathhouse.Records;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Production;

namespace Bathhouse.Staffing;

/// <summary>
/// A bathhouse job (docs/bathhouse.md "Staff").
/// </summary>
public enum StaffRole
{
    /// <summary>
    /// Stands behind the check-in counter, so it is staffed.
    /// </summary>
    Clerk,

    /// <summary>
    /// Scrubs the dirty tubs of the building.
    /// </summary>
    Scrubber,

    /// <summary>
    /// Carries dirty towels and soap to the washing machine and clean towels to the racks and the counter.
    /// </summary>
    Laundry,

    /// <summary>
    /// Stocks the tea tables with Tea Sets and clears their used Teaware.
    /// </summary>
    Tea,
}

/// <summary>
/// The jobs' task types, names and pieces, and giving one to the player's selected characters (a piece's panel button). A job sits in the
/// character's job list (<c>CharacterCold.Jobs</c>) on the piece it was given at; the jobs panel shows and removes it and the game saves it.
/// </summary>
public static class StaffRoles
{
    /// <summary>
    /// Every role.
    /// </summary>
    public static readonly StaffRole[] All = Enum.GetValues<StaffRole>();

    /// <summary>
    /// A role's task type.
    /// </summary>
    public static TaskType TypeOf(StaffRole role) => (TaskType)(role switch
    {
        StaffRole.Clerk => Ids.ClerkJob,
        StaffRole.Scrubber => Ids.ScrubberJob,
        StaffRole.Laundry => Ids.LaundryJob,
        _ => Ids.TeaJob,
    });

    /// <summary>
    /// The role of a task type; null for another type.
    /// </summary>
    public static StaffRole? RoleOf(TaskType type) => All.Cast<StaffRole?>().FirstOrDefault(r => TypeOf(r!.Value) == type);

    /// <summary>
    /// A role's name in the jobs panel and on buttons.
    /// </summary>
    public static string Name(StaffRole role) => role switch
    {
        StaffRole.Clerk => "Bathhouse clerk",
        StaffRole.Scrubber => "Tub scrubber",
        StaffRole.Laundry => "Laundry runner",
        _ => "Tea server",
    };

    /// <summary>
    /// The role a piece's panel offers; null for a piece that offers none.
    /// </summary>
    public static StaffRole? OfferedBy(string? record) => record switch
    {
        Ids.Counter => StaffRole.Clerk,
        Ids.SmallTub or Ids.MediumTub or Ids.LargeTub or Ids.OilBath or Ids.BloodBath => StaffRole.Scrubber,
        Ids.WashingMachine => StaffRole.Laundry,
        Ids.TeaTable => StaffRole.Tea,
        _ => null,
    };

    /// <summary>
    /// Gives each of the player's selected characters the role's job at a piece (one job of a role per character: one held elsewhere moves here).
    /// </summary>
    public static void Assign(World world, string site, StaffRole role)
    {
        var type = TypeOf(role);
        foreach (var who in world.Player.Selection)
        {
            if (!world.Characters.TryResolveNext(who, out int slot) || world.Characters.Cold(slot) is not { IsPlayer: true } cold) continue;

            cold.Jobs.RemoveAll(j => j.Type == type);
            cold.Jobs.Add(new PermaJob(type, site));
            cold.Orders |= Meitou.Simulation.Player.CharacterOrders.Jobs;
            DecisionSystem.Request(cold);
        }
    }
}
