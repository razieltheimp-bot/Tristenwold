using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>What people know a hero as: from a villain to a hero of the land.</summary>
public enum Repute { Villain, Rough, Unknown, Decent, Trusted, Hero }

/// <summary>What sort of thing an item is.</summary>
public enum ItemKind { Material, Herb, Potion, Food, Gear, Trophy, Tool }

/// <summary>Where a piece of gear is worn.</summary>
public enum Slot { None, Weapon, Offhand, Head, Body, Cloak, Ring, Neck }

/// <summary>How a weapon is held, which decides how its wielder swings it.</summary>
public enum Grip { None, OneHand, TwoHand, Ranged, Caster }

/// <summary>What eating or drinking something does: health, mana and stamina back (as shares of the whole), and
/// for a while, more might (a share more damage) or a ward (more armour).</summary>
public sealed record Effect(double Health = 0, double Mana = 0, double Stamina = 0, double Might = 0, double Ward = 0, double Seconds = 0);

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

    // What only forged pieces have (see Forging): a share more speed and health, points of critical chance, and a fire.
    public double Speed { get; init; }
    public double Crit { get; init; }
    public double Health { get; init; }
    public DamageKind? Fire { get; init; }
    /// <summary>A forged weapon's plus to hit (points off the chance a blow glances), and flat damage added to every blow.</summary>
    public int ToHit { get; init; }
    /// <summary>The die a forged weapon's fire rolls on every blow: 6, 8, 10 or 12, or 0 if it has none.</summary>
    public int FireDie { get; init; }
    /// <summary>The abilities it came out with, by what they add to (see Forging.Abilities).</summary>
    public IReadOnlyDictionary<string, double>? Powers { get; init; }
    public int Power { get; init; }
    public bool Forged { get; init; }
    /// <summary>One of a kind, and it stays with who earned it: no shop or scrapper will take it.</summary>
    public bool Bound { get; init; }
    public ForgeGrade? Grade { get; init; }
    public string[]? Parts { get; init; }
    // How it shows: the blade's length and width, and the colours of the parts added to it.
    public float Length { get; init; } = 1;
    public float Width { get; init; } = 1;
    public float GuardWidth { get; init; }
    public string? GuardTint { get; init; }
    public string? PommelTint { get; init; }
    public string? GripTint { get; init; }

    public bool Wearable => Slot != Slot.None;
    public bool Usable => Use is not null;
}

/// <summary>What's left of Soren this time: how far the sea has got into him (0 to 1), and how he answers now.</summary>
public sealed record SorenForm(string Is, float Size, float Sea, string Hail, string Retreat, string Lost);

/// <summary>What's left of Lu Wukong this time: how far the beast has got into him (0 to 1), and how he talks now.</summary>
public sealed record WukongForm(string Is, float Size, float Beast, string Hail, string Offer, string Retreat, string Lost);

/// <summary>The two great blades, what it takes to get them, and the man who won't stay dead.</summary>
public static class Legend
{
    /// <summary>The chance the black blade drops when Soren's killed, and what he leaves otherwise.</summary>
    public const double WebChance = 0.015;
    /// <summary>At this share of his blood he goes for his ship, and you've this long to bring him down.</summary>
    public const double SorenFleeAt = 0.25, SorenFlightSeconds = 8;
    /// <summary>He's stronger every time he comes back, up to a point.</summary>
    public const double SorenPerDeath = 0.2, SorenMost = 2.2;
    /// <summary>How long between his hunts for whoever took his blade: seconds of play.</summary>
    public const double SorenHuntEvery = 30 * 60;

