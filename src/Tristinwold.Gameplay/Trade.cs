using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>A trade a hero can take up and get better at.</summary>
public enum Job { Herbalism, Smithing, Taming, Fishing, Mining, Woodcutting, Cooking, Farming, Firemaking, Crafting, Agility, Thieving, Trapping, Building }

/// <summary>
/// What a hero carries and wears, and how good they are at their trades. Saved with the company.
/// </summary>
public sealed class Pack
{
    public Dictionary<string, int> Items { get; set; } = [];
    public Dictionary<Slot, string> Worn { get; set; } = [];
    public Dictionary<Job, int> JobXp { get; set; } = [];

    public int Count(string id) => Items.GetValueOrDefault(id);

    public void Add(string id, int n = 1)
    {
        if (n <= 0 || Gameplay.Items.Get(id) is null) return;
        Items[id] = Count(id) + n;
    }

    public bool Take(string id, int n = 1)
    {
        if (Count(id) < n) return false;
        if ((Items[id] -= n) == 0) Items.Remove(id);
        return true;
    }

    public bool Has(IEnumerable<(string Id, int N)> needs) => needs.All(n => Count(n.Id) >= n.N);

    /// <summary>Everything carried, gear first, then the rest by kind and name.</summary>
    public IEnumerable<(Item Item, int N)> Contents => Items
        .Select(kv => (Item: Gameplay.Items.Get(kv.Key)!, N: kv.Value)).Where(x => x.Item is not null)
        .OrderBy(x => x.Item.Kind == ItemKind.Gear ? 0 : 1).ThenBy(x => x.Item.Kind).ThenBy(x => x.Item.Name);

    // ---- gear ---------------------------------------------------------------------------------------------

    public Item? WornIn(Slot slot) => Worn.TryGetValue(slot, out var id) ? Gameplay.Items.Get(id) : null;

    /// <summary>Puts on a piece of gear from the pack; what was in that slot goes back in the pack. A two-handed
    /// weapon needs both hands, so the off-hand comes off too.</summary>
    public bool Equip(string id)
    {
        if (Gameplay.Items.Get(id) is not { Wearable: true } item || !Take(id)) return false;
        Unequip(item.Slot);
        Worn[item.Slot] = id;
        if (item.Slot == Slot.Weapon && item.Grip is Grip.TwoHand or Grip.Ranged && WornIn(Slot.Offhand) is { } offhand && offhand.Id != "spellbook") Unequip(Slot.Offhand);
        if (item.Slot == Slot.Offhand && WornIn(Slot.Weapon) is { Grip: Grip.TwoHand or Grip.Ranged } && item.Id != "spellbook") Unequip(Slot.Weapon);
        return true;
    }

    public void Unequip(Slot slot)
    {
        if (!Worn.Remove(slot, out var id)) return;
        Add(id);
    }

    /// <summary>What everything worn adds: a share more damage, and armour.</summary>
    public double Damage => Worn.Values.Sum(id => Gameplay.Items.Get(id)?.Damage ?? 0);
    public double Armor => Worn.Values.Sum(id => Gameplay.Items.Get(id)?.Armor ?? 0);
    /// <summary>What forged gear adds: a share more speed and health, and points of critical chance.</summary>
    public double Speed => Worn.Values.Sum(id => Gameplay.Items.Get(id)?.Speed ?? 0);
    public double Crit => Worn.Values.Sum(id => Gameplay.Items.Get(id)?.Crit ?? 0);
    public double Health => Worn.Values.Sum(id => Gameplay.Items.Get(id)?.Health ?? 0);
    /// <summary>What everything worn adds up to of one forged ability.</summary>
    public double Ability(string key) => Worn.Values.Sum(id => Gameplay.Items.Get(id)?.Powers?.GetValueOrDefault(key) ?? 0);
    /// <summary>What a piece of forged armour's featherstep does to the height of a jump: at most double.</summary>
    public double JumpHeight => 1 + Math.Min(Forging.MostJump, Ability("jump"));
    public int ToHit => WornIn(Slot.Weapon)?.ToHit ?? 0;
    public int Power => WornIn(Slot.Weapon)?.Power ?? 0;

    // ---- trades -------------------------------------------------------------------------------------------

    public const int MaxJobLevel = 10;

    public int Level(Job job)
    {
        var xp = JobXp.GetValueOrDefault(job);
        var level = 1;
        while (level < MaxJobLevel && xp >= XpFor(level + 1)) level++;
        return level;
    }

