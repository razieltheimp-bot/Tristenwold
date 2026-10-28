using Tristinwold.Nemesis;

namespace Tristinwold.World;

/// <summary>
/// The words the generator dresses its places in: epithets, troubles and rumours for new
/// cities, and what each village, camp and lair is like. Kept in the world of the saga: the
/// Tongs, the Magi, the Rift, the Ashborn, Angels and Demons.
/// </summary>
static class Lore
{
    static readonly string[] Rulers =
    [
        "A one-eyed jarl", "A council of merchant widows", "A boy-king and his frightened regent", "The Magi, openly",
        "A Tong boss who calls herself the Harbormistress", "An old general who never took off his armour",
        "Twin sisters who have not spoken to each other in nine years", "A priest of {divine} and the priest's many debts",
        "A jarl nobody has seen in a year", "The guild of salt-merchants",
    ];

    static readonly string[] Troubles =
    [
        "The wells have turned to salt, and someone is selling clean water at a ruinous price.",
        "Ashborn refugees sleep outside the walls, and every week the gate stays shut a little longer.",
        "Since the Rift opened the bells ring on their own at midnight.",
        "Wolves have taken the north road, and the wolves are larger than wolves should be.",
        "The dead were seen walking in the lower town on the last new moon.",
        "A Tong war is coming: the Drowned Hand want the river trade, and the Lantern Boys already have it.",
        "The harvest failed twice, and the granary keeper has grown oddly rich.",
        "Children have started drawing the same door, over and over, without being taught.",
        "A sickness that turns the skin grey is moving house to house.",
        "The silver in the counting-house keeps turning to rust.",
    ];

    static readonly string[] Rumours =
    [
        "People say a stranger came through who knew everyone's name before he was told.",
        "They say an Angel stood on the walls one dawn and wept.",
        "There is talk of a Keystone hidden somewhere in the old town.",
        "The Magi have been buying every old map in the city.",
        "A demon is said to drink at the tavern on feast nights, and to pay well.",
        "Someone has been leaving coins on the graves of people who are not dead yet.",
        "The Seven Sins have a priest here, though nobody can say who.",
    ];

    static readonly string[] CityEpithets =
    [
        "the City of {thing}", "the {adj} Hold", "the Town of {thing}", "the {adj} Gate", "the Seat of {thing}",
        "the City Under {thing}", "the Last Light of the {dir}",
    ];

    static readonly string[] Things = ["Bells", "Salt", "Ash", "Lanterns", "Ravens", "Antlers", "Graves", "Wells", "Chains", "Glass", "Thorns", "Rain"];
    static readonly string[] Adjectives = ["Grey", "Iron", "Frozen", "Sunken", "Burning", "Silent", "Weeping", "Broken", "Golden", "Black"];
    static readonly string[] Directions = ["North", "East", "West", "Far Shore", "High Passes"];

    public static (string Epithet, string Story, IReadOnlyList<string> Places) City(Rng rng, string name, bool town, Biome biome)
    {
        var epithet = Fill(rng, rng.Pick(CityEpithets));
        if (town) epithet = epithet.Replace("City", "Town");
        var keep = rng.Pick(["Hall", "Keep", "Tower", "Court", "Longhouse"]);
        var places = new List<string>
        {
            $"The {rng.Pick(Adjectives)} {keep}",
            Names.Inn(rng),
            $"The Temple of {Names.Divine(rng)}",
            $"The {rng.Pick(["Fish", "Salt", "Horse", "Rag", "Spice", "Bone"])} Market",
            $"The {rng.Pick(["Old", "Under", "Drowned", "Lantern"])} Quarter",
        };
        if (!town) places.Add($"The {rng.Pick(["Smithy", "Tannery", "Library", "Barracks", "Bathhouse"])}");
        var ruler = Fill(rng, rng.Pick(Rulers));
        var setting = biome switch
        {
            Biome.Snowfield => "in the snow",
            Biome.Tundra => "on the cold plain",
            Biome.Marsh => "on stilts above the marsh",
            Biome.Crags => "cut into the mountainside",
            Biome.PineForest => "among the pines",
            _ => "in the autumn woods",
        };
        var story = $"A {(town ? "town" : "city")} {setting}. {ruler} rules from {places[0]}. {rng.Pick(Troubles)} {rng.Pick(Rumours)}";
        return (epithet, story, places);
    }