    /// <summary>He drowned once, long ago, and it didn't take. He keeps coming back from the sea, and it keeps a little more of him each time.</summary>
    public static readonly IReadOnlyList<SorenForm> SorenForms =
    [
        new("a huge man in a red coat, with a beard like a black storm", 1.2f, 0,
            "The Dread Pirate Soren walks up through the smoke with his pipe lit and a black blade at his hip. \"Yours, is it? It was. Stand aside, or don't. I've no quarrel with the dead, and that's what you are.\"",
            "\"Enough. You'll keep.\" A chain comes rattling over the wall and takes him round the waist, and his ship starts to haul.",
            "He looks down at the wound and takes the pipe from his mouth, and for the first time he looks his age. \"Well,\" he says. \"That's new.\" He goes down, and the tide comes up the street to fetch him."),
        new("a big man gone grey, barnacles at his collar", 1.28f, 0.25f,
            "Soren's back, and the coat's still wet. There are barnacles at his throat he doesn't seem to feel. \"I've been somewhere. I can't think where. It was very quiet. You'll pay for the quiet.\"",
            "\"Not yet. Not here.\" He says it to something that isn't you. The chain takes him.",
            "\"It's cold,\" he says, quite surprised. \"Why's it so...\" Then salt water runs out of his mouth, more than a man could hold."),
        new("something in a red coat, crusted with coral", 1.36f, 0.45f,
            "He comes up the quay with the tide behind him. He doesn't smoke now; he's forgotten what the pipe's for, and carries it anyway. His eyes have gone the flat black of deep water. \"Soren,\" he says, as if checking. \"Yes. Town. Burn it.\"",
            "He stops and tilts his head, listening to the water. Then he walks backward off the wall, and the chain does the rest.",
            "It doesn't say anything. It looks at the sea, the way a dog looks at a door."),
        new("a shape that was a man, with weed for a beard", 1.46f, 0.65f,
            "The smell gets there first. What's in the coat moves wrong now, too many joints in it, weed where the beard was, and when it opens its mouth the sound comes a moment after. \"Sooo. Ren.\" It raises the black blade, and it still knows how.",
            "It folds up, all at once, and goes over the wall like something being poured.",
            "It falls apart at the seams, and small things run out of it and make for the water."),
        new("a drowned thing, wearing the rags of a coat", 1.58f, 0.85f,
            "Nobody who sees it now would call it a pirate. It stands at the gate with the tide round its knees, on a street a mile from the shore. There's no word in what it says, only a long low sound, like a ship's timbers working in a swell.",
            "The sound stops. The water goes out of the street, and takes it along.",
            "It sinks into the stones, and the stones are wet for a week."),
        new("the sea, wearing a red coat", 1.7f, 1,
            "The sea has come to town, and it's wearing a red coat. It has no face to speak of, and doesn't need one. It remembers one thing: that this was all once its to take.",
            "It goes out, like a tide.",
            "It breaks, and runs away down the gutters, and every well in town tastes of salt. It will be back. The sea always is."),
    ];

    public static SorenForm FormOf(int deaths) => SorenForms[Math.Min(deaths, SorenForms.Count - 1)];
    public static double SorenStrength(int deaths) => Math.Min(SorenMost, 1 + deaths * SorenPerDeath);

    /// <summary>The chance Lu Wukong comes to a siege, and the chance it costs him his life if nobody's there to help him hold it.</summary>
    public const double LuComes = 1.0 / 3, LuFalls = 0.25;
    /// <summary>On a pirate's deck he doesn't stay to die: at this share of his blood he goes back over the rail, and you've this long to bring him down.</summary>
    public const double WukongFleeAt = 0.25, WukongFlightSeconds = 6;
    public const string WukongName = "Lu Wukong";
    /// <summary>What he hands one of his own after a siege held.</summary>
    public const int MonkeyShare = 300;

