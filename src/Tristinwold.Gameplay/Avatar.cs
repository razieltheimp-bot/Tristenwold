namespace Tristinwold.Gameplay;

/// <summary>
/// How a hero looks, picked apart from their class. On a PC the hero has a real body (see the game's Humans): their
/// people, sex and age pick the body, and the face, eyes, hair and its colour, clothes, skin tone, height and build
/// dress it. Where the real bodies aren't (the phone), the older choices stand in: a head, body, arms and legs from
/// any of the nine KayKit figures, and something on the head. Kept on the character as a short line of text
/// ("head=Mage;body=Knight;..."), so the server needs to know nothing about it.
/// </summary>
public sealed record Avatar(string Head, string Body, string Arms, string Legs, string Hat, string Outfit, string Skin, string Height)
{
    /// <summary>Their people (see <see cref="Races"/>).</summary>
    public string Race { get; init; } = "human";
    /// <summary>"Male" or "Female": the real bodies come in both.</summary>
    public string Sex { get; init; } = "Male";
    /// <summary>"Adult", "Seasoned" (an adult's body, an older face) or "Elder".</summary>
    public string Age { get; init; } = "Adult";
    /// <summary>The face the skin is painted from: "Natural" (their people's own mix), "Fair", "Olive" or "Dark".</summary>
    public string Face { get; init; } = "Natural";
    /// <summary>The colour of their eyes: "Natural" (their people's) or a colour.</summary>
    public string Eyes { get; init; } = "Natural";
    /// <summary>The hair style: "Natural" (as their body was made), "None" (shaved), or one of <see cref="Styles"/>.</summary>
    public string Style { get; init; } = "Natural";
    /// <summary>What covers the hair on a real body: "none", "Hood" or "Cap".</summary>
    public string Headwear { get; init; } = "none";
    /// <summary>For dragonkind: the colour of their dragon.</summary>
    public string Dragon { get; init; } = "Red";
    /// <summary>For dragonkind: the people they're half of, or "none" for pure dragonkind.</summary>
    public string Half { get; init; } = "none";
    public string Build { get; init; } = "Average";
    public string Beard { get; init; } = "none";
    public string Hair { get; init; } = "Brown";
    public string Horns { get; init; } = "none";
    /// <summary>The colour of their shirt or tunic, and of their trousers: "Class" (what the class wears) or a cloth.</summary>
    public string Top { get; init; } = "Class";
    public string Bottom { get; init; } = "Class";
    /// <summary>Whether the sleeves are long or the arms bare: "Class", "Long" or "Short".</summary>
    public string Sleeves { get; init; } = "Class";
    /// <summary>What's worn over the clothes that isn't armour: "Class" (the class's own kit) or one of <see cref="Wears"/>.</summary>
    public string Wear { get; init; } = "Class";
    /// <summary>The plant people's two looks: leaf and root in their "Spring", bare wood and branch in their "Fall".</summary>
    public string Form { get; init; } = "Spring";

    public static readonly (string Name, float Width)[] Builds = [("Slim", 0.9f), ("Average", 1f), ("Broad", 1.12f)];
    public static readonly string[] Beards = ["none", "Stubble", "Short", "Long", "Forked", "Braided"];
    public static readonly (string Name, string Hex)[] Hairs =
    [
        ("Brown", "#5a3a22"), ("Black", "#1c1a1a"), ("Auburn", "#8a3a1c"), ("Blond", "#d8b060"), ("Copper", "#b8642a"),
        ("Grey", "#8a8a88"), ("White", "#e8e8e4"),
        ("Chestnut", "#6e3f22"), ("Honey", "#c08a3e"), ("Platinum", "#e0d8bc"), ("Red", "#a02818"), ("Raven", "#16161e"), ("Moss", "#4a6a34"), ("Sapphire", "#2a4a9a"), ("Rose", "#c0587a"),
    ];
    public static readonly string[] HornStyles = ["none", "Nubs", "Swept", "Ram", "Long"];
    public static readonly string[] Sexes = ["Male", "Female"];
    public static readonly string[] Ages = ["Adult", "Seasoned", "Elder"];
    public static readonly string[] Faces = ["Natural", "Fair", "Olive", "Dark"];
    public static readonly string[] EyeColours = ["Natural", "Brown", "Hazel", "Blue", "Green", "Grey", "Amber", "Red"];
    /// <summary>The hair styles the real bodies were made with, by sex (see tools/humans): a hero may wear any of their sex's.</summary>
    public static readonly (string Id, string Name)[] MaleStyles = [("Short01", "Short"), ("Short02", "Cropped"), ("Short03", "Swept"), ("Short04", "Tousled"), ("Afro01", "Afro")];
    public static readonly (string Id, string Name)[] FemaleStyles = [("Bob01", "Bob"), ("Bob02", "Long bob"), ("Long01", "Long"), ("Braid01", "Braid"), ("Ponytail01", "Ponytail")];
    public static readonly string[] Headwears = ["none", "Hood", "Cap"];
    /// <summary>Cuts built on the head from the body's own measure rather than baked into a body, so anyone can wear them.</summary>
    public static readonly (string Id, string Name)[] Cuts = [("Topknot", "Top-knot"), ("Bun", "Bun"), ("Mohawk", "Mohawk"), ("Spikes", "Spikes"), ("Mane", "Mane"), ("Twintails", "Twin tails")];
    public static bool IsCut(string style) => Cuts.Any(c => c.Id == style);
    public static IEnumerable<string> Styles(string sex) => ["Natural", "None", .. (sex == "Female" ? FemaleStyles : MaleStyles).Select(s => s.Id), .. Cuts.Select(c => c.Id)];

