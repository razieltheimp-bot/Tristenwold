using Tristinwold.Nemesis;

namespace Tristinwold.Shared;

/// <summary>
/// What Vann is doing when he speaks, for a voice (a language model) to put into words.
/// <see cref="Facts"/> is plain prose about the moment; the voice must not reveal more than it.
/// </summary>
public sealed record VannScene(string Trick, string Player, string Persona, bool Revealed, string Facts);

/// <summary>Turns a scene into one in-character line. Returns null to keep the written fallback.</summary>
public interface IVannVoice
{
    Task<string?> SpeakAsync(VannScene scene, IReadOnlyList<string> recentLines, CancellationToken ct);
}

/// <summary>
/// Vann, the Rogue Magus, playing the shared world. He never attacks a meeting directly: he
/// nudges small things so the companies never stand in the same room ("How Vann keeps them
/// apart" in The Rings of Tristinwold). Everything here is decided by the seeded dice; a voice
/// only changes how it's worded.
/// </summary>
public sealed partial class ServerCore
{
    DateTime _nextScheme = DateTime.MinValue;

    static readonly string[] FallbackPlaces =
        ["the Gallows Perch", "the Docks", "the Needle", "the drowned chapel", "the Exchange", "the Salt Barrel", "the Warrens", "the Tong sea-caves"];

    /// <summary>Somewhere in the city Vann can send people: the campaign's own places.</summary>
    IReadOnlyList<string> Places => State.Stage.Count > 0
        ? State.Stage.Where(p => Atlas.Default.Cities.All(c => c.Name != p)).Distinct().ToList()
        : FallbackPlaces;

    static readonly string[] Misdeeds =
    [
        "set fire to a warehouse on the Docks", "sold a Ravenscrew runner to the Watch", "robbed the Temple of the Tide's poor box",
        "cut a deal with the Drowned Hand", "left a man to drown under the Needle", "forged a Guild charter",
    ];

    static readonly string[] Omens =
    [
        "The clock tower strikes thirteen. Nobody else seems to notice.",
        "A dead man walks past you on the Docks, nods, and is gone.",
        "A candle on a sill burns backward, growing taller as you watch.",
        "You find a note in your own handwriting that you never wrote: \"Don't take the job at the Needle.\"",
        "For a moment every bell in the Outer Ring rings at once, then all of them stop mid-swing.",
        "A child in the street calls you by name and runs. You have never met him.",
    ];

    string Persona(Group g) => g.VannRevealed ? "Vann" : g.SavedMan is { } s ? Capitalise(s) : "A stranger's voice";

    Outgoing Whisper(Group g, string trick, string facts, string fallback)
    {
        Remember($"to {g.Player}: {fallback}");
        return new(g.Player, new Notice(NoticeKind.Vann, fallback, Persona(g)),
            Scene: new VannScene(trick, g.Player, Persona(g), g.VannRevealed, facts));
    }

    Outgoing Rumour(string except, string trick, string facts, string fallback)
    {
        Remember($"rumour: {fallback}");
        return new(null, new Notice(NoticeKind.Vann, fallback, "Word on the street"), Except: except,
            Scene: new VannScene(trick, "everyone", "a rumour spreading through Tristinwold", false, facts));
    }

    void Remember(string line)
    {
        State.VannMemory.Add(line);
        if (State.VannMemory.Count > 30) State.VannMemory.RemoveRange(0, State.VannMemory.Count - 30);
    }

    IEnumerable<Group> OnlineGroups => State.Groups.Values.Where(g => _online.Contains(g.Player));

    // ---- Unprompted schemes, called every few seconds by the server ----------------------------------

    public IReadOnlyList<Outgoing> Tick()
    {
        if (!_options.VannEnabled) return [];
        var now = _clock();
        var groups = OnlineGroups.ToList();
        if (groups.Count == 0) return [];
        if (_nextScheme == DateTime.MinValue) { ScheduleNextScheme(now); return []; }
        if (now < _nextScheme) return [];
        ScheduleNextScheme(now);
        Dirty = true;

        var schemes = new List<(Func<IReadOnlyList<Outgoing>> Run, double Weight)>
        {
            (() => Omen(_rng.Pick(groups)), 1.0),
            (() => SetANemesisOn(_rng.Pick(groups)), 1.0),
        };
        if (groups.Count >= 2)
        {
            schemes.Add((() => Slander(groups), 1.0));
            schemes.Add((() => Steer(groups), groups.Any(g => g.SavedMan is not null) ? 2.0 : 0.0));
        }
        return _rng.PickWeighted(schemes.Where(s => s.Weight > 0).ToList())();
    }

