namespace Tristinwold.Gameplay;

/// <summary>
/// How a hero looks, picked apart from their class: a head, body, arms and legs from any of the nine KayKit
/// figures (they all share one skeleton, so any mix stands and moves), something on the head, the colours of
/// their clothes and skin, and their height. Kept on the character as a short line of text
/// ("head=Mage;body=Knight;..."), so the server needs to know nothing about it.
/// </summary>
public sealed record Avatar(string Head, string Body, string Arms, string Legs, string Hat, string Outfit, string Skin, string Height)
{
    /// <summary>Their people (see <see cref="Races"/>).</summary>
    public string Race { get; init; } = "human";
    /// <summary>For dragonkind: the colour of their dragon.</summary>
    public string Dragon { get; init; } = "Red";
    /// <summary>For dragonkind: the people they're half of, or "none" for pure dragonkind.</summary>
    public string Half { get; init; } = "none";
    public string Build { get; init; } = "Average";
    public string Beard { get; init; } = "none";
    public string Hair { get; init; } = "Brown";
    public string Horns { get; init; } = "none";

    public static readonly (string Name, float Width)[] Builds = [("Slim", 0.9f), ("Average", 1f), ("Broad", 1.12f)];
    public static readonly string[] Beards = ["none", "Stubble", "Short", "Long", "Forked", "Braided"];
    public static readonly (string Name, string Hex)[] Hairs =
    [
        ("Brown", "#5a3a22"), ("Black", "#1c1a1a"), ("Auburn", "#8a3a1c"), ("Blond", "#d8b060"), ("Copper", "#b8642a"),
        ("Grey", "#8a8a88"), ("White", "#e8e8e4"),
    ];
    public static readonly string[] HornStyles = ["none", "Nubs", "Swept", "Ram", "Long"];

    public bool IsDragonkind => Race == Races.Dragonkind;
    public bool PureDragon => IsDragonkind && Half == "none";

    /// <summary>Their people as it works out: a half-dragon is half of each.</summary>
    public Race People => !IsDragonkind ? Races.Get(Race) : PureDragon ? Races.Get(Races.Dragonkind) : Races.Half(Races.Get(Half));

    /// <summary>A dragonkind's dragon; null for everyone else.</summary>
    public DragonBlood? Blood => IsDragonkind ? Races.Dragon(Dragon) ?? Races.Dragons[0] : null;
    public float Width => (Builds.FirstOrDefault(b => b.Name == Build).Width is > 0 and var w ? w : 1) * (float)People.Width;
    public string HairHex => Hairs.FirstOrDefault(h => h.Name == Hair).Hex ?? "#5a3a22";

    /// <summary>What they are: "Elf", "Red dragonkind", "Gold half-dragon dwarf".</summary>
    public string Kind => Blood is { } d ? (PureDragon ? $"{d.Colour} dragonkind" : $"{d.Colour} half-dragon {Races.Get(Half).Name.ToLowerInvariant()}") : People.Name;
    /// <summary>The nine figures the parts come from.</summary>
    public static readonly string[] Figures =
        ["Knight", "Barbarian", "Mage", "Rogue", "Rogue_Hooded", "Skeleton_Warrior", "Skeleton_Rogue", "Skeleton_Mage", "Skeleton_Minion"];

    /// <summary>What can go on the head, and the figure each one is borrowed from.</summary>
    public static readonly (string Id, string Name, string Figure)[] Hats =
    [
        ("none", "Nothing", ""),
        ("Knight_Helmet", "Knight's helmet", "Knight"),
        ("Barbarian_Hat", "Horned hat", "Barbarian"),
        ("Mage_Hat", "Wizard's hat", "Mage"),
        ("Skeleton_Warrior_Helmet", "Old war helm", "Skeleton_Warrior"),
        ("Skeleton_Rogue_Hood", "Ragged hood", "Skeleton_Rogue"),
        ("Skeleton_Mage_Hat", "Lich's hat", "Skeleton_Mage"),
    ];

    /// <summary>Clothes colours: multiplied into the figure's own colours, so "Natural" leaves them be.</summary>
    public static readonly (string Name, string Hex)[] Outfits =
    [
        ("Natural", "#ffffff"), ("Crimson", "#e87a70"), ("Forest", "#90c890"), ("Royal", "#90a8f0"), ("Violet", "#c0a0e8"),
        ("Gold", "#f8dc90"), ("Rust", "#d8a070"), ("Ash", "#b0b0b0"), ("Midnight", "#7070a0"),
    ];

    public static readonly (string Name, string Hex)[] Skins =
    [
        ("Natural", "#ffffff"), ("Fair", "#fff0e6"), ("Tan", "#ecc8a4"), ("Brown", "#c49468"), ("Deep", "#94684a"),
        ("Ashen", "#b8b8c0"), ("Frost", "#c0dcff"), ("Verdant", "#b0dc98"),
    ];

