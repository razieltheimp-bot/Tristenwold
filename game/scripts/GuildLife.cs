using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Life in the guilds (see <see cref="Guilds"/>): their yards outside Tristinwold's walls and the people in them;
/// teachers who give lessons and set each class's trials; standing and ranks; duels with your rival; and the drama
/// that comes to you, piece by piece, as you rise and as time passes.
/// </summary>
public partial class Main
{
    GuildBook _guilds = new();
    RaidBook _raids = new();
    readonly List<RaidLair> _raidLairs = [];
    Roamers _roamers = null!;

    public RaidBook RaidLog => _raids;
    /// <summary>Every raid's lair, the Ashen Crypt first.</summary>
    public IReadOnlyList<RaidLair> RaidLairs => _raidLairs;
    public RaidLair LairOf(Raid r) => _raidLairs.FirstOrDefault(l => l.Raid.Id == r.Id) ?? AddLair(r);

    RaidLair AddLair(Raid r)
    {
        var lair = new RaidLair(this, r);
        _raidLairs.Add(lair);
        AddChild(lair);
        return lair;
    }

    /// <summary>The nearest lair that suits the hero, near home or out in the far country (worked out now and then).</summary>
    Raid? _suited;
    double _lastFar;

    /// <summary>
    /// The far country's lairs (see <see cref="FarLairs"/>) come and go as the hero travels: each is there, waiting,
    /// by the time they could see it, and let go again once they're well past it (unless they're in the middle of it).
    /// </summary>
    void TendFarLairs()
    {
        if (_clock - _lastFar < 3) return;
        _lastFar = _clock;
        var at = _player.GlobalPosition;
        foreach (var r in FarLairs.Near(_world, at.X, at.Z, 1200)) LairOf(r);
        foreach (var l in _raidLairs.Where(l => l.Raid.Id.StartsWith("raid:far:") && !l.Fighting && !l.Built && l.Raid.DistanceTo(at.X, at.Z) > 2400).ToList())
        {
            _raidLairs.Remove(l);
            l.QueueFree();
        }
        var level = _player.Hero.Level;
        _suited = Raids.All.Concat(FarLairs.Near(_world, at.X, at.Z, 4000))
            .Where(r => !Raids.Outgrown(r, level) && !Raids.Daunting(r, level)).MinBy(r => r.DistanceTo(at.X, at.Z));
    }
    public RaidLair CryptNode => LairOf(Raids.AshenCrypt);
    public Roamers RoamersNode => _roamers;
    TownWatch _watch = null!;
    public TownWatch WatchNode => _watch;
    Skylife _sky = null!;
    RiderFlights _riders = null!;
    Raiders _raiders = null!;
    RaidRecord _raidRecord = new();
    public Raiders RaidersNode => _raiders;
    public RiderFlights RiderFlightsNode => _riders;
    public Skylife SkyNode => _sky;
    TownLife _life = null!;
    public TownLife TownLifeNode => _life;
    readonly HashSet<string> _dramaTold = [];
    (Enemy Foe, Person Rival, Action<bool>? Then)? _duel;
    double _guildClock;

    public GuildBook GuildBook => _guilds;