    void ScheduleNextScheme(DateTime now)
    {
        // Somewhere between half and one-and-a-half times the average, so he's never predictable.
        var minutes = _options.VannMinutesBetweenSchemes * (0.5 + _rng.NextDouble());
        _nextScheme = now.AddMinutes(minutes);
    }

    IReadOnlyList<Outgoing> Omen(Group g)
    {
        var omen = _rng.Pick(Omens);
        return [new(g.Player, new Notice(NoticeKind.Vann, omen),
            Scene: new VannScene("omen", g.Player, "the world itself, slipping in time", false,
                $"Time is thin around {g.Player}'s company. Describe one small impossible thing they notice, like: {omen}"))];
    }

    /// <summary>"Reputations": frame one company so the others distrust it on sight.</summary>
    IReadOnlyList<Outgoing> Slander(List<Group> groups)
    {
        var target = _rng.Pick(groups);
        var misdeed = _rng.Pick(Misdeeds);
        return [Rumour(target.Player, "slander",
            $"Vann is framing {target.Player}'s company so the other companies distrust them. The lie: they {misdeed}. It is not true.",
            $"They say {target.Player}'s company {misdeed}. Folk are keeping their distance.")];
    }

    /// <summary>"Delays" and "bad information": the Saved Man gently steers his company away from the others.</summary>
    IReadOnlyList<Outgoing> Steer(List<Group> groups)
    {
        var mine = groups.Where(g => g.SavedMan is not null).ToList();
        if (mine.Count == 0) return [];
        var g = _rng.Pick(mine);
        var other = _rng.Pick(groups.Where(o => o != g).ToList());
        var away = _rng.Pick(Places.Where(p => !p.Equals(other.Area, StringComparison.OrdinalIgnoreCase)).ToList());
        var facts = g.VannRevealed
            ? $"Vann has been unmasked by {g.Player}'s company and taunts them. He knows {other.Player}'s company is in {Or(other.Area, "the city")} and wants {g.Player}'s company kept away from them; he tells them, coldly, that it is too late to find the others."
            : $"Vann, posing as {g.SavedMan}, the grateful person {g.Player}'s company rescued, travels with them as a warm, helpful friend. He secretly wants to keep them away from {other.Player}'s company, who are in {Or(other.Area, "the city")}. He suggests, kindly and plausibly, that they go to {away} instead, and hints that {other.Player}'s company can't be trusted.";
        var fallback = g.VannRevealed
            ? $"Looking for {other.Player}'s company? You'll always be a street behind them. I made sure of it."
            : $"Friend, I've heard {other.Player}'s company is trouble. Let's try {away} instead. I have a feeling about it.";
        return [Whisper(g, "steer", facts, fallback)];
    }

    /// <summary>He points a nemesis that already hates this company straight at them.</summary>
    IReadOnlyList<Outgoing> SetANemesisOn(Group g)
    {
        var hunter = World.Living
            .Where(n => n.Players.TryGetValue(g.Player, out var r) && (r.TimesKilledThem > 0 || r.TimesTheyFled > 0 || r.TimesTheyBeatMe > 0))
            .OrderByDescending(n => n.Power)
            .FirstOrDefault();
        if (hunter is null) return Omen(g);

        var events = World.Empower(hunter.Id, 20, $"A grey-robed man whispered to it where {g.Player}'s company would be.");
        var outgoing = new List<Outgoing>
        {
            Whisper(g, "set_nemesis",
                g.VannRevealed
                    ? $"Vann tells {g.Player}'s company that he has sent {hunter.FullName}, a {hunter.Species} that already has a grudge against them, to find them, stronger than before."
                    : $"Vann, posing as {g.SavedMan}, 'warns' {g.Player}'s company that {hunter.FullName}, a {hunter.Species} with a grudge against them, has been seen asking about them. He is the one who told it where they are.",
                g.VannRevealed
                    ? $"{hunter.FullName} knows where you sleep now. I told it."
                    : $"Be careful. I heard {hunter.FullName} has been asking after you. Someone must have told it where we'd be."),
        };
        // If the game knows how to spawn its kind, it comes looking right now.
        if (hunter.BaseForm is { } baseForm)
            outgoing.Add(new(g.Player, new NemesisHunts(hunter.Id, hunter.Kind, hunter.Species, baseForm, World.ModifiersFor(hunter), null)));
        outgoing.AddRange(WorldChanged(events));
        return outgoing;
    }

    // ---- Reactions to what the companies do -------------------------------------------------------

