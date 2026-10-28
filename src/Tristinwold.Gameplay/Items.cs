namespace Tristinwold.Gameplay;

/// <summary>What sort of thing an item is.</summary>
public enum ItemKind { Material, Herb, Potion, Food, Gear, Trophy, Tool }

/// <summary>Where a piece of gear is worn.</summary>
public enum Slot { None, Weapon, Offhand, Head, Body, Cloak }

/// <summary>How a weapon is held, which decides how its wielder swings it.</summary>
public enum Grip { None, OneHand, TwoHand, Ranged, Caster }

/// <summary>What eating or drinking something does: health, magicka and stamina back (as shares of the whole), and
/// for a while, more might (a share more damage) or a ward (more armour).</summary>
public sealed record Effect(double Health = 0, double Magicka = 0, double Stamina = 0, double Might = 0, double Ward = 0, double Seconds = 0);

/// <summary>
/// Something a hero can carry: a pelt, a herb, a potion, a sword. Gear is worn in a slot, makes the hero stronger,
/// and shows on them: <see cref="Look"/> names the piece of a KayKit model it borrows ("Knight/1H_Sword"), or a
/// piece grown for it ("graft:crown"), and <see cref="Tint"/> paints it (body armour paints the hero's body).
/// </summary>
public sealed record Item(string Id, string Name, ItemKind Kind, int Value, string Text)
{
    public Slot Slot { get; init; }
    public Grip Grip { get; init; }
    /// <summary>A weapon's bonus to all the damage its wielder does (0.15 = 15% more).</summary>
    public double Damage { get; init; }
    public double Armor { get; init; }
    public string? Look { get; init; }
    public string? Tint { get; init; }
    public bool Metal { get; init; }
    /// <summary>A glow in the piece (a cinder axe's embers, a shadow cloak's violet).</summary>
    public string? Glow { get; init; }
    public Effect? Use { get; init; }

    public bool Wearable => Slot != Slot.None;
    public bool Usable => Use is not null;
}

/// <summary>Everything there is to carry, find, make and buy.</summary>
public static class Items
{
    static Item Mat(string id, string name, int value, string text) => new(id, name, ItemKind.Material, value, text);
    static Item Herb(string id, string name, int value, string text) => new(id, name, ItemKind.Herb, value, text);
    static Item Trophy(string id, string name, int value, string text) => new(id, name, ItemKind.Trophy, value, text);
    static Item Fish(string id, string name, int value, string text) => new(id, name, ItemKind.Material, value, text);

    /// <summary>The ten dragon colours, for scales and the armour made from them.</summary>
    public static readonly (string Id, string Name, string Tint)[] DragonColours =
    [
        ("red", "Red", "#a8281c"), ("blue", "Blue", "#2a4ab0"), ("green", "Green", "#2f7a2a"), ("black", "Black", "#2a2430"),
        ("white", "White", "#dfe6ee"), ("gold", "Gold", "#d8a030"), ("silver", "Silver", "#b8bcc4"), ("bronze", "Bronze", "#8c5a2a"),
        ("copper", "Copper", "#b86434"), ("brass", "Brass", "#c09a44"),
    ];

