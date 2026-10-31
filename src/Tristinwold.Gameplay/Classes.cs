using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>The five callings a character can follow: the way of the blade, of the shadow, of magic, of the word and the song, or of the workshop.</summary>
public enum Calling { Warrior, Rogue, Mage, Bard, Artificer, Druid }

/// <summary>
/// One of a class's moves. <paramref name="Power"/> is base damage (or, for a heal, the share of health
/// restored); <paramref name="Range"/> how far it reaches; <paramref name="Seconds"/> how long it lasts
/// for the ones that last. Skill ranks raise its power.
/// </summary>
public sealed record Ability(
    string Id,
    string Name,
    string Text,
    AttackKind Pays,
    double Cost,
    double Cooldown,
    double Power,
    double Range,
    DamageKind Kind = DamageKind.Physical,
    double Seconds = 0);

/// <summary>
/// A class: its calling, its look, its starting numbers, and its moves. Left button: <see cref="Primary"/>.
/// Right button: <see cref="Signature"/>. Q: <see cref="Utility"/>. R, when the gauge is full: <see cref="Limit"/>.
/// <see cref="Passive"/> is always on.
/// </summary>
public sealed record Discipline(
    string Id,
    string Name,
    Calling Calling,
    string Text,
    double Health,
    double Mana,
    double Stamina,
    double Armor,
    Ability Primary,
    Ability Signature,
    Ability Utility,
    Ability Limit,
    Ability Passive)
{
    /// <summary>The model and kit it wears (a key into the game's looks).</summary>
    public string Look => Id;

    public IEnumerable<Ability> Skills => [Primary, Signature, Utility, Passive, Limit];

    /// <summary>Its element can be chosen (Q cycles fire, frost and shock).</summary>
    public bool Elemental => Utility.Id == "attune";
}

/// <summary>
/// The fifteen classes, three to each calling, each fighting its own way:
/// <list type="bullet">
/// <item>Warriors: the Martial Artist (fists, feet and speed), the Barbarian (a great axe and rage), the
/// Paladin (sword, shield and holy light).</item>
/// <item>Rogues: the Ranger (a crossbow and beasts), the Thief (quick knives and light fingers), the
/// Assassin (poison, shadows and one perfect strike).</item>
/// <item>Mages: the Wizard (studied elements), the Warlock or Witch (pacts, curses and stolen life), the
/// Sorcerer (raw, wild magic in the blood).</item>
/// <item>Bards: the Poet (verse that cuts and verse that rouses), the Scribe (glyphs, seals and scrolls), the
/// Musician (a lute, a drum and a song that shakes the ground).</item>
/// <item>Artificers, each with a brass golem at their side that grows as they do: the Alchemist (volatile vials,
/// elemental hazards, and reagents refined for the markets), the Ether-Aeronaut (a harpoon, ship modules, and a
/// golem worn as armour), the Saboteur (a wrench, caltrops and traps, scrap for the black market, trackers on
/// wagons).</item>
/// </list>
/// </summary>
public static class Classes
{
    static Ability Attune => new("attune", "Attune", "Change element: fire, frost or shock.", AttackKind.Spell, 0, 0.2, 0, 0);