    /// <summary>The guild yards: their people, a few props, and a name on the map.</summary>
    void AddGuilds()
    {
        foreach (var g in Guilds.All)
        {
            foreach (var m in g.Everyone) Townsfolk(m.Npc, m.Look);
            var centre = new Vector3((float)g.X, 0, (float)g.Z);
            City.Zones.Add(new Zone(Cap(g.Hall), centre, CompoundFence, 10));
            if (g.Calling != Calling.Druid) Compound(g, centre); // the Root Circle's house is the Heartwood itself
            var props = g.Calling switch
            {
                Calling.Warrior => new[] { ("medieval/props/weaponrack.gltf", -8, 4), ("medieval/props/target.gltf", 6, 9), ("medieval/props/target.gltf", 9, 5), ("medieval/props/flag_red.gltf", 0, -9), ("medieval/props/weaponrack.gltf", -4, -8) },
                Calling.Rogue => [("medieval/props/crate_A_big.gltf", 8, -4), ("medieval/props/crate_long_B.gltf", 6, 8), ("medieval/props/barrel.gltf", -9, 3), ("medieval/props/sack.gltf", -7, -6), ("medieval/props/tent.gltf", 12, 2)],
                Calling.Bard => [("medieval/props/tent.gltf", 0, -10), ("medieval/props/flag_yellow.gltf", -6, -9), ("medieval/props/barrel.gltf", 9, 3), ("medieval/props/crate_open.gltf", -9, 4), ("medieval/props/bucket_water.gltf", 7, -6)],
                Calling.Artificer => [("medieval/props/barrel.gltf", 8, -4), ("medieval/props/crate_A_big.gltf", -8, 6), ("medieval/props/bucket_water.gltf", 6, 8), ("medieval/props/resource_stone.gltf", -9, -6), ("medieval/props/flag_red.gltf", 0, -10)],
                _ => [("medieval/props/flag_blue.gltf", 0, -10), ("medieval/props/bucket_water.gltf", 7, 6), ("medieval/props/resource_stone.gltf", -9, 5), ("medieval/props/crate_open.gltf", 8, -7)],
            };
            foreach (var (path, dx, dz) in props)
            {
                var at = centre + new Vector3(dx, 0, dz);
                Models.Place(this, path, at with { Y = City.FloorAt(at) }, 5, dx * 0.3f);
            }
        }
    }

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>What someone says when you walk up: a rival's depends on how things stand between you.</summary>
    string Greeting(Person person)
    {
        if (Guilds.Member(person.Npc.Id) is { Role: GuildRole.Rival } && Guilds.OfMember(person.Npc.Id) is { } g)
            return Guilds.RivalLine(g, _guilds.For(g.Calling));
        // The townsfolk know who you are.
        if (person.Npc.HomeId == "city:tristinwold" && _standing.Aside(_lootRng) is { } aside) return $"{aside} {person.Npc.Greeting}";
        return person.Npc.Greeting;
    }

    /// <summary>The guild's side of a conversation: joining, lessons, trials, standing, gossip, drama, duels.</summary>
    IEnumerable<(string, Action)> GuildTalk(Person person)
    {
        var npc = person.Npc;
        if (npc.Id == "tristinwold/ilse") { yield return ($"I want to join {Guilds.Of(Calling.Mage).Name}.", () => Guild(person, Calling.Mage)); yield break; }
        if (Guilds.Member(npc.Id) is not { } member || Guilds.OfMember(npc.Id) is not { } g) yield break;
        var mine = _player.Class.Calling == g.Calling;
        var record = _guilds.For(g.Calling);

        // Drama comes to you through someone in the guild.
        if (mine && Guilds.NextDrama(_guilds, g.Calling) is { } story && story.TellerId == npc.Id)
            yield return ($"{story.Title}: what's going on?", () => TellDrama(person, story));

        switch (member.Role)
        {
            case GuildRole.Leader:
                if (!mine) yield return ($"I want to join {g.Name}.", () => Guild(person, g.Calling));
                else yield return ("How do I stand in the guild?", () => Say(person, StandingText(g)));
                break;
            case GuildRole.Teacher:
            {
                var d = Classes.Get(member.Teaches!);
                if (_player.Class.Id != d.Id)
                {
                    var again = _player.Hero.Knows(d.Id);
                    yield return (again ? $"I'll take up the way of the {d.Name} again." : $"Teach me the way of the {d.Name}.", () => _hud.Talk(Who(npc), $"{d.Text} Left: {d.Primary.Name}. Right: {d.Signature.Name}. Q: {d.Utility.Name}. R: {d.Limit.Name}.",
                        (again ? $"I remember it. (Be a {d.Name} again.)" : $"I'll learn it. (Become a {d.Name}, and keep what you were.)", () =>
                        {
                            var was = _player.Class.Name;
                            _player.Join(d.Id);
                            _hud.Toast(again
                                ? $"You are a {d.Name} again, with every rank you had in it. The {was}'s ranks keep for when you go back (any teacher, or the sheet, K)."
                                : $"You are now a {d.Name}, and still a {was} underneath: an immortal keeps every way they've learned. {_player.Hero.Level / 2} skill points to spend on it (K); the {was}'s ranks keep for when you take it up again.", 11);
                            Save();
                            Say(person, again ? "Welcome back. The hands remember." : "Good. We start now. We always start now.");
                        }),
                        ("Not yet.", () => Say(person, "The door's open."))));
                    break;
                }
                var cost = LessonCost;
                yield return (_guilds.LessonLevel >= _player.Hero.Level ? "Teach me. (You've had this level's lesson.)" : $"Teach me. ({cost} crowns for a skill point)", () => Lesson(person));
                var step = _guilds.Step(d.Id);
                if (ClassQuests.For(_world, d, step) is { } trial)
                {
                    var entry = _journal.Get(trial.Id);
                    if (entry is null)
                        yield return ("Is there a trial for me?", () => _hud.Talk(Who(npc), trial.Pitch,
                            ("I'll do it.", () => { Accept(trial); Say(person, "Then go. Come back changed."); }),
                            ("Not yet.", () => Say(person, "It'll wait. I'm patient. Mostly."))));
                    else if (entry.Stage == QuestStage.Active) yield return ("About my trial...", () => Say(person, $"{entry.Objective}. Go on."));
                }
                else if (step >= ClassQuests.Steps) yield return ("How am I doing?", () => Say(person, $"You're {ClassQuests.MasteryTitle(d.Id)}. There's nothing left I can teach you, except humility."));
                break;
            }
            case GuildRole.Rival:
                if (_duel is null) yield return ("Spar with me.", () => StartDuel(person, null));
                yield return ("What's your problem with me?", () => Say(person, record.Rivalry > 0
                    ? $"You. You're my problem. You turned up and everyone started looking at you instead."
                    : $"Problem? I don't have a problem. You're the one who keeps winning."));
                break;
            case GuildRole.Member:
                yield return ("What's the talk of the guild?", () => Say(person, Gossip(g, member)));
                break;
        }
    }

