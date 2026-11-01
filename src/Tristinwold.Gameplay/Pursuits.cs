using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>A crop: what seed it comes from, what it gives, how long it takes to grow, and who can grow it.</summary>
public sealed record Crop(string Seed, string Gives, string Name, int Level, double GrowSeconds, int Min, int Max, string Colour);

/// <summary>
/// Gardening in the allotments (and a hero's own garden): plant a seed, and it grows while you play; come back
/// when it's ready and harvest it. Better gardeners grow more, and the rarer herbs.
/// </summary>
public static class Farming
{
    public static readonly IReadOnlyList<Crop> Crops =
    [
        new("potato_seed", "potato", "Potatoes", 1, 6 * 60, 3, 5, "#c8a060"),
        new("onion_seed", "onion", "Onions", 1, 6 * 60, 2, 4, "#d8c890"),
        new("cabbage_seed", "cabbage", "Cabbages", 2, 8 * 60, 1, 3, "#7ab050"),
        new("emberleaf_seed", "emberleaf", "Emberleaf", 3, 8 * 60, 2, 4, "#d04a20"),
        new("bluebell_seed", "bluebell", "Bluebells", 3, 8 * 60, 2, 4, "#4a6ad8"),
        new("bloodcap_spore", "bloodcap", "Bloodcaps", 5, 12 * 60, 1, 3, "#b01818"),
        new("frostbloom_seed", "frostbloom", "Frostbloom", 6, 14 * 60, 1, 3, "#9ad8ff"),
        new("silverthorn_seed", "silverthorn", "Silverthorn", 7, 14 * 60, 1, 3, "#c0c8c8"),
    ];

    public static Crop? FromSeed(string seed) => Crops.FirstOrDefault(c => c.Seed == seed);
    public static Crop? Of(string gives) => Crops.FirstOrDefault(c => c.Gives == gives);

    /// <summary>How far along a planting is (0 to 1).</summary>
    public static double Growth(Crop c, double plantedAt, double now) => Math.Clamp((now - plantedAt) / c.GrowSeconds, 0, 1);

    /// <summary>What a ripe crop gives: more for a better gardener.</summary>
    public static int Yield(Crop c, int level, Rng rng) => c.Min + rng.Next(c.Max - c.Min + 1) + (level - c.Level >= 3 && rng.Chance(0.5) ? 1 : 0);

    public static int PlantXp(Crop c) => 3 + 2 * c.Level;
    public static int HarvestXp(Crop c) => 8 + 5 * c.Level;
}

/// <summary>
/// Firemaking: a log and a tinderbox make a fire anywhere outside town. Better wood burns longer, and takes more
/// skill; at a fire a hero can cook, and resting by it heals.
/// </summary>
public static class Firemaking
{
    /// <summary>(log, level, seconds it burns, experience).</summary>
    public static readonly IReadOnlyList<(string Log, int Level, double Seconds, int Xp)> Logs =
    [
        ("pine_log", 1, 75, 10), ("oak_log", 3, 105, 18), ("yew_log", 6, 150, 30),
    ];

    public static (string Log, int Level, double Seconds, int Xp)? Best(Pack pack) =>
        Logs.Where(l => pack.Count(l.Log) > 0 && pack.Level(Job.Firemaking) >= l.Level).Select(l => ((string, int, double, int)?)l).LastOrDefault();

    /// <summary>The chance a fire catches first time: a beginner fumbles now and then.</summary>
    public static double Catches(int level) => Math.Min(0.98, 0.7 + 0.04 * level);

    /// <summary>How much of a hero's health comes back each second by a fire, out of a fight.</summary>
    public const double WarmHeal = 0.02;
}

/// <summary>A sort of person whose pocket can be picked: how hard it is, and what they carry.</summary>
public sealed record Mark(string Kind, int Level, int CoinMin, int CoinMax, (string Id, double Chance)[] Things);

/// <summary>
/// Thieving: lifting a purse while you talk. Harder marks carry more. Rogues have quick hands, a thief quicker
/// still. Caught, there's a fine, the city thinks less of you, and the mark won't let you near them for a while.
/// </summary>
public static class Thieving
{
    public static readonly Mark Commoner = new("commoner", 1, 1, 5, [("bread", 0.15), ("linen", 0.08)]);
    public static readonly Mark Merchant = new("merchant", 4, 5, 14, [("silver_ring", 0.03), ("ancient_coin", 0.1)]);
    public static readonly Mark Guard = new("guard", 5, 4, 10, [("iron_dagger", 0.05), ("healing_draught", 0.1)]);
    public static readonly Mark Gentry = new("gentry", 7, 12, 35, [("rough_garnet", 0.06), ("gold_ring", 0.02), ("pearl", 0.04)]);

