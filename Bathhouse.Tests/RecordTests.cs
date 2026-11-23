using Bathhouse.Customers;
using Bathhouse.Records;
using Meitou.Content;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Construction;

namespace Bathhouse.Tests;

/// <summary>
/// The records the data phase adds, applied to the installed game's data as a last .mod file would be: they merge cleanly, every link lands, and
/// the game's own rules read the machines as designed (docs/bathhouse.md "Records"). Skipped without a game install.
/// </summary>
public sealed class RecordTests
{
    /// <summary>
    /// The base game with the mod's records applied, loaded once for the class.
    /// </summary>
    static readonly Lazy<GameDatabase?> LoadedGame = new(Load);

    [Fact]
    public void Every_link_names_a_record_the_game_has()
    {
        var game = Game();

        var broken = BathhouseData.Records(game)
            .SelectMany(r => r.References.SelectMany(l => l.Value.Select(link => $"{r.Name}.{l.Key} -> {link.TargetStringId}")))
            .Where(line => game.Find(line[(line.LastIndexOf("-> ", StringComparison.Ordinal) + 3)..]) is null);

        Assert.Empty(broken);
    }

    [Theory]
    [InlineData(Ids.TowelLoom, "584-gamedata.base", Ids.Towel)]
    [InlineData(Ids.SoapPress, "1965-gamedata.base", Ids.Soap)]
    [InlineData(Ids.TeaDryer, "42309-changes_otto.mod", Ids.GreenfruitTea)]
    [InlineData(Ids.Kiln, "1515-gamedata.base", Ids.Teaware)]
    public void A_production_machine_turns_its_input_into_its_output(string machine, string input, string output)
    {
        var data = Machine(machine);

        Assert.True(data.IsProducer);
        Assert.Equal([input], data.Inputs.Select(i => i.Item));
        Assert.Equal(output, data.Output?.Item);
    }

    [Fact]
    public void The_washing_machine_crafts_towels_and_robes_from_dirty_ones_and_soap()
    {
        var game = Game();
        var data = Machine(Ids.WashingMachine);

        Assert.True(data.IsCrafting);
        Assert.Equal([Ids.Towel, Ids.Robe], data.Craft!.Options.Select(o => o.Item));
        Assert.Equal([Ids.DirtyTowel, Ids.Soap], game.Find(Ids.Towel)!.GetReferences("ingredients").Select(l => l.TargetStringId));
        Assert.Equal([Ids.DirtyRobe, Ids.Soap], game.Find(Ids.Robe)!.GetReferences("ingredients").Select(l => l.TargetStringId));
    }

    [Fact]
    public void The_tea_bench_crafts_tea_sets_from_teaware_and_tea()
    {
        var game = Game();

        Assert.True(Machine(Ids.TeaBench).IsCrafting);
        Assert.Equal([Ids.Teaware, Ids.GreenfruitTea], game.Find(Ids.TeaSet)!.GetReferences("ingredients").Select(l => l.TargetStringId));
    }

    [Fact]
    public void The_boiler_is_a_consumer_that_needs_no_operator()
    {
        var data = Machine(Ids.Boiler);

        Assert.True(data.IsConsumer);
        Assert.True(data.IsLight);
        Assert.False(data.Operable);
    }

    [Theory]
    [InlineData(Ids.SmallTub)]
    [InlineData(Ids.MediumTub)]
    [InlineData(Ids.LargeTub)]
    [InlineData(Ids.OilBath)]
    [InlineData(Ids.BloodBath)]
    public void A_tub_is_a_storage_that_takes_nothing_so_only_shift_clicks_use_it(string tub)
    {
        var data = Machine(tub);

        Assert.True(data.IsStorage);
        Assert.False(data.Operable);
        Assert.False(data.Accepts(Ids.Towel, FcsRecordType.Item));
        Assert.False(data.Accepts("1914-gamedata.base", FcsRecordType.Item));
    }

    [Fact]
    public void The_counter_stores_only_towels_and_soap()
    {
        var data = Machine(Ids.Counter);

        Assert.True(data.IsStorage);
        Assert.True(data.Accepts(Ids.Towel, FcsRecordType.Item));
        Assert.True(data.Accepts(Ids.Soap, FcsRecordType.Item));
        Assert.False(data.Accepts(Ids.DirtyTowel, FcsRecordType.Item));
    }

    [Theory]
    [InlineData(Ids.SmallTub)]
    [InlineData(Ids.MediumTub)]
    [InlineData(Ids.LargeTub)]
    [InlineData(Ids.OilBath)]
    [InlineData(Ids.BloodBath)]
    [InlineData(Ids.Boiler)]
    public void Tubs_and_the_boiler_steam_with_a_system_the_mod_ships(string piece)
    {
        var game = Game();
        var effects = game.Find(piece)!.Instances.Values
            .Select(i => game.Find(i.Target))
            .Where(r => r?.Type == FcsRecordType.Effect)
            .ToList();
        var library = new Meitou.Data.Particles.ParticleLibrary();
        library.AddScript(File.ReadAllText(SteamScript()), "bathhouse_steam.pu");

        var steam = Assert.Single(effects);
        Assert.Empty(library.Errors);
        Assert.NotNull(library.FindSystem(steam!.GetString("particle system")));
    }