    /// <summary>Experience to reach a level in a trade (from the start).</summary>
    public static int XpFor(int level) => (int)Math.Round(40 * Math.Pow(level - 1, 1.5));

    /// <summary>Practice: experience in a trade. Returns the new level if it went up.</summary>
    public int? Practise(Job job, int xp)
    {
        var before = Level(job);
        JobXp[job] = JobXp.GetValueOrDefault(job) + xp;
        var after = Level(job);
        return after > before ? after : null;
    }

    /// <summary>Makes something, if the hero has the skill and the makings. The makings are used up.</summary>
    public bool Make(Recipe r)
    {
        if (Level(r.Job) < r.Level || !Has(r.Needs)) return false;
        foreach (var (id, n) in r.Needs) Take(id, n);
        Add(r.Makes, r.Count);
        Practise(r.Job, 8 + 4 * r.Level);
        return true;
    }
}

/// <summary>Something a trade makes: what, how many, from what, and how skilled you must be. Smithing is done at a
/// forge; herbalism needs an alchemist's kit (or the apothecary's bench); cooking a tavern's kitchen.</summary>
public sealed record Recipe(Job Job, int Level, string Makes, int Count, (string Id, int N)[] Needs)
{
    public string Id => $"{Job}:{Makes}";
    public Item Item => Items.Get(Makes)!;
}

public static class Recipes
{
    /// <summary>What a forge will burn: every piece made at one takes a log, the cheapest first.</summary>
    public static readonly IReadOnlyList<string> Fuels = ["pine_log", "oak_log", "yew_log", "rotwood"];

    static Recipe Forge(int level, string makes, params (string, int)[] needs) => new(Job.Smithing, level, makes, 1, needs);
    static Recipe Brew(int level, string makes, params (string, int)[] needs) => new(Job.Herbalism, level, makes, 1, needs);
    static Recipe Cook(int level, string makes, int count, params (string, int)[] needs) => new(Job.Cooking, level, makes, count, needs);
    static Recipe Craft(int level, string makes, params (string, int)[] needs) => new(Job.Crafting, level, makes, 1, needs);

