using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// The customers' part of the sidecar (layout version 2) and of the state hash: open, prices, schedule, the complaints' day, groups and visits.
/// </summary>
public static class ClienteleSave
{
    /// <summary>
    /// Writes the customers' state.
    /// </summary>
    public static void Write(BinaryWriter w, Clientele clientele)
    {
        w.Write(clientele.Open);
        foreach (var service in Enum.GetValues<Service>()) w.Write(clientele.Prices.Percent(service));
        w.Write(clientele.NextGroupAt);
        w.Write(clientele.Serial);
        WriteComplaints(w, clientele.Complaints);
        WriteGroups(w, clientele.Groups);
        WriteVisits(w, clientele.Visits);
    }

    /// <summary>
    /// Reads the customers' state written by <see cref="Write"/>.
    /// </summary>
    public static void Read(BinaryReader r, Clientele clientele)
    {
        clientele.Clear();
        clientele.Open = r.ReadBoolean();
        foreach (var service in Enum.GetValues<Service>()) clientele.Prices.Set(service, r.ReadInt32());
        clientele.NextGroupAt = r.ReadDouble();
        clientele.Serial = r.ReadInt32();
        ReadComplaints(r, clientele.Complaints);
        ReadGroups(r, clientele);
        ReadVisits(r, clientele);
    }

    /// <summary>
    /// Adds the customers' state to the world's hash.
    /// </summary>
    public static void Hash(ref StateHasher hasher, Clientele clientele)
    {
        hasher.Add(clientele.Open);
        foreach (var service in Enum.GetValues<Service>()) hasher.Add(clientele.Prices.Percent(service));
        hasher.Add(clientele.NextGroupAt);
        hasher.Add(clientele.Serial);
        hasher.Add(clientele.Complaints.Day);
        foreach (var (faction, lost) in clientele.Complaints.Lost)
        {
            hasher.Add(Rng.StableHash(faction));
            hasher.Add(lost);
        }

        foreach (var group in clientele.Groups) hasher.Add(Rng.StableHash(group.Tag));
        foreach (var visit in clientele.Visits)
        {
            hasher.Add(visit.Customer.Value);
            hasher.Add((int)visit.Phase);
            hasher.Add(visit.Price);
        }
    }

    /// <summary>
    /// Writes the complaints' day and tally.
    /// </summary>
    static void WriteComplaints(BinaryWriter w, Complaints complaints)
    {
        w.Write(complaints.Day);
        w.Write(complaints.Lost.Count);
        foreach (var (faction, lost) in complaints.Lost)
        {
            w.Write(faction);
            w.Write(lost);
        }
    }

    /// <summary>
    /// Reads the complaints' day and tally.
    /// </summary>
    static void ReadComplaints(BinaryReader r, Complaints complaints)
    {
        complaints.Day = r.ReadInt32();
        complaints.Lost.Clear();
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++) complaints.Lost[r.ReadString()] = r.ReadInt32();
    }

    /// <summary>
    /// Writes the groups.
    /// </summary>
    static void WriteGroups(BinaryWriter w, IReadOnlyCollection<CustomerGroup> groups)
    {
        w.Write(groups.Count);
        foreach (var group in groups)
        {
            w.Write(group.Tag);
            w.Write(group.Kind);
            w.Write(group.Counter);
            w.Write(group.SpawnedAt);
            w.Write(group.Enlisted);
        }
    }

    /// <summary>
    /// Reads the groups.
    /// </summary>
    static void ReadGroups(BinaryReader r, Clientele clientele)
    {
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            var group = new CustomerGroup { Tag = r.ReadString(), Kind = r.ReadString(), Counter = r.ReadString(), SpawnedAt = r.ReadDouble() };
            group.Enlisted = r.ReadBoolean();
            clientele.Restore(group);
        }
    }

    /// <summary>
    /// Writes the visits.
    /// </summary>
    static void WriteVisits(BinaryWriter w, IReadOnlyCollection<Visit> visits)
    {
        w.Write(visits.Count);
        foreach (var visit in visits)
        {
            w.Write(visit.Customer.Value);
            w.Write(visit.Group);
            w.Write(visit.Kind);
            w.Write((int)visit.Phase);
            w.Write(visit.ArrivedAt);
            w.Write(visit.Waited);
            w.Write(visit.Price);
        }
    }

    /// <summary>
    /// Reads the visits.
    /// </summary>
    static void ReadVisits(BinaryReader r, Clientele clientele)
    {
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            var visit = new Visit { Customer = new LastingId(r.ReadUInt64()), Group = r.ReadString(), Kind = r.ReadString() };
            visit.Phase = (VisitPhase)r.ReadInt32();
            visit.ArrivedAt = r.ReadDouble();
            visit.Waited = r.ReadDouble();
            visit.Price = r.ReadInt64();
            clientele.Restore(visit);
        }
    }
}
