using Tristinwold.Gameplay;

namespace Tristinwold.Shared;

/// <summary>
/// The city everyone shares: one Tristinwold economy on the server. Every company's sales, purchases, deliveries
/// and gifts are done to it, its days turn over while anyone is playing (ten minutes a day, as in a game on its
/// own), and every game gets the city as it stands, at most once a second.
/// </summary>
public sealed partial class ServerCore
{
    bool _cityChanged;
    DateTime _cityClock = DateTime.MinValue;

    public CityEconomy City => State.City ??= CityEconomy.Start(_rng.NextULong());

    CitySnapshot CityMessage() => new(City, State.CitySeconds);

    IReadOnlyList<Outgoing> OnCityTrade(string player, CityTrade t)
    {
        var news = City.News.Count;
        // Only a living deviant can take a town, under its own name and kind, and only one too strong for the hero it was set on.
        if (t.Action is { Kind: "assault_begin" } begin)
        {
            if (begin.Assault is not { } a || !Guid.TryParse(a.By, out var id) || World.Find(id) is not { Alive: true } dev
                || !State.Groups.TryGetValue(player, out var g) || g.Level > 0 && !World.Outclasses(dev, g.Level)) return [];
            t = t with { Action = begin with { Assault = new Assault { Town = a.Town, By = a.By, Name = dev.FullName, Kind = dev.BaseForm ?? dev.Kind, Level = dev.Level, Rank = (int)dev.Rank } } };
        }
        City.Apply(t.Action);
        _cityChanged = true;
        // A gift that finishes the works is news for everybody else (the giver's game has already said so).
        return [.. City.News.Skip(news).Select(n => new Outgoing(null, new Notice(NoticeKind.World, $"In Tristinwold: {n}"), Except: player))];
    }

    /// <summary>The city's clock, and its news; and the city as it stands to every game, when it's changed.</summary>
    double _storeSeconds;
    const double StoreEvery = 30;

    IReadOnlyList<Outgoing> TickCity()
    {
        var now = _clock();
        var roster = new List<Outgoing>();
        // The city only lives while someone's in it, and a stalled server doesn't run the days on in a rush.
        if (_cityClock != DateTime.MinValue)
        {
            var passed = Math.Clamp((now - _cityClock).TotalSeconds, 0, 60);
            if (_online.Count > 0) State.CitySeconds += passed;
            // Heroes' homes don't wait for anyone: their people work, and their shops serve, as long as the server's up.
            if ((_storeSeconds += passed) >= StoreEvery)
            {
                City.TendStores(_storeSeconds);
                City.TendHalls(_storeSeconds);
                City.TendKorrin();
                City.TendRaid();
                if (City.TendMotions(_storeSeconds)) _cityChanged = true;
                // The strong go for the strong: past every hero playing, Ronacse and the worst deviants come for the towns.
                var heard = City.News.Count;
                var sacked = City.TendAssaults(StrongestOnline(),
                    World.Living.Select(n => new AssaultCandidate(n.Id.ToString(), n.FullName, n.BaseForm ?? n.Kind, n.Level, (int)n.Rank, Tristinwold.Deviant.DeviantWorld.Might(n))).ToList());
                roster.AddRange(City.News.Skip(heard).Select(n => new Outgoing(null, new Notice(NoticeKind.World, n))));
                var grown = new List<Tristinwold.Deviant.WorldEvent>();
                foreach (var a in sacked)
                    if (Guid.TryParse(a.By, out var id)) grown.AddRange(World.Empower(id, 10, "It sacked a town, and nobody could stop it."));
                if (sacked.Count > 0) roster.AddRange(WorldChanged(grown));
                // The named deviants on the roster can sit at a council's table, and come for the heroes who rule one.
                var schemed = City.TendWarlords(World.Living.Select(Warlords.From).ToList());
                _storeSeconds = 0;
                _cityChanged = City.Stores.Count + City.Halls.Count > 0 || City.Assaults.Count > 0 || sacked.Count > 0 || schemed || _cityChanged;
            }
        }
        _cityClock = now;
        var news = City.Advance(State.CitySeconds);
        if (news.Count == 0 && !_cityChanged) return roster;
        _cityChanged = false;
        Dirty = true;
        return [.. roster, new(null, CityMessage()), .. news.Select(n => new Outgoing(null, new Notice(NoticeKind.World, $"In Tristinwold: {n}")))];
    }
}
