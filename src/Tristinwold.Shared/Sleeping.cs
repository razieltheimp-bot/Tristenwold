namespace Tristinwold.Shared;

/// <summary>
/// Heroes left asleep. When a player goes, their hero stays where they were, standing about and shifting a little, for
/// everyone else to see. Anyone ready to fight other players (B) can go for them: the blows are counted here, against
/// the hero's own health, and a sleeper left alone a couple of minutes is whole again. Cut one down and the winner has a
/// share of their purse, as a copy, the way a fall between heroes pays (and once an hour, the same count). Nobody fights
/// in a tavern, asleep or awake. When the player comes back they're told who went for them, and wake where they lay.
/// </summary>
public sealed partial class ServerCore
{
    /// <summary>Where each player online would lie if they left now (see <see cref="SleepSpot"/>).</summary>
    readonly Dictionary<string, SleepSpot> _spots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The share of a sleeper's purse their killer wins: small, for a hero who couldn't fight back.</summary>
    public const double SleeperShare = 0.1;
    /// <summary>A sleeper left alone this long is whole again.</summary>
    public static readonly TimeSpan SleeperMends = TimeSpan.FromMinutes(2);
    public const double MostSleeperHealth = 1_000_000;

    public Sleepers SleepersMessage() => new([.. State.Sleepers.Values.OrderBy(s => s.Player, StringComparer.OrdinalIgnoreCase).Select(s => s.ToView())]);

    IReadOnlyList<Outgoing> OnSleepSpot(string player, SleepSpot s)
    {
        if (!float.IsFinite(s.X) || !float.IsFinite(s.Y) || !float.IsFinite(s.Z) || !float.IsFinite(s.Yaw) || !double.IsFinite(s.MaxHealth)) return [];
        _spots[player] = s with { Crowns = Math.Clamp(s.Crowns, 0, MostSpoil), MaxHealth = Math.Clamp(s.MaxHealth, 1, MostSleeperHealth) };
        return [];
    }

    /// <summary>A player went: their hero stays where they'd wake, looking as they were last seen.</summary>
    void FallAsleep(string player)
    {
        var seen = _sights.GetValueOrDefault(player);
        var spot = _spots.GetValueOrDefault(player);
        _spots.Remove(player);
        if (seen is null && spot is null) return; // never stepped out into the world: nothing to leave behind
        var health = spot?.MaxHealth ?? 100;
        State.Sleepers[player] = new Sleeper
        {
            Player = player,
            Name = seen?.Name is { Length: > 0 } name ? name : player,
            Look = seen?.Look ?? "Knight",
            Avatar = seen?.Avatar,
            X = spot?.X ?? seen!.X,
            Y = spot?.Y ?? seen!.Y,
            Z = spot?.Z ?? seen!.Z,
            Yaw = spot?.Yaw ?? seen!.Yaw,
            Crowns = spot?.Crowns ?? 0,
            MaxHealth = health,
            Health = health,
            Safe = spot?.Safe ?? false,
        };
        Dirty = true;
    }

    /// <summary>A player's back: their sleeper is gone, and they hear what happened to it, if anything did.</summary>
    WhileAway? WakeUp(string player)
    {
        if (!State.Sleepers.Remove(player, out var s)) return null;
        Dirty = true;
        return s.Attackers.Count > 0 || s.Killer is not null ? new WhileAway([.. s.Attackers], s.Killer, s.Taken) : null;
    }

    /// <summary>A blow at a sleeper: counted here, since nobody's there to take it.</summary>
    IReadOnlyList<Outgoing> StrikeSleeper(string player, StrikeHero blow)
    {
        if (!State.Sleepers.TryGetValue(blow.Target.Trim(), out var s) || s.Fallen || s.Safe) return [];
        if (s.Player.Equals(player, StringComparison.OrdinalIgnoreCase)) return [];
        var now = _clock();
        if (now - s.LastBlow > SleeperMends) s.Health = s.MaxHealth; // left alone long enough to be whole again
        s.LastBlow = now;
        if (!s.Attackers.Contains(player, StringComparer.OrdinalIgnoreCase)) s.Attackers.Add(player);
        s.Health -= Math.Min(blow.Damage, MostBlow);
        Dirty = true;
        if (s.Health > 0) return [new(null, SleepersMessage())];

        // Cut down: the winner has their share, once an hour, as for any fall between heroes.
        s.Health = 0;
        s.Fallen = true;
        s.Killer = player;
        var outgoing = new List<Outgoing> { new(null, SleepersMessage()) };
        var pair = $"{player}|{s.Player}";
        if (_spoiled.TryGetValue(pair, out var last) && now - last < SpoilsEvery)
            outgoing.Add(new(player, new Notice(NoticeKind.World, $"You've cut down {s.Name} where they slept, but there's nothing more to win from them so soon.")));
        else
        {
            _spoiled[pair] = now;
            var share = (int)Math.Floor(s.Crowns * SleeperShare);
            s.Taken += share;
            outgoing.Add(new(player, new HeroSpoils(s.Player, [], share, 0, 0)));
        }
        outgoing.Add(new(null, new Notice(NoticeKind.World, $"{player} has cut down {s.Name} where they slept."), Except: player));
        return outgoing;
    }
}
