namespace Tristinwold.Gameplay;

/// <summary>
/// Two more of the incomplete immortals, and the worst of them, both on the server for everyone.
/// <para>
/// Korrin: the first thing the Magi made, before the Ringing worked. A half-dragon elf, kept asleep in a stasis tank in
/// the warehouse by Tristinwold's east gate, with a brass plate that says only SUBJECT ONE. Anyone can pull the lever.
/// Awake he's pure malice and quite mad: he takes Tristinwold for the seat of an empire, shuts every stall and the
/// caravan yard, and every other day his thugs take another town and shut that too. Thugs and grunts obey him from
/// the first; the deviants don't. He fights in two forms, the second as strong again as the first. Killed, he's back
/// in three days and takes the city again, unless whoever beat him drags him back to the tank.
/// </para>
/// <para>
/// Ronacse: Pride, of the Seven Deadly Sins, and the only one of them loose on this side. A demon who passes for a
/// paladin in gold plate, until he stops bothering to. He climbed out of hell through the Rift and means to open it for
/// his six siblings: every hero he humbles breaks a seal, and every time he's killed one mends. He calls meteors down
/// on whoever he's fighting.
/// </para>
/// </summary>
public static class Undying
{
    /// <summary>How long a killed one stays down, in days.</summary>
    public const int KorrinDown = 3, PrideDown = 1;
    /// <summary>How often Korrin's thugs take another town, in days.</summary>
    public const int ConquerEvery = 2;
    /// <summary>The seals on the six other Sins.</summary>
    public const int Seals = 6;

    public static readonly string[] Siblings = ["Wrath", "Envy", "Greed", "Sloth", "Gluttony", "Lust"];

    public const string Capital = "city:tristinwold";

    public static readonly Avatar KorrinLook = Avatar.For("Paladin") with
    {
        Race = Races.Dragonkind, Dragon = "Black", Half = "elf", Sex = "Male", Horns = "Long", Skin = "Ashen", Style = "None", Build = "Average", Height = "Tall",
        Top = "Black", Bottom = "Black", Wear = "Class", Outfit = "Natural", Eyes = "Red", Headwear = "none",
    };

    public static readonly Avatar KorrinFinalLook = KorrinLook with { Build = "Broad", Horns = "Ram", Eyes = "Red" };

    public static readonly Avatar PrideLook = Avatar.For("Paladin") with
    {
        Race = "human", Sex = "Male", Hair = "Copper", Style = "Short03", Beard = "Stubble", Build = "Broad", Height = "Tall", Top = "Gold", Bottom = "Black",
        Wear = "Class", Outfit = "Natural", Eyes = "Amber", Headwear = "none",
    };

    public static readonly Avatar PrideUnboundLook = PrideLook with { Hair = "White", Style = "Spikes", Top = "Class", Bottom = "Black", Eyes = "Red" };
}

public sealed partial class CityEconomy
{
    // Korrin.
    /// <summary>Out of the tank, and who let him out.</summary>
    public bool KorrinFree { get; set; }
    public string KorrinFreedBy { get; set; } = "";
    /// <summary>The day he's back on his feet, after being killed.</summary>
    public int KorrinBack { get; set; }
    public int KorrinDeaths { get; set; }
    public int KorrinWins { get; set; }
    /// <summary>The towns his thugs hold, besides Tristinwold, and the day they take the next.</summary>
    public List<string> KorrinTowns { get; set; } = [];
    public int KorrinNext { get; set; }

    /// <summary>He's awake and on his feet: Tristinwold is his.</summary>
    public bool KorrinHolds => KorrinFree && Day >= KorrinBack;
    public int KorrinLevel => Drifters.Level(KorrinWins, KorrinDeaths);

    /// <summary>Nobody's buying or selling here: Korrin holds it.</summary>
    public bool TradeShut(string? town) => KorrinHolds && town is not null && (town == Undying.Capital || KorrinTowns.Contains(town));

    bool KorrinFreed(string? by)
    {
        if (KorrinFree || by is not { Length: > 0 and <= 60 }) return false;
        (KorrinFree, KorrinFreedBy, KorrinBack, KorrinNext, KorrinInfluence) = (true, by, Day, Day + Undying.ConquerEvery, Syndicate.StartInfluence);
        News.Add($"{by} has opened the tank in the warehouse by the east gate. Whatever was in it is awake, and Tristinwold is his now: every stall is shut.");
        return true;
    }

    /// <summary>Korrin brought down in his second form: back in three days, or back in the tank if whoever beat him dragged him there.</summary>
    bool KorrinFell(string? by, bool sealedAway)
    {
        if (!KorrinHolds) return false;
        KorrinDeaths++;
        KorrinTowns.Clear();
        (KorrinInfluence, FedUntil) = (0, 0);
        DepotsBurned.Clear();
        CaptainsBought.Clear();
        if (sealedAway) (KorrinFree, KorrinBack) = (false, 0);
        else KorrinBack = Day + Undying.KorrinDown;
        News.Add(sealedAway
            ? $"{by} has beaten Korrin and dragged him back into the tank. The stalls are open again. The lever is still there."
            : $"{by} has beaten Korrin, and his thugs have scattered: Tristinwold trades again. He'll be back. He always is.");
        return true;
    }

    /// <summary>Every couple of days his thugs take the nearest town they haven't, and shut its trade.</summary>
    public void TendKorrin()
    {
        if (!KorrinHolds || Day < KorrinNext) return;
        KorrinNext = Day + Undying.ConquerEvery;
        if (Roads.Towns.Values.Where(t => !KorrinTowns.Contains(t.Id)).OrderBy(t => t.DistanceTo(0, 0)).FirstOrDefault() is not { } town) return;
        KorrinTowns.Add(town.Id);
        News.Add($"Korrin's thugs have taken {town.Name}. Its market is shut and its roads are his, by order of the Emperor of Tristinwold.");
        Tightened();
    }

    // Ronacse, Pride.
    public int PrideDeaths { get; set; }
    public int PrideWins { get; set; }
    public int PrideBack { get; set; }
    /// <summary>How many of the seals on his six siblings he's broken.</summary>
    public int SealsBroken { get; set; }

    public bool PrideDown => PrideBack > Day;
    public int PrideLevel => Drifters.Level(PrideWins, PrideDeaths);

    bool PrideWon(string? who)
    {
        PrideWins++;
        if (SealsBroken >= Undying.Seals) return true;
        SealsBroken++;
        News.Add(SealsBroken >= Undying.Seals
            ? "The last seal on the Rift is broken. Below the world, six of something are waking."
            : $"Ronacse has humbled {who ?? "a hero"}, and broken a seal on the Rift: {SealsBroken} of {Undying.Seals}. Something called {Undying.Siblings[SealsBroken - 1]} stirs below.");
        return true;
    }

    bool PrideFell(string? by)
    {
        if (PrideDown) return false;
        PrideDeaths++;
        PrideBack = Day + Undying.PrideDown;
        if (SealsBroken > 0) SealsBroken--;
        News.Add($"{by ?? "Someone"} has brought down Ronacse. A seal on the Rift is mended{(SealsBroken > 0 ? $" ({SealsBroken} still broken)" : "")}. He'll be back. Pride always is.");
        return true;
    }
}
