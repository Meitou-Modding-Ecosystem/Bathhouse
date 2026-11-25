using Bathhouse.Staffing;
using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// The customers' part of the sidecar (layout versions 2 to 7) and of the state hash: open, prices, schedule, the complaints' day, groups, visits
/// and, from version 3, the tea and massage prices, the visits' massage and the dirty tubs; from version 4, the visits' towel and soap; from
/// version 6, the guest book (not hashed); from version 7, the visits' tea.
/// </summary>
public static class ClienteleSave
{
    /// <summary>
    /// Writes the customers' state.
    /// </summary>
    public static void Write(BinaryWriter w, Clientele clientele, TubCare care)
    {
        w.Write(clientele.Open);
        foreach (var service in Enum.GetValues<Service>()) w.Write(clientele.Prices.Percent(service));
        w.Write(clientele.NextGroupAt);
        w.Write(clientele.Serial);
        WriteComplaints(w, clientele.Complaints);
        WriteGroups(w, clientele.Groups);
        WriteVisits(w, clientele.Visits);
        w.Write(care.Dirty.Count);
        foreach (string tub in care.Dirty) w.Write(tub);
        clientele.Book.Write(w);
        w.Write(clientele.Closed.Count);
        foreach (string piece in clientele.Closed) w.Write(piece);
    }

    /// <summary>
    /// Reads the customers' state written by <see cref="Write"/> in layout <paramref name="version"/> (2: bath, towel and soap prices only).
    /// </summary>
    public static void Read(BinaryReader r, Clientele clientele, TubCare care, int version)
    {
        clientele.Clear();
        clientele.Open = r.ReadBoolean();
        foreach (var service in Services(version)) clientele.Prices.Set(service, r.ReadInt32());
        clientele.NextGroupAt = r.ReadDouble();
        clientele.Serial = r.ReadInt32();
        ReadComplaints(r, clientele.Complaints);
        ReadGroups(r, clientele);
        ReadVisits(r, clientele, version);
        var dirty = new List<string>();
        int count = version >= 3 ? r.ReadInt32() : 0;
        for (int k = 0; k < count; k++) dirty.Add(r.ReadString());
        care.Restore(dirty);
        if (version >= 6) clientele.Book.Read(r);
        else clientele.Book.Clear();
        int closed = version >= 8 ? r.ReadInt32() : 0;
        for (int k = 0; k < closed; k++) clientele.Closed.Add(r.ReadString());
    }

    /// <summary>
    /// The services whose prices a layout holds.
    /// </summary>
    static IEnumerable<Service> Services(int version) => version >= 3 ? Enum.GetValues<Service>() : [Service.Bath, Service.Towel, Service.Soap];

    /// <summary>
    /// Adds the customers' state to the world's hash.
    /// </summary>
    public static void Hash(ref StateHasher hasher, Clientele clientele, TubCare care)
    {
        hasher.Add(care.Dirty.Count);
        foreach (string tub in care.Dirty) hasher.Add(Rng.StableHash(tub));

        hasher.Add(clientele.Open);
        hasher.Add(clientele.Closed.Count);
        foreach (string piece in clientele.Closed) hasher.Add(Rng.StableHash(piece));
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
            hasher.Add(visit.Owed);
            hasher.Add(visit.Massaged);
            hasher.Add(visit.BedWait);
            hasher.Add(visit.Towel);
            hasher.Add(visit.Soap);
            hasher.Add(visit.Drunk);
            hasher.Add(visit.TeaOffered);
            hasher.Add(visit.ServeWait);
            hasher.Add(visit.LateTea);
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
            w.Write(visit.DirtyTub);
            w.Write(visit.Owed);
            w.Write(visit.Bed ?? "");
            w.Write(visit.Massaged);
            w.Write(visit.BedWait);
            w.Write(visit.MassagePay);
            w.Write(visit.Towel);
            w.Write(visit.Soap);
            w.Write(visit.Table ?? "");
            w.Write(visit.Sitting);
            w.Write(visit.Drunk);
            w.Write(visit.TeaWait);
            w.Write(visit.TeaOffered);
            w.Write(visit.ServeWait);
            w.Write(visit.LateTea);
        }
    }

    /// <summary>
    /// Reads the visits.
    /// </summary>
    static void ReadVisits(BinaryReader r, Clientele clientele, int version)
    {
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            var visit = new Visit { Customer = new LastingId(r.ReadUInt64()), Group = r.ReadString(), Kind = r.ReadString() };
            visit.Phase = (VisitPhase)r.ReadInt32();
            visit.ArrivedAt = r.ReadDouble();
            visit.Waited = r.ReadDouble();
            visit.Price = r.ReadInt64();
            if (version >= 3) ReadMassage(r, visit);
            if (version >= 4) ReadSupplies(r, visit);
            if (version >= 7) ReadTea(r, visit);
            if (version >= 8) ReadTeaFirst(r, visit);
            else visit.TeaOffered = visit.Phase != VisitPhase.Arriving;
            clientele.Restore(visit);
        }
    }

    /// <summary>
    /// Reads a visit's tea table, whether it sits there, and its hours drinking and getting there (layout 7).
    /// </summary>
    static void ReadTea(BinaryReader r, Visit visit)
    {
        string table = r.ReadString();
        visit.Table = table.Length == 0 ? null : table;
        visit.Sitting = r.ReadBoolean();
        visit.Drunk = r.ReadDouble();
        visit.TeaWait = r.ReadDouble();
    }

    /// <summary>
    /// Reads whether a visit got a towel and soap (layout 4).
    /// </summary>
    /// <summary>
    /// Reads whether a visit had its chance at tea before the bath, its hours waiting for a Tea Set and whether that made it late tea (layout 8).
    /// </summary>
    static void ReadTeaFirst(BinaryReader r, Visit visit)
    {
        visit.TeaOffered = r.ReadBoolean();
        visit.ServeWait = r.ReadDouble();
        visit.LateTea = r.ReadBoolean();
    }

    static void ReadSupplies(BinaryReader r, Visit visit)
    {
        visit.Towel = r.ReadBoolean();
        visit.Soap = r.ReadBoolean();
    }

    /// <summary>
    /// Reads a visit's dirty tub, what it owes and its massage (layout 3).
    /// </summary>
    static void ReadMassage(BinaryReader r, Visit visit)
    {
        visit.DirtyTub = r.ReadBoolean();
        visit.Owed = r.ReadInt64();
        string bed = r.ReadString();
        visit.Bed = bed.Length == 0 ? null : bed;
        visit.Massaged = r.ReadDouble();
        visit.BedWait = r.ReadDouble();
        visit.MassagePay = r.ReadDouble();
    }
}