    /// <summary>
    /// The gourd at his hip holds the same spiked fruit Turlin eats, pressed and left to turn, and he's been drinking it for
    /// forty years. So he comes back too, and he knows it, and he thinks that's funny. But every death costs him a verse of
    /// every song he knows, and what comes in where the verses were is the ape. The last thing it can take is why he's there;
    /// it hasn't managed that yet. He doesn't remember Soren's name at the end, but he still turns up.
    /// </summary>
    public static readonly IReadOnlyList<WukongForm> WukongForms =
    [
        new("a hooded ape with a gourd at his hip, singing", 1, 0,
            "A patched sail comes round the headland with the whole crew singing, and an ape in a hood rides the chain down off her yard with a gourd in one hand and a staff in the other. \"Soren! You great wet disgrace! Who's robbing the poor now? I told you, that's my brother's job!\" Lu Wukong lands in the street and bows to the town. \"Evening. Don't get up.\"",
            "He drops onto a barrel and offers you the gourd, then thinks better of it and has it himself. \"I only rob pirates. It's the one honest trade left, and I've forty hands who think so too. Nobody burns a town while we're about, and when they try, we come. There's a berth on the Drunken Monkey, if you fight like that on purpose.\"",
            "He takes a long pull at the gourd, salutes you with it, and goes backward over the rail. \"Not today! I've a chorus to finish!\"",
            "He sits down, quite gently, looks at the gourd and shakes it to hear what's left. \"Ah,\" he says. \"That's the second verse gone.\" He laughs, and stops, and the gourd rolls out of his hand. When you look back, there's nothing there but the smell of old fruit."),
        new("a hooded ape, greyer than he was, who hums where the words should be", 1.06f, 0.2f,
            "The patched sail's back. So's the singing, though it stops in a strange place, and the crew carry on past it without him. Lu Wukong lands in the street a little heavier than he used to. \"Soren! I died, you know. It's terrible. Everyone should try it.\" He looks at his hand as if it had said something. \"Who's first?\"",
            "He offers you the gourd, and this time he means it, then takes it back. \"I had a whole speech for this. It rhymed. We only rob pirates, and we stand in the road when Soren comes. That's the short of it, and the short's all I've got. There's a berth, if you want one.\"",
            "He goes for the rail on all fours and doesn't seem to notice. \"Tell my brother!\" he shouts, and then, from the other deck, \"Tell him something!\"",
            "\"That's not funny,\" he says, in a small, puzzled voice, the first time you've heard him say so. Then the fruit smell, and nothing."),
        new("an ape the size of a door, in a hood that doesn't fit him now", 1.14f, 0.4f,
            "Something heavy comes down the chain and cracks the street. The hood's split at the seam. He takes a while to find his feet, and longer to find his grin, but it comes. \"Red coat. Bad. I know that one.\" He has a go at the song and gets four notes in. His crew finish it for him, very loudly, without looking at him.",
            "He holds the gourd out at arm's length and watches you, the way you'd watch a dog you weren't sure of. \"Good pirate,\" he says, and thumps his own chest, and then yours. \"Boat. Yes? Bad ones, we take. Good ones, no. You come.\" It isn't quite a question.",
            "He snatches up the staff and goes over the side in one long swing, and you hear him hit the water laughing, or something like it.",
            "He looks at you, and for a moment he's all there, and he looks sorry for you. Then he isn't. The gourd cracks by itself."),
        new("a great grey ape who drags his staff, and keeps the gourd in his teeth", 1.24f, 0.6f,
            "You hear the crew before you see the ship. They've got him chained to the yard by the ankle now, for his own sake, and they let him go when they see the red coat. He comes through the smoke on his knuckles with the gourd in his teeth and the staff dragging behind him, and he stops in front of the town, and turns round, and stands there.",
            "The first mate speaks for him now, a lean old woman with one hand on his arm. \"He won't know you tomorrow. He'll know you're ours, though. Ask me how, I couldn't tell you. There's a berth, if you'll have it. We rob pirates, and we stand in the road. He still does both.\" The ape looks at you. He holds out the gourd.",
            "The crew get a line on him and he lets them, grumbling, and goes back over the rail like a sack of wet sand.",
            "It takes four of them to get him off you, afterward, though he's already dead. His fist won't open. There's a tooth in it, and when you look again, there's only the tooth."),
        new("something the size of a hay cart, with a rag of hood on one shoulder", 1.36f, 0.8f,
            "The crew don't sing now. They haul in silence, and when the ship touches they open the rail and stand well back. It walks up the quay upright for the first few steps, the way it must remember walking. Then it sees the red coat, and every window on the street rattles.",
            "It doesn't look at you. The old woman with one hand does. \"Nobody told him to come. We don't tell him anything. He wakes up, and he goes to where the fire is. The Monkey goes with him, and robs what it can on the way. There's a berth, if you've the stomach for it.\"",
            "It stops, and sniffs, and turns its back on you, and walks into the sea as if it had remembered something, and the crew go after it with the boats.",
            "It goes over slowly, a limb at a time, facing the town. It doesn't try to get away. Then there's a smell of old fruit, and a dent in the road."),
        new("the ape, and nothing else: all grey hide and old iron chain, and no words left at all", 1.5f, 1,
            "There's a shape in the bay, wading, with a broken yard-arm for a staff and a chain round its neck that goes back to a ship it's towing. There isn't a song in it, or a joke, or a name. But it gets between the red coat and the town, and it stands there. Nobody asked it to. Nobody could.",
            "The crew's a dozen old hands now, and they don't look at it either. One of them tips the last of a gourd out on the stones for it, and it bends down, very carefully, and touches the wet place with one finger. \"There's a berth,\" the old hand says. \"There's always a berth. That's all that's left of what he told us.\"",
            "It turns and wades out, and the ship follows it, because the ship has no choice.",
            "It falls in the shallows, between the town and the fire, still facing the right way. The water runs brown and sweet for a while. It'll be back. It doesn't know how not to."),
    ];

    public static WukongForm WukongOf(int deaths) => WukongForms[Math.Min(deaths, WukongForms.Count - 1)];
    public static double WukongStrength(int deaths) => Math.Min(SorenMost, 1 + deaths * SorenPerDeath);