    public static IReadOnlyList<Recipe> All { get; } =
    [
        // Smelting and tanning.
        new(Job.Smithing, 1, "iron_ingot", 1, [("scrap_iron", 3)]),
        new(Job.Smithing, 1, "iron_ingot", 1, [("iron_ore", 2)]),
        new(Job.Smithing, 3, "silver_ingot", 1, [("silver_ore", 2), ("coal", 1)]),
        new(Job.Smithing, 5, "gold_ingot", 1, [("gold_ore", 2), ("coal", 1)]),
        new(Job.Smithing, 3, "steel_ingot", 1, [("iron_ingot", 1), ("coal", 2)]),
        new(Job.Smithing, 1, "leather", 1, [("deer_hide", 1)]),
        new(Job.Smithing, 1, "leather", 1, [("boar_hide", 1)]),
        new(Job.Smithing, 1, "leather", 1, [("cow_hide", 1)]),
        new(Job.Smithing, 1, "leather", 1, [("horse_hide", 1)]),
        new(Job.Smithing, 2, "leather", 2, [("ape_hide", 1)]),
        new(Job.Smithing, 2, "leather", 3, [("troll_hide", 1)]),
        // Iron.
        Forge(1, "iron_dagger", ("iron_ingot", 1), ("leather", 1)),
        Forge(1, "iron_sword", ("iron_ingot", 2), ("leather", 1)),
        Forge(1, "iron_axe", ("iron_ingot", 2), ("leather", 1)),
        Forge(1, "iron_hammer", ("iron_ingot", 2), ("leather", 1)),
        Forge(1, "iron_mace", ("iron_ingot", 2), ("leather", 1)),
        Forge(4, "steel_hammer", ("steel_ingot", 2), ("leather", 1)),
        Forge(2, "iron_greatsword", ("iron_ingot", 3), ("leather", 1)),
        Forge(2, "iron_greataxe", ("iron_ingot", 3), ("leather", 1)),
        Forge(1, "wooden_shield", ("leather", 1), ("iron_ingot", 1)),
        Forge(2, "iron_shield", ("iron_ingot", 3), ("leather", 1)),
        Forge(2, "iron_helm", ("iron_ingot", 2), ("leather", 1)),
        Forge(3, "iron_mail", ("iron_ingot", 4), ("leather", 2)),
        Forge(2, "hunting_crossbow", ("iron_ingot", 1), ("leather", 1), ("antler", 1)),
        // Leather and fur.
        Forge(1, "leather_armor", ("leather", 4)),
        Forge(1, "leather_cap", ("leather", 2)),
        Forge(2, "fur_armor", ("leather", 2), ("wolf_pelt", 3)),
        Forge(1, "wolf_cloak", ("wolf_pelt", 2)),
        Forge(2, "bear_cloak", ("bear_pelt", 1), ("leather", 1)),
        Forge(1, "traveller_cloak", ("linen", 3)),
        Forge(1, "horned_hat", ("leather", 1), ("antler", 1)),
        Forge(4, "ape_leather_armor", ("leather", 3), ("ape_hide", 2), ("deathcrest_horn", 1)),
        // Steel.
        Forge(3, "steel_dagger", ("steel_ingot", 1), ("leather", 1)),
        Forge(4, "steel_sword", ("steel_ingot", 2), ("leather", 1)),
        Forge(4, "steel_axe", ("steel_ingot", 2), ("leather", 1)),
        Forge(5, "steel_greatsword", ("steel_ingot", 4), ("leather", 1)),
        Forge(5, "steel_greataxe", ("steel_ingot", 4), ("leather", 1)),
        Forge(4, "steel_shield", ("steel_ingot", 3), ("leather", 1)),
        Forge(4, "spiked_shield", ("steel_ingot", 2), ("iron_ingot", 2), ("leather", 1)),
        Forge(4, "steel_helm", ("steel_ingot", 2), ("leather", 1)),
        Forge(5, "steel_plate", ("steel_ingot", 5), ("leather", 2)),
        Forge(5, "heavy_crossbow", ("steel_ingot", 2), ("leather", 1), ("antler", 1)),
        // From the stranger things.
        Forge(5, "kipine_cloak", ("kipine_feather", 4), ("linen", 2)),
        Forge(6, "devilhide_armor", ("devil_hide", 3), ("leather", 2), ("brimstone", 2)),
        Forge(6, "amethyst_staff", ("amethyst_shard", 4), ("heartwood", 1)),
        Forge(8, "storm_staff", ("storm_feather", 3), ("heartwood", 1), ("amethyst_shard", 2)),
        // Dragons.
        Forge(7, "dragonbone_sword", ("dragon_bone", 2), ("steel_ingot", 1), ("leather", 1)),
        Forge(8, "dragonbone_greatsword", ("dragon_bone", 4), ("steel_ingot", 1), ("leather", 1)),
        .. Items.DragonColours.Select(c => Forge(8, $"dragonscale_armor_{c.Id}", ($"dragon_scale_{c.Id}", 6), ("leather", 3), ("dragon_bone", 1))),
        .. Items.DragonColours.Select(c => Forge(7, $"dragonscale_helm_{c.Id}", ($"dragon_scale_{c.Id}", 3), ("leather", 1))),
        // Potions.
        Brew(1, "healing_draught", ("emberleaf", 2), ("ladys_mantle", 1)),
        Brew(1, "mana_tonic", ("bluebell", 2), ("ghostcap", 1)),
        Brew(1, "stamina_brew", ("marsh_reed", 2), ("thistle", 1)),
        Brew(2, "stamina_brew", ("snowberry", 2), ("thistle", 1)),
        Brew(4, "greater_healing", ("healing_draught", 1), ("bloodcap", 1), ("troll_fat", 1)),
        Brew(5, "elixir_of_might", ("bloodcap", 2), ("silverthorn", 1), ("troll_fat", 1)),
        Brew(5, "warding_draught", ("frostbloom", 2), ("silverthorn", 1), ("nightshade", 1)),
        // Meals, at a tavern's kitchen: any fish will grill.
        .. new[] { "herring", "mackerel", "cod", "sea_bass", "perch", "trout", "carp", "pike", "arctic_char", "ice_pike", "salmon" }
            .Select(f => Cook(1, "grilled_fish", 1, (f, 1))),
        Cook(2, "roast_venison", 1, ("venison", 2)),
        Cook(3, "fish_stew", 2, ("cod", 1), ("herring", 2), ("bread", 1)),
        Cook(3, "smoked_eel", 1, ("eel", 1), ("pine_log", 1)),
        Cook(4, "hunters_pie", 2, ("venison", 2), ("bread", 1), ("ladys_mantle", 1)),
        Cook(6, "seafood_platter", 1, ("lobster", 1), ("salmon", 1), ("eel", 1)),
        Cook(1, "roast_bird", 1, ("game_bird", 1)),
        Cook(2, "vegetable_stew", 2, ("potato", 2), ("onion", 1), ("cabbage", 1)),
        Cook(3, "rabbit_stew", 2, ("rabbit", 1), ("potato", 1), ("onion", 1)),
        Cook(1, "roast_meat", 1, ("raw_meat", 2)),
        Cook(2, "berry_tart", 2, ("raspberries", 3), ("bread", 1)),
        Cook(2, "berry_tart", 2, ("bilberries", 3), ("bread", 1)),
        Cook(2, "honey_cake", 2, ("honeycomb", 1), ("bread", 1)),
        Cook(3, "mushroom_stew", 2, ("penny_bun", 2), ("wild_garlic", 1), ("onion", 1)),
        Cook(3, "mushroom_stew", 2, ("chanterelle", 2), ("wild_garlic", 1), ("onion", 1)),
        // The jeweller's bench and the loom.
        Craft(1, "linen_tunic", ("linen", 4)),
        Craft(1, "linen", ("flax", 3)),
        Craft(2, "linen", ("wool", 2)),
        Craft(2, "silver_ring", ("silver_ingot", 1)),
        Craft(2, "silver_amulet", ("silver_ingot", 2)),
        Craft(3, "garnet", ("rough_garnet", 1)),
        Craft(3, "padded_jerkin", ("linen", 4), ("leather", 2)),
        Craft(4, "gold_ring", ("gold_ingot", 1)),
        Craft(5, "pearl_necklace", ("silver_ingot", 1), ("pearl", 3)),
        Craft(5, "garnet_ring", ("gold_ingot", 1), ("garnet", 1)),
        Craft(6, "sapphire", ("rough_sapphire", 1)),
        Craft(7, "sapphire_amulet", ("gold_ingot", 2), ("sapphire", 1)),
        new(Job.Smithing, 1, "iron_nails", 10, [("iron_ingot", 1)]),
        new(Job.Building, 1, "plank", 2, [("pine_log", 1)]),
        new(Job.Building, 2, "plank", 3, [("oak_log", 1)]),
        new(Job.Building, 3, "home_charm", 1, [("oak_log", 1), ("silver_ingot", 1)]),
    ];

