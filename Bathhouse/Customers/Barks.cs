using Meitou.Simulation;

namespace Bathhouse.Customers;

/// <summary>
/// The moments a customer says something (docs/bathhouse.md "Barks").
/// </summary>
public enum BarkEvent
{
    /// <summary>
    /// Getting into a tub.
    /// </summary>
    InTub,

    /// <summary>
    /// The water went cold.
    /// </summary>
    Cold,

    /// <summary>
    /// Leaving unhappy, without paying.
    /// </summary>
    Complaint,

    /// <summary>
    /// Leaving happy after paying.
    /// </summary>
    Happy,

    /// <summary>
    /// Turned away by the price.
    /// </summary>
    Pricey,

    /// <summary>
    /// Picking a fight.
    /// </summary>
    Fight,
}

/// <summary>
/// The customers' lines, per kind and event (two each, picked by the world's deterministic random numbers). English only: the engine has no
/// translation catalog for mods yet.
/// </summary>
public static class Barks
{
    /// <summary>
    /// The roll's counter salt.
    /// </summary>
    const ulong Salt = 0xBA2C_5A75;

    /// <summary>
    /// Every kind's lines by event, in <see cref="BarkEvent"/> order.
    /// </summary>
    static readonly Dictionary<string, string[][]> Lines = new()
    {
        ["drifters"] =
        [
            ["Hot water. Haven't seen that in years.", "Don't mind the dust, it comes off eventually."],
            ["Cold as the road. Figures.", "I could've stayed outside for this."],
            ["Not paying for a puddle.", "Worse than sleeping rough."],
            ["Worth every cat. Almost.", "I feel like a new drifter."],
            ["That much? I'll wash in a ditch.", "You charge nobles' prices to drifters?"],
            ["You think I won't fight for my cats?", "Right, that's it!"],
        ],
        ["traders"] =
        [
            ["Ah, a civilised end to a long route.", "Mind the ledgers, they're waterproof. Mostly."],
            ["Lukewarm. I'll note that in my accounts.", "The water's colder than my margins."],
            ["Unacceptable value. I'll tell the Guild.", "I've had better service from a cage."],
            ["Good trade. I'll be back.", "A fair price for honest steam."],
            ["I know a markup when I see one.", "The Guild would never pay that."],
            ["Guards! Er, my guards!", "You'll regret cheating a trader."],
        ],
        ["hunters"] =
        [
            ["Washing off the last bounty.", "Quiet. I'm off duty."],
            ["Cold water. Like the trail.", "This is a cold case now."],
            ["No pay for a cold soak.", "I've collected on less."],
            ["Clean hands, clean conscience.", "That earned its price."],
            ["Your price has a bounty on it.", "Too rich for a working hunter."],
            ["You're worth more to me dead.", "Wrong person to cheat."],
        ],
        ["shek"] =
        [
            ["A warrior's rest. I accept it.", "Hot enough to temper a blade."],
            ["Cold water is for the weak!", "Is this a test of endurance?"],
            ["There is no honour in this bath.", "The Shek do not pay for shame."],
            ["Good. My horns feel lighter.", "A fine soak. Kral would approve."],
            ["You haggle like a Holy Nation priest.", "Too much. Fight me for a discount?"],
            ["Draw your weapons!", "Finally, a proper welcome!"],
        ],
        ["skeletons"] =
        [
            ["Oil temperature nominal.", "Lubrication cycle started."],
            ["Warning: coolant cold.", "Joints seizing."],
            ["Service below specification.", "Filing a fault report."],
            ["Systems refreshed.", "Rust levels reduced. Thank you."],
            ["Cost exceeds value. Declined.", "Pricing error detected."],
            ["Hostile mode engaged.", "Threat response initiated."],
        ],
        ["crabs"] =
        [
            ["Into the pot with me, then!", "Boil me gently, friend."],
            ["Not even hot enough to cook a crab.", "This pot's gone cold."],
            ["Raw service. Like the meat.", "We'll eat better than we bathed."],
            ["Cooked to perfection!", "Red as a crab. Lovely."],
            ["Pay that? Snip snip.", "We raid for less."],
            ["Claws out!", "Crab rave, lads!"],
        ],
        ["nobles"] =
        [
            ["Adequate. Barely.", "The steam is acceptable."],
            ["Tepid! How dare you.", "My servants run hotter baths."],
            ["I've never been so insulted.", "You'll hear from my lawyers."],
            ["Exquisite. Do keep it up.", "My compliments to the boiler."],
            ["A peasant's bath at a lord's price?", "Not worth my coin."],
            ["Guards, teach this rabble manners!", "Seize them!"],
        ],
        ["cannibals"] =
        [
            ["Soft meat stews best.", "Warm, like the feast."],
            ["Cold meat. Bad meat.", "No fire, no feast."],
            ["Not good. Eat the keeper.", "Bad bath. Hungry."],
            ["Good stew-bath.", "Warm and full."],
            ["Too much. Eat instead.", "Coins? Meat."],
            ["Meat!", "Feast time!"],
        ],
    };

    /// <summary>
    /// The customer says one of its kind's lines for the event.
    /// </summary>
    public static void Say(World world, CharacterId who, string kind, BarkEvent what, RngPurpose purpose)
    {
        if (!Lines.TryGetValue(kind, out var lines)) return;

        var choices = lines[(int)what];
        float roll = Rng.Float(Rng.Hash(world.Seed, Rng.Key(who), purpose, Salt ^ (ulong)world.Tick));
        world.Bark(who, choices[(int)(roll * choices.Length) % choices.Length]);
    }

    /// <summary>
    /// Whether every kind has lines for every event (for the tests).
    /// </summary>
    public static bool Complete() =>
        CustomerTypes.All.All(t => Lines.TryGetValue(t.Key, out var l) && l.Length == Enum.GetValues<BarkEvent>().Length
            && l.All(e => e.Length > 0));
}
