using System.Text.Json;

namespace Tristinwold.Shared;

/// <summary>A job on the shared board. The secret is for the DM's eyes only.</summary>
public sealed class Contract
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string PostedBy { get; init; }
    public int Pay { get; init; }
    public string? Where { get; init; }
    public string? Secret { get; init; }
    /// <summary>The person in danger on this job. The first one a group saves becomes their Vann.</summary>
    public string? InDanger { get; init; }

    /// <summary>Posted by Vann to set one company against another.</summary>
    public string? RivalOf { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Open;
    public string? ClaimedBy { get; set; }
    public string? FinishedBy { get; set; }

    public PublicContract ToPublic() => new(Id, Title, PostedBy, Pay, Where, Status, ClaimedBy);
}

public sealed class DeadDrop
{
    public required string Area { get; init; }
    public required string From { get; init; }
    public required string Text { get; init; }
    public int Day { get; init; }
    /// <summary>Vann got to it first. The DM can let it through later.</summary>
    public bool Intercepted { get; set; }
    /// <summary>When Vann forged the note, what it really said.</summary>
    public string? Original { get; set; }
}

/// <summary>One player's table: their own party of three, adventuring in the shared city.</summary>
public sealed class Group
{
    public required string Player { get; init; }
    public string Area { get; set; } = "";
    public List<string> Party { get; set; } = [];
    /// <summary>The party member whose fate is tied to Vann's defeat. Hidden until the DM reveals it.</summary>
    public string? Keystone { get; set; }
    public bool KeystoneRevealed { get; set; }
    /// <summary>Who this group saved first: their Vann, once the reveal comes.</summary>
    public string? SavedMan { get; set; }
    public int ContractsDone { get; set; }
    /// <summary>The Saved Man has shown them who he is.</summary>
    public bool VannRevealed { get; set; }
    public DateTime LastSeen { get; set; }

    public GroupView ToView(bool online) => new(Player, Area, Party, online);
}

/// <summary>Everything the groups share, saved by the server between sessions.</summary>
public sealed class CampaignState
{
    public string Campaign { get; set; } = "The Saved Man";
    public List<Contract> Contracts { get; set; } = [];
    public List<DeadDrop> Drops { get; set; } = [];
    public Dictionary<string, Group> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Consequences that carry over between tables ("warehouse_burned", "silt_spared").</summary>
    public HashSet<string> Flags { get; set; } = [];
    public string? NemesisWorldJson { get; set; }

    /// <summary>
    /// Where the story is staged: Skyrim location name -> Tristinwold place. Areas reported by the
    /// game are renamed through this, so dead drops, near misses and Vann all speak Tristinwold.
    /// Names not listed pass through unchanged. Edit freely in the campaign file.
    /// </summary>
    public Dictionary<string, string> Places { get; set; } = [];

    /// <summary>The Tristinwold name for a place the game reported.</summary>
    public string Place(string skyrimArea)
    {
        foreach (var (skyrim, tristinwold) in Places)
            if (skyrim.Equals(skyrimArea, StringComparison.OrdinalIgnoreCase)) return tristinwold;
        return skyrimArea;
    }

    /// <summary>
    /// Campaigns 1 and 2 are staged in Riften: a lakeside city of docks, a warren under the
    /// streets (the Ratway) and a thieves' guild. Campaign 3's Last Ring is its own walled city.
    /// </summary>
    public static Dictionary<string, string> RiftenAsTristinwold() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Riften"] = "Tristinwold",
        ["The Ragged Flagon"] = "the Gallows Perch",
        ["The Ratway"] = "the Warrens",
        ["The Ratway Warrens"] = "the Warrens",
        ["Ratway Vaults"] = "the Tong sea-caves",
        ["The Bee and Barb"] = "the Salt Barrel",
        ["Riften Fishery"] = "the Docks",
        ["Lake Honrich"] = "the harbor",
        ["Goldenglow Estate"] = "the Needle",
        ["Temple of Mara"] = "the drowned chapel",
        ["Honorhall Orphanage"] = "the Lantern Boys' den",
        ["Black-Briar Meadery"] = "the Exchange",
        ["Riften Hall of the Dead"] = "the Magi archive",
        ["Mistveil Keep"] = "the Crown",
        ["Riften Jail"] = "the Watch house",
    };
    /// <summary>Things Vann has said lately, so he doesn't repeat himself (and so a voice model has memory).</summary>
    public List<string> VannMemory { get; set; } = [];

    public static CampaignState Load(string path) =>
        JsonSerializer.Deserialize<CampaignState>(File.ReadAllText(path), Wire.Options)
        ?? throw new InvalidDataException($"Empty campaign file: {path}");

    public void Save(string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions(Wire.Options) { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>The day-one contract board from "The Rings of Tristinwold", Campaign 1.</summary>
    public static CampaignState SavedMan() => new()
    {
        Campaign = "The Saved Man",
        Places = RiftenAsTristinwold(),
        Contracts =
        [
            new() { Id = "rats", Title = "Rats in the cellar of the Salt Barrel tavern", PostedBy = "Tavern owner", Pay = 10,
                Where = "the Salt Barrel (the Bee and Barb, Riften)", InDanger = "the Salt Barrel's cellar boy",
                Secret = "Giant rats fleeing something deeper in the sewers." },
            new() { Id = "spice", Title = "Escort a spice wagon to the Inner Ring gate", PostedBy = "Merchant Guild", Pay = 25,
                Where = "the Docks to the Exchange (Riften Fishery to Black-Briar Meadery)", InDanger = "the wagon driver",
                Secret = "The wagon is smuggling a Magi relic." },
            new() { Id = "brother", Title = "Find my missing brother, last seen at the Needle", PostedBy = "A fisherman's wife", Pay = 15,
                Where = "the Needle (Goldenglow Estate, on Lake Honrich)", InDanger = "the fisherman's brother",
                Secret = "He's chained in the lighthouse cellar. Likely first rescue." },
            new() { Id = "debt", Title = "Collect an overdue debt from a dock gambler", PostedBy = "Ravenscrew Tong", Pay = 20,
                Where = "the Docks (Riften Fishery)", InDanger = "the dock gambler",
                Secret = "The gambler is being hunted by the rival Tong." },
            new() { Id = "chapel", Title = "Clear the drowned chapel of \"singing\"", PostedBy = "Temple of the Tide", Pay = 40,
                Where = "the drowned chapel (Temple of Mara, Riften)", InDanger = "a trapped acolyte",
                Secret = "Time is skipping inside: candles burn backward. Vann's fingerprints." },
            new() { Id = "gutter_prince", Title = "Bounty: the Gutter Prince, alive", PostedBy = "City Watch", Pay = 100,
                Where = "the Lantern Boys' den (Honorhall Orphanage, Riften)", InDanger = "the Gutter Prince",
                Secret = "A rival Tong boss with a sympathetic story." },
        ],
    };
}