    public static IEnumerable<Recipe> For(Job job) => All.Where(r => r.Job == job);
}

/// <summary>What a creature leaves when it dies: things it would (pelts from wolves, scales from dragons), and coin
/// from anything that carries it. Dragons leave a great deal; the bosses of the lairs, their trophies and gear. Nothing
/// that's killed leaves nothing: the first thing on its list is what it always has.</summary>
public static class Loot
{
    public sealed record Drop(List<(string Id, int N)> Items, int Crowns);

    /// <summary>(item, chance, how many from, how many to).</summary>
    static (string Id, double Chance, int Min, int Max)[] Table(string kind) => kind switch
    {
        "wolf" or "shadow_wolf" => [("wolf_pelt", 0.9, 1, 1), ("wolf_fang", 0.6, 1, 2), (kind == "shadow_wolf" ? "shadow_essence" : "wolf_fang", 0.4, 1, 1)],
        "ice_wolf" => [("frost_pelt", 0.9, 1, 1), ("wolf_fang", 0.6, 1, 2)],
        "bear" or "rock_bear" => [("bear_pelt", 0.95, 1, 1), ("bear_claw", 0.8, 1, 3)],
        "boar" => [("boar_hide", 0.9, 1, 1), ("boar_tusk", 0.7, 1, 2), ("venison", 0.8, 1, 2)],
        "deer" => [("deer_hide", 0.9, 1, 1), ("antler", 0.5, 1, 1), ("venison", 0.9, 1, 2)],
        "fox" => [("fox_pelt", 0.9, 1, 1)],
        "cow" => [("raw_meat", 0.95, 2, 3), ("cow_hide", 0.9, 1, 1)],
        "sheep" => [("wool", 0.95, 1, 3), ("raw_meat", 0.8, 1, 2)],
        "pig" => [("raw_meat", 0.95, 2, 3)],
        "hen" => [("feather", 0.9, 1, 3), ("raw_meat", 0.5, 1, 1)],
        "horse" or "wild_horse" => [("horse_hide", 0.95, 1, 1), ("raw_meat", 0.9, 1, 3)],
        "dog" => [("scruffy_pelt", 0.9, 1, 1), ("raw_meat", 0.5, 1, 1)],
        "cat" => [("scruffy_pelt", 0.9, 1, 1)],
        "hawk" => [("feather", 0.95, 2, 4)],
        "giant_rat" => [("rat_tail", 0.8, 1, 1)],
        "crystal_rat" => [("amethyst_shard", 0.6, 1, 1), ("rat_tail", 0.5, 1, 1)],
        "mud_crab" => [("crab_chitin", 0.9, 1, 2)],
        "goblin" or "goblin_tinker" => [("goblin_trinket", 0.6, 1, 1), ("scrap_iron", 0.7, 1, 2), ("bread", 0.2, 1, 1)],
        "spriggan" => [("heartwood", 0.8, 1, 1), ("ladys_mantle", 0.4, 1, 2)],
        "frost_troll" => [("troll_fat", 0.9, 1, 2), ("troll_hide", 0.7, 1, 1)],
        "draugr" or "draugr_overlord" or "cinder_thrall" or "hollow_soldier" or "familiar" =>
            [("bone_dust", 0.8, 1, 2), ("ancient_coin", 0.4, 1, 2), ("scrap_iron", 0.4, 1, 1), (kind == "cinder_thrall" ? "ember_ash" : kind == "hollow_soldier" ? "ectoplasm" : "bone_dust", 0.5, 1, 1)],
        "sea_wraith" => [("ectoplasm", 0.9, 1, 2), ("ancient_coin", 0.4, 1, 2)],
        "pirate" or "pirate_captain" =>
            [("ancient_coin", 0.6, 1, 3), ("linen", 0.4, 1, 2), ("iron_dagger", 0.12, 1, 1), ("healing_draught", 0.15, 1, 1), ("ale", 0.3, 1, 1)],
        "marine" => [("bread", 0.4, 1, 2), ("linen", 0.3, 1, 1), ("iron_dagger", 0.1, 1, 1), ("healing_draught", 0.2, 1, 1)],
        "raider" or "raider_chief" =>
            [("raw_meat", 0.5, 1, 2), ("leather", 0.5, 1, 2), ("ancient_coin", 0.3, 1, 2), ("iron_dagger", 0.15, 1, 1), ("healing_draught", 0.15, 1, 1)],
        "dock_thug" or "lantern_boy" or "drowned_hand" or "bandit" or "bandit_chief" =>
            [("bread", 0.4, 1, 2), ("linen", 0.4, 1, 2), ("iron_dagger", 0.1, 1, 1), ("healing_draught", 0.12, 1, 1), ("leather", 0.25, 1, 1)],
        "kipine" => [("kipine_feather", 0.95, 2, 4), ("antler", 0.7, 1, 2), ("goblin_trinket", 0.6, 1, 3), ("ancient_coin", 0.5, 1, 3)],
        "deathcrest_ape" => [("ape_hide", 0.9, 1, 1), ("deathcrest_horn", 0.6, 1, 2), ("bone_dust", 0.4, 1, 2)],
        "imp" => [("imp_horn", 0.7, 1, 2), ("brimstone", 0.4, 1, 1)],
        "dire_imp" => [("devil_hide", 0.8, 1, 1), ("brimstone", 0.8, 1, 3), ("imp_horn", 0.6, 1, 2)],
        "winged_ape" => [("black_feather", 0.9, 2, 3), ("ape_hide", 0.6, 1, 1), ("iron_greatsword", 0.12, 1, 1)],
        "stormwing" => [("storm_feather", 0.6, 1, 1)],
        "velvet_assassin" => [("iron_dagger", 0.4, 1, 1), ("healing_draught", 0.3, 1, 1), ("linen", 0.3, 1, 1)],
        "watchman" or "squire" => [("bread", 0.6, 1, 2), ("leather", 0.4, 1, 1), ("iron_dagger", 0.08, 1, 1)],
        "young_thief" or "initiate" or "protege" or "understudy" => [("bread", 0.6, 1, 1), ("linen", 0.5, 1, 1)],
        "apprentice" => [("linen", 0.6, 1, 2), ("healing_draught", 0.15, 1, 1)],
        "monkey_hand" => [("ale", 0.6, 1, 2), ("linen", 0.4, 1, 2), ("ancient_coin", 0.4, 1, 2), ("healing_draught", 0.15, 1, 1)],
        "dread_soren" => [("ancient_coin", 1, 3, 6), ("linen", 0.5, 1, 2)],
        "lu_wukong" => [("cracked_gourd", 1, 1, 1), ("ancient_coin", 0.8, 2, 4)],
        "turlin" or "giant_ape" => [("fruit_rind", 1, 1, 1), ("ape_hide", 0.7, 1, 1)],
        "golem" => [("scrap_iron", 0.9, 1, 3), ("iron_nails", 0.7, 2, 4)],
        "forge_golem" => [("scrap_iron", 1, 3, 5), ("iron_ingot", 0.8, 1, 2), ("coal", 0.8, 2, 4)],
        "cinder_warden" => [("ember_ash", 1, 2, 4), ("bone_dust", 0.8, 1, 3), ("ancient_coin", 0.7, 2, 4)],
        "goblin_king" => [("goblin_trinket", 1, 2, 4), ("scrap_iron", 0.9, 2, 4), ("ancient_coin", 0.6, 1, 3)],
        "briar_mother" => [("heartwood", 1, 2, 3), ("ladys_mantle", 0.9, 2, 4)],
        "ghost_captain" => [("ectoplasm", 1, 2, 3), ("ancient_coin", 0.9, 2, 5)],
        "crystal_basilisk" => [("amethyst_shard", 1, 2, 4)],
        "duskprowler" => [("shadow_essence", 1, 1, 3), ("wolf_fang", 0.7, 2, 3)],
        "thunderhawk" => [("storm_feather", 1, 2, 3), ("feather", 0.9, 2, 4)],
        "guardian_moss" or "guardian_elder" => [("heartwood", 1, 2, 4), ("ladys_mantle", 0.9, 2, 4)],
        "guardian_bark" => [("heartwood", 1, 3, 5), ("oak_log", 0.9, 2, 4)],
        "guardian_blight" => [("shadow_essence", 1, 1, 3), ("heartwood", 0.8, 1, 2)],
        _ => [],
    };

