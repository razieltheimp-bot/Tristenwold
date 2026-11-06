using System.Text;
using Tristinwold.Deviant;

namespace Tristinwold.Shared;

public sealed class ServerOptions
{
    public string Password { get; init; } = "";
    /// <summary>When false, only the DM console can close contracts. With no DM, players close their own.</summary>
    public bool PlayersFinishContracts { get; init; } = true;
    /// <summary>Chance that Vann intercepts a dead drop.</summary>
    public double InterceptChance { get; init; } = 0.25;
    /// <summary>How long after a group leaves an area another arriving group still finds its tracks.</summary>
    public TimeSpan NearMissWindow { get; init; } = TimeSpan.FromMinutes(3);
    /// <summary>Chance that two groups in the same place get pulled apart by a time-skip.</summary>
    public double RewindChance { get; init; } = 0.35;
    public int CoppersPerCrown { get; init; } = 100;

    /// <summary>Vann plays himself: forges notes, posts rival jobs, spreads rumours, steers each company.</summary>
    public bool VannEnabled { get; init; } = true;
    /// <summary>Average real minutes between Vann's unprompted schemes.</summary>
    public double VannMinutesBetweenSchemes { get; init; } = 6;
    public double ForgeChance { get; init; } = 0.2;
    public double RivalJobChance { get; init; } = 0.4;
    /// <summary>Contracts a company finishes before the Saved Man shows them who he is.</summary>
    public int ContractsBeforeReveal { get; init; } = 3;
    /// <summary>When two companies with hidden Keystones share a place and no time-skip pulls them apart.</summary>
    public double KeystoneMeetChance { get; init; } = 0.5;
}

/// <summary>
/// A message for one player, or for everyone (To = null) except perhaps one. When Scene is
/// set the message is one of Vann's lines and a voice may rewrite its text before it's sent.
/// </summary>
public sealed record Outgoing(string? To, Message Message, string? Except = null, VannScene? Scene = null);