    /// <summary>Plain cloth for shirts and trousers, picked apart from each other.</summary>
    public static readonly (string Name, string Hex)[] Cloths =
    [
        ("Class", ""), ("White", "#e8e4d8"), ("Linen", "#cdbf9f"), ("Sand", "#b09a70"), ("Russet", "#8a4a26"), ("Crimson", "#8a2020"), ("Rose", "#b86a7a"),
        ("Gold", "#c49a3a"), ("Olive", "#6a6a30"), ("Forest", "#2f5a34"), ("Teal", "#2a6a6a"), ("Royal", "#2a3f8a"), ("Violet", "#5a3a7a"), ("Slate", "#4a5058"), ("Charcoal", "#2e2e32"), ("Black", "#17171a"),
    ];
    public static readonly string[] Forms = ["Spring", "Fall"];
    public static readonly string[] SleeveStyles = ["Class", "Long", "Short"];
    public static readonly string[] Wears = ["Class", "Plain", "Vest", "Jerkin", "Robe", "Wrapped robe", "Sash", "Cloak"];
    public static string? ClothHex(string name) => Cloths.FirstOrDefault(c => c.Name == name).Hex is { Length: > 0 } hex ? hex : null;
    public static string StyleName(string style) => style switch
    {
        "Natural" => "As made", "None" => "Shaved",
        _ => MaleStyles.Concat(FemaleStyles).Concat(Cuts).FirstOrDefault(s => s.Id == style).Name ?? style,
    };
    public bool Female => Sex == "Female";

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
            "Ranger" or "Assassin" or "Saboteur" or "Rogue_Hooded" => ("Rogue_Hooded", "none"),
            "Wizard" or "Mage" => ("Mage", "Mage_Hat"),
            "Warlock" or "Sorcerer" or "Poet" or "Scribe" or "Stormdruid" or "Spiritcaller" => ("Mage", "none"),
            _ => ("Rogue", "none"),
        };
        return new(figure, figure, figure, figure, hat, "Natural", "Natural", "Average") { Headwear = figure == "Rogue_Hooded" ? "Hood" : "none" };
    }

    public string Encode() => $"head={Head};body={Body};arms={Arms};legs={Legs};hat={Hat};outfit={Outfit};skin={Skin};height={Height}" +
                              $";race={Race};dragon={Dragon};half={Half};build={Build};beard={Beard};hair={Hair};horns={Horns}" +
                              $";sex={Sex};age={Age};face={Face};eyes={Eyes};style={Style};headwear={Headwear}" +
                              $";top={Top};bottom={Bottom};sleeves={Sleeves};wear={Wear};form={Form}";

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
            Sex = Pick("sex", "Male", Sexes),
            Age = Pick("age", "Adult", Ages),
            Face = Pick("face", "Natural", Faces),
            Eyes = Pick("eyes", "Natural", EyeColours),
            Style = Pick("style", "Natural", Styles(Pick("sex", "Male", Sexes))),
            Headwear = Pick("headwear", "none", Headwears),
            Top = Pick("top", "Class", Cloths.Select(c => c.Name)),
            Bottom = Pick("bottom", "Class", Cloths.Select(c => c.Name)),
            Sleeves = Pick("sleeves", "Class", SleeveStyles),
            Wear = Pick("wear", "Class", Wears),
            Form = Pick("form", "Spring", Forms),
        };
    }

    /// <summary>A look thrown together at random ("Surprise me").</summary>
    public static Avatar Random(Random rng)
    {
        string Any(string[] from) => from[rng.Next(from.Length)];
        var sex = Any(Sexes);
        return new Avatar(Any(Figures), Any(Figures), Any(Figures), Any(Figures), Hats[rng.Next(Hats.Length)].Id,
            Outfits[rng.Next(Outfits.Length)].Name, Skins[rng.Next(Skins.Length)].Name, Heights[rng.Next(Heights.Length)].Name)
        {
            Race = Races.All[rng.Next(Races.All.Count)].Id,
            Dragon = Races.Dragons[rng.Next(Races.Dragons.Count)].Colour,
            Half = rng.Next(2) == 0 ? "none" : Races.Halves.ElementAt(rng.Next(Races.Halves.Count())).Id,
            Build = Builds[rng.Next(Builds.Length)].Name,
            Beard = rng.Next(3) == 0 ? Any(Beards) : "none",
            Hair = Hairs[rng.Next(Hairs.Length)].Name,
            Horns = rng.Next(4) == 0 ? Any(HornStyles) : "none",
            Sex = sex,
            Age = rng.Next(4) == 0 ? Any(Ages) : "Adult",
            Face = Any(Faces),
            Eyes = Any(EyeColours),
            Style = Any([.. Styles(sex)]),
            Headwear = rng.Next(3) == 0 ? Any(Headwears) : "none",
            Top = Cloths[rng.Next(Cloths.Length)].Name,
            Bottom = Cloths[rng.Next(Cloths.Length)].Name,
            Sleeves = Any(SleeveStyles),
            Wear = Any(Wears),
            Form = Any(Forms),
        };
    }
}