    /// <summary>What a creature of this kind drops. A youngling doesn't leave much: only the one thing, small.</summary>
    public static Drop Roll(string kind, Rng rng, bool young = false)
    {
        var items = new List<(string, int)>();
        var table = Table(kind);
        if (young)
        {
            if (DragonColour(kind) is { } scale) items.Add(($"dragon_scale_{scale}", 1));
            else if (table.Length > 0) items.Add((table[0].Id, 1));
            return new(items, 0);
        }
        foreach (var (id, chance, min, max) in table)
            if (rng.Chance(chance)) items.Add((id, min + rng.Next(max - min + 1)));
        var crowns = kind switch
        {
            "goblin" or "goblin_tinker" => rng.Next(6),
            "dock_thug" or "lantern_boy" or "drowned_hand" or "bandit" => 3 + rng.Next(10),
            "bandit_chief" or "draugr_overlord" => 15 + rng.Next(25),
            "raider" => 5 + rng.Next(12),
            "raider_chief" => 30 + rng.Next(40),
            "velvet_assassin" => 10 + rng.Next(20),
            "draugr" or "sea_wraith" => rng.Next(8),
            _ => 0,
        };
        if (DragonColour(kind) is { } colour) (items, crowns) = Dragon(kind, colour, rng);
        if (items.Count == 0 && crowns == 0 && table.Length > 0) items.Add((table[0].Id, table[0].Min));
        return new(Merge(items), crowns);
    }

