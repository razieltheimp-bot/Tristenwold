using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The hero's name in the world (see <see cref="Standing"/>), and the two careers on either side of the law. Captain
/// Harlan Vale at the Watch House takes on recruits: a beat is a walk from post to post about the city, with a footpad
/// to collar on most of them, and the beats and arrests make a watchman, a sergeant, and at last the captain. Out on
/// the frontier, beat one of a camped clan's raiders and its warchief may offer you a place (see <see cref="Raiders"/>);
/// ride with the clan on a place and you take a share of the plunder, and a price on your head in Tristinwold. The
/// Watch stops an outlaw at sight; the price is paid to Captain Vale, or, for a fifth on top, to Nell Corran the
/// smuggler. And the townsfolk know who you are: by what they say when you walk up, by what the shops charge, and by
/// what they tell of you when you ask for news.
/// </summary>
public partial class Main
{
    Standing _standing = new();
    Enemy? _trouble;
    double _standingTick, _haltedAt = -60;

    public Standing Standing => _standing;
    /// <summary>For the self-test: the footpad on the beat, and the warchief's door.</summary>
    public Enemy? Footpad => _trouble;
    public (string Verb, Action Act)? ClanActionForTest(Vector3 pos) => ClanAction(pos);

    /// <summary>Captain Vale's spot, by the Watch House's door.</summary>
    public static readonly Vector3 WatchHouseAt = new(60, 0, 51);
    public const string WatchCaptain = "tristinwold/watchcaptain";
    /// <summary>How near a post counts as walked, and how far trouble gets before it's got away.</summary>
    const float PostReach = 9, TroubleEscape = 70;

    void AddWatchHouse()
    {
        Townsfolk(new Npc(WatchCaptain, "Captain Harlan Vale", Sex.Male, 54, "captain of the Tristinwold Watch", "city:tristinwold", "Tristinwold",
            "Harlan Vale has kept the Watch for eleven years and the peace for most of them. His son Corin fights for the Iron Circle and drinks with the wrong officers, and Harlan pretends not to know either.",
            ["the law", "old battles"], "dry", "The Watch House. State your business, or walk on.", WatchHouseAt.X, WatchHouseAt.Z), "guard");
    }

    // ---- the Watch -----------------------------------------------------------------------------------