    /// <summary>"Bad information": sometimes the note that arrives isn't the one that was left.</summary>
    string? ForgeNote(Group author)
    {
        if (!_options.VannEnabled || !_rng.Chance(_options.ForgeChance)) return null;
        var place = _rng.Pick(Places);
        return _rng.Next(2) == 0
            ? $"Change of plan: meet us at {place} at dusk instead. Come alone. - {author.Player}"
            : $"Don't wait for us. We've gone on to {place}. Don't follow. - {author.Player}";
    }

    /// <summary>"Rival jobs": hire another company to stand in the way of this one.</summary>
    IReadOnlyList<Outgoing> PostRivalJob(Group claimer, Contract contract)
    {
        if (!_options.VannEnabled || contract.RivalOf is not null || OnlineGroups.Count() < 2 || !_rng.Chance(_options.RivalJobChance))
            return [];
        var id = $"rival-{contract.Id}-{_rng.Next(1000)}";
        var rival = new Contract
        {
            Id = id,
            Title = $"Discreet work: make sure the company meddling in \"{contract.Title}\" fails. No questions.",
            PostedBy = "A well-dressed stranger",
            Pay = Math.Max(20, contract.Pay * 2),
            Where = contract.Where,
            RivalOf = contract.Id,
            Secret = $"Vann posted this to set another company against {claimer.Player}'s.",
        };
        State.Contracts.Add(rival);
        Remember($"posted a rival job against {claimer.Player}");
        return
        [
            new(null, new Board(PublicBoard())),
            new(null, new Notice(NoticeKind.Contract, $"New on the board, pinned by a well-dressed stranger: \"{rival.Title}\" ({rival.Pay} gp)."), Except: claimer.Player),
        ];
    }

    /// <summary>The first rescue binds him to a company; enough jobs later, the mask comes off.</summary>
    IReadOnlyList<Outgoing> AfterContract(Group g, bool firstRescue)
    {
        if (!_options.VannEnabled || g.SavedMan is null) return [];
        if (firstRescue)
        {
            return [Whisper(g, "grateful",
                $"{g.Player}'s company has just saved {g.SavedMan}. That person is secretly Vann, though nobody knows it. He is warm, grateful, quietly brilliant, and offers to travel with them as a guide, scribe or healer. He remembers their names.",
                $"You saved my life. I don't know how to repay that, but let me travel with you. I know these streets, and I'm good with wounds.")];
        }
        if (!g.VannRevealed && g.ContractsDone >= _options.ContractsBeforeReveal)
        {
            g.VannRevealed = true;
            Dirty = true;
            return
            [
                new(g.Player, new Notice(NoticeKind.Vann,
                    $"{Capitalise(g.SavedMan)} stops smiling. \"You've done this before, you know. You saved me. You always save me.\" Every job, every coincidence: he steered them all. His name is Vann.",
                    "Vann"),
                    Scene: new VannScene("reveal", g.Player, "Vann", true,
                        $"The midpoint betrayal. {g.SavedMan}, whom {g.Player}'s company rescued and trusted, reveals that he is Vann, the Rogue Magus, and that he steered every job they took. He says some version of: 'You've done this before. You saved me. You always save me.' Cold now, but not cruel.")),
                new(null, new Notice(NoticeKind.Vann, $"Somewhere in Tristinwold, {g.Player}'s company has learned who they really saved."), Except: g.Player),
            ];
        }
        return [];
    }

    /// <summary>
    /// Two Keystones in one place: time stops for everyone else nearby. That's the tell, and
    /// the one thing Vann can't stop.
    /// </summary>
    IReadOnlyList<Outgoing> KeystonesMeet(Group a, Group b, string area)
    {
        if (a.Keystone is null || b.Keystone is null || (a.KeystoneRevealed && b.KeystoneRevealed)) return [];
        a.KeystoneRevealed = b.KeystoneRevealed = true;
        Dirty = true;
        var text = $"In {area}, time stops. The crowd freezes mid-step, a gull hangs in the air, and only two people can still move: {a.Keystone} of {a.Player}'s company and {b.Keystone} of {b.Player}'s. An hourglass glints in each of their eyes.";
        Remember($"the Keystones {a.Keystone} and {b.Keystone} met in {area}");
        return
        [
            new(a.Player, new Notice(NoticeKind.World, text)),
            new(b.Player, new Notice(NoticeKind.World, text)),
            new(null, new KeystoneRevealed(a.Player, a.Keystone)),
            new(null, new KeystoneRevealed(b.Player, b.Keystone)),
            new(a.Player, new Notice(NoticeKind.Vann, "No. Not again.", "Vann"),
                Scene: new VannScene("keystones_met", a.Player, "Vann", true,
                    $"Two Keystones, {a.Keystone} and {b.Keystone}, have just met in {area} despite everything Vann did to keep them apart. For one moment he cannot hide his fear. Very short.")),
        ];
    }

    static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