    public static IReadOnlyList<Discipline> All { get; } =
    [
        // ---- warriors ----
        new("MartialArtist", "Martial Artist", Calling.Warrior,
            "Unarmoured and fast. Strings punches and kicks together, leaps in with a flying kick, and slips blows rather than taking them.",
            130, 60, 160, 30,
            new("flurry", "Flurry", "Quick punches; every third blow is a kick that hits twice as hard.", AttackKind.Weapon, 7, 0.32, 13, 2.2),
            new("flying_kick", "Flying Kick", "Leap at a foe up to 10 m away and kick them hard.", AttackKind.Weapon, 25, 4, 34, 10),
            new("inner_focus", "Inner Focus", "Breathe: heal over a few seconds and move with haste.", AttackKind.Weapon, 30, 18, 0.3, 0, Seconds: 8),
            new("hundred_fists", "Hundred Fists", "Eight blows in a heartbeat on the foe in front of you.", AttackKind.Weapon, 0, 0, 16, 3),
            new("evasion", "Evasion", "A chance to slip any blow entirely.", AttackKind.Weapon, 0, 0, 0.2, 0)),
        new("Barbarian", "Barbarian", Calling.Warrior,
            "A great axe and a greater temper. Cleaves through everything in front of it, leaps into the thick of a fight, and rages.",
            210, 40, 130, 60,
            new("cleave", "Cleave", "A heavy swing that hits every foe in front of you.", AttackKind.Weapon, 16, 0.9, 30, 2.8),
            new("leap_slam", "Leap Slam", "Leap at a foe up to 12 m away and smash the ground, hurting all around.", AttackKind.Weapon, 30, 6, 32, 12),
            new("rage", "Rage", "Hit far harder and heal on every kill for a while; you take a little more harm.", AttackKind.Weapon, 20, 20, 0.6, 0, Seconds: 10),
            new("whirlwind", "Whirlwind", "Spin with the axe, striking everything around you, hard.", AttackKind.Weapon, 0, 0, 90, 7),
            new("thick_hide", "Thick Hide", "More health, and wounds close even in a fight.", AttackKind.Weapon, 0, 0, 0.004, 0)),
        new("Paladin", "Paladin", Calling.Warrior,
            "Sword, shield and the light of the gods. Holds the line, heals the company, and burns the dead and the corrupted.",
            170, 90, 110, 140,
            new("smite", "Smite", "A sword stroke blessed with sunlight.", AttackKind.Weapon, 12, 0.6, 22, 2.4, DamageKind.Sun),
            new("holy_light", "Holy Light", "Heal yourself and your companions, and sear foes close by with sunlight.", AttackKind.Spell, 35, 10, 0.3, 8, DamageKind.Sun),
            new("shield_wall", "Shield Wall", "Raise the shield: Protect and Shell for a while.", AttackKind.Weapon, 25, 20, 0, 0, Seconds: 8),
            new("judgement", "Divine Judgement", "Sunfire on every foe around you, and every companion healed whole.", AttackKind.Spell, 0, 0, 80, 9, DamageKind.Sun),
            new("devotion", "Aura of Devotion", "You and your companions are harder to hurt; the undead and corrupted take more from your light.", AttackKind.Spell, 0, 0, 0.15, 0)),

        // ---- rogues ----
        new("Ranger", "Ranger", Calling.Rogue,
            "A crossbow, a sharp eye and the wild on their side. Shoots from afar, looses volleys, and every beast is a friend.",
            140, 70, 135, 60,
            new("shoot", "Shoot", "A crossbow bolt at the foe you face, up to 35 m.", AttackKind.Weapon, 9, 0.7, 20, 35),
            new("volley", "Volley", "Bolts at up to four foes at once.", AttackKind.Weapon, 30, 6, 18, 30),
            new("call_wild", "Call of the Wild", "Your beasts are healed and fight harder for a while.", AttackKind.Weapon, 25, 20, 0.5, 0, Seconds: 12),
            new("rain_of_bolts", "Rain of Bolts", "A storm of bolts on the foe you face and everything near it.", AttackKind.Weapon, 0, 0, 55, 35),
            new("beastmaster", "Beastmaster", "Any young creature will follow you, and your beasts grow faster and stronger.", AttackKind.Weapon, 0, 0, 0.25, 0)),
        new("Thief", "Thief", Calling.Rogue,
            "Two knives and quick hands. Darts in and out, lifts a purse with every stroke, and vanishes in smoke.",
            130, 60, 145, 40,
            new("stab", "Quick Stab", "Fast, cheap strikes with both knives.", AttackKind.Weapon, 6, 0.35, 12, 2.0),
            new("pilfer", "Pilfer", "A strike that steals a few crowns as it lands.", AttackKind.Weapon, 15, 3, 20, 2.4),
            new("smoke_bomb", "Smoke Bomb", "Foes nearby lose sight of you, and your next strike is a sure critical.", AttackKind.Weapon, 25, 15, 0, 10, Seconds: 5),
            new("grand_heist", "Grand Heist", "Strike every foe around you and rob them all.", AttackKind.Weapon, 0, 0, 45, 7),
            new("light_fingers", "Light Fingers", "More crowns from every fight and every job; running costs nothing.", AttackKind.Weapon, 0, 0, 0.5, 0)),
        new("Assassin", "Assassin", Calling.Rogue,
            "A hooded blade that waits for the perfect moment. Steps through shadows behind a foe and ends them.",
            125, 70, 135, 40,
            new("assassinate", "Stab", "A precise dagger strike, often critical.", AttackKind.Weapon, 8, 0.5, 15, 2.0),
            new("shadowstep", "Shadowstep", "Step through the shadows behind a foe up to 15 m away and strike: a sure critical.", AttackKind.Spell, 25, 5, 22, 15),
            new("poison_blades", "Poisoned Blades", "Your blades drip poison for a while: every strike adds poison.", AttackKind.Weapon, 20, 18, 10, 0, DamageKind.Poison, Seconds: 12),
            new("death_mark", "Death Mark", "One foe, marked and finished: a single blow of terrible force.", AttackKind.Weapon, 0, 0, 160, 3),
            new("backstab", "Backstab", "Strikes on a foe not fighting you hit far harder; more critical hits.", AttackKind.Weapon, 0, 0, 1.5, 0)),

        // ---- mages ----
        new("Wizard", "Wizard", Calling.Mage,
            "A studied mage with a staff. Masters fire, frost and shock, and hurls bolts and blasts of them.",
            110, 180, 90, 30,
            new("bolt", "Elemental Bolt", "A bolt of your element at the foe you face, up to 28 m.", AttackKind.Spell, 14, 0.8, 26, 28),
            new("blast", "Elemental Blast", "A burst of your element where the foe stands, hurting everything near it.", AttackKind.Spell, 40, 6, 36, 28),
            Attune,
            new("meteor", "Meteor", "A falling star on the foe you face and all around it.", AttackKind.Spell, 0, 0, 110, 30, DamageKind.Fire),
            new("arcane_mastery", "Arcane Mastery", "Stronger spells and a deeper well of mana.", AttackKind.Spell, 0, 0, 0.3, 0)),
        new("Warlock", "Warlock or Witch", Calling.Mage,
            "Power bought with a pact. Blasts with eldritch fire, drinks the life of foes, and calls a familiar from beyond.",
            125, 150, 95, 40,
            new("eldritch_blast", "Eldritch Blast", "A crackling bolt of otherworldly fire, up to 26 m.", AttackKind.Spell, 12, 0.75, 24, 26, DamageKind.Fire),
            new("drain_life", "Drain Life", "Tear the life from a foe and take it for yourself.", AttackKind.Spell, 30, 5, 30, 20, DamageKind.Poison),
            new("familiar", "Summon Familiar", "A spectral familiar fights beside you for half a minute.", AttackKind.Spell, 45, 30, 0, 0, Seconds: 30),
            new("soul_harvest", "Soul Harvest", "Drain every foe around you at once.", AttackKind.Spell, 0, 0, 60, 10, DamageKind.Poison),
            new("dark_pact", "Dark Pact", "Every kill restores health and mana.", AttackKind.Spell, 0, 0, 0.1, 0)),
        new("Sorcerer", "Sorcerer", Calling.Mage,
            "Magic in the blood, no staff needed. Fires quick arcane missiles, chains lightning through crowds, and sometimes the magic surges.",
            105, 165, 95, 30,
            new("missiles", "Arcane Missiles", "Quick, cheap bolts of your element, up to 24 m.", AttackKind.Spell, 7, 0.4, 14, 24),
            new("chain_lightning", "Chain Lightning", "Lightning that leaps between up to five foes.", AttackKind.Spell, 35, 5, 28, 24, DamageKind.Shock),
            Attune,
            new("dragonfire", "Dragonfire", "The dragon in your blood wakes: fire pours from your hands over everything in front of you.", AttackKind.Spell, 0, 0, 85, 12, DamageKind.Fire),
            new("wild_surge", "Wild Surge", "Spells sometimes surge and strike twice; mana returns quickly.", AttackKind.Spell, 0, 0, 0.15, 0)),

        // ---- bards ----
        new("Poet", "Poet", Calling.Bard,
            "Words are weapons. Cuts foes with barbed verse, rouses the company with an ode, and leaves enemies too stung to fight.",
            125, 150, 115, 40,
            new("barbed_verse", "Barbed Verse", "A line so sharp it cuts: golden words at the foe you face, up to 22 m.", AttackKind.Spell, 9, 0.6, 18, 22, DamageKind.Sun),
            new("rousing_ode", "Rousing Ode", "You and your companions are healed, quickened and fight harder for a while.", AttackKind.Spell, 35, 14, 0.2, 0, Seconds: 10),
            new("mocking_couplet", "Mocking Couplet", "Foes within 10 m are left speechless and forget you for a few seconds.", AttackKind.Spell, 25, 16, 0, 10, Seconds: 4),
            new("epic_of_ages", "Epic of Ages", "The song of your deeds: golden words sear every foe around you, and every companion is healed whole.", AttackKind.Spell, 0, 0, 75, 10, DamageKind.Sun),
            new("muse", "The Muse", "Stronger spells, and every foe that falls gives back mana and stamina.", AttackKind.Spell, 0, 0, 0.15, 0)),
        new("Scribe", "Scribe", Calling.Bard,
            "A quill, a book and the old words of power. Darts of enchanted ink, glyphs that burst where foes stand, and scrolls that ward.",
            115, 165, 100, 40,
            new("inkbolt", "Inkbolt", "A dart of crackling enchanted ink at the foe you face, up to 24 m.", AttackKind.Spell, 8, 0.55, 15, 24, DamageKind.Shock),
            new("glyph", "Glyph of Warding", "Write a glyph under the foe you face; a heartbeat later it bursts, hurting everything near it.", AttackKind.Spell, 35, 6, 34, 26, DamageKind.Shock),
            new("scroll", "Scroll of Protection", "Read a scroll: Protect, Shell and Regen for a while.", AttackKind.Spell, 30, 22, 0, 0, Seconds: 8),
            new("unmaking", "Words of Unmaking", "Read a word from the first book: every foe within 12 m is struck hard and loses its way.", AttackKind.Spell, 0, 0, 90, 12, DamageKind.Shock),
            new("marginalia", "Marginalia", "You note every weakness: each blow on the same foe hits harder than the last, up to a third more.", AttackKind.Spell, 0, 0, 0.07, 0)),
        new("Musician", "Musician", Calling.Bard,
            "A lute on the back and a drum at the belt. Jarring chords, a wall of sound that throws foes back, and a hymn the company fights to.",
            135, 140, 125, 45,
            new("discord", "Discord", "A jarring chord at the foe you face, up to 18 m; every fourth note lands twice as hard.", AttackKind.Spell, 7, 0.45, 13, 18),
            new("thunder_chord", "Thunderous Chord", "A wave of sound before you that hurts and throws back every foe in it.", AttackKind.Spell, 30, 6, 28, 10),
            new("battle_hymn", "Battle Hymn", "Play: you and your companions are quickened and heal for a while.", AttackKind.Spell, 30, 20, 0, 0, Seconds: 10),
            new("crescendo", "Crescendo", "A song that builds until the ground shakes: every foe within 14 m is struck three times.", AttackKind.Spell, 0, 0, 40, 14),
            new("perfect_pitch", "Perfect Pitch", "Mana and stamina come back quickly while you play.", AttackKind.Spell, 0, 0, 0.015, 0)),
        // ---- artificers ----
        new("Alchemist", "Alchemist", Calling.Artificer,
            "A tinker of reagents and a trader in their fruits. Flings volatile vials, lays elemental hazards across a chokepoint, and refines what the land gives into goods the markets pay dearly for.",
            130, 120, 110, 40,
            new("vial", "Volatile Vial", "A vial of your element flung at the foe you face, up to 22 m.", AttackKind.Spell, 10, 0.7, 20, 22),
            new("hazard_flask", "Hazard Flask", "A flask that bursts where the foe stands into a pool of your element, hurting everything in it for a while.", AttackKind.Spell, 35, 10, 14, 22, Seconds: 8),
            Attune,
            new("reagent_storm", "Reagent Storm", "Every vial on your belt at once: your element over everything around you, and a hazard left burning where you stand.", AttackKind.Spell, 0, 0, 70, 9, Seconds: 8),
            new("refiner", "Refiner", "What you gather, fish and hunt sells for more, and your preserving salts keep goods on the road: more for every load you trade away from home.", AttackKind.Spell, 0, 0, 0.2, 0)),
        new("EtherAeronaut", "Ether-Aeronaut", Calling.Artificer,
            "An engineer of ships and ether. Harpoons foes to the deck, pins them where they stand, climbs inside the golem and wears it as armour, and fits ships with modules that outrun pirates.",
            150, 90, 130, 55,
            new("harpoon", "Ether Harpoon", "A harpoon at the foe you face, up to 18 m, that drags them to you.", AttackKind.Weapon, 12, 0.8, 18, 18, DamageKind.Shock),
            new("anchor", "Anchor Shot", "A charged harpoon that pins a foe where it stands for a while.", AttackKind.Weapon, 25, 8, 22, 18, DamageKind.Shock, 5),
            new("merge", "Merge", "Climb inside your golem and wear it: Protect and Shell, and your blows carry its weight, for a while.", AttackKind.Spell, 40, 30, 0.5, 0, Seconds: 14),
            new("overdrive", "Overdrive", "The engine opened wide: you and your golem move and strike like lightning for a while, and a ship under you surges ahead.", AttackKind.Spell, 0, 0, 0.6, 0, Seconds: 12),
            new("tinker", "Tinker", "You fit ships with modules at the shipyard (a bilge pump that halves what boarders hole; light sails that cut a quarter off every leg), and your golem is sturdier.", AttackKind.Spell, 0, 0, 0.25, 0)),
        // ---- druids ----
        new("Stormdruid", "Elemental Druid", Calling.Druid,
            "The wood's own weather in your hands. Calls down fire, frost and lightning, and the ground itself, and every storm answers to you.",
            125, 165, 100, 30,
            new("bolt", "Nature's Bolt", "A bolt of your element at the foe you face, up to 28 m.", AttackKind.Spell, 14, 0.8, 25, 28),
            new("blast", "Wrath of the Wild", "A burst of your element where the foe stands, hurting everything near it.", AttackKind.Spell, 40, 6, 34, 28),
            Attune,
            new("meteor", "Heart of the Storm", "The sky falls on the foe you face and all around it.", AttackKind.Spell, 0, 0, 105, 30, DamageKind.Shock),
            new("arcane_mastery", "Old Weather", "Stronger spells and a deeper well of mana.", AttackKind.Spell, 0, 0, 0.3, 0)),
        new("Shapeshifter", "Shapeshifter", Calling.Druid,
            "Fights as the beasts fight. Starts with the wolf, and learns any beast it studies by fighting enough of them: bear, boar, rat and on and on. Nothing undead, and no dragons.",
            160, 90, 150, 40,
            new("flurry", "Claw and Fang", "Quick blows; every third one hits twice as hard.", AttackKind.Weapon, 0, 0.45, 15, 2.2),
            new("flying_kick", "Pounce", "Leap at a foe up to 10 m away and bring them down hard.", AttackKind.Weapon, 25, 6, 24, 10),
            new("shift", "Beast Form", "Take the shape of a beast you've studied, cycling through them, and back to your own. Stronger, tougher, and it stays until you change.", AttackKind.Weapon, 0, 1.5, 0.35, 0),
            new("hundred_fists", "Feeding Frenzy", "Eight savage blows in a heartbeat on the foe in front of you.", AttackKind.Weapon, 0, 0, 12, 3),
            new("evasion", "Animal Instinct", "A chance to slip any blow entirely.", AttackKind.Weapon, 0, 0, 0.2, 0)),
        new("Spiritcaller", "Spiritcaller", Calling.Druid,
            "Speaks to what's left when the body's gone. Sends spirits of the wild to fight, and draws the life out of anything that stands against them.",
            120, 170, 95, 30,
            new("eldritch_blast", "Spirit Bolt", "A bolt of pale spirit-fire, up to 26 m.", AttackKind.Spell, 12, 0.75, 24, 26, DamageKind.Shock),
            new("drain_life", "Soul Tether", "Pull the life out of a foe and keep it.", AttackKind.Spell, 30, 5, 30, 20, DamageKind.Frost),
            new("familiar", "Spectral Beast", "A spectral animal fights beside you for half a minute.", AttackKind.Spell, 40, 25, 0, 0, Seconds: 30),
            new("soul_harvest", "The Pack Returns", "Every spirit of the wood falls on the foes around you.", AttackKind.Spell, 0, 0, 60, 10, DamageKind.Frost),
            new("dark_pact", "Spirit Debt", "Every kill restores health and mana.", AttackKind.Spell, 0, 0, 0.1, 0)),
        new("Saboteur", "Saboteur", Calling.Artificer,
            "The wrong kind of artificer. A wrench that stuns, smoke caltrops, spring traps, and a nose for parts: breaks gear and wrecks down for the black market, and plants trackers on wagons for an ambush.",
            135, 70, 150, 40,
            new("wrench", "Wrench", "A heavy wrench; every fourth blow rings a foe's head and stuns them a moment.", AttackKind.Weapon, 7, 0.45, 15, 2.2),
            new("caltrops", "Smoke Caltrops", "Caltrops and smoke at your feet: foes near you are hobbled, and lose sight of you.", AttackKind.Weapon, 25, 12, 8, 5, Seconds: 6),
            new("trap", "Spring Trap", "A trap set at your feet: the first foe to step on it is bitten hard and held fast.", AttackKind.Weapon, 30, 15, 45, 1.5, Seconds: 4),
            new("demolition", "Demolition", "Every charge in your pack at once: a blast that throws everything around you.", AttackKind.Weapon, 0, 0, 95, 8, DamageKind.Fire),
            new("scrapper", "Scrapper", "Break gear and wreckage down into parts the black market pays well for; a tracker planted on a wagon shows where it goes.", AttackKind.Weapon, 0, 0, 0.25, 0)),
    ];

    public static Discipline Get(string id) => All.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? For(id);

    /// <summary>The class for a character's look. Characters made before there were fifteen classes keep the nearest.</summary>
    public static Discipline For(string look) => look switch
    {
        "Knight" => Get("Paladin"),
        "Mage" => Get("Wizard"),
        "Rogue" => Get("Thief"),
        "Rogue_Hooded" => Get("Assassin"),
        _ => All.FirstOrDefault(d => d.Id == look) ?? All[2],
    };

    public static IEnumerable<Discipline> Of(Calling calling) => All.Where(d => d.Calling == calling);

    /// <summary>Who teaches each calling in Tristinwold, and where: you can join (or change) your class there.</summary>
    public static string GuildOf(Calling c) => c switch
    {
        Calling.Warrior => "the Iron Circle",
        Calling.Rogue => "the Velvet Knives",
        Calling.Bard => "the Bards' College",
        Calling.Artificer => "the Brass Foundry",
        Calling.Druid => "the Root Circle",
        _ => "the Magi Archive",
    };
}
