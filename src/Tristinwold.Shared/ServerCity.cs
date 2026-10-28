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
        City.Apply(t.Action);
        _cityChanged = true;
        // A gift that finishes the works is news for everybody else (the giver's game has already said so).
        return [.. City.News.Skip(news).Select(n => new Outgoing(null, new Notice(NoticeKind.World, $"In Tristinwold: {n}"), Except: player))];
    }

    /// <summary>The city's clock, and its news; and the city as it stands to every game, when it's changed.</summary>
    IReadOnlyList<Outgoing> TickCity()
    {
        var now = _clock();
        // The city only lives while someone's in it, and a stalled server doesn't run the days on in a rush.
        if (_cityClock != DateTime.MinValue && _online.Count > 0) State.CitySeconds += Math.Clamp((now - _cityClock).TotalSeconds, 0, 60);
        _cityClock = now;
        var news = City.Advance(State.CitySeconds);
        if (news.Count == 0 && !_cityChanged) return [];
        _cityChanged = false;
        Dirty = true;
        return [new(null, CityMessage()), .. news.Select(n => new Outgoing(null, new Notice(NoticeKind.World, $"In Tristinwold: {n}")))];
    }
}
