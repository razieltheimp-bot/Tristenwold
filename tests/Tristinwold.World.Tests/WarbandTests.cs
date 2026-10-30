using System.Text.Json;
using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>Barbarian clans that raid the frontier's small places, and the few guards those places have.</summary>
public class WarbandTests
{
    static readonly WorldGen W = WorldGen.Default;

    [Fact]
    public void Clans_roam_the_frontier_and_go_for_the_small_places_never_near_the_walls()
    {
        var clans = Warband.Near(0, 0, 6000).ToList();
        Assert.True(clans.Count >= 5, $"only {clans.Count} clans");
        Assert.All(clans, c => Assert.True(Math.Sqrt(c.X * c.X + c.Z * c.Z) > Warband.Beyond));
        Assert.All(clans, c => Assert.InRange(c.Raiders, 4, 7));
        Assert.All(clans, c => Assert.All(c.Targets(W), s => Assert.True(Warband.Raidable(s) && Math.Sqrt(s.X * s.X + s.Z * s.Z) > Warband.Beyond)));
        Assert.Equal(clans.Select(c => c.Name), Warband.Near(0, 0, 6000).Select(c => c.Name)); // the same clans for everyone
        Assert.True(clans.Select(c => c.Name).Distinct().Count() > 2);
    }

    [Fact]
    public void A_clan_rides_from_place_to_place_and_raids_each_in_turn()
    {
        var clan = Warband.Near(0, 0, 6000).First(c => c.Targets(W).Count >= 2);
        var raids = new HashSet<string>();
        for (var round = 0; round < 12; round++)
        {
            var onRoad = clan.Where(W, round * Warband.Round + 10);
            Assert.Null(onRoad.Raiding);
            var raiding = clan.Where(W, (round + 1) * Warband.Round - 10);
            Assert.NotNull(raiding.Raiding);
            Assert.Equal(round, raiding.Round);
            Assert.Equal((raiding.Raiding!.X, raiding.Raiding.Z), (raiding.X, raiding.Z));
            raids.Add(raiding.Raiding.Id);
        }
        Assert.True(raids.Count >= 2, "it only ever raids one place");
    }

    [Fact]
    public void Small_places_have_a_couple_of_guards_and_no_more()
    {
        var places = W.SettlementsNear(0, 0, 6000).ToList();
        Assert.All(places.Where(s => s.Kind == SettlementKind.Town), s => Assert.Equal(3, Warband.Guards(s)));
        Assert.All(places.Where(s => s.Kind is SettlementKind.Farmstead or SettlementKind.Inn), s => Assert.Equal(1, Warband.Guards(s)));
        Assert.All(places.Where(s => s.Kind is SettlementKind.BanditCamp or SettlementKind.Den or SettlementKind.Barrow), s => Assert.Equal(0, Warband.Guards(s)));
        Assert.All(places.Where(s => s.Kind != SettlementKind.City), s => Assert.True(Warband.Guards(s) <= 3));
    }

    [Fact]
    public void A_raid_nobody_stops_plunders_the_place_once_and_a_broken_clan_rests()
    {
        var clan = Warband.Near(0, 0, 6000).First(c => c.Targets(W).Count > 0);
        var place = clan.Where(W, Warband.Round - 10).Raiding!;
        var market = TownMarkets.For(place);
        var before = market.Stock.Values.Sum();
        var taken = market.Plundered(1);
        Assert.True(taken > 0 && market.Stock.Values.Sum() < before * 0.6);
        Assert.Equal(1, market.Raided);

        var record = new RaidRecord();
        Assert.True(record.Reckon(clan, 0));
        Assert.False(record.Reckon(clan, 0)); // only once
        Assert.False(record.IsBroken(clan, 100));
        record.Break(clan, 1, 100);
        Assert.True(record.IsBroken(clan, 100 + RaidRecord.BrokenSeconds - 1));
        Assert.False(record.IsBroken(clan, 100 + RaidRecord.BrokenSeconds + 1));
        Assert.False(record.Reckon(clan, 1)); // the raid it broke is spared
        Assert.Equal(1, record.Defended);
        var copy = JsonSerializer.Deserialize<RaidRecord>(JsonSerializer.Serialize(record))!;
        Assert.True(copy.IsBroken(clan, 200));
    }
}