    static readonly (string Id, int Most)[] Riches =
    [
        ("dragon_bone", 2), ("gold_ingot", 4), ("silver_ingot", 6), ("rough_sapphire", 3), ("rough_garnet", 4), ("pearl", 5), ("ancient_coin", 10),
        ("amethyst_shard", 4), ("storm_feather", 3), ("dragon_heart", 1), ("shadow_essence", 4),
    ];
    static readonly string[] Prizes = ["steel_greatsword", "steel_greataxe", "dragonbone_sword", "heavy_crossbow", "steel_plate", "steel_helm", "kipine_cloak", "steel_shield"];

    /// <summary>The plunder of a dozen towns, when he goes down: four to six kinds of rich things, and a piece or two off the Red Reckoning's own forge.</summary>
    public static List<(string Id, int N)> Plunder(Rng rng)
    {
        var pool = Riches.ToList();
        var loot = new List<(string, int)>();
        for (var n = 4 + rng.Next(3); n > 0; n--)
        {
            var (id, most) = rng.Pick(pool);
            pool.Remove((id, most));
            loot.Add((id, 1 + rng.Next(most)));
        }
        var forge = Forging.Forges.First(f => f.Id == Forging.RedReckoning);
        for (var n = rng.Chance(0.4) ? 2 : 1; n > 0; n--)
            loot.Add((Forging.Serial(rng.Pick(Prizes), rng.NextULong(), forge.Id, forge.Name, 10, 30, "the Dread Pirate Soren"), 1));
        return loot;
    }

    public const string Rubilacxe = "rubilacxe", RubilacxeScabbard = "rubilacxe_scabbard", Arachnicalibur = "arachnicalibur", ArachnicaliburScabbard = "arachnicalibur_scabbard";
    public const int SiegeBounty = 2000;
    /// <summary>How often Soren sees a blow coming, while the black blade's still his.</summary>
    public const double SorenForesees = 0.25;
    /// <summary>What a hero's name must be: trusted, for the stone; rough or worse, to take the black blade from Soren.</summary>
    public const Repute BladeRepute = Repute.Trusted, WebRepute = Repute.Rough;

    public static Item Blade(string id, string name, string tint, string glow, double damage, Dictionary<string, double> powers, string text) =>
        new(id, name, ItemKind.Gear, 9999, text) { Slot = Slot.Weapon, Grip = Grip.OneHand, Look = "Knight/1H_Sword", Damage = damage, ToHit = 4, Power = 4, Powers = powers, Tint = tint, Glow = glow, Metal = true, Bound = true };