    int LessonCost => 20 * _player.Hero.Level;

    /// <summary>A lesson from your class's teacher: once a level, for a fee, a skill point.</summary>
    void Lesson(Person person)
    {
        if (_guilds.LessonLevel >= _player.Hero.Level) { Say(person, $"We've done what we can at this level. Come back when you've grown (level {_player.Hero.Level + 1})."); return; }
        if (_player.Crowns < LessonCost) { Say(person, $"Lessons cost {LessonCost} crowns. I have a roof to fix."); return; }
        _player.Crowns -= LessonCost;
        _guilds.LessonLevel = _player.Hero.Level;
        _player.Hero.Points++;
        Raise(_player.Class.Calling, 3);
        Save();
        Say(person, "Again. Slower. Again. Good. (You have a new skill point to spend: K.)");
    }

    string StandingText(Guild g)
    {
        var r = _guilds.For(g.Calling);
        var rank = Guilds.RankOf(r.Standing);
        var next = (int)rank + 1 < Guilds.RankStanding.Length ? $" {Guilds.RankStanding[(int)rank + 1] - r.Standing} more and you'll be {(GuildRank)((int)rank + 1)}." : " There's no higher to go.";
        return $"You're {Guilds.Title(rank, g)}.{next} Duels with {g.Rival.Npc.First}: {r.DuelsWon} won, {r.DuelsLost} lost.";
    }