    [Fact]
    public void The_steam_uses_a_particle_material_of_the_base_game()
    {
        var install = GameInstall.Locate();
        Assert.SkipWhen(install is null, "No game install (KENSHI_PATH).");
        var library = new Meitou.Data.Particles.ParticleLibrary();
        library.AddScript(File.ReadAllText(SteamScript()), "bathhouse_steam.pu");
        var materials = Directory.EnumerateFiles(Path.Combine(install!.DataDirectory, "particles", "materials"), "*.material")
            .Select(File.ReadAllText);

        Assert.All(library.AllSystems, s => Assert.Contains(materials, m => s.Techniques.All(t => m.Contains("material " + t.Material))));
    }

    /// <summary>
    /// The mod's steam script in the repo.
    /// </summary>
    static string SteamScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Bathhouse", "items", "particles", "bathhouse_steam.pu"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "Bathhouse", "items", "particles", "bathhouse_steam.pu");
    }

    [Fact]
    public void The_water_tank_stores_only_water()
    {
        var game = Game();

        Assert.True(Machine(Ids.WaterTank).IsStorage);
        Assert.Equal(["1914-gamedata.base"], game.Find(Ids.WaterTank)!.GetReferences("limit inventory").Select(l => l.TargetStringId));
    }

    [Fact]
    public void The_soap_shelf_stores_only_soap_and_is_researched_with_the_basics()
    {
        var game = Game();

        Assert.True(Machine(Ids.SoapShelf).IsStorage);
        Assert.Equal([Ids.Soap], game.Find(Ids.SoapShelf)!.GetReferences("limit inventory").Select(l => l.TargetStringId));
        Assert.Contains(Ids.SoapShelf, game.Find(Ids.BasicsResearch)!.GetReferences("enable buildings").Select(l => l.TargetStringId));
    }

    [Theory]
    [InlineData(Ids.BathGoal, Ids.BathGoalType)]
    [InlineData(Ids.VisitGoal, Ids.VisitGoalType)]
    public void Every_race_and_the_player_faction_weigh_the_goals(string id, int type)
    {
        var game = Game();
        var goal = game.Find(id)!;
        var owners = game.OfType(FcsRecordType.Race).Append(game.Find("204-gamedata.base")!);

        Assert.Equal(type, goal.GetInt("enum"));
        Assert.Equal(3, goal.GetInt("classification"));
        Assert.All(owners, o => Assert.Contains(o.GetReferences("AI Goals"), l => l.TargetStringId == id && l.Values.Value1 == 100));
    }

    [Fact]
    public void Every_spawnable_customer_kind_names_a_squad_and_a_faction_of_the_game()
    {
        var game = Game();

        Assert.All(CustomerTypes.All.Where(k => k.Spawnable), k =>
        {
            Assert.Equal(FcsRecordType.SquadTemplate, game.Find(k.Template)?.Type);
            Assert.Equal(FcsRecordType.Faction, game.Find(k.Faction)?.Type);
        });
    }

    [Fact]
    public void Research_unlocks_every_building()
    {
        var game = Game();
        var unlocked = game.OfType(FcsRecordType.Research)
            .SelectMany(r => r.GetReferences("enable buildings"))
            .Select(l => l.TargetStringId)
            .ToHashSet();

        var buildings = BathhouseData.Records(game).Where(r => r.RecordType == FcsRecordType.Building).Select(r => r.StringId);

        Assert.All(buildings, b => Assert.Contains(b, unlocked));
    }

    /// <summary>
    /// The machine data the game builds for one of the mod's buildings.
    /// </summary>
    static MachineData Machine(string id)
    {
        var game = Game();
        return MachineData.From(game, game.Find(id)!) ?? throw new InvalidOperationException($"No machine data for {id}.");
    }

    /// <summary>
    /// The loaded game, or a skip when there is no install.
    /// </summary>
    static GameDatabase Game()
    {
        Assert.SkipWhen(LoadedGame.Value is null, "No game install (KENSHI_PATH).");
        return LoadedGame.Value!;
    }

    /// <summary>
    /// Loads the base game and applies the mod's records as its .mod file; null without an install.
    /// </summary>
    static GameDatabase? Load()
    {
        if (GameInstall.Locate() is not { } install) return null;

        return GameDatabase.Load(LoadOrder.BaseGame(install), game => game.Apply(BathhouseData.File(game), Ids.FileName));
    }
}