    public static Settlement Site(Rng rng, string id, SettlementKind kind, Biome biome, double x, double z, double raw, double danger)
    {
        var floor = Math.Max(raw, WorldGen.WaterLevel + 1.2);
        Settlement Make(string name, string epithet, string story, double radius, int people, SiteLair? lair = null) =>
            new(id, kind, name, epithet, story, x, z, radius, floor, biome, [], lair, people);

        switch (kind)
        {
            case SettlementKind.Hamlet:
            {
                var trade = biome switch
                {
                    Biome.Marsh => "eel-fishers", Biome.Tundra or Biome.Snowfield => "herders", Biome.Crags => "goat-keepers",
                    Biome.PineForest => "woodcutters", _ => "orchard-keepers",
                };
                return Make(Names.Place(rng), $"a hamlet of {trade}", $"A handful of {trade}' houses. {rng.Pick(HamletWorries)}", 30, 4 + rng.Next(3));
            }
            case SettlementKind.Farmstead:
                return Make($"{Names.Family(rng)} {rng.Pick(["Farm", "Steading", "Croft"])}", "a farmstead",
                    $"One family and their fields. {rng.Pick(FarmWorries)}", 16, 2 + rng.Next(2));
            case SettlementKind.HuntingCamp:
                return Make(rng.Chance(0.5) ? $"{Names.Given(rng, Sex.Other)}'s Camp" : $"{Names.Place(rng)} Camp", "a hunting camp",
                    "Hides on frames and a fire that never goes out. The hunters know every trail for a day's walk.", 12, 2);
            case SettlementKind.Inn:
                return Make(Names.Inn(rng), "a roadside inn", $"Beds, stew and gossip for anyone on the road. {rng.Pick(Rumours)}", 14, 3);
            case SettlementKind.Shrine:
            {
                var god = Names.Divine(rng);
                return Make($"The Shrine of {god}", "a shrine",
                    $"Standing stones and offerings to {god}. Pilgrims come, and not all of them leave.", 10, 1 + rng.Next(2));
            }
            case SettlementKind.Mine:
                return Make($"{Names.Place(rng)} Mine", $"a {rng.Pick(["silver", "iron", "copper", "salt", "moonstone"])} mine",
                    $"Miners and their lamps. {rng.Pick(MineWorries)}", 18, 3);
            case SettlementKind.AshbornCamp:
                return Make($"{Names.Place(rng)} Refuge", "an Ashborn refuge",
                    "Half-demon children of the Rift and the few who care for them, hiding from the cities that walled them out.", 20, 3 + rng.Next(2));
            case SettlementKind.BanditCamp:
                return Make($"{Names.Place(rng)} {rng.Pick(["Hideout", "Fort", "Lookout", "Redoubt"])}", "a bandit camp",
                    $"Bandits watch the road from here. Their chief is called {Names.Given(rng, Sex.Other)} the {rng.Pick(Nicknames)}.", 22, 0,
                    new SiteLair("bandit", 2 + rng.Next(Math.Min(3, (int)danger + 1)), "bandit_chief"));
            case SettlementKind.Barrow:
                return Make($"{Names.Place(rng)} {rng.Pick(["Barrow", "Tomb", "Crypt"])}", "a barrow",
                    "A tomb of the first kings. Whatever is inside is not resting.", 18, 0,
                    new SiteLair("draugr", 2 + rng.Next(2), danger > 1.4 ? "draugr_overlord" : null));
            default:
            {
                var (beast, count) = biome switch
                {
                    Biome.Snowfield or Biome.Tundra => rng.Chance(0.4) ? ("frost_troll", 1) : ("ice_wolf", 3),
                    Biome.Marsh => rng.Chance(0.5) ? ("mud_crab", 3) : ("giant_rat", 4),
                    Biome.Crags => rng.Chance(0.5) ? ("bear", 1) : ("frost_troll", 1),
                    Biome.AutumnForest => rng.Chance(0.4) ? ("spriggan", 1) : ("wolf", 3),
                    _ => rng.Chance(0.4) ? ("bear", 1) : ("wolf", 3),
                };
                return Make($"{rng.Pick(["Wolfjaw", "Bonecrack", "Gutter", "Bleak", "Howling", "Bloodmoss", "Chill", "Moaning", "Gnaw"])} {rng.Pick(["Cave", "Den", "Hollow", "Grotto"])}",
                    "a beast's den", "Something lairs here. The hunters give it a wide berth.", 14, 0, new SiteLair(beast, count));
            }
        }
    }

    static readonly string[] HamletWorries =
    [
        "The elder wants nothing to do with Tristinwold or its Tongs.", "Lately the dogs bark at nothing every night.",
        "A good place, if you don't ask where the miller's first wife went.", "They pay a Tong tithe and pretend they don't.",
        "Someone keeps leaving carved birds on doorsteps.", "The well is said to be bottomless; children dare each other to shout into it.",
    ];

    static readonly string[] FarmWorries =
    [
        "Something has been taking the sheep.", "The eldest son left for the city and never wrote.", "Good soil, and old bones in it.",
        "They swear the scarecrow moves.",
    ];

    static readonly string[] MineWorries =
    [
        "The deep tunnel was sealed after a cave-in. Some nights there is knocking from the other side.",
        "The vein is running out, and the foreman won't say so.", "The ore comes up warm, which ore should not.",
    ];

    static readonly string[] Nicknames = ["Red", "Butcher", "Smiling", "Fox", "Tall", "Quiet", "Hook", "Lucky", "Ashen", "Crow"];

    public static string Fill(Rng rng, string template) => template
        .Replace("{divine}", Names.Divine(rng))
        .Replace("{thing}", rng.Pick(Things))
        .Replace("{adj}", rng.Pick(Adjectives))
        .Replace("{dir}", rng.Pick(Directions));
}