    /// <summary>What sort of mark someone is, by what they do.</summary>
    public static Mark MarkFor(string occupation)
    {
        var o = occupation.ToLowerInvariant();
        if (o.Contains("guard") || o.Contains("watch") || o.Contains("sergeant") || o.Contains("knight")) return Guard;
        if (o.Contains("master") || o.Contains("mistress") || o.Contains("laureate") || o.Contains("archmage") || o.Contains("factor") || o.Contains("noble") || o.Contains("lord") || o.Contains("lady")) return Gentry;
        if (o.Contains("merchant") || o.Contains("trader") || o.Contains("smith") || o.Contains("monger") || o.Contains("keeper") || o.Contains("cook") || o.Contains("provisioner") || o.Contains("apothecary")) return Merchant;
        return Commoner;
    }

    /// <summary>The chance of lifting a purse unseen.</summary>
    public static double Chance(Mark m, int level, Calling calling, string classId) =>
        Math.Clamp(0.6 + 0.06 * (level - m.Level) + (calling == Calling.Rogue ? 0.1 : 0) + (classId == "Thief" ? 0.1 : 0), 0.08, 0.95);

    public static int Xp(Mark m) => 6 + 6 * m.Level;

    /// <summary>The Watch's fine for being caught.</summary>
    public static int Fine(Mark m) => 8 + 4 * m.Level;

    /// <summary>How long a mark who caught you keeps their hand on their purse (seconds of play).</summary>
    public const double WarySeconds = 5 * 60;

    public static List<(string Id, int N)> Lift(Mark m, Rng rng, out int crowns)
    {
        crowns = m.CoinMin + rng.Next(m.CoinMax - m.CoinMin + 1);
        return [.. m.Things.Where(t => rng.Chance(t.Chance)).Select(t => (t.Id, 1))];
    }
}

/// <summary>One obstacle of an agility course: what it is, how to cross it, and what it takes.</summary>
public sealed record Obstacle(string Name, string Verb, int Level, int Xp);

/// <summary>
/// Agility: a course of obstacles taken in order. A fall hurts a little and costs the lap; a whole lap is worth a
/// bonus. Every level makes a hero a little quicker on their feet.
/// </summary>
public static class Agility
{
    public static readonly IReadOnlyList<Obstacle> Course =
    [
        new("the balance log", "walk the balance log", 1, 6),
        new("the climbing net", "climb the net", 1, 6),
        new("the rope swing", "swing across", 2, 9),
        new("the stepping stones", "hop the stepping stones", 2, 8),
        new("the wall", "scale the wall", 3, 10),
        new("the slide", "slide down", 1, 5),
    ];

    public const int LapBonus = 40;

    /// <summary>The chance of falling at an obstacle: less as you improve.</summary>
    public static double FallChance(Obstacle o, int level) => Math.Clamp(0.2 + 0.05 * (o.Level - level), 0.02, 0.45);

    /// <summary>A hero's speed on their feet: a percent a level.</summary>
    public static double Speed(int level) => 1 + 0.01 * (level - 1);
}

/// <summary>
/// Trapping: set a snare out in the wilds, come back in a few minutes, and see what's in it: what's caught depends
/// on the land. More snares at once as you improve.
/// </summary>
public static class Trapping
{
    public static (string Id, double Weight)[] Catches(string biome) => biome switch
    {
        "PineForest" => [("rabbit", 3), ("game_bird", 2), ("marten_pelt", 1)],
        "Tundra" or "Snowfield" => [("snow_hare_pelt", 3), ("game_bird", 1), ("rabbit", 1)],
        "Marsh" => [("game_bird", 3), ("rabbit", 1), ("feather", 1)],
        _ => [("rabbit", 4), ("game_bird", 2), ("fox_pelt", 0.6)],
    };

    /// <summary>How long a snare takes to catch something (seconds of play).</summary>
    public static double Wait(int level, Rng rng) => 120 + rng.NextDouble() * 120 - 6 * (level - 1);

    public static int MaxSnares(int level) => 2 + level / 3;

