using System.Numerics;
using Meitou.Simulation.Construction;

namespace Bathhouse.Records;

/// <summary>
/// Where people stand to work or use a piece (docs/bathhouse.md "Work spots"): in front of it (its local +Z, the side its model faces,
/// docs/structures.md), <see cref="Clearance"/> past its front face. The machines' operators stand there (a use node on the machine), and so do
/// staff taking or putting goods and bathers stepping into a tub.
/// </summary>
public static class WorkSpots
{
    /// <summary>
    /// Ground units (0.1 m) between a piece's front face and where a person stands: room for a body, so nobody stands inside the model.
    /// </summary>
    public const float Clearance = 6;

    /// <summary>
    /// How near its use node a machine's operator works (the functionality's <c>use range</c>; the game's Textiles has 40): close, so the operator
    /// walks up to the spot instead of stopping anywhere within reach of it.
    /// </summary>
    public const float OperatorReach = 10;

    /// <summary>
    /// Ground units each front use node sits beside the work spot, across the piece's front (local X): the game takes a machine's position marker
    /// from its use nodes only when it has two or more, and with fewer works it from its origin, inside the model (docs/bathhouse.md "Work spots").
    /// </summary>
    const float NodeSpread = 3;

    /// <summary>
    /// A front spot for a piece without a measured face: 15 units out.
    /// </summary>
    const float Unmeasured = 15;

    /// <summary>
    /// Each piece's front face, in ground units from its origin along local +Z: its own model's extent at its finished height (the structure
    /// import scales the model to the .json <c>height</c> and centres it).
    /// </summary>
    static readonly Dictionary<string, float> Faces = new()
    {
        [Ids.SmallTub] = 6.3f,
        [Ids.MediumTub] = 6.7f,
        [Ids.LargeTub] = 13.0f,
        [Ids.OilBath] = 9.2f,
        [Ids.BloodBath] = 6.2f,
        [Ids.MassageStation] = 3.3f,
        [Ids.Counter] = 5.0f,
        [Ids.Lockers] = 3.7f,
        [Ids.Hamper] = 3.9f,
        [Ids.RobeHamper] = 5.3f,
        [Ids.TowelRack] = 2.6f,
        [Ids.SoapShelf] = 2.6f,
        [Ids.TeaTable] = 8.7f,
        [Ids.TeaShelf] = 3.0f,
        [Ids.TeawareShelf] = 2.4f,
        [Ids.TeaSetCabinet] = 3.1f,
        [Ids.UsedTeaRack] = 2.6f,
        [Ids.TowelLoom] = 11.2f,
        [Ids.SoapPress] = 5.3f,
        [Ids.TeaDryer] = 5.5f,
        [Ids.Kiln] = 10.6f,
        [Ids.WashingMachine] = 4.5f,
        [Ids.TeaBench] = 4.4f,
    };

    /// <summary>
    /// A front use node's turn about Y, in radians: -90 degrees, as the game's own front nodes are (the Shop Counter's and the Cooking Stove's at
    /// local +Z), so the node's marker (its local -X) points back at the piece. The build ghost draws it as the green arrow.
    /// </summary>
    public const float NodeYaw = -MathF.PI / 2;

    /// <summary>
    /// Whether a piece has a measured front, so a work spot and a use node of its own.
    /// </summary>
    public static bool Measured(string record) => Faces.ContainsKey(record);

    /// <summary>
    /// How far in front of its origin a piece's spot is.
    /// </summary>
    public static float Out(string record) => Faces.TryGetValue(record, out float face) ? face + Clearance : Unmeasured;

    /// <summary>
    /// The piece's spot in its own frame.
    /// </summary>
    public static Vector3 Local(string record) => new(0, 0, Out(record));

    /// <summary>
    /// The piece's two front use nodes in its own frame: either side of its spot.
    /// </summary>
    public static Vector3[] Nodes(string record) => [Local(record) - new Vector3(NodeSpread, 0, 0), Local(record) + new Vector3(NodeSpread, 0, 0)];

    /// <summary>
    /// The piece's spot in the world.
    /// </summary>
    public static Vector3 Front(BuildSite site) => site.Position + Vector3.Transform(Local(site.Record), site.Rotation);
}