    static string? DragonColour(string kind) =>
        kind.StartsWith("dragon_") ? kind[7..].Replace("_corrupt", "") : null;

    /// <summary>A dragon: an armful of its scales, its bones, often its heart, and the gold it lay on.</summary>
    static (List<(string, int)>, int) Dragon(string kind, string colour, Rng rng)
    {
        var items = new List<(string, int)> { ($"dragon_scale_{colour}", 4 + rng.Next(4)), ("dragon_bone", 2 + rng.Next(3)) };
        if (rng.Chance(0.6)) items.Add(("dragon_heart", 1));
        if (kind.EndsWith("_corrupt")) items.Add(("rift_crystal", 1 + rng.Next(2)));
        return (items, 150 + rng.Next(250));
    }

    /// <summary>What a lair's hoard holds besides its coin: its boss's trophy, some of what it's made of, and now and
    /// then (always, the first time) a piece of gear you can't get anywhere else.</summary>
    public static List<(string Id, int N)> Hoard(string raidId, Rng rng, bool first)
    {
        (string Trophy, (string, int)[] Stuff, string? Unique) h = raidId switch
        {
            "raid:ashen_crypt" => ("warden_skull", [("ember_ash", 4), ("ancient_coin", 4), ("bone_dust", 3)], "cinder_greataxe"),
            "raid:goblin_warren" => ("goblin_trinket", [("goblin_trinket", 4), ("scrap_iron", 6), ("iron_ingot", 2)], "goblin_crown"),
            "raid:rock_bear_den" => ("bear_plate", [("bear_pelt", 2), ("bear_claw", 4), ("leather", 2)], "bear_cloak"),
            "raid:briar_ruin" => ("briar_heart", [("heartwood", 3), ("ladys_mantle", 4), ("emberleaf", 4)], "kipine_cloak"),
            "raid:hollow_bastion" => ("hollow_badge", [("ectoplasm", 4), ("ancient_coin", 6), ("steel_ingot", 2)], "hollow_shield"),
            "raid:amethyst_hollow" => ("basilisk_heart", [("amethyst_shard", 6), ("steel_ingot", 2)], "amethyst_staff"),
            "raid:sunken_forge" => ("forge_core", [("steel_ingot", 5), ("iron_ingot", 6), ("coal", 10)], "steel_plate"),
            "raid:gloaming_ruin" => ("dusk_pelt", [("shadow_essence", 5), ("ancient_coin", 6)], "duskprowler_cloak"),
            "raid:storm_eyrie" => ("thunder_plume", [("storm_feather", 5), ("ancient_coin", 8)], "storm_staff"),
            _ => ("ancient_coin", [], null),
        };
        var items = new List<(string, int)> { (h.Trophy, 1) };
        items.AddRange(h.Stuff);
        items.Add(("healing_draught", 1 + rng.Next(2)));
        if (h.Unique is { } unique && (first || rng.Chance(0.25))) items.Add((unique, 1));
        return Merge(items);
    }