    /// <summary>What a sprung snare holds: the catch, and with a rabbit or a bird, its pelt or feathers too.</summary>
    public static List<(string Id, int N)> Catch(string biome, Rng rng)
    {
        var id = rng.PickWeighted(Catches(biome));
        var got = new List<(string, int)> { (id, 1) };
        if (id == "rabbit") got.Add(("rabbit_pelt", 1));
        if (id == "game_bird") got.Add(("feather", 1 + rng.Next(3)));
        return got;
    }

    public static int Xp(string id) => 8 + (Items.Get(id)?.Value ?? 0);
}

/// <summary>A stage of building a house, or a piece for it: what it needs, and how skilled a builder must be.</summary>
public sealed record BuildStep(string Id, string Name, int Level, (string Id, int N)[] Needs, int Xp, string Text);

/// <summary>
/// A hero's own house, on a plot bought from the Exchange: built in stages (foundation, frame, walls, roof, door
/// and windows), then furnished. A bed to rest well in, a chest to keep things, a hearth to cook at, a garden, a
/// trophy wall that hardens the hero's arm for every trophy hung, and a charm to come home by.
/// </summary>
public static class Housing
{
    public const int PlotPrice = 250;

    public static readonly IReadOnlyList<BuildStep> Stages =
    [
        new("foundation", "Lay the foundation", 1, [("stone_block", 20)], 40, "A level bed of squared stone."),
        new("frame", "Raise the frame", 1, [("plank", 20), ("iron_nails", 20)], 60, "Posts and beams: you can see the house in it now."),
        new("walls", "Build the walls", 2, [("stone_block", 20), ("plank", 15), ("iron_nails", 15)], 80, "Stone below, timber above."),
        new("roof", "Roof it", 3, [("plank", 20), ("iron_nails", 20), ("stone_block", 10)], 100, "Rafters and slates: it keeps the rain out."),
        new("door", "Hang the door and glaze the windows", 3, [("plank", 6), ("iron_ingot", 2), ("linen", 4)], 60, "A door that shuts, windows with curtains: it's a home."),
    ];

    public static readonly IReadOnlyList<BuildStep> Furniture =
    [
        new("bed", "A bed", 2, [("plank", 8), ("linen", 6)], 40, "Rest at home: healed whole, and well rested (a tenth more experience for half an hour)."),
        new("chest", "A storage chest", 2, [("plank", 10), ("iron_nails", 10)], 40, "Keep things at home rather than carry them."),
        new("garden", "A garden", 1, [("plank", 4)], 25, "Two beds of good soil by the house, for anything you'd grow in the allotments."),
        new("hearth", "A hearth", 3, [("stone_block", 12), ("iron_ingot", 2)], 50, "Cook at home."),
        new("trophy_wall", "A trophy wall", 4, [("plank", 6), ("iron_nails", 6)], 50, "Hang the trophies of the lairs: each makes your arm a little surer (a percent more harm, up to ten)."),
        new("farm_plot", "A farm plot", 1, [("plank", 6)], 25, "A companion at home keeps it, and what it grows goes in the chest."),
        new("home_forge", "A forge", 3, [("stone_block", 12), ("iron_ingot", 3)], 50, "A companion at home works what's in the chest at it: ore to ingots, hides to leather."),
        new("shop_counter", "A shop counter", 2, [("plank", 12), ("iron_nails", 8)], 40, "Tell a companion at home what to sell from the chest and what to buy for it, and your house is a shop. Tell them nothing, and it's a counter."),
        new("alchemy_bench", "An alchemy bench", 3, [("plank", 10), ("iron_nails", 6), ("linen", 2)], 50, "A companion at home brews from the herbs in the chest."),
    ];

    public static BuildStep Step(string id) => Stages.Concat(Furniture).First(s => s.Id == id);

    /// <summary>The next wing on a finished house: there's no end to them, as long as the timber holds out, and each is bigger work than the last.</summary>
    public static BuildStep Room(int built) => new("room", built == 0 ? "Add a wing" : $"Add wing {built + 1}", Math.Min(10, 4 + built / 2),
        [("plank", 20 + 4 * built), ("stone_block", 15 + 3 * built), ("iron_nails", 15 + 3 * built), ("iron_ingot", 2 + built / 2)], 60, "More house. There's always room for more house.");

    /// <summary>How long well rested lasts (seconds of play), and what it's worth.</summary>
    public const double RestedSeconds = 30 * 60, RestedXp = 1.1;