    public static IReadOnlyList<Item> All { get; } =
    [
        // ---- what beasts and monsters leave behind ---------------------------------------------------------
        Mat("wolf_pelt", "Wolf Pelt", 8, "Grey fur, thick for winter. A tanner turns it to leather; a smith to a cloak."),
        Mat("frost_pelt", "Frost Wolf Pelt", 16, "White fur that stays cold to the touch."),
        Mat("wolf_fang", "Wolf Fang", 4, "Long and yellow."),
        Mat("bear_pelt", "Bear Pelt", 22, "A whole bearskin, heavy as a man."),
        Mat("bear_claw", "Bear Claw", 9, "Longer than your finger."),
        Mat("boar_hide", "Boar Hide", 7, "Bristly and tough. Good leather."),
        Mat("boar_tusk", "Boar Tusk", 6, "Curved, sharp, yellowed."),
        Mat("deer_hide", "Deer Hide", 6, "Soft hide. Good leather."),
        Mat("antler", "Antler", 9, "A good spread of tines."),
        Mat("fox_pelt", "Fox Pelt", 11, "Red fur, prized for trim."),
        Mat("skeever_tail", "Skeever Tail", 1, "Nobody wants this."),
        Mat("crab_chitin", "Mudcrab Chitin", 4, "A plate of shell."),
        Mat("goblin_trinket", "Goblin Trinket", 6, "Bits of string, bone and stolen silver."),
        Mat("scrap_iron", "Scrap Iron", 3, "Rusty bits. A smith can smelt three into an ingot."),
        Mat("bone_dust", "Bone Dust", 3, "Ground from the walking dead."),
        Mat("ancient_coin", "Ancient Coin", 12, "Minted for kings nobody remembers."),
        Mat("heartwood", "Heartwood", 10, "From a spriggan's core. It still feels warm."),
        Mat("troll_fat", "Troll Fat", 15, "Rank and greasy, and it makes strong potions."),
        Mat("troll_hide", "Troll Hide", 24, "Thick as a door."),
        Mat("kipine_feather", "Kipine Feather", 18, "Long, smoke-grey, soft as owl down."),
        Mat("deathcrest_horn", "Deathcrest Horn", 16, "Ridged, banded light and dark."),
        Mat("ape_hide", "Ape Hide", 12, "Striped hide. Tans to fine leather."),
        Mat("imp_horn", "Imp Horn", 6, "Small, hot to the touch."),
        Mat("brimstone", "Brimstone", 9, "Yellow, stinking, it burns blue."),
        Mat("devil_hide", "Devil Hide", 32, "Red-brown, leathery, and it doesn't burn."),
        Mat("black_feather", "Black Feather", 10, "From a winged ape's wing."),
        Mat("storm_feather", "Storm Feather", 26, "Blue, and it crackles when you stroke it."),
        Mat("amethyst_shard", "Amethyst Shard", 20, "Violet crystal that hums."),
        Mat("shadow_essence", "Shadow Essence", 22, "A wisp of dark that won't stay in the jar."),
        Mat("ectoplasm", "Ectoplasm", 15, "What's left of a ghost. Cold."),
        Mat("ember_ash", "Ember Ash", 6, "Still glowing."),
        // ---- dragons -----------------------------------------------------------------------------------
        .. DragonColours.Select(c => Mat($"dragon_scale_{c.Id}", $"{c.Name} Dragon Scale", 45, $"A {c.Name.ToLowerInvariant()} dragon's scale, as big as a hand and harder than steel.")),
        Mat("dragon_bone", "Dragon Bone", 60, "Light, and stronger than any steel."),
        Trophy("dragon_heart", "Dragon Heart", 250, "Still warm. Worth a fortune to the right buyer."),
        Mat("rift_crystal", "Rift Crystal", 120, "Violet, veined with gold, and it whispers."),
        // ---- the smith's stock ---------------------------------------------------------------------------
        Mat("iron_ingot", "Iron Ingot", 12, "For the forge."),
        Mat("steel_ingot", "Steel Ingot", 26, "Iron and coal, folded. For better work."),
        Mat("coal", "Coal", 2, "For smelting steel."),
        Mat("leather", "Leather", 10, "Tanned hide."),
        Mat("linen", "Linen", 4, "A bolt of cloth."),
        // ---- herbs of the wilds ------------------------------------------------------------------------
        Herb("emberleaf", "Emberleaf", 4, "Red leaves that tingle on the tongue. The heart of a healing draught."),
        Herb("ladys_mantle", "Lady's Mantle", 5, "Soft green leaves that close wounds."),
        Herb("bloodcap", "Bloodcap", 6, "A red mushroom. Strengthens, in small doses."),
        Herb("bluebell", "Bluebell", 4, "Blue flowers that clear the head. Restores magicka."),
        Herb("ghostcap", "Ghostcap", 8, "A pale mushroom that glows faintly at night."),
        Herb("marsh_reed", "Marsh Reed", 3, "Hollow reeds, sweet inside. Restores stamina."),
        Herb("nightshade", "Nightshade", 7, "Dark berries. Poison, unless you know what you're doing."),
        Herb("snowberry", "Snowberry", 5, "White berries from the cold north."),
        Herb("frostbloom", "Frostbloom", 9, "An icy blue flower. Wards against harm."),
        Herb("silverthorn", "Silverthorn", 8, "A thorny shrub with silver leaves. Steels the body."),
        Herb("thistle", "Mountain Thistle", 4, "Purple and prickly. Restores stamina."),
        // ---- potions and food --------------------------------------------------------------------------
        new("healing_draught", "Healing Draught", ItemKind.Potion, 30, "Heals half your wounds.") { Use = new Effect(Health: 0.5) },
        new("greater_healing", "Greater Healing", ItemKind.Potion, 70, "Heals you completely.") { Use = new Effect(Health: 1) },
        new("magicka_tonic", "Magicka Tonic", ItemKind.Potion, 30, "Restores half your magicka.") { Use = new Effect(Magicka: 0.5) },
        new("stamina_brew", "Stamina Brew", ItemKind.Potion, 25, "Restores all your stamina.") { Use = new Effect(Stamina: 1) },
        new("elixir_of_might", "Elixir of Might", ItemKind.Potion, 80, "A quarter more damage for two minutes.") { Use = new Effect(Might: 0.25, Seconds: 120) },
        new("warding_draught", "Warding Draught", ItemKind.Potion, 70, "Armour like plate for two minutes.") { Use = new Effect(Ward: 60, Seconds: 120) },
        new("bread", "Bread", ItemKind.Food, 3, "Heals a little.") { Use = new Effect(Health: 0.1, Stamina: 0.2) },
        new("ale", "Ale", ItemKind.Food, 4, "A tankard of brown ale. Restores some stamina.") { Use = new Effect(Stamina: 0.3) },
        new("travel_rations", "Travel Rations", ItemKind.Food, 8, "Dried meat, hard cheese, an apple. Heals a fair bit.") { Use = new Effect(Health: 0.25, Stamina: 0.5) },
        new("alchemist_kit", "Alchemist's Kit", ItemKind.Tool, 40, "Mortar, pestle, little bottles. Lets you brew potions anywhere (from your pack, I)."),
        // ---- tools of the town's trades ----------------------------------------------------------------
        new("fishing_rod", "Fishing Rod", ItemKind.Tool, 12, "Ash rod, horsehair line, a bone hook. Face open water and press E to cast."),
        new("pickaxe", "Pickaxe", ItemKind.Tool, 18, "For ore veins. Walk up to one and press E."),
        new("woodcutters_axe", "Woodcutter's Axe", ItemKind.Tool, 15, "A broad, heavy bit. For felling trees at a woodlot."),
        // ---- fish (raw: cook them, or sell them) ---------------------------------------------------------
        Fish("herring", "Herring", 4, "Silver and slippery. The harbour's bread and butter."),
        Fish("mackerel", "Mackerel", 6, "Striped blue-green, oily and good grilled."),
        Fish("cod", "Cod", 8, "Big, white-fleshed, a fisherman's staple."),
        Fish("sea_bass", "Sea Bass", 11, "A fine fish; the Crown's cooks pay well for it."),
        Fish("eel", "Eel", 9, "It doesn't want to be caught, and it doesn't want to be cooked."),
        Fish("lobster", "Lobster", 18, "Blue-black and furious."),
        Fish("salmon", "Salmon", 16, "Pink-fleshed and strong; it fought all the way in."),
        Fish("perch", "Perch", 4, "A little striped river fish."),
        Fish("trout", "Trout", 7, "Speckled, from cold clear water."),
        Fish("carp", "Carp", 6, "Bronze and bony, from still water."),
        Fish("pike", "Pike", 12, "All teeth. Mind your fingers."),
        Fish("golden_carp", "Golden Carp", 45, "A carp the colour of a crown. They say it brings luck."),
        Fish("arctic_char", "Arctic Char", 10, "Red-bellied, from icy northern water."),
        Fish("ice_pike", "Ice Pike", 17, "Pale as frost, and just as cold to hold."),
        Mat("pearl", "Pearl", 45, "A pearl from a harbour oyster, pale and perfect."),
        Mat("old_boot", "Old Boot", 1, "Somebody's boot. Somebody's foot isn't in it, thankfully."),
        // ---- the quarry and the mines --------------------------------------------------------------------
        Mat("iron_ore", "Iron Ore", 4, "Rust-red rock. A forge smelts two into an iron ingot."),
        Mat("silver_ore", "Silver Ore", 10, "Grey rock threaded with silver. Two smelt to a silver ingot."),
        Mat("gold_ore", "Gold Ore", 18, "Rock with a yellow gleam. Two smelt to a gold ingot."),
        Mat("silver_ingot", "Silver Ingot", 28, "Bright, soft, and much wanted by jewellers."),
        Mat("gold_ingot", "Gold Ingot", 50, "Heavy and warm-coloured. The Crown mints these."),
        Mat("rough_garnet", "Rough Garnet", 25, "A dark red stone, uncut."),
        Mat("rough_sapphire", "Rough Sapphire", 45, "A deep blue stone, uncut."),
        Mat("stone_block", "Stone Block", 2, "Squared grey stone. The city always needs more."),
        // ---- the woodlot ---------------------------------------------------------------------------------
        Mat("pine_log", "Pine Log", 4, "Straight, light and full of sap."),
        Mat("oak_log", "Oak Log", 7, "Heavy, hard and slow-grown: shipwrights want it."),
        Mat("yew_log", "Yew Log", 14, "Red-hearted and springy. Bowyers prize it."),
        // ---- the kitchen ---------------------------------------------------------------------------------
        Mat("venison", "Venison", 5, "Raw meat from deer or boar. Cook it."),
        new("grilled_fish", "Grilled Fish", ItemKind.Food, 8, "Charred skin, flaky inside. Heals a fair bit and restores stamina.") { Use = new Effect(Health: 0.25, Stamina: 0.4) },
        new("fish_stew", "Fish Stew", ItemKind.Food, 18, "A thick harbour stew. Heals well, and you're harder to hurt for two minutes.") { Use = new Effect(Health: 0.4, Stamina: 0.3, Ward: 12, Seconds: 120) },
        new("roast_venison", "Roast Venison", ItemKind.Food, 14, "Rich and filling. Heals well, and your blows land harder for two minutes.") { Use = new Effect(Health: 0.35, Stamina: 0.3, Might: 0.08, Seconds: 120) },
        new("hunters_pie", "Hunter's Pie", ItemKind.Food, 24, "Venison and gravy under a crust. Heals most of your wounds.") { Use = new Effect(Health: 0.6, Stamina: 0.6) },
        new("seafood_platter", "Seafood Platter", ItemKind.Food, 45, "Lobster, salmon and eel, fit for the Crown. Heals you fully and fires you up.") { Use = new Effect(Health: 1, Stamina: 1, Magicka: 0.5, Might: 0.12, Seconds: 180) },
        new("smoked_eel", "Smoked Eel", ItemKind.Food, 15, "Keeps for weeks. Heals and restores magicka.") { Use = new Effect(Health: 0.3, Magicka: 0.4) },
        // ---- weapons ----------------------------------------------------------------------------------
        Gear("iron_sword", "Iron Sword", 40, Slot.Weapon, Grip.OneHand, "Knight/1H_Sword", damage: 0.1),
        Gear("steel_sword", "Steel Sword", 110, Slot.Weapon, Grip.OneHand, "Knight/1H_Sword", damage: 0.2, tint: "#e4ecf4", metal: true),
        Gear("dragonbone_sword", "Dragonbone Sword", 420, Slot.Weapon, Grip.OneHand, "Knight/1H_Sword", damage: 0.36, tint: "#efe4c8"),
        Gear("iron_greatsword", "Iron Greatsword", 60, Slot.Weapon, Grip.TwoHand, "Knight/2H_Sword", damage: 0.16),
        Gear("steel_greatsword", "Steel Greatsword", 160, Slot.Weapon, Grip.TwoHand, "Knight/2H_Sword", damage: 0.28, tint: "#e4ecf4", metal: true),
        Gear("dragonbone_greatsword", "Dragonbone Greatsword", 560, Slot.Weapon, Grip.TwoHand, "Knight/2H_Sword", damage: 0.46, tint: "#efe4c8"),
        Gear("iron_axe", "Iron Axe", 40, Slot.Weapon, Grip.OneHand, "Barbarian/1H_Axe", damage: 0.12),
        Gear("steel_axe", "Steel Axe", 110, Slot.Weapon, Grip.OneHand, "Barbarian/1H_Axe", damage: 0.22, tint: "#e4ecf4", metal: true),
        Gear("iron_greataxe", "Iron Greataxe", 60, Slot.Weapon, Grip.TwoHand, "Barbarian/2H_Axe", damage: 0.18),
        Gear("steel_greataxe", "Steel Greataxe", 160, Slot.Weapon, Grip.TwoHand, "Barbarian/2H_Axe", damage: 0.3, tint: "#e4ecf4", metal: true),
        Gear("cinder_greataxe", "The Warden's Cinder Axe", 600, Slot.Weapon, Grip.TwoHand, "Barbarian/2H_Axe", damage: 0.42, tint: "#2a2018", glow: "#ff6a20"),
        Gear("iron_dagger", "Iron Dagger", 25, Slot.Weapon, Grip.OneHand, "Rogue/Knife", damage: 0.08),
        Gear("steel_dagger", "Steel Dagger", 80, Slot.Weapon, Grip.OneHand, "Rogue/Knife", damage: 0.16, tint: "#e4ecf4", metal: true),
        Gear("shadow_dagger", "Duskfang", 520, Slot.Weapon, Grip.OneHand, "Rogue/Knife", damage: 0.34, tint: "#1a1424", glow: "#a050ff"),
        Gear("hunting_crossbow", "Hunting Crossbow", 50, Slot.Weapon, Grip.Ranged, "Rogue/1H_Crossbow", damage: 0.1),
        Gear("heavy_crossbow", "Heavy Crossbow", 150, Slot.Weapon, Grip.Ranged, "Rogue/2H_Crossbow", damage: 0.24),
        Gear("oak_wand", "Oak Wand", 35, Slot.Weapon, Grip.Caster, "Mage/1H_Wand", damage: 0.08),
        Gear("apprentice_staff", "Apprentice's Staff", 60, Slot.Weapon, Grip.Caster, "Mage/2H_Staff", damage: 0.14),
        Gear("amethyst_staff", "Amethyst Staff", 380, Slot.Weapon, Grip.Caster, "Mage/2H_Staff", damage: 0.32, tint: "#9a6ad8", glow: "#9a50ff"),
        Gear("storm_staff", "Stormcaller's Staff", 560, Slot.Weapon, Grip.Caster, "Mage/2H_Staff", damage: 0.4, tint: "#6aa0f0", glow: "#8ad0ff"),
        // ---- shields and off-hands ------------------------------------------------------------------------
        Gear("wooden_shield", "Wooden Shield", 20, Slot.Offhand, Grip.None, "Barbarian/Barbarian_Round_Shield", armor: 10),
        Gear("iron_shield", "Iron Shield", 50, Slot.Offhand, Grip.None, "Knight/Round_Shield", armor: 18),
        Gear("steel_shield", "Steel Tower Shield", 130, Slot.Offhand, Grip.None, "Knight/Rectangle_Shield", armor: 30, tint: "#e4ecf4", metal: true),
        Gear("spiked_shield", "Spiked Shield", 120, Slot.Offhand, Grip.None, "Knight/Spike_Shield", armor: 24, damage: 0.05),
        Gear("hollow_shield", "Shield of the Hollow Legion", 480, Slot.Offhand, Grip.None, "Knight/Badge_Shield", armor: 44, tint: "#a8e0f0", glow: "#6ac8ff"),
        Gear("spellbook", "Spellbook", 60, Slot.Offhand, Grip.None, "Mage/Spellbook_open", damage: 0.08),
        Gear("parrying_dagger", "Parrying Dagger", 30, Slot.Offhand, Grip.None, "Rogue/Knife_Offhand", damage: 0.05, armor: 4),
        // ---- helmets and hats -------------------------------------------------------------------------
        Gear("iron_helm", "Iron Helm", 40, Slot.Head, Grip.None, "Knight/Knight_Helmet", armor: 12),
        Gear("steel_helm", "Steel Helm", 100, Slot.Head, Grip.None, "Knight/Knight_Helmet", armor: 20, tint: "#e4ecf4", metal: true),
        Gear("horned_hat", "Horned Fur Hat", 30, Slot.Head, Grip.None, "Barbarian/Barbarian_Hat", armor: 8),
        Gear("wizard_hat", "Wizard's Hat", 40, Slot.Head, Grip.None, "Mage/Mage_Hat", damage: 0.05, armor: 2),
        Gear("goblin_crown", "The Goblin King's Crown", 400, Slot.Head, Grip.None, "graft:crown", armor: 14, damage: 0.06, tint: "#e0b040", metal: true),
        // ---- body armour (it paints the body) -------------------------------------------------------------
        Gear("leather_armor", "Leather Armour", 50, Slot.Body, Grip.None, null, armor: 14, tint: "#8a6a4a"),
        Gear("fur_armor", "Fur Armour", 70, Slot.Body, Grip.None, null, armor: 18, tint: "#a8a29a"),
        Gear("ape_leather_armor", "Striped Leathers", 160, Slot.Body, Grip.None, null, armor: 26, tint: "#c8c0b4"),
        Gear("iron_mail", "Iron Mail", 90, Slot.Body, Grip.None, null, armor: 26, tint: "#9aa0a8", metal: true),
        Gear("steel_plate", "Steel Plate", 220, Slot.Body, Grip.None, null, armor: 40, tint: "#d4dae2", metal: true),
        Gear("devilhide_armor", "Devilhide Armour", 360, Slot.Body, Grip.None, null, armor: 44, tint: "#8a3a22", glow: "#ff5a20"),
        .. DragonColours.Select(c => Gear($"dragonscale_armor_{c.Id}", $"{c.Name} Dragonscale Armour", 800, Slot.Body, Grip.None, null, armor: 62, tint: c.Tint, metal: true)),
        .. DragonColours.Select(c => Gear($"dragonscale_helm_{c.Id}", $"{c.Name} Dragonscale Helm", 360, Slot.Head, Grip.None, "Knight/Knight_Helmet", armor: 28, tint: c.Tint, metal: true)),
        // ---- cloaks ------------------------------------------------------------------------------------
        Gear("traveller_cloak", "Traveller's Cloak", 20, Slot.Cloak, Grip.None, "Rogue/Rogue_Cape", armor: 2, tint: "#6a5a46"),
        Gear("wolf_cloak", "Wolf-Fur Cloak", 45, Slot.Cloak, Grip.None, "Barbarian/Barbarian_Cape", armor: 6, tint: "#a8a8a4"),
        Gear("bear_cloak", "Bearskin Cloak", 70, Slot.Cloak, Grip.None, "Barbarian/Barbarian_Cape", armor: 9, tint: "#5a3e2a"),
        Gear("knight_cape", "Knight's Cape", 60, Slot.Cloak, Grip.None, "Knight/Knight_Cape", armor: 4, tint: "#a02828"),
        Gear("kipine_cloak", "Kipine-Feather Cloak", 240, Slot.Cloak, Grip.None, "Mage/Mage_Cape", armor: 12, damage: 0.04, tint: "#6a5e56"),
        Gear("duskprowler_cloak", "Duskprowler's Mantle", 520, Slot.Cloak, Grip.None, "Rogue/Rogue_Cape", armor: 16, damage: 0.08, tint: "#1a1424", glow: "#a050ff"),
        // ---- trophies of the lairs -------------------------------------------------------------------------
        Trophy("warden_skull", "The Cinder Warden's Skull", 220, "Still smouldering."),
        Trophy("bear_plate", "Rock Bear's Stone Plate", 180, "A plate of rock that grew on a living bear."),
        Trophy("briar_heart", "Heart of the Briar", 200, "A red rose that never wilts."),
        Trophy("hollow_badge", "Badge of the Hollow Legion", 210, "A captain's badge. Cold as the grave."),
        Trophy("basilisk_heart", "Crystal Heart", 260, "Amethyst, beating faintly."),
        Trophy("forge_core", "Forge Core", 280, "The golem's furnace heart, still hot."),
        Trophy("dusk_pelt", "Duskprowler's Pelt", 300, "Black fur that drinks the light."),
        Trophy("thunder_plume", "Thunderhawk's Plume", 320, "It crackles. Your hair stands on end."),
    ];