    IEnumerable<(string, Action)> StandingTalk(Person person)
    {
        var id = person.Npc.Id;
        var w = _standing.Watch;
        if (id == WatchCaptain)
        {
            if (!w.In) yield return ("I want to join the Watch.", () => JoinWatch(person));
            else if (w.Patrol is null) yield return ("Give me a beat to walk.", () => StartBeat(person));
            else if (w.Patrol.Walked) yield return ("Reporting back: the beat's walked.", () => ReportBeat(person));
            else yield return ("About the beat...", () => Say(person, $"Then why are you here? {char.ToUpper(w.Patrol.NextPost![0])}{w.Patrol.NextPost[1..]} is next. Walk it."));
            yield return ("How do I stand with the Watch?", () => Say(person, w.In
                ? $"You're {WatchBook.Title(w.Rank)}: {w.Patrols} beat{(w.Patrols == 1 ? "" : "s")} walked, {w.Arrests} arrest{(w.Arrests == 1 ? "" : "s")}, {w.Earned} crowns drawn. {w.NextStep(_player.Hero.Level)}"
                : $"The town knows you as {_standing.Name}. {(_standing.WhyNotWatch is { } why ? $"The Watch won't have you: {why}." : "The Watch could use you, if you want honest work.")}"));
            if (_standing.Outlaw) yield return ($"Pay what's on my head. ({_standing.Bounty} crowns)", () => PayBounty(person, false));
        }
        else if (id == Smuggler && _standing.Outlaw)
            yield return ($"Make the Watch forget my face. ({(int)Math.Ceiling(_standing.Bounty * (1 + Standing.SmugglersCut))} crowns)", () => PayBounty(person, true));
    }

    void JoinWatch(Person captain)
    {
        if (_standing.WhyNotWatch is { } why) { Say(captain, $"No. {char.ToUpper(why[0])}{why[1..]}, and the Watch doesn't take your kind."); return; }
        _standing.Watch.Join();
        _hud.Toast("You're a recruit of the Tristinwold Watch. Ask Captain Vale for a beat to walk: post to post about the city, and whatever trouble you meet on the way is yours to collar.", 10);
        Say(captain, "Good. The pay's bad and the hours are worse. You walk the beat I give you, post to post, and anything you find on the way, you bring in. Come back here when it's walked.");
        Save();
    }

    void StartBeat(Person captain)
    {
        if (_standing.Watch.StartPatrol(_lootRng) is not { } beat) return;
        var posts = string.Join(", then ", beat.Posts);
        Say(captain, $"Walk {posts}, and come back here. Keep your eyes open.");
        _hud.Toast($"A beat to walk: {posts}. The tracker shows the next post.", 9);
        Save();
    }

    void ReportBeat(Person captain)
    {
        var w = _standing.Watch;
        var arrests = w.Patrol?.Arrests ?? 0;
        var before = w.Rank;
        var (pay, promoted) = w.Report(_player.Hero.Level);
        if (pay == 0) return;
        _player.Crowns += pay;
        _standing.Good(1 + arrests, arrests > 0 ? "collared a footpad for the Watch" : "walked a beat for the Watch");
        Sound.Ui("coin", -2);
        Say(captain, $"{(arrests > 0 ? "One in the cells. Good." : "All quiet. Good.")} Here's your pay: {pay} crowns." + (promoted is { } r ? $" And there's more: you're {WatchBook.Title(r)} now." : ""));
        _hud.Toast($"Beat walked: +{pay} crowns ({w.Patrols} walked, {w.Arrests} arrests).", 6);
        if (promoted is { } rank)
        {
            _hud.Toast($"Captain Vale makes you {WatchBook.Title(rank)}.", 9);
            Sound.Ui("quest");
            if (rank == WatchRank.Captain && !_guilds.Titles.Contains("Captain of the Watch")) _guilds.Titles.Add("Captain of the Watch");
        }
        _ = before;
        Save();
    }

    void PayBounty(Person to, bool smuggler)
    {
        var purse = _player.Crowns;
        if (_standing.PayBounty(ref purse, smuggler) is not { } cost)
        {
            Say(to, smuggler ? "Then come back when you've got it. Through the back way." : "Then you'll pay it in the cells. Guards!");
            return;
        }
        _player.Crowns = purse;
        _watch.Hostile = false;
        Sound.Ui("coin", -2);
        Say(to, smuggler ? $"{cost} crowns, and a word in the right ear. By morning there's no bill at the gate with your face on it. There never was." : $"{cost} crowns, paid. The bill comes down. Don't make me put it up again.");
        _hud.Toast($"The price on your head is paid ({cost} crowns). The Watch stands down.", 7);
        Save();
    }

    /// <summary>Time on the beat: posts reached, trouble met, and the Watch's eye on an outlaw.</summary>
    void TendStanding(double delta)
    {
        if ((_standingTick -= delta) > 0) return;
        _standingTick = 1;
        var pos = _player.GlobalPosition;
        if (_watch.Hostile != _standing.Outlaw) _watch.Hostile = _standing.Outlaw;
        if (_standing.Outlaw && TownWatch.InTristinwold(pos) && _watch.GuardsNear(pos, 18) > 0 && _clock - _haltedAt > 45)
        {
            _haltedAt = _clock;
            _hud.Toast($"\"Halt, in the name of the Watch!\" There are {_standing.Bounty} crowns on your head. Pay it at the Watch House, or to Nell Corran by the south pier, or fight.", 8);
        }

        if (_standing.Watch.Patrol is not { Walked: false } beat) return;
        if (_trouble is { } t && (!IsInstanceValid(t) || t.State == EnemyState.Dead)) _trouble = null;
        if (_trouble is { } away && away.GlobalPosition.DistanceTo(pos) > TroubleEscape)
        {
            beat.TroubleMet = true;
            _hud.Toast($"{away.Stats.Name} has got away. The captain won't like it.", 6);
            _trouble = null;
        }
        if (TownWatch.PostAt(beat.NextPost!) is not { } at || at.DistanceTo(pos with { Y = at.Y }) > PostReach) return;
        if (beat.TroubleHere && _trouble is null) { SpawnFootpad(beat.NextPost!); return; }
        if (_trouble is not null) return; // collar them first
        var post = beat.NextPost!;
        _standing.Watch.Reached(post);
        _hud.Toast(beat.Walked ? $"{char.ToUpper(post[0])}{post[1..]}: all quiet. The beat's walked; report back to Captain Vale at the Watch House." : $"{char.ToUpper(post[0])}{post[1..]}: all quiet. On to {beat.NextPost}.", 6);
    }

    /// <summary>Trouble at a post: a footpad at work, who'd rather fight than come quietly.</summary>
    void SpawnFootpad(string post)
    {
        var p = _player;
        var at = p.GlobalPosition + p.GlobalTransform.Basis.Z * -10;
        var e = Enemy.Create(Bestiary.Get("dock_thug"), at with { Y = City.FloorAt(at) + 0.5f });
        e.Stats.Name = "Footpad";
        e.Criminal = true;
        AddEnemy(e);
        e.RefreshPlate();
        e.Provoke(p);
        _trouble = e;
        _hud.Toast($"A footpad at {post}, a knife at someone's purse-strings. He sees the Watch's colours on you and comes at you instead. Collar him.", 7);
    }

    /// <summary>What a death means to the hero's name: an arrest on the beat, a watchman killed, a cutthroat put down.</summary>
    void StandingSlain(Enemy victim)
    {
        if (victim == _trouble)
        {
            _trouble = null;
            _standing.Watch.Arrest();
            _standing.Good(2, "collared a footpad on the beat");
            _hud.Toast("The footpad goes down, and a pair of watchmen drag him off to the cells. An arrest to your name.", 6);
            return;
        }
        if (victim.Watch)
        {
            // A village's own guard: a black mark, unless you rode on the place with your clan (that's a price already).
            if (victim.Keeps is not null) { if (_raiders.Mine is not { Raiding: not null }) _standing.Evil(3, "cut down a village guard"); return; }
            _standing.Evil(8, "killed a watchman of Tristinwold");
            _standing.Wanted(Standing.WatchmanBounty);
            _watch.Hostile = true;
            _hud.Toast($"You've killed a watchman. There's a price on your head now: {_standing.Bounty} crowns. The Watch will stop you at sight.", 9);
            return;
        }
        if (victim.Criminal && TownWatch.InTristinwold(victim.GlobalPosition)) _standing.Good(1, "put down a cutthroat in the streets");
    }

    IEnumerable<string> StandingLines(Vector3 pos)
    {
        if (_standing.Outlaw) yield return $"Outlaw: {_standing.Bounty} crowns on your head. The Watch stops you at sight";
        if (_standing.Watch.Patrol is { } beat)
        {
            if (beat.Walked) yield return $"The Watch: report back to the Watch House ({Direction(pos, (WatchHouseAt.X, WatchHouseAt.Z))})";
            else if (_trouble is not null) yield return $"The Watch: collar the footpad at {beat.NextPost}";
            else if (TownWatch.PostAt(beat.NextPost!) is { } at) yield return $"The Watch: walk to {beat.NextPost} ({Direction(pos, (at.X, at.Z))})";
        }
        if (_standing.Clan.In && _raiders.Mine is { } band)
            yield return band.Raiding is { } r ? $"Your clan: {band.W.Name} are down on {r.Name}. Ride with them ({Direction(pos, (r.X, r.Z))})" : $"Your clan: {band.W.Name} are camped nearby";
    }

    // ---- the clan ------------------------------------------------------------------------------------

    /// <summary>A clan's offer, taken: sworn to it, struck off the Watch, and the city hears.</summary>
    /// <summary>The self-test's crimes are always seen.</summary>
    public bool CaughtSure { get; set; }

    public void JoinClan(Warband w)
    {
        var wasWatch = _standing.Watch.In;
        if (!_standing.JoinClan(w)) return;
        _hud.Toast($"You're a spear of {w.Name} now. Ride with them when they fall on a place, and you'll take a share of the plunder, and a name in Tristinwold you won't like.{(wasWatch ? " The Watch doesn't know yet: get seen on a raid, and it will." : "")}", 10);
        Sound.Ui("quest", -3);
        Save();
    }

    /// <summary>Rode with the clan on a place, and it fell: the share, the price, and the clan's regard.</summary>
    public void RodeWith(Warband w, Settlement place, int round)
    {
        var (share, promoted) = _standing.Clan.Rode(w, _player.Hero.Level);
        _player.Crowns += share;
        _standing.Evil(6, $"rode with {w.Name} on {place.Name}");
        var seen = _standing.Caught(Standing.RaidSeen, Standing.RaidBounty, _lootRng, sure: CaughtSure);
        if (_trade.Towns.TryGetValue(place.Id, out var m)) m.Plundered(_city.Day);
        _raiders.Record.Reckon(w, round);
        Sound.Ui("coin", -2);
        _hud.Toast(seen
            ? $"{place.Name} is plundered. Your share: {share} crowns. Somebody knew your face: {Standing.RaidBounty} more crowns on your head in Tristinwold ({_standing.Bounty} in all){(_standing.Watch.Dismissals > 0 && !_standing.Watch.In ? ", and the Watch has struck you off" : "")}."
            : $"{place.Name} is plundered. Your share: {share} crowns, and nobody who saw you lived to say so.", 10);
        if (promoted is { } rank)
        {
            _hud.Toast(rank == ClanRank.Warchief
                ? $"{w.Chief} hands you the clan's horn. You are warchief of {w.Name}."
                : $"The clan calls you {_standing.Clan.Title}.", 9);
            Sound.Ui("quest");
            if (rank == ClanRank.Warchief && !_guilds.Titles.Contains($"Warchief of {w.Name}")) _guilds.Titles.Add($"Warchief of {w.Name}");
        }
        Save();
    }

    /// <summary>Near your clan's warchief: a word with him.</summary>
    (string Verb, Action Act)? ClanAction(Vector3 pos)
    {
        if (!_standing.Clan.In || _raiders.Chief is not { } chief || chief.GlobalPosition.DistanceTo(pos) > 4.5f) return null;
        return ($"speak with {_standing.Clan.Chief}", () => ClanTalk(chief));
    }

    void ClanTalk(Enemy chief)
    {
        var c = _standing.Clan;
        var who = $"{c.Chief}, warchief of {c.ClanName}";
        _hud.Talk(who, c.Rank switch
        {
            ClanRank.Spearman => "\"Spear. Keep up, and keep your mouth shut.\"",
            ClanRank.Raider => "\"Raider. The lads say you fight. Good. Fight more.\"",
            ClanRank.Champion => "\"Champion. When I'm gone, they'll look to you. Don't be in a hurry.\"",
            _ => "\"Warchief. The clan's yours. Where do we ride?\"",
        },
            ("How do I stand with the clan?", () => _hud.Talk(who, $"\"You're {c.Title}. {c.Raids} raid{(c.Raids == 1 ? "" : "s")} ridden, {c.Plunder} crowns taken. {c.NextStep(_player.Hero.Level)}\"", ("...", _hud.EndTalk))),
            ("I'm done riding with you.", () =>
            {
                _hud.EndTalk();
                _standing.Clan.Leave();
                _raiders.Disown();
                _hud.Toast($"You turn your back on {c.ClanName}. They don't take it well.", 8);
                Save();
            }),
            ("Nothing. Ride on.", _hud.EndTalk));
    }
}