    /// <summary>The talk of the guild: what's happened, what's been chosen, who's rising.</summary>
    string Gossip(Guild g, GuildMember who)
    {
        var f = _guilds.Flags;
        var r = _guilds.For(g.Calling);
        var lines = new List<string>();
        switch (g.Calling)
        {
            case Calling.Warrior:
                if (f.Contains("defended_odo")) lines.Add("Odo talks about you like you're a saint. Corin talks about you like you're a disease.");
                if (f.Contains("blamed_odo")) lines.Add("Odo's been quiet since the business with the sword. He'll come round. Maybe.");
                if (f.Contains("tong_money")) lines.Add("Nice new roof. Shame about what paid for it.");
                if (f.Contains("double_game")) lines.Add("Half the Ravenscrew's dock men are in the Watch cells. Brannoc can't stop grinning.");
                if (f.Contains("first_blade")) lines.Add("The First Blade. The drills have never been so sharp, or so early.");
                break;
            case Calling.Rogue:
                if (f.Contains("covered_mags")) lines.Add("Mags's sister is walking again. Mags won't say who paid for the healer.");
                if (f.Contains("sold_jory")) lines.Add("Jory doesn't sit on your side of the room any more.");
                if (f.Contains("parley")) lines.Add("The Gutter Prince sends his regards. Nobody can quite believe it.");
                if (f.Contains("burned_den")) lines.Add("The Lantern Boys cross the street when they see a Knife now.");
                if (f.Contains("found_informant")) lines.Add("Coyle won't be selling anything to anyone again.");
                break;
            case Calling.Bard:
                if (f.Contains("exposed_fennick")) lines.Add("They still sing Tobin's song in the Salt Barrel, and boo when Fennick walks in.");
                if (f.Contains("pips_song")) lines.Add("Tobin's drumming again. On everything. Thank you, I suppose.");
                if (f.Contains("let_song_go")) lines.Add("Tobin doesn't drum much any more. Fennick's song is still everywhere.");
                if (f.Contains("wrote_libel")) lines.Add("Nice new roof. Hollis won't sit under it.");
                if (f.Contains("acrostic")) lines.Add("Somebody's pinned your pamphlet up in the Watch House, with the first letters circled.");
                if (f.Contains("midwinter")) lines.Add("You sang at the Crown! Hal cried it through every street for a week.");
                break;
            default:
                if (f.Contains("liesl_book")) lines.Add("Liesl says you're the only one who doesn't look at her like she's a crack in a wall.");
                if (f.Contains("rift_study")) lines.Add("Liesl's notes are full of doors now. Selwyn's worried. So am I.");
                if (f.Contains("accused_isadora")) lines.Add("Isadora hasn't forgiven you for the book. She has a long memory, and a longer list.");
                if (f.Contains("council_seat")) lines.Add("Councillor. You. The Archive has never been so... lively.");
                if (f.Contains("read_vann")) lines.Add("You've had a haunted look since you came back from the Archive. What did you read?");
                break;
        }
        var rank = Guilds.RankOf(r.Standing);
        lines.Add(rank switch
        {
            GuildRank.Initiate => $"You're new. Everyone's watching to see what you do. {g.Rival.Npc.First} most of all.",
            GuildRank.Member => $"You're one of us now. {g.Leader.Npc.First} mentions your name, sometimes.",
            GuildRank.Adept => $"People are starting to say your name and {g.Rival.Npc.First}'s in the same breath.",
            GuildRank.Veteran => "The new ones ask about you. I tell them the truth, mostly.",
            _ => "There'll be songs. Bad ones, but songs.",
        });
        return lines[(int)(Time.GetTicksMsec() / 4000 % (ulong)lines.Count)];
    }

    // ---- drama --------------------------------------------------------------------------------------

    void TellDrama(Person person, Storylet story)
    {
        var options = story.Choices.Select(c => (c.Label, (Action)(() =>
        {
            if (c.Duel)
            {
                var rival = Locals.First(p => p.Npc.Id == Guilds.Of(story.Guild).Rival.Id);
                StartDuel(rival, won => Resolve(story, c, won));
            }
            else Resolve(story, c, true);
        }))).ToArray();
        _hud.Talk($"{story.Title}. {Who(person.Npc)}", story.Intro, options);
    }

    void Resolve(Storylet story, StoryChoice c, bool won)
    {
        var before = _guilds.Rank(story.Guild);
        Guilds.Choose(_guilds, story, c, won, _standing);
        if (c.Crowns != 0 && (won || !c.Duel)) _player.Crowns = Math.Max(0, _player.Crowns + c.Crowns);
        _hud.Talk(story.Title, won || c.Loss is null ? c.Outcome : c.Loss, ("...", _hud.EndTalk));
        RankedUp(story.Guild, before);
        Save();
    }

    void Raise(Calling c, int standing)
    {
        var before = _guilds.Rank(c);
        _guilds.For(c).Standing += standing;
        RankedUp(c, before);
    }

    void RankedUp(Calling c, GuildRank before)
    {
        var now = _guilds.Rank(c);
        if (now > before) _hud.Toast($"You are now {Guilds.Title(now, Guilds.Of(c))}.", 9);
    }

    /// <summary>Time passes in the guilds: drama comes looking for you, and you hear what your rival's been up to.</summary>
    void TendGuilds(double delta)
    {
        _guilds.PlaySeconds += delta;
        if ((_guildClock += delta) < 5) return;
        _guildClock = 0;
        var calling = _player.Class.Calling;
        var g = Guilds.Of(calling);
        if (Guilds.NextDrama(_guilds, calling) is { } story && _dramaTold.Add(story.Id))
        {
            var teller = Guilds.Member(story.TellerId)!.Npc;
            _hud.Toast($"Trouble in {g.Name}: {teller.Name} wants a word at {g.Hall} ({Direction(_player.GlobalPosition, (teller.X, teller.Z))}).", 10);
        }
        if (_guilds.PlaySeconds >= _guilds.NextRivalNewsAt)
        {
            _guilds.NextRivalNewsAt = _guilds.PlaySeconds + Guilds.RivalNewsGapSeconds;
            _hud.Toast(Guilds.RivalNews(g, _guilds.RivalNewsCount++), 9);
        }
    }