    static List<(string, int)> Merge(List<(string Id, int N)> items) =>
        [.. items.GroupBy(i => i.Id).Select(g => (g.Key, g.Sum(i => i.N)))];

    public static string Describe(IEnumerable<(string Id, int N)> items) => string.Join(", ", items.Select(i => Items.Count(i.Id, i.N)));
}

/// <summary>A shop: who keeps it, what they sell. Every shopkeeper buys anything at half what it's worth.</summary>
/// <param name="Kitchen">A tavern's kitchen, where a hero can cook.</param>
/// <param name="Trade">The trade it's the place for (a fishmonger's is fishing): its keeper talks about it.</param>
public sealed record Shop(string Id, string Name, string Keeper, string Greeting, string[] Sells, bool Forge = false, bool Bench = false, bool Beasts = false,
    bool Kitchen = false, Job? Trade = null, bool Workshop = false)
{
    public static int Price(Item item) => item.Value;
    public static int Offer(Item item) => Math.Max(1, item.Value / 2);
}

public static class Shops
{
    public static readonly Shop Provisions = new("shop:provisions", "Hollis's Provisions", "Hollis Brandt",
        "Bread, rope, leather, meat for your beasts. If it keeps you alive on the road, I sell it.",
        ["bread", "travel_rations", "ale", "meat", "linen", "leather", "coal", "healing_draught", "traveller_cloak", "alchemist_kit", "fishing_rod", "tinderbox", "spade", "snare"]);

    public static readonly Shop Smithy = new("shop:smithy", "The Anvil Gate", "Hulda Ironhand",
        "Steel's honest. People aren't. What do you need hitting, or hitting you less?",
        ["iron_ingot", "steel_ingot", "coal", "pine_log", "leather", "iron_dagger", "iron_sword", "iron_axe", "iron_hammer", "iron_mace", "iron_greatsword", "iron_greataxe",
         "hunting_crossbow", "wooden_shield", "iron_shield", "leather_cap", "iron_helm", "horned_hat", "leather_armor", "iron_mail", "knight_cape"],
        Forge: true);

    public static readonly Shop Apothecary = new("shop:apothecary", "Saffron's Remedies", "Old Saffron",
        "Herbs, tinctures, wands for the learned. Bring me what you pick in the wild, and I'll pay for it.",
        ["healing_draught", "mana_tonic", "stamina_brew", "alchemist_kit", "emberleaf", "ladys_mantle", "bluebell",
         "oak_wand", "apprentice_staff", "spellbook", "wizard_hat"],
        Bench: true);

    /// <summary>Young beasts, raised tame (see <see cref="Gameplay.Menagerie"/>), and meat to feed them.</summary>
    public static readonly Shop Menagerie = new("shop:menagerie", "Wren's Menagerie", "Wren Halloway",
        "Every one of them raised by my own hand, and every one of them tame. Mostly. No dragons: nobody sells dragons.",
        ["meat", "snare", "leather"], Beasts: true);

    /// <summary>Bryn's stall by the east gate: rods, and the morning's catch. Bryn buys any fish.</summary>
    public static readonly Shop Fishmonger = new("shop:fishmonger", "Bryn's Fish Stall", "Bryn Saltmarsh",
        "Herring, mackerel, cod, whatever the boats brought. Or catch your own: rods are twelve crowns, and the sea's free.",
        ["fishing_rod", "herring", "mackerel", "cod", "grilled_fish", "smoked_eel"], Trade: Job.Fishing);

