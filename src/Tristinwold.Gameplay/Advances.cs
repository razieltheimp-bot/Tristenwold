using Tristinwold.Deviant;

namespace Tristinwold.Gameplay;

/// <summary>What a learned move does: hits what's in front, hits everything near, mends, guards, quickens, or is always on.</summary>
public enum MoveStyle { Strike, Burst, Mend, Ward, Haste, Passive }

/// <summary>A move a class comes into as it levels: the level, and how it works. The move itself is an ordinary <see cref="Ability"/>.</summary>
public sealed record Advance(int Level, MoveStyle Style, Ability Move);

/// <summary>
/// A hero starts with their class's four moves, and earns three more as they level, at <see cref="First"/>,
/// <see cref="Second"/> and <see cref="Third"/>. They're all kept, and which of them sit on the quick keys is the hero's choice.
/// </summary>
public static class Advances
{
    public const int First = 6, Second = 12, Third = 18;
    /// <summary>The level an Ether-Aeronaut's construct takes to the air.</summary>
    public const int FlightLevel = First;
    /// <summary>The level a Shapeshifter can hold a half-form: the beast and their own people in one body.</summary>
    public const int HalfFormLevel = 8;

    static Advance[] Three(Ability first, MoveStyle firstStyle, Ability second, MoveStyle secondStyle, Ability third, MoveStyle thirdStyle) =>
        [new(First, firstStyle, first), new(Second, secondStyle, second), new(Third, thirdStyle, third)];

    static Ability Blow(string id, string name, string text, double cost, double cooldown, double power, DamageKind kind = DamageKind.Physical) =>
        new(id, name, text, AttackKind.Weapon, cost, cooldown, power, 3, kind);
    static Ability Shot(string id, string name, string text, double cost, double cooldown, double power, double range, DamageKind kind, AttackKind pays = AttackKind.Spell) =>
        new(id, name, text, pays, cost, cooldown, power, range, kind);
    static Ability Round(string id, string name, string text, double cost, double cooldown, double power, double radius, DamageKind kind, AttackKind pays = AttackKind.Spell) =>
        new(id, name, text, pays, cost, cooldown, power, radius, kind);
    static Ability Mend(string id, string name, string text, double cost, double cooldown, double share, AttackKind pays = AttackKind.Spell) =>
        new(id, name, text, pays, cost, cooldown, share, 0);
    static Ability Guard(string id, string name, string text, double cost, double cooldown, double seconds, AttackKind pays = AttackKind.Spell) =>
        new(id, name, text, pays, cost, cooldown, 0, 0, Seconds: seconds);