    static Item Gear(string id, string name, int value, Slot slot, Grip grip, string? look, double damage = 0, double armor = 0,
        string? tint = null, bool metal = false, string? glow = null) =>
        new(id, name, ItemKind.Gear, value, Describe(slot, grip, damage, armor))
        { Slot = slot, Grip = grip, Look = look, Damage = damage, Armor = armor, Tint = tint, Metal = metal, Glow = glow };

    static string Describe(Slot slot, Grip grip, double damage, double armor)
    {
        var what = slot switch
        {
            Slot.Weapon => grip switch { Grip.TwoHand => "A two-handed weapon.", Grip.Ranged => "A ranged weapon.", Grip.Caster => "A caster's focus.", _ => "A one-handed weapon." },
            Slot.Offhand => "For the off hand.", Slot.Head => "Worn on the head.", Slot.Body => "Body armour.", _ => "A cloak.",
        };
        var stats = new List<string>();
        if (damage > 0) stats.Add($"+{damage:P0} damage");
        if (armor > 0) stats.Add($"+{armor:0} armour");
        return stats.Count > 0 ? $"{what} {string.Join(", ", stats)}." : what;
    }

    static readonly Dictionary<string, Item> ById = All.ToDictionary(i => i.Id);

    public static Item? Get(string id) => ById.GetValueOrDefault(id);

    /// <summary>"2 Wolf Pelts", "Wolf Fang".</summary>
    public static string Count(string id, int n)
    {
        var name = Get(id)?.Name ?? id;
        return n == 1 ? name : $"{n} {Plural(name)}";
    }

    /// <summary>Things you count by the lump or the bolt, not one by one.</summary>
    static readonly string[] Uncounted =
    [
        "Ash", "Dust", "Fat", "Leather", "Linen", "Iron", "Coal", "Essence", "Ectoplasm", "Brimstone", "Heartwood", "Chitin", "Bread",
        "Rations", "Armour", "Mail", "Plate", "Leathers", "Mantle", "Healing",
    ];

    static string Plural(string name) =>
        Uncounted.Any(name.EndsWith) ? name
        : name.EndsWith("sh") || name.EndsWith("ch") || name.EndsWith('s') ? name + "es"
        : name.EndsWith('y') && !name.EndsWith("ey") ? name[..^1] + "ies"
        : name + "s";
}