    /// <summary>The Greyhollow quarry and mine, west of the walls: picks, stone and ore.</summary>
    public static readonly Shop Quarry = new("shop:quarry", "Greyhollow Quarry", "Dagna Flint",
        "Stone for the walls, ore for the smelters, coal for everyone. Grab a pick and swing it, if you've the arms for it.",
        ["pickaxe", "stone_block", "iron_ore", "coal", "tinderbox", "bread", "healing_draught"], Trade: Job.Mining);

    /// <summary>Hollin's woodlot, south-west of the walls: axes and timber.</summary>
    public static readonly Shop Woodlot = new("shop:woodlot", "Hollin's Woodlot", "Hollin Ashby",
        "Pine for the fires, oak for the ships, and a yew or two for the bowyers. Fell what's marked, and I'll buy every log.",
        ["woodcutters_axe", "pine_log", "oak_log", "bread", "tinderbox", "traveller_cloak"], Trade: Job.Woodcutting);

    /// <summary>The Salt Barrel's kitchen, down by the south road: ale, hot food, and a hearth a hero can cook at.</summary>
    public static readonly Shop SaltBarrel = new("shop:salt_barrel", "The Salt Barrel", "Fenna Rook",
        "Sit down before you fall down. Ale's four, stew's eighteen, and if you've caught something, the hearth's yours.",
        ["ale", "bread", "grilled_fish", "fish_stew", "roast_venison", "vegetable_stew", "roast_bird"], Kitchen: true, Trade: Job.Cooking);

    /// <summary>The allotments south of the walls: seeds, and a plot to grow them in.</summary>
    public static readonly Shop Allotments = new("shop:allotments", "The Commons Allotments", "Gilly Thatch",
        "Seed potatoes, onion sets, cabbage seed; herb seed for them with the knack. Pick a bed, any bed, and mind the slugs.",
        ["potato_seed", "onion_seed", "cabbage_seed", "emberleaf_seed", "bluebell_seed", "bloodcap_spore", "frostbloom_seed", "silverthorn_seed", "potato", "onion", "cabbage"],
        Trade: Job.Farming);

    /// <summary>The jeweller by the Exchange, with a bench and a loom anyone can use.</summary>
    public static readonly Shop Jeweller = new("shop:jeweller", "Brightwater's Jewels", "Ilka Brightwater",
        "Rings, amulets, a pearl or two. Bring me silver and gold and I'll lend you my bench; there's a loom in the back for linen.",
        ["silver_ring", "silver_amulet", "linen", "linen_tunic"], Trade: Job.Crafting, Workshop: true);

    /// <summary>The keeper of the Tumblers' Run, the obstacle course west of the walls.</summary>
    public static readonly Shop Course = new("shop:course", "The Tumblers' Run", "Cole Sparrow",
        "Log, net, rope, stones, wall, slide. In that order, and don't stop. Every lap makes you quicker.",
        ["stamina_brew", "travel_rations", "bread", "healing_draught", "traveller_cloak"], Trade: Job.Agility);

    public static IReadOnlyList<Shop> All { get; } = [Provisions, Smithy, Apothecary, Menagerie, Fishmonger, Quarry, Woodlot, SaltBarrel, Allotments, Jeweller, Course];

    /// <summary>A price with a buyer's markup (never less than 1 crown).</summary>
    public static int Charge(int price, double markup) => price < 0 ? price : Math.Max(1, (int)Math.Round(price * markup));

    /// <summary>Buys one of something: the crowns go, the item comes. Meat goes to the beasts' store, not the pack.</summary>
    /// <param name="markup">What this buyer pays, as a share of the price (their people and blood: see <see cref="Races.Prices"/>).</param>
    public static bool Buy(Shop shop, string id, Pack pack, ref int crowns, ref int meat, double markup = 1)
    {
        if (!shop.Sells.Contains(id)) return false;
        var price = Charge(id == "meat" ? MeatPrice : Items.Get(id) is { } item ? Shop.Price(item) : -1, markup);
        if (price < 0 || crowns < price) return false;
        crowns -= price;
        if (id == "meat") meat++;
        else pack.Add(id);
        return true;
    }

    public const int MeatPrice = 4;

    /// <summary>Sells one of something from the pack.</summary>
    public static bool Sell(string id, Pack pack, ref int crowns)
    {
        if (Items.Get(id) is not { } item || !pack.Take(id)) return false;
        crowns += Shop.Offer(item);
        return true;
    }
}