    static readonly Dictionary<string, Advance[]> All = new()
    {
        ["MartialArtist"] = Three(
            Blow("rising_kick", "Rising Dragon Kick", "A leaping kick that lands with your whole weight.", 30, 8, 36), MoveStyle.Strike,
            Guard("iron_breath", "Iron Breath", "Breathe, and let the blows come: Protect and Shell for a while.", 35, 30, 10, AttackKind.Weapon), MoveStyle.Ward,
            Round("thousand_palms", "Thousand Palms", "Blows too quick to count, on everything around you.", 45, 16, 58, 5, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Burst),
        ["Barbarian"] = Three(
            Blow("skull_splitter", "Skull Splitter", "One great overhand blow.", 30, 8, 40), MoveStyle.Strike,
            Round("earthshaker", "Earthshaker", "Bring your weapon down, and the ground throws everything near you.", 40, 14, 48, 6, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Burst,
            Mend("unbroken", "Unbroken", "You refuse to fall: a third of your wounds close.", 40, 40, 0.33, AttackKind.Weapon), MoveStyle.Mend),
        ["Paladin"] = Three(
            Blow("hammer_of_dawn", "Hammer of Dawn", "A blow with the sunrise behind it.", 30, 8, 36, DamageKind.Sun), MoveStyle.Strike,
            Mend("lay_on_hands", "Lay on Hands", "A third of your wounds close under your own hand.", 45, 35, 0.35), MoveStyle.Mend,
            Round("aura_of_dawn", "Aura of Dawn", "Light breaks out of you, and burns what stands near.", 50, 16, 56, 7, DamageKind.Sun), MoveStyle.Burst),
        ["Ranger"] = Three(
            Shot("piercing_arrow", "Piercing Arrow", "A heavy shaft, drawn all the way back.", 28, 8, 38, 30, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Strike,
            Guard("second_wind", "Second Wind", "Your legs come back to you: you move like the wind for a while.", 25, 28, 10, AttackKind.Weapon), MoveStyle.Haste,
            Round("hail_of_arrows", "Hail of Arrows", "Everything around you is under the arrows.", 42, 16, 54, 14, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Burst),
        ["Thief"] = Three(
            Blow("cheap_shot", "Cheap Shot", "Low, fast and thoroughly unfair.", 24, 7, 34), MoveStyle.Strike,
            Guard("smoke_step", "Smoke Step", "You're quicker than they thought, for a while.", 25, 26, 9, AttackKind.Weapon), MoveStyle.Haste,
            Round("fan_of_knives", "Fan of Knives", "A knife for everyone near you.", 40, 14, 50, 7, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Burst),
        ["Assassin"] = Three(
            Blow("hamstring", "Hamstring", "A poisoned cut at the back of the leg.", 26, 8, 36, DamageKind.Poison), MoveStyle.Strike,
            Guard("veil", "Veil", "Nothing quite lands on you for a while: Protect and Shell.", 30, 30, 8, AttackKind.Weapon), MoveStyle.Ward,
            Round("death_blossom", "Death Blossom", "You turn once, and every blade is wet.", 42, 16, 56, 5, DamageKind.Poison, AttackKind.Weapon), MoveStyle.Burst),
        ["Wizard"] = Three(
            Shot("frost_lance", "Frost Lance", "A spear of ice at the foe you face.", 30, 7, 36, 28, DamageKind.Frost), MoveStyle.Strike,
            Guard("mana_shield", "Mana Shield", "Your will stands between you and the blow: Protect and Shell.", 40, 30, 10), MoveStyle.Ward,
            Round("starfall", "Starfall", "The sky comes down around you, a piece at a time.", 55, 18, 62, 12, DamageKind.Fire), MoveStyle.Burst),
        ["Warlock"] = Three(
            Shot("hex_bolt", "Hex Bolt", "A word nobody should say, thrown.", 28, 7, 36, 26, DamageKind.Shock), MoveStyle.Strike,
            Mend("blood_pact", "Blood Pact", "Your patron settles a little of what you're owed: a third of your wounds close.", 40, 35, 0.33), MoveStyle.Mend,
            Round("dark_harvest", "Dark Harvest", "The life goes out of everything near you.", 52, 16, 56, 9, DamageKind.Frost), MoveStyle.Burst),
        ["Sorcerer"] = Three(
            Shot("arc_lash", "Arc Lash", "A whip of lightning that doesn't want to stop.", 30, 7, 38, 26, DamageKind.Shock), MoveStyle.Strike,
            Guard("blink", "Blink", "You're somewhere else, and quicker, for a while.", 30, 26, 9), MoveStyle.Haste,
            Round("ring_of_fire", "Ring of Fire", "Fire goes out from you in a ring.", 50, 16, 58, 9, DamageKind.Fire), MoveStyle.Burst),
        ["Poet"] = Three(
            Shot("cutting_verse", "Cutting Verse", "A couplet so unkind it draws blood.", 26, 7, 34, 22, DamageKind.Shock), MoveStyle.Strike,
            Mend("ode_of_mending", "Ode of Mending", "A kind word, well put: a third of your wounds close.", 38, 32, 0.33), MoveStyle.Mend,
            Round("epic_refrain", "Epic Refrain", "A line nobody near you can stand to hear again.", 48, 16, 52, 9, DamageKind.Shock), MoveStyle.Burst),
        ["Scribe"] = Three(
            Shot("word_of_binding", "Word of Binding", "A word written on the air, and it bites.", 26, 7, 34, 22, DamageKind.Sun), MoveStyle.Strike,
            Guard("margin_note", "Margin Note", "You've written yourself out of harm's way: Protect and Shell.", 35, 30, 10), MoveStyle.Ward,
            Round("final_draft", "Final Draft", "Everything near you is struck out.", 48, 16, 54, 9, DamageKind.Sun), MoveStyle.Burst),
        ["Musician"] = Three(
            Shot("sharp_note", "Sharp Note", "One note, far too high, right at them.", 26, 7, 34, 22, DamageKind.Shock), MoveStyle.Strike,
            Mend("lullaby", "Lullaby", "You play yourself back together: a third of your wounds close.", 38, 32, 0.33), MoveStyle.Mend,
            Round("final_chorus", "Final Chorus", "Everything near you shakes apart.", 48, 16, 54, 9, DamageKind.Shock), MoveStyle.Burst),
        ["Alchemist"] = Three(
            Shot("acid_flask", "Acid Flask", "It eats through what it lands on.", 28, 7, 36, 18, DamageKind.Poison), MoveStyle.Strike,
            Mend("elixir_of_vigour", "Elixir of Vigour", "One of the good ones: a third of your wounds close.", 38, 32, 0.33), MoveStyle.Mend,
            Round("grand_detonation", "Grand Detonation", "You've mixed all of it at once.", 52, 18, 60, 10, DamageKind.Fire), MoveStyle.Burst),
        ["EtherAeronaut"] = Three(
            new("jet_hull", "Jet Hull", "Inside your construct, jump again in the air and it flies: hold jump to climb, and descend to sink.", AttackKind.Spell, 0, 0, 0, 0), MoveStyle.Passive,
            Shot("ether_lance", "Ether Lance", "A lance of raw ether from the hull's forward vents.", 32, 8, 42, 26, DamageKind.Shock), MoveStyle.Strike,
            Round("boiler_burst", "Boiler Burst", "You open every valve at once.", 50, 18, 60, 9, DamageKind.Fire), MoveStyle.Burst),
        ["Stormdruid"] = Three(
            Shot("thorn_lash", "Thorn Lash", "The nearest brambles do as they're told.", 28, 7, 36, 24, DamageKind.Poison), MoveStyle.Strike,
            Mend("rejuvenate", "Rejuvenate", "The green comes back into you: a third of your wounds close.", 38, 32, 0.33), MoveStyle.Mend,
            Round("earthquake", "Earthquake", "The ground under everything near you remembers it can move.", 52, 18, 60, 11, DamageKind.Physical), MoveStyle.Burst),
        ["Shapeshifter"] = [.. Three(
            Blow("rending_bite", "Rending Bite", "All the teeth you've got.", 28, 8, 38), MoveStyle.Strike,
            Mend("wild_mending", "Wild Mending", "Beasts heal fast: a third of your wounds close.", 38, 32, 0.33, AttackKind.Weapon), MoveStyle.Mend,
            Round("primal_roar", "Primal Roar", "A sound from before there were words, and it hurts.", 45, 16, 54, 8, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Burst),
            // A fourth, between the first and the second: the beast and the hero's own people in one body, for a while, with a long rest after.
            new Advance(HalfFormLevel, MoveStyle.Ward, new Ability("half_form", "Half-Form", "For a while the beast and your own people in one body, stronger than either, standing tall: then back to yourself. Takes the shape of the beast you wear, or the first you learned.", AttackKind.Weapon, 30, 90, 0.6, 0, Seconds: 25))],
        ["Spiritcaller"] = Three(
            Shot("ghost_lance", "Ghost Lance", "A cold spear of what's left of something.", 28, 7, 36, 26, DamageKind.Frost), MoveStyle.Strike,
            Guard("spirit_shield", "Spirit Shield", "The dead stand round you for a while: Protect and Shell.", 38, 30, 10), MoveStyle.Ward,
            Round("wailing_host", "Wailing Host", "Every spirit in hearing comes through you at once.", 52, 16, 58, 10, DamageKind.Frost), MoveStyle.Burst),
        ["Saboteur"] = Three(
            Shot("shrapnel_shot", "Shrapnel Shot", "A handful of bolts and bad intentions.", 26, 7, 36, 18, DamageKind.Physical, AttackKind.Weapon), MoveStyle.Strike,
            Mend("field_repairs", "Field Repairs", "You've mended worse with less: a third of your wounds close.", 36, 32, 0.33, AttackKind.Weapon), MoveStyle.Mend,
            Round("blast_charge", "Blast Charge", "You were carrying this the whole time.", 48, 18, 58, 9, DamageKind.Fire, AttackKind.Weapon), MoveStyle.Burst),
    };

    /// <summary>A class's three, in the order they come.</summary>
    public static IReadOnlyList<Advance> Of(string classId) => All.TryGetValue(classId, out var moves) ? moves : [];

    public static Advance? For(string moveId) => All.Values.SelectMany(a => a).FirstOrDefault(a => a.Move.Id == moveId);

    public static IEnumerable<Advance> Everything => All.Values.SelectMany(a => a);

    /// <summary>What a hero of this class and level has come into so far.</summary>
    public static IEnumerable<Advance> Known(string classId, int level) => Of(classId).Where(a => a.Level <= level);

    /// <summary>What's new between two levels.</summary>
    public static IEnumerable<Advance> Between(string classId, int from, int to) => Of(classId).Where(a => a.Level > from && a.Level <= to);
}