    public static Item Sheath(string id, string name, string tint, Dictionary<string, double> powers, string text) =>
        new(id, name, ItemKind.Gear, 4999, text) { Slot = Slot.Neck, Powers = powers, Tint = tint, Bound = true };
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
        Mat("horse_hide", "Horse Hide", 7, "Short-haired and supple. Tans to good leather."),
        Mat("scruffy_pelt", "Scruffy Pelt", 2, "Thin fur, not much of it. Somebody will take it."),
        Mat("fruit_rind", "Split Fruit Rind", 40, "A spiked red rind, split down the side and still warm. Whatever was in it has gone."),
        Mat("cracked_gourd", "Cracked Gourd", 60, "A drinking gourd with a split in it. It smells of old fruit, and it's never quite empty."),
        Mat("boar_tusk", "Boar Tusk", 6, "Curved, sharp, yellowed."),
        Mat("deer_hide", "Deer Hide", 6, "Soft hide. Good leather."),
        Mat("antler", "Antler", 9, "A good spread of tines."),
        Mat("fox_pelt", "Fox Pelt", 11, "Red fur, prized for trim."),
        Mat("rat_tail", "Giant Rat Tail", 1, "Nobody wants this."),
        Mat("crab_chitin", "Mud Crab Chitin", 4, "A plate of shell."),
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
        Herb("bluebell", "Bluebell", 4, "Blue flowers that clear the head. Restores mana."),
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
        new("mana_tonic", "Mana Tonic", ItemKind.Potion, 30, "Restores half your mana.") { Use = new Effect(Mana: 0.5) },
        new("stamina_brew", "Stamina Brew", ItemKind.Potion, 25, "Restores all your stamina.") { Use = new Effect(Stamina: 1) },
        new("elixir_of_might", "Elixir of Might", ItemKind.Potion, 80, "A quarter more damage for two minutes.") { Use = new Effect(Might: 0.25, Seconds: 120) },
        new("warding_draught", "Warding Draught", ItemKind.Potion, 70, "Armour like plate for two minutes.") { Use = new Effect(Ward: 60, Seconds: 120) },
        new("bread", "Bread", ItemKind.Food, 3, "Heals a little.") { Use = new Effect(Health: 0.1, Stamina: 0.2) },
        new("ale", "Ale", ItemKind.Food, 4, "A tankard of brown ale. Restores some stamina.") { Use = new Effect(Stamina: 0.3) },
        new("travellers_tea", "Traveller's Tea", ItemKind.Food, 6, "Irohe's own blend, in a twist of paper. Brewed on any fire, it warms you through. Heals a fair bit and restores stamina.") { Use = new Effect(Health: 0.2, Stamina: 0.6) },
        new("travel_rations", "Travel Rations", ItemKind.Food, 8, "Dried meat, hard cheese, an apple. Heals a fair bit.") { Use = new Effect(Health: 0.25, Stamina: 0.5) },
        new("alchemist_kit", "Alchemist's Kit", ItemKind.Tool, 40, "Mortar, pestle, little bottles. Lets you brew potions anywhere (from your pack, I)."),
        // ---- tools of the town's trades ----------------------------------------------------------------
        new("fishing_rod", "Fishing Rod", ItemKind.Tool, 12, "Ash rod, horsehair line, a bone hook. Face open water and press E to cast."),
        new("pickaxe", "Pickaxe", ItemKind.Tool, 18, "For ore veins. Walk up to one and press E."),
        new("woodcutters_axe", "Woodcutter's Axe", ItemKind.Tool, 15, "A broad, heavy bit. For felling trees at a woodlot."),
        new("tinderbox", "Tinderbox", ItemKind.Tool, 5, "Flint, steel and char-cloth. With a log, light a fire anywhere outside town (from your pack, I)."),
        new("spade", "Spade", ItemKind.Tool, 8, "For digging where a treasure map says."),
        new("snare", "Snare", ItemKind.Tool, 4, "A wire noose on a peg. Set it in the wild (from your pack, I) and come back later."),
        new("torn_map", "Torn Map", ItemKind.Tool, 15, "A scrap of a treasure map. Read it (from your pack, I) to begin a hunt; you'll need a spade."),
        new("home_charm", "Home Charm", ItemKind.Tool, 60, "A carved token from your own hearth. Use it (from your pack, I) to go home, once every five minutes."),
        // ---- the allotments ------------------------------------------------------------------------------
        new("potato_seed", "Potato Seed", ItemKind.Material, 1, "A seed potato. Plant it in an allotment."),
        new("onion_seed", "Onion Seed", ItemKind.Material, 1, "Onion sets. Plant them in an allotment."),
        new("cabbage_seed", "Cabbage Seed", ItemKind.Material, 2, "Cabbage seed. Plant it in an allotment."),
        new("emberleaf_seed", "Emberleaf Seed", ItemKind.Material, 3, "Grows emberleaf in a garden, if you've the knack."),
        new("bluebell_seed", "Bluebell Seed", ItemKind.Material, 3, "Grows bluebells in a garden."),
        new("bloodcap_spore", "Bloodcap Spores", ItemKind.Material, 6, "Grows bloodcaps, in good dark soil."),
        new("frostbloom_seed", "Frostbloom Seed", ItemKind.Material, 8, "Grows frostbloom, for a patient gardener."),
        new("silverthorn_seed", "Silverthorn Seed", ItemKind.Material, 8, "Grows silverthorn, for a skilled gardener."),
        new("apple", "Greywater Apple", ItemKind.Food, 3, "Sharp and cold, straight off the tree.") { Use = new Effect(Health: 0.08, Stamina: 0.15) },
        new("pear", "Greywater Pear", ItemKind.Food, 3, "So ripe it barely holds together.") { Use = new Effect(Health: 0.1, Stamina: 0.1) },
        new("plum", "Long Orchard Plum", ItemKind.Food, 4, "Dark, sweet, and everywhere in August.") { Use = new Effect(Health: 0.1, Stamina: 0.15) },
        new("brambleberries", "Brambleberries", ItemKind.Food, 4, "A hatful from the terraces.") { Use = new Effect(Health: 0.12, Stamina: 0.1) },
        new("cider", "Greywater Cider", ItemKind.Food, 5, "Sharp and cold. Restores some stamina.") { Use = new Effect(Stamina: 0.35) },
        new("mead", "Honey Mead", ItemKind.Food, 7, "Sweet, and stronger than it tastes.") { Use = new Effect(Health: 0.05, Stamina: 0.4) },
        new("spiced_wine", "Spiced Wine", ItemKind.Food, 9, "Warm, and good for what ails you.") { Use = new Effect(Health: 0.15, Stamina: 0.3) },
        new("meat_pie", "Meat Pie", ItemKind.Food, 7, "Hot from the oven. Heals a fair bit.") { Use = new Effect(Health: 0.25, Stamina: 0.2) },
        new("hearty_stew", "Hearty Stew", ItemKind.Food, 8, "A bowl of whatever's been simmering all day.") { Use = new Effect(Health: 0.3, Stamina: 0.35) },
        new("potato", "Potato", ItemKind.Food, 2, "Raw. Better in a stew.") { Use = new Effect(Health: 0.05) },
        new("onion", "Onion", ItemKind.Material, 2, "Makes a stew a stew."),
        new("cabbage", "Cabbage", ItemKind.Food, 3, "Crunchy. Better in a stew.") { Use = new Effect(Health: 0.05, Stamina: 0.1) },
        // ---- snares --------------------------------------------------------------------------------------
        Mat("rabbit", "Rabbit", 4, "A snared rabbit. Cook it."),
        Mat("game_bird", "Grouse", 5, "A plump game bird. Roast it."),
        Mat("rabbit_pelt", "Rabbit Pelt", 3, "Soft and small."),
        Mat("marten_pelt", "Marten Pelt", 18, "Rich brown fur, prized by furriers."),
        Mat("snow_hare_pelt", "Snow Hare Pelt", 12, "White as the north."),
        Mat("feather", "Feather", 1, "A good flight feather."),
        // The farms, and what grows wild.
        Mat("wool", "Wool", 4, "A fleece's worth, washed. The loom makes cloth of it."),
        Mat("cow_hide", "Cowhide", 7, "Thick hide off a cow or a horse. A tanner makes good leather of it."),
        Mat("raw_meat", "Raw Meat", 4, "Beef, mutton or pork. Cook it."),
        new("roast_meat", "Roast Meat", ItemKind.Food, 9, "A good hot joint. Heals a fair bit and restores stamina.") { Use = new Effect(Health: 0.3, Stamina: 0.3) },
        new("raspberries", "Raspberries", ItemKind.Food, 2, "A handful from the autumn woods. Heals a little.") { Use = new Effect(Health: 0.06, Stamina: 0.1) },
        new("bilberries", "Bilberries", ItemKind.Food, 2, "Dark and sweet, from under the pines. Heals a little.") { Use = new Effect(Health: 0.06, Stamina: 0.1) },
        new("honeycomb", "Honeycomb", ItemKind.Food, 6, "From a wild hive, stings and all. Restores stamina.") { Use = new Effect(Health: 0.1, Stamina: 0.35) },
        Mat("penny_bun", "Penny Bun", 3, "A fat brown mushroom from the autumn woods. Good in a stew."),
        Mat("chanterelle", "Chanterelle", 4, "A golden mushroom from under the pines. Good in a stew."),
        Mat("wild_garlic", "Wild Garlic", 2, "Broad leaves, and a smell you can't miss. A cook's friend."),
        Mat("flax", "Flax", 2, "Blue-flowered stalks. Spun at a loom, they make linen."),
        new("berry_tart", "Berry Tart", ItemKind.Food, 10, "Sweet and filling. Heals a fair bit and restores stamina.") { Use = new Effect(Health: 0.25, Stamina: 0.35) },
        new("mushroom_stew", "Mushroom Stew", ItemKind.Food, 12, "Earthy and rich. Heals well and restores mana.") { Use = new Effect(Health: 0.35, Mana: 0.3) },
        new("honey_cake", "Honey Cake", ItemKind.Food, 9, "Sticky and good. Restores a great deal of stamina.") { Use = new Effect(Health: 0.15, Stamina: 0.6) },
        // ---- the jeweller's bench ------------------------------------------------------------------------
        Mat("garnet", "Garnet", 60, "A cut garnet, deep red."),
        Mat("sapphire", "Sapphire", 110, "A cut sapphire, blue as deep water."),
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
        Mat("iron_nails", "Iron Nails", 1, "A handful of nails, from an iron ingot at the forge."),
        Mat("plank", "Plank", 3, "A sawn plank, from a log at your building site."),
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
        new("seafood_platter", "Seafood Platter", ItemKind.Food, 45, "Lobster, salmon and eel, fit for the Crown. Heals you fully and fires you up.") { Use = new Effect(Health: 1, Stamina: 1, Mana: 0.5, Might: 0.12, Seconds: 180) },
        new("vegetable_stew", "Vegetable Stew", ItemKind.Food, 12, "Potato, onion and cabbage. Heals and restores stamina.") { Use = new Effect(Health: 0.3, Stamina: 0.5) },
        new("roast_bird", "Roast Grouse", ItemKind.Food, 12, "Crisp skin and dark meat. Heals a fair bit.") { Use = new Effect(Health: 0.3, Stamina: 0.3) },
        new("rabbit_stew", "Rabbit Stew", ItemKind.Food, 20, "Rabbit, potato and onion. Heals well, and keeps you going for two minutes.") { Use = new Effect(Health: 0.45, Stamina: 0.6, Ward: 8, Seconds: 120) },
        new("smoked_eel", "Smoked Eel", ItemKind.Food, 15, "Keeps for weeks. Heals and restores mana.") { Use = new Effect(Health: 0.3, Mana: 0.4) },
        new("spoiled_herbs", "Spoiled Herbs", ItemKind.Material, 1, "Picked in the rot, and black to the root. No use for anything."),
        new("rotwood", "Rotwood", ItemKind.Material, 2, "Corrupted wood. It'll burn, but what it burns off is poison."),
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
        // ---- what the Northwood's guardians leave: worth a very great deal, and a very great crime --------------------
        Gear("mosswarden_staff", "Ulmoth's Living Staff", 1400, Slot.Weapon, Grip.Caster, "Mage/2H_Staff", damage: 0.5, tint: "#6aa04a", glow: "#a0ff60"),
        Gear("barkbreaker_maul", "Korrun's Heartwood Maul", 1400, Slot.Weapon, Grip.TwoHand, "Barbarian/2H_Axe", damage: 0.56, tint: "#8a6a44", glow: "#d070ff"),
        Gear("drowned_mantle", "Mantle of the Drowned Root", 1400, Slot.Body, Grip.None, null, armor: 66, tint: "#3f6a5a"),
        Legend.Blade(Legend.Rubilacxe, "Rubilacxe", "#c8d4e0", "#fff0c0", 0.75, new Dictionary<string, double> { ["smite"] = 4, ["sever"] = 0.05, ["undying"] = 1 },
            "The sword from the stone. +4 to hit, +4 damage. On a critical blow, 4d6 more; and one such blow in twenty takes a limb. Once a day it holds you at the edge of death. It came only to one who was good, and who ended the fifth guardian."),
        Legend.Sheath(Legend.RubilacxeScabbard, "Scabbard of Rubilacxe", "#6a4a2a", new Dictionary<string, double> { ["resist_physical"] = 25 },
            "While you wear it, blades, points and blunt blows do a quarter less."),
        Legend.Blade(Legend.Arachnicalibur, "Arachnicalibur", "#22202a", "#a080ff", 0.75, new Dictionary<string, double> { ["crit"] = 5, ["drain"] = 8 },
            "The Web of Life connects all things, all stories, all lives. Those who worship the Web are said to see signs and their reverberations along the strands that connect us, allowing them a limited prescience of the future. This blade is their most sacred relic."),
        Legend.Sheath(Legend.ArachnicaliburScabbard, "Scabbard of Arachnicalibur", "#2a2233", new Dictionary<string, double> { ["dodge"] = 30 },
            "Arachnicalibur's connection to the Web lets its wielder avoid danger in battle, as they easily dodge most attacks. As long as they hold the scabbard, anyway."),
        Gear("sorens_reaver", "Soren's Boarding Cutlass", 1500, Slot.Weapon, Grip.TwoHand, "Barbarian/2H_Axe", damage: 0.6, tint: "#1a1414", glow: "#ff5020"),
        Gear("wukongs_staff", "Lu Wukong's Chained Staff", 1200, Slot.Weapon, Grip.TwoHand, "Mage/Staff", damage: 0.5, tint: "#5a4030", glow: "#ffd040"),
        // ---- what Brasswake makes for its own: it doesn't leave the island ----------------------------------------
        Isle(Gear("aether_gauntlet", "Aether Gauntlet", 0, Slot.Weapon, Grip.OneHand, "Knight/1H_Sword", damage: 0.55, tint: "#c9a040", metal: true, glow: "#7fd8ff"), "A brass fist that hits with the steam behind it. Made on Brasswake, and it stays on Brasswake."),
        Isle(Gear("steamweave_coat", "Steamweave Coat", 0, Slot.Body, Grip.OneHand, "Rogue", armor: 0.3, tint: "#6a4a2c"), "Wired cloth with warm pipes in the lining. Made on Brasswake, and it stays on Brasswake."),
        Isle(Gear("spring_buckler", "Spring Buckler", 0, Slot.Offhand, Grip.OneHand, "Knight/Round_Shield", armor: 0.25, tint: "#b88a3a", metal: true), "A shield that gives, and gives back. Made on Brasswake, and it stays on Brasswake."),
        Isle(Gear("orrery_ring", "Orrery Ring", 0, Slot.Ring, Grip.OneHand, null, armor: 0.1, tint: "#d8b050", metal: true, glow: "#ffd27a"), "Seven tiny rings, turning round each other, keeping the island's time. Made on Brasswake, and it stays on Brasswake."),
        Gear("blightthorn", "Blightthorn", 1400, Slot.Weapon, Grip.OneHand, "Rogue/Knife", damage: 0.52, tint: "#2a3a22", glow: "#80ff40"),
        Gear("iron_hammer", "Iron Hammer", 45, Slot.Weapon, Grip.OneHand, "@hammer", damage: 0.12),
        Gear("steel_hammer", "Steel Hammer", 115, Slot.Weapon, Grip.OneHand, "@hammer", damage: 0.22, tint: "#e4ecf4", metal: true),
        Gear("iron_mace", "Iron Mace", 45, Slot.Weapon, Grip.OneHand, "@mace", damage: 0.12),
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
        Gear("leather_cap", "Leather Cap", 18, Slot.Head, Grip.None, "Barbarian/Barbarian_Hat", armor: 6, tint: "#7a5a3a"),
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
        // ---- rings and amulets (made at a jeweller's bench) -------------------------------------------------
        Gear("silver_ring", "Silver Ring", 70, Slot.Ring, Grip.None, null, damage: 0.03),
        Gear("gold_ring", "Gold Ring", 120, Slot.Ring, Grip.None, null, damage: 0.05, armor: 2),
        Gear("garnet_ring", "Garnet Ring", 220, Slot.Ring, Grip.None, null, damage: 0.08, armor: 2),
        Gear("silver_amulet", "Silver Amulet", 80, Slot.Neck, Grip.None, null, armor: 5),
        Gear("pearl_necklace", "Pearl Necklace", 150, Slot.Neck, Grip.None, null, damage: 0.03, armor: 5),
        Gear("sapphire_amulet", "Sapphire Amulet", 300, Slot.Neck, Grip.None, null, damage: 0.05, armor: 9),
        // ---- clothes from the loom -------------------------------------------------------------------------
        Gear("linen_tunic", "Linen Tunic", 25, Slot.Body, Grip.None, null, armor: 6, tint: "#d8ccb4"),
        Gear("padded_jerkin", "Padded Jerkin", 55, Slot.Body, Grip.None, null, armor: 13, tint: "#9a8a6a"),
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