    // ---- duels --------------------------------------------------------------------------------------

    /// <summary>
    /// A duel with your rival, in their guild's yard: first to be beaten down to the edge yields, nobody dies. They
    /// fight as their class, as strong as you are (stronger if the rivalry runs hot).
    /// </summary>
    public void StartDuel(Person rival, Action<bool>? then)
    {
        if (_duel is not null) return;
        _hud.EndTalk();
        var g = Guilds.OfMember(rival.Npc.Id)!;
        var record = _guilds.For(g.Calling);
        var level = Math.Max(1, _player.Hero.Level + Math.Clamp(record.Rivalry, -2, 3));
        var at = rival.GlobalPosition;
        var foe = Enemy.Create(Bestiary.Get("bandit_chief"), at with { Y = City.FloorAt(at) + 0.3f }, look: g.Rival.Look);
        foe.Spar(rival.Npc.Name, 150 + 24 * level, 9 + 1.7 * level);
        AddEnemy(foe);
        rival.Visible = false;
        rival.CollisionLayer = 0;
        foe.Provoke(_player);
        _duel = (foe, rival, then);
        _player.Face(at);
        _hud.Toast($"{rival.Npc.First} draws. First to yield loses!", 6);
    }

    void OnSparEnded(Enemy foe, bool won)
    {
        if (_duel is not { } duel || duel.Foe != foe) return;
        _duel = null;
        if (duel.Rival.Npc.Id.StartsWith("drifter:"))
        {
            DrifterDuelOver(foe, duel.Rival);
            duel.Then?.Invoke(won);
            Save();
            return;
        }
        if (duel.Rival.Npc.Id.StartsWith("hall:"))
        {
            HallDuelOver(foe);
            duel.Then?.Invoke(won);
            Save();
            return;
        }
        if (Guilds.OfMember(duel.Rival.Npc.Id) is not { } g)
        {
            CouncilDuelOver(foe, duel.Rival, won);
            duel.Then?.Invoke(won);
            Save();
            return;
        }
        var record = _guilds.For(g.Calling);
        if (won) { record.DuelsWon++; Raise(g.Calling, 5); }
        else { record.DuelsLost++; _player.Stats.Heal(_player.Stats.MaxHealth * 0.3); }
        _hud.Toast(won ? $"{duel.Rival.Npc.First} yields." : $"You yield to {duel.Rival.Npc.First}.", 6);
        if (won) foreach (var u in _journal.DuelWon(duel.Rival.Npc.Id).ToList()) Apply(u);
        GetTree().CreateTimer(2.5).Timeout += () =>
        {
            if (IsInstanceValid(foe)) foe.QueueFree();
            if (IsInstanceValid(duel.Rival)) { duel.Rival.Visible = true; duel.Rival.CollisionLayer = 1; }
        };
        duel.Then?.Invoke(won);
        Save();
    }

    /// <summary>Someone other than a guild's rival steps up to a hero, for blood: same footing as a guild duel, and whoever goes down stays down.</summary>
    void StartBloodDuel(Person rival, Action<bool>? then)
    {
        if (_duel is not null) return;
        _hud.EndTalk();
        var level = Math.Max(1, _player.Hero.Level);
        var at = rival.GlobalPosition;
        var foe = Enemy.Create(Bestiary.Get("bandit_chief"), at with { Y = City.FloorAt(at) + 0.3f });
        foe.Spar(rival.Npc.Name, 150 + 24 * level, 9 + 1.7 * level);
        AddEnemy(foe);
        rival.Visible = false;
        rival.CollisionLayer = 0;
        foe.Provoke(_player);
        _duel = (foe, rival, then);
        _player.Face(at);
        _hud.Toast($"{rival.Npc.First} draws. This one's to the death.", 6);
    }

    public bool Dueling => _duel is not null;

    /// <summary>A class trial finished: the next one opens, the guild takes note, and the last makes you a master.</summary>
    void ClassTrialDone(string discipline, int step)
    {
        _guilds.ClassSteps[discipline] = step + 1;
        Raise(Classes.Get(discipline).Calling, 15 + 10 * step);
        if (step + 1 < ClassQuests.Steps) { _hud.Toast("Your teacher has another trial for you.", 7); return; }
        var title = ClassQuests.MasteryTitle(discipline);
        if (!_guilds.Titles.Contains(title)) _guilds.Titles.Add(title);
        _player.Hero.Points += 2;
        _hud.Toast($"You are {title}! Two skill points to spend (K).", 12);
    }
}