    public const double CharmCooldown = 5 * 60;

    /// <summary>What hanging trophies is worth: a percent more harm each, up to ten.</summary>
    public static double TrophyBonus(int hung) => 1 + 0.01 * Math.Min(10, hung);
}

/// <summary>A feat: something worth having done, and what it's worth.</summary>
public sealed record Feat(string Id, string Name, string Text, int Points);

/// <summary>
/// Feats: what a hero has done beyond fighting (and some of the fighting too), counted as they play and kept
/// with the company. Each is worth points; the total is shown with the hero's skills.
/// </summary>
public static class Feats
{
    /// <summary>(feat, the counter it watches, how many it takes).</summary>
    public static readonly IReadOnlyList<(Feat Feat, string Counter, int Needs)> All =
    [
        (new("first_catch", "Something on the Line", "Land your first fish.", 5), "fish", 1),
        (new("angler", "Angler", "Land a hundred fish.", 20), "fish", 100),
        (new("pearl_diver", "Pearl of the Harbour", "Fish up a pearl.", 15), "pearl", 1),
        (new("first_ore", "Stone and Iron", "Break open your first vein.", 5), "veins", 1),
        (new("deep_miner", "Deep Miner", "Break open a hundred veins.", 20), "veins", 100),
        (new("first_tree", "Timber!", "Fell your first tree.", 5), "trees", 1),
        (new("forester", "Forester", "Fell a hundred trees.", 20), "trees", 100),
        (new("first_meal", "Something Hot", "Cook your first meal.", 5), "meals", 1),
        (new("first_fire", "Fire-Starter", "Light your first fire.", 5), "fires", 1),
        (new("hearth_keeper", "Keeper of Fires", "Light fifty fires.", 15), "fires", 50),
        (new("green_fingers", "Green Fingers", "Harvest your first crop.", 5), "harvests", 1),
        (new("smallholder", "Smallholder", "Harvest fifty crops.", 20), "harvests", 50),
        (new("first_lap", "Off the Mark", "Run a whole lap of the Tumblers' Run.", 10), "laps", 1),
        (new("tumbler", "Tumbler", "Run twenty-five laps.", 20), "laps", 25),
        (new("light_fingers", "Light Fingers", "Lift your first purse.", 5), "pockets", 1),
        (new("cutpurse", "Cutpurse", "Lift fifty purses.", 20), "pockets", 50),
        (new("caught", "Caught Red-Handed", "Get caught.", 5), "caught", 1),
        (new("first_snare", "Something in the Snare", "Catch something in a snare.", 5), "snared", 1),
        (new("trapper", "Trapper", "Catch fifty things in snares.", 20), "snared", 50),
        (new("x_marks", "X Marks the Spot", "Dig up a buried treasure.", 15), "treasures", 1),
        (new("treasure_hunter", "Treasure Hunter", "Dig up ten.", 25), "treasures", 10),
        (new("jeweller", "Fine Work", "Make a piece of jewellery.", 10), "jewellery", 1),
        (new("landowner", "Landowner", "Buy a plot of land.", 10), "plots", 1),
        (new("homeowner", "Home at Last", "Finish building your house.", 30), "house", 1),
        (new("well_rested", "Well Rested", "Sleep in your own bed.", 5), "slept", 1),
        (new("contractor", "Contractor", "Fill five contracts at the Exchange.", 15), "contracts", 5),
        (new("benefactor", "Benefactor", "Give to the city's works.", 10), "donations", 1),
        (new("dockhand", "Strong Back", "Carry a hundred crates on the docks.", 15), "crates", 100),
        (new("courier", "Courier", "Deliver ten of the Exchange's letters.", 10), "letters", 10),
        (new("master_of_a_trade", "Master of a Trade", "Reach level 10 in any trade.", 30), "best_trade", 10),
        (new("jack_of_all", "Jack of All Trades", "Reach level 5 in ten trades.", 30), "trades_at_5", 10),
    ];

    /// <summary>The feats a counter's new value has just earned.</summary>
    public static IEnumerable<Feat> Earned(string counter, int value, ISet<string> already) =>
        All.Where(f => f.Counter == counter && value >= f.Needs && !already.Contains(f.Feat.Id)).Select(f => f.Feat);

    public static int Points(IEnumerable<string> earned) => All.Where(f => earned.Contains(f.Feat.Id)).Sum(f => f.Feat.Points);
}