    /// <summary>One of Brasswake's own: it can't be sold, and it's left at the gangway.</summary>
    static Item Isle(Item made, string text) => made with { Text = text, Bound = true };

    static Item Gear(string id, string name, int value, Slot slot, Grip grip, string? look, double damage = 0, double armor = 0,
        string? tint = null, bool metal = false, string? glow = null) =>
        new(id, name, ItemKind.Gear, value, Describe(slot, grip, damage, armor))
        { Slot = slot, Grip = grip, Look = look, Damage = damage, Armor = armor, Tint = tint, Metal = metal, Glow = glow };

    static string Describe(Slot slot, Grip grip, double damage, double armor)
    {
        var what = slot switch
        {
            Slot.Weapon => grip switch { Grip.TwoHand => "A two-handed weapon.", Grip.Ranged => "A ranged weapon.", Grip.Caster => "A caster's focus.", _ => "A one-handed weapon." },
            Slot.Offhand => "For the off hand.", Slot.Head => "Worn on the head.", Slot.Body => "Body armour.", Slot.Ring => "A ring.", Slot.Neck => "Worn at the throat.", _ => "A cloak.",
        };
        var stats = new List<string>();
        if (damage > 0) stats.Add($"+{damage:P0} damage");
        if (armor > 0) stats.Add($"+{armor:0} armour");
        return stats.Count > 0 ? $"{what} {string.Join(", ", stats)}." : what;
    }

    static readonly Dictionary<string, Item> ById = All.ToDictionary(i => i.Id);

    public static Item? Get(string id) => ById.GetValueOrDefault(id) ?? Forging.Resolve(id);

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