    public static readonly (string Name, float Scale)[] Heights = [("Short", 0.68f), ("Average", 0.74f), ("Tall", 0.8f)];

    public string HatFigure => Hats.FirstOrDefault(h => h.Id == Hat).Figure ?? "";
    public string HatName => Hats.FirstOrDefault(h => h.Id == Hat).Name ?? "Nothing";
    public string OutfitHex => Outfits.FirstOrDefault(o => o.Name == Outfit).Hex ?? "#ffffff";
    /// <summary>Their skin: as picked, or their people's own (orcs and goblins are green) if left natural.</summary>
    public string SkinHex => Skin == "Natural" ? People.Skin : Skins.FirstOrDefault(s => s.Name == Skin).Hex ?? "#ffffff";
    public float Scale => (Heights.FirstOrDefault(h => h.Name == Height).Scale is > 0 and var s ? s : 0.74f) * (float)People.Size;

    /// <summary>How much bigger or smaller than an average human (for their collision, and the camera).</summary>
    public float Size => Scale / 0.74f;

    /// <summary>What a figure is called on the character page.</summary>
    public static string Called(string figure) => figure switch
    {
        "Rogue_Hooded" => "Hooded rogue",
        "Skeleton_Warrior" => "Skeleton warrior",
        "Skeleton_Rogue" => "Skeleton rogue",
        "Skeleton_Mage" => "Skeleton mage",
        "Skeleton_Minion" => "Skeleton",
        _ => figure,
    };

    /// <summary>The look each class starts with: its own figure from head to toe, and its own hat.</summary>
    public static Avatar For(string classId)
    {
        var (figure, hat) = classId switch
        {
            "Barbarian" => ("Barbarian", "Barbarian_Hat"),
            "Paladin" or "Knight" => ("Knight", "Knight_Helmet"),
            "Ranger" or "Assassin" or "Rogue_Hooded" => ("Rogue_Hooded", "none"),
            "Wizard" or "Mage" => ("Mage", "Mage_Hat"),
            "Warlock" or "Sorcerer" => ("Mage", "none"),
            _ => ("Rogue", "none"),
        };
        return new(figure, figure, figure, figure, hat, "Natural", "Natural", "Average");
    }

    public string Encode() => $"head={Head};body={Body};arms={Arms};legs={Legs};hat={Hat};outfit={Outfit};skin={Skin};height={Height}" +
                              $";race={Race};dragon={Dragon};half={Half};build={Build};beard={Beard};hair={Hair};horns={Horns}";

    /// <summary>Reads a saved look; anything missing or unknown is left as the class would have it. Null for none.</summary>
    public static Avatar? Parse(string? text, string classId = "")
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var a = For(classId);
        var parts = text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        string Pick(string key, string fallback, IEnumerable<string> allowed) =>
            parts.TryGetValue(key, out var v) && allowed.Contains(v) ? v : fallback;
        return new(
            Pick("head", a.Head, Figures), Pick("body", a.Body, Figures), Pick("arms", a.Arms, Figures), Pick("legs", a.Legs, Figures),
            Pick("hat", a.Hat, Hats.Select(h => h.Id)), Pick("outfit", a.Outfit, Outfits.Select(o => o.Name)),
            Pick("skin", a.Skin, Skins.Select(s => s.Name)), Pick("height", a.Height, Heights.Select(h => h.Name)))
        {
            Race = Pick("race", "human", Races.All.Select(r => r.Id)),
            Dragon = Pick("dragon", "Red", Races.Dragons.Select(d => d.Colour)),
            Half = Pick("half", "none", ["none", .. Races.Halves.Select(r => r.Id)]),
            Build = Pick("build", "Average", Builds.Select(b => b.Name)),
            Beard = Pick("beard", "none", Beards),
            Hair = Pick("hair", "Brown", Hairs.Select(h => h.Name)),
            Horns = Pick("horns", "none", HornStyles),
        };
    }

    /// <summary>A look thrown together at random ("Surprise me").</summary>
    public static Avatar Random(Random rng)
    {
        string Any(string[] from) => from[rng.Next(from.Length)];
        return new(Any(Figures), Any(Figures), Any(Figures), Any(Figures), Hats[rng.Next(Hats.Length)].Id,
            Outfits[rng.Next(Outfits.Length)].Name, Skins[rng.Next(Skins.Length)].Name, Heights[rng.Next(Heights.Length)].Name)
        {
            Race = Races.All[rng.Next(Races.All.Count)].Id,
            Dragon = Races.Dragons[rng.Next(Races.Dragons.Count)].Colour,
            Half = rng.Next(2) == 0 ? "none" : Races.Halves.ElementAt(rng.Next(Races.Halves.Count())).Id,
            Build = Builds[rng.Next(Builds.Length)].Name,
            Beard = rng.Next(3) == 0 ? Any(Beards) : "none",
            Hair = Hairs[rng.Next(Hairs.Length)].Name,
            Horns = rng.Next(4) == 0 ? Any(HornStyles) : "none",
        };
    }
}