/// <summary>
/// The shared-world rules, with no networking in it: connections hand it messages and send
/// whatever it returns. Not thread-safe: the server calls it under one lock.
/// </summary>
public sealed partial class ServerCore
{
    readonly ServerOptions _options;
    readonly Func<DateTime> _clock;
    readonly Rng _rng;
    readonly HashSet<string> _online = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (string Area, DateTime Left)> _lastLeft = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Where each player online was last seen (not saved: it means nothing once they're gone).</summary>
    readonly Dictionary<string, Moved> _sights = new(StringComparer.OrdinalIgnoreCase);

    public CampaignState State { get; }
    public DeviantWorld World { get; private set; }
    /// <summary>Set whenever something worth saving changed.</summary>
    public bool Dirty { get; set; }

    public ServerCore(CampaignState state, ServerOptions? options = null, ulong seed = 0, Func<DateTime>? clock = null)
    {
        State = state;
        _options = options ?? new ServerOptions();
        _clock = clock ?? (() => DateTime.UtcNow);
        _rng = new Rng(seed == 0 ? (ulong)DateTime.UtcNow.Ticks : seed);
        World = state.DeviantWorldJson is { } json ? DeviantWorld.FromJson(json) : new DeviantWorld(_rng.NextULong());
    }

    public IEnumerable<string> Online => _online;

    // ---- Connections --------------------------------------------------------------------------

    public bool PasswordOk(string password) => _options.Password.Length == 0 || password == _options.Password;

    /// <summary>Has anyone played under this name in this campaign?</summary>
    public bool Known(string name) => State.Groups.ContainsKey(name.Trim());

    public IReadOnlyList<Outgoing> Connect(Hello hello)
    {
        var name = hello.Player.Trim();
        if (hello.ProtocolVersion != Wire.ProtocolVersion)
            return [new(name, new Rejected(Wire.Mismatch(Wire.ProtocolVersion, hello.ProtocolVersion)))];
        if (_options.Password.Length > 0 && hello.Password != _options.Password)
            return [new(name, new Rejected("Wrong password."))];
        if (name.Length is 0 or > 24)
            return [new(name, new Rejected("Pick a player name of 1-24 characters."))];
        if (_online.Contains(name))
            return [new(name, new Rejected($"{name} is already connected."))];

        if (!State.Groups.TryGetValue(name, out var group))
            State.Groups[name] = group = new Group { Player = name };
        if (hello.Party.Count > 0) group.Party = [.. hello.Party];
        // Every table has one Keystone, and nobody knows who until the DM says so.
        if (group.Keystone is null && group.Party.Count > 0) group.Keystone = _rng.Pick(group.Party);
        group.LastSeen = _clock();
        _online.Add(name);
        Dirty = true;

        var outgoing = new List<Outgoing>
        {
            new(name, new Welcome(name, State.Campaign, [.. State.Flags])),
            new(name, new DeviantSnapshot(World.ToJson())),
            new(name, new Board(PublicBoard())),
            new(name, CityMessage()),
            new(null, RosterMessage()),
            new(null, new Notice(NoticeKind.World, $"{name}'s company has arrived in Tristinwold."), Except: name),
        };
        // Everyone already out there, where they were last seen, so the newcomer sees them at once.
        foreach (var (other, where) in _sights)
            if (!other.Equals(name, StringComparison.OrdinalIgnoreCase)) outgoing.Add(new(name, new Seen(other, where)));
        return outgoing;
    }

    public IReadOnlyList<Outgoing> Disconnect(string player)
    {
        if (!_online.Remove(player)) return [];
        _sights.Remove(player);
        if (State.Groups.TryGetValue(player, out var g) && g.Area.Length > 0) _lastLeft[player] = (g.Area, _clock());
        return [new(null, RosterMessage())];
    }

    // ---- Player messages ------------------------------------------------------------------------

    public IReadOnlyList<Outgoing> Handle(string player, Message message)
    {
        if (!_online.Contains(player) || !State.Groups.TryGetValue(player, out var group)) return [];
        group.LastSeen = _clock();
        return message switch
        {
            Presence p => OnPresence(group, p),
            ReportBattle r => OnBattle(player, r.Report),
            Rested => AdvanceDay($"{player}'s company rests. A day passes in Tristinwold."),
            ClaimContract c => OnClaim(group, c.ContractId),
            AbandonContract a => OnAbandon(group, a.ContractId),
            FinishContract f when _options.PlayersFinishContracts => OnPlayerFinish(group, f.ContractId),
            FinishContract => [new(player, new Notice(NoticeKind.Error, "The DM closes contracts in this campaign. Tell them you're done."))],
            LeaveDrop d => OnDrop(group, d),
            Say s when !string.IsNullOrWhiteSpace(s.Text) => [new(null, new Notice(NoticeKind.Chat, Clip(s.Text, 400), player))],
            EncounterStarted e => OnEncounter(player, e),
            CityTrade t => OnCityTrade(player, t),
            Moved m => OnMoved(player, m),
            StrikeHero s => OnStrikeHero(player, s),
            HeroFell f => OnHeroFell(player, f),
            _ => [],
        };
    }

    /// <summary>A hero moved: everyone else sees where. Nothing is saved; a position is stale the moment it's sent.</summary>
    IReadOnlyList<Outgoing> OnMoved(string player, Moved m)
    {
        var where = m with { Look = Clip(m.Look, 40), Avatar = m.Avatar is null ? null : Clip(m.Avatar, 400), Name = Clip(m.Name, 40), Clip = m.Clip is null ? null : Clip(m.Clip, 40) };
        _sights[player] = where;
        return [new(null, new Seen(player, where), Except: player)];
    }

    /// <summary>The most one blow between heroes may do, and the most of anything one fall may hand over.</summary>
    public const double MostBlow = 1_000_000;
    public const int MostSpoil = 10_000_000;

    /// <summary>A hero struck another: the blow goes to the one it landed on, whose game decides what it does.</summary>
    IReadOnlyList<Outgoing> OnStrikeHero(string player, StrikeHero s)
    {
        var target = _online.FirstOrDefault(o => o.Equals(s.Target?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null || target.Equals(player, StringComparison.OrdinalIgnoreCase) || !double.IsFinite(s.Damage) || s.Damage <= 0) return [];
        return [new(target, new HeroStruck(player, Math.Min(s.Damage, MostBlow), s.Kind, s.Attack, s.Crit))];
    }

    /// <summary>A hero went down to another: the winner is told what's theirs for it.</summary>
    IReadOnlyList<Outgoing> OnHeroFell(string player, HeroFell f)
    {
        var killer = _online.FirstOrDefault(o => o.Equals(f.Killer?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (killer is null || killer.Equals(player, StringComparison.OrdinalIgnoreCase)) return [];
        static int Fair(int n) => Math.Clamp(n, 0, MostSpoil);
        var items = (f.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.Id) && i.N > 0).Take(500)
            .Select(i => new SpoilItem(Clip(i.Id, 80), Fair(i.N))).ToList();
        return
        [
            new(killer, new HeroSpoils(player, items, Fair(f.Crowns), Fair(f.GearCrowns), Fair(f.Bounty))),
            new(null, new Notice(NoticeKind.World, $"{killer} has cut down {player}{(f.Bounty > 0 ? $", and collects the {Fair(f.Bounty)} crowns on {player}'s head" : "")}."), Except: killer),
        ];
    }

    IReadOnlyList<Outgoing> OnPresence(Group group, Presence p)
    {
        var area = State.Place(Clip(p.Area, 80));
        if (p.Party.Count > 0) group.Party = [.. p.Party.Take(3)];
        if (area == group.Area) return [new(null, RosterMessage())];

        var now = _clock();
        if (group.Area.Length > 0) _lastLeft[group.Player] = (group.Area, now);
        group.Area = area;
        Dirty = true;

        var outgoing = new List<Outgoing> { new(null, RosterMessage()) };

        var notes = State.Drops
            .Where(d => d.Area == area && !d.Intercepted && !d.From.Equals(group.Player, StringComparison.OrdinalIgnoreCase))
            .Select(d => new DeadDropView(d.From, d.Text, d.Day)).ToList();
        if (notes.Count > 0) outgoing.Add(new(group.Player, new Drops(area, notes)));

        // Near misses: the groups share a city but Vann keeps them from meeting.
        foreach (var other in State.Groups.Values.Where(g => g != group && _online.Contains(g.Player) && g.Area == area))
        {
            // Vann can't always stop it: if two hidden Keystones slip through, time stops.
            if (!_rng.Chance(_options.RewindChance) && _rng.Chance(_options.KeystoneMeetChance) && KeystonesMeet(group, other, area) is { Count: > 0 } meeting)
            {
                outgoing.AddRange(meeting);
                continue;
            }
            outgoing.Add(new(group.Player, new Notice(NoticeKind.NearMiss, $"{other.Player}'s company is in {area} too. Look about for them.")));
            outgoing.Add(new(other.Player, new Notice(NoticeKind.NearMiss, $"{group.Player}'s company has come to {area}.")));
        }
        foreach (var (who, (leftArea, when)) in _lastLeft)
        {
            if (leftArea != area || who.Equals(group.Player, StringComparison.OrdinalIgnoreCase) || now - when > _options.NearMissWindow) continue;
            if (State.Groups.TryGetValue(who, out var g) && g.Area == area) continue; // already reported above
            outgoing.Add(new(group.Player, new Notice(NoticeKind.NearMiss,
                $"Fresh tracks in {area}: {who}'s company was here only minutes ago.")));
        }
        return outgoing;
    }

    IReadOnlyList<Outgoing> OnBattle(string player, BattleReport report)
    {
        // Each table only speaks for itself.
        var events = World.RecordBattle(report with { Players = [player], AreaId = State.Place(Clip(report.AreaId, 80)) });
        return WorldChanged(events);
    }

    IReadOnlyList<Outgoing> OnEncounter(string player, EncounterStarted e)
    {
        var spawns = World.PlanEncounter(State.Place(Clip(e.Area, 80)), e.Enemies.Take(32).ToList(), [player]);
        if (spawns.Count == 0) return [];
        Dirty = true; // the plan used the world's dice
        return [new(player, new DeviantAppears(spawns))];
    }

    public IReadOnlyList<Outgoing> AdvanceDay(string announcement)
    {
        var events = World.AdvanceDay();
        var outgoing = new List<Outgoing> { new(null, new Notice(NoticeKind.World, announcement)) };
        outgoing.AddRange(WorldChanged(events));
        return outgoing;
    }

    IReadOnlyList<Outgoing> WorldChanged(IReadOnlyList<WorldEvent> events)
    {
        State.DeviantWorldJson = World.ToJson();
        Dirty = true;
        var outgoing = new List<Outgoing> { new(null, new DeviantSnapshot(State.DeviantWorldJson)) };
        if (events.Count > 0) outgoing.Add(new(null, new DeviantEvents(events)));
        return outgoing;
    }

    IReadOnlyList<Outgoing> OnClaim(Group group, string id)
    {
        var contract = Find(id);
        if (contract is null) return [Error(group.Player, "No such contract.")];
        if (contract.Status != ContractStatus.Open)
            return [Error(group.Player, $"\"{contract.Title}\" is already taken{(contract.ClaimedBy is null ? "" : $" by {contract.ClaimedBy}")}.")];
        contract.Status = ContractStatus.Claimed;
        contract.ClaimedBy = group.Player;
        Dirty = true;
        return
        [
            new(null, new Board(PublicBoard())),
            new(null, new Notice(NoticeKind.Contract, $"{group.Player}'s company took the contract \"{contract.Title}\".")),
            .. PostRivalJob(group, contract),
        ];
    }

    IReadOnlyList<Outgoing> OnAbandon(Group group, string id)
    {
        var contract = Find(id);
        if (contract is null || contract.Status != ContractStatus.Claimed || contract.ClaimedBy != group.Player)
            return [Error(group.Player, "You haven't claimed that contract.")];
        contract.Status = ContractStatus.Open;
        contract.ClaimedBy = null;
        Dirty = true;
        return
        [
            new(null, new Board(PublicBoard())),
            new(null, new Notice(NoticeKind.Contract, $"\"{contract.Title}\" is back on the board.")),
        ];
    }

    IReadOnlyList<Outgoing> OnPlayerFinish(Group group, string id)
    {
        var contract = Find(id);
        if (contract is null || contract.Status != ContractStatus.Claimed || contract.ClaimedBy != group.Player)
            return [Error(group.Player, "You haven't claimed that contract.")];
        return Complete(contract).Outgoing;
    }

    /// <summary>Closes a contract and pays the group that held it. The DM sees who became Vann.</summary>
    public (IReadOnlyList<Outgoing> Outgoing, string DmNote) Complete(Contract contract)
    {
        var player = contract.ClaimedBy;
        contract.Status = ContractStatus.Done;
        contract.FinishedBy = player;
        Dirty = true;
        var outgoing = new List<Outgoing>
        {
            new(null, new Board(PublicBoard())),
            new(null, new Notice(NoticeKind.Contract, $"\"{contract.Title}\" is done{(player is null ? "" : $", thanks to {player}'s company")}.")),
        };
        var note = $"\"{contract.Title}\" closed.";
        if (player is not null && State.Groups.TryGetValue(player, out var group))
        {
            group.ContractsDone++;
            outgoing.Add(new(player, new Reward(contract.Id, contract.Pay * _options.CoppersPerCrown)));
            // Vann's rule: the first person a table saves is Vann, whoever it was.
            var firstRescue = group.SavedMan is null && contract.InDanger is not null;
            if (firstRescue)
            {
                group.SavedMan = contract.InDanger;
                note += $" {player}'s table saved {contract.InDanger}: that is their Vann.";
            }
            outgoing.AddRange(AfterContract(group, firstRescue));
        }
        return (outgoing, note);
    }

    IReadOnlyList<Outgoing> OnDrop(Group group, LeaveDrop d)
    {
        var text = Clip(d.Text, 600);
        if (text.Length == 0) return [];
        var area = State.Place(Clip(d.Area, 80));
        var forged = ForgeNote(group);
        var drop = new DeadDrop
        {
            Area = area, From = group.Player, Day = World.Day,
            Text = forged ?? text,
            Original = forged is null ? null : text,
            Intercepted = forged is null && _rng.Chance(_options.InterceptChance),
        };
        State.Drops.Add(drop);
        Dirty = true;

        var outgoing = new List<Outgoing> { new(group.Player, new Notice(NoticeKind.World, $"You leave a note hidden in {area}.")) };
        if (!drop.Intercepted)
        {
            foreach (var other in State.Groups.Values.Where(g => g != group && _online.Contains(g.Player) && g.Area == area))
                outgoing.Add(new(other.Player, new Drops(area, [new DeadDropView(drop.From, drop.Text, drop.Day)])));
        }
        return outgoing;
    }

    // ---- DM console ------------------------------------------------------------------------------

    public const string DmHelp = """
        who                        groups, areas, keystones, who their Vann is
        board                      contracts with their secrets
        post <id> <pay> <title>    pin a new contract
        done <id>                  close a contract and pay whoever holds it
        reopen <id>                put a contract back on the board
        say <text>                 announce to every table
        tell <player> <text>       whisper to one table
        keystone <player> [name]   reveal that table's Keystone (or set it to <name>)
        flag <name> | unflag <name>   world consequences shared by every table
        drops                      list dead drops (* = intercepted by Vann, ~ = forged)
        release <n>                let intercepted drop n reach its area
        day                        advance the world one day
        deviants                    living deviants
        save                       save now
        """;

    public (string Reply, IReadOnlyList<Outgoing> Outgoing) Dm(string line)
    {
        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", []);
        var rest = parts.Length > 1 ? parts[1].Trim() : "";
        string Arg(int i) => rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(i) ?? "";
        string After(int i) => string.Join(' ', rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(i));

        switch (parts[0].ToLowerInvariant())
        {
            case "help": return (DmHelp, []);
            case "who":
            {
                var sb = new StringBuilder();
                foreach (var g in State.Groups.Values)
                    sb.AppendLine($"{g.Player} [{(_online.Contains(g.Player) ? "online" : "offline")}] in {Or(g.Area, "?")}: " +
                        $"{string.Join(", ", g.Party)} | keystone {Or(g.Keystone, "?")}{(g.KeystoneRevealed ? " (revealed)" : "")} | " +
                        $"saved man: {Or(g.SavedMan, "none yet")} | contracts done {g.ContractsDone}");
                return (sb.Length == 0 ? "No groups yet." : sb.ToString().TrimEnd(), []);
            }
            case "board":
                return (string.Join('\n', State.Contracts.Select(c =>
                    $"[{c.Id}] {c.Status}{(c.ClaimedBy is null ? "" : $" ({c.ClaimedBy})")} {c.Title} - {c.Pay} gp, {c.PostedBy}. Secret: {Or(c.Secret, "-")}")), []);
            case "post":
            {
                if (!int.TryParse(Arg(1), out var pay) || Arg(0).Length == 0 || After(2).Length == 0)
                    return ("usage: post <id> <pay> <title>", []);
                if (Find(Arg(0)) is not null) return ($"A contract with id {Arg(0)} already exists.", []);
                State.Contracts.Add(new Contract { Id = Arg(0), Title = After(2), PostedBy = "The DM", Pay = pay });
                Dirty = true;
                return ("Posted.", [new(null, new Board(PublicBoard())), new(null, new Notice(NoticeKind.Contract, $"New on the board: \"{After(2)}\" ({pay} gp)."))]);
            }
            case "done":
            {
                if (Find(Arg(0)) is not { } c) return ("No such contract.", []);
                var (outgoing, note) = Complete(c);
                return (note, outgoing);
            }
            case "reopen":
            {
                if (Find(Arg(0)) is not { } c) return ("No such contract.", []);
                c.Status = ContractStatus.Open;
                c.ClaimedBy = c.FinishedBy = null;
                Dirty = true;
                return ("Reopened.", [new(null, new Board(PublicBoard()))]);
            }
            case "say":
                return ("Sent.", [new(null, new Notice(NoticeKind.Dm, rest, "DM"))]);
            case "tell":
                return State.Groups.ContainsKey(Arg(0))
                    ? ("Sent.", [new(Arg(0), new Notice(NoticeKind.Dm, After(1), "DM"))])
                    : ("No such player.", []);
            case "keystone":
            {
                if (!State.Groups.TryGetValue(Arg(0), out var g)) return ("No such player.", []);
                if (After(1).Length > 0) g.Keystone = After(1);
                if (g.Keystone is null) return ("That table has no party yet.", []);
                g.KeystoneRevealed = true;
                Dirty = true;
                return ($"{g.Player}'s Keystone is {g.Keystone}.", [new(null, new KeystoneRevealed(g.Player, g.Keystone))]);
            }
            case "flag":
                if (rest.Length == 0) return (string.Join(", ", State.Flags), []);
                State.Flags.Add(rest);
                Dirty = true;
                return ($"Flag set: {rest}", []);
            case "unflag":
                Dirty |= State.Flags.Remove(rest);
                return ($"Flag cleared: {rest}", []);
            case "drops":
                return (string.Join('\n', State.Drops.Select((d, i) =>
                    $"{i}{(d.Intercepted ? "*" : d.Original is null ? " " : "~")} day {d.Day} {d.Area} from {d.From}: {d.Text}{(d.Original is null ? "" : $"  [really: {d.Original}]")}")), []);
            case "release":
            {
                if (!int.TryParse(Arg(0), out var i) || i < 0 || i >= State.Drops.Count) return ("No such drop.", []);
                var d = State.Drops[i];
                d.Intercepted = false;
                Dirty = true;
                var outgoing = State.Groups.Values
                    .Where(g => _online.Contains(g.Player) && g.Area == d.Area && g.Player != d.From)
                    .Select(g => new Outgoing(g.Player, new Drops(d.Area, [new DeadDropView(d.From, d.Text, d.Day)])))
                    .ToList();
                return ("Released.", outgoing);
            }
            case "day":
                return ($"Day {World.Day + 1}.", AdvanceDay("A day passes in Tristinwold."));
            case "deviants":
                return (string.Join('\n', World.Living.OrderByDescending(n => n.Rank)
                    .Select(n => $"{n.Rank,-8} {n.FullName} ({n.Species}, Lv {n.Level}, power {n.Power}) home {Or(n.HomeArea, "?")}")), []);
            default:
                return ($"Unknown command. Try: help", []);
        }
    }

    // ---- Helpers -----------------------------------------------------------------------------

    Contract? Find(string id) => State.Contracts.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    List<PublicContract> PublicBoard() => State.Contracts.Select(c => c.ToPublic()).ToList();

    Roster RosterMessage() => new(State.Groups.Values.Select(g => g.ToView(_online.Contains(g.Player))).ToList());

    static Outgoing Error(string player, string text) => new(player, new Notice(NoticeKind.Error, text));

    static string Clip(string s, int max) => s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();

    static string Or(string? s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;
}
