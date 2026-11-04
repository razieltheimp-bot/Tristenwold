namespace Tristinwold.World.Tests;

/// <summary>The king's roads: laid from the gates to the cities about, on dry land, never too steep, and the ground cut to them.</summary>
public class HighwaysTests
{
    static readonly WorldGen W = WorldGen.Default;

    [Fact]
    public void Roads_run_from_the_gates_to_the_cities_a_wagon_can_reach()
    {
        var roads = W.Highways.Roads;
        foreach (var id in new[] { "city:galeholt", "city:cinderhelm", "city:greywater", "city:mirewatch" })
            Assert.Contains(roads, r => r.To == id);
        foreach (var r in roads)
        {
            var s = W.Find(r.To)!;
            Assert.True(r.Length > 30, r.ToName);
            var (x0, z0) = r.Line[0];
            if (r.Spur) Assert.True(roads.Where(o => o != r).Any(o => o.Nearest(x0, z0).Distance < 1), $"the spur to {r.ToName} leaves another road");
            else Assert.True(Highways.Starts.Any(p => Math.Abs(p.X - x0) < 1 && Math.Abs(p.Z - z0) < 1), $"the road to {r.ToName} sets out from a gate");
            Assert.True(s.DistanceTo(r.Line[^1].X, r.Line[^1].Z) < s.Radius + 2, $"the road to {r.ToName} ends at the town's edge");
        }
        // Every smaller place a wagon trades with has its road too, unless it sits on one already.
        var spurs = roads.Count(r => r.Spur);
        var trading = W.SitesNear(0, 0, Highways.Reach).Count(TownMarkets.Trades);
        Assert.True(spurs >= 10, $"{spurs} spurs for {trading} trading places");
        // (The generator also names places out in the ocean ring and on the far shores past it; no road goes to those.)
        foreach (var s in W.SitesNear(0, 0, Highways.Reach).Where(s => TownMarkets.Trades(s) && W.Lie(s.X, s.Z) >= WorldGen.WaterLevel + 1 && Northwood.Sea(s.X, s.Z) < 0.25 && s.X is > -2960 and < 1180 && s.Z < 1780))
            Assert.True(W.Highways.Near(s.X, s.Z) < s.Radius + 45 || W.Highways.To(s.Id) is not null, $"no road reaches {s.Name} ({s.What}) at {s.X:0},{s.Z:0}");
    }

    [Fact]
    public void A_road_is_dry_land_all_the_way_keeps_out_of_the_wood_and_is_never_too_steep()
    {
        foreach (var r in W.Highways.Roads)
            for (var i = 0; i < r.Line.Length; i++)
            {
                var (x, z) = r.Line[i];
                Assert.True(Northwood.Sea(x, z) < 0.5, $"the road to {r.ToName} runs into the sea at {x:0},{z:0}");
                Assert.True(r.Heights[i] >= WorldGen.WaterLevel + 1.4, $"the road to {r.ToName} is under water at {x:0},{z:0}");
                Assert.True(!Northwood.Inside(x, z) || i < 3, $"the road to {r.ToName} goes into the wood at {x:0},{z:0}");
                Assert.True(W.Highways.OnRoad(x, z), $"the road to {r.ToName} isn't on its own line at {x:0},{z:0}");
                if (i > 0) Assert.True(Math.Abs(r.Heights[i] - r.Heights[i - 1]) / (r.Along[i] - r.Along[i - 1]) <= Highways.MaxGrade + 0.001, $"the road to {r.ToName} is too steep at {x:0},{z:0}");
            }
    }

    [Fact]
    public void The_ground_is_cut_to_the_road_and_left_alone_away_from_it()
    {
        var road = W.Highways.To("city:galeholt")!;
        var onIt = 0;
        for (var i = 0; i < road.Line.Length; i += 7)
        {
            var (x, z) = road.Line[i];
            if (W.SettlementsNear(x, z, 200).Any(s => s.DistanceTo(x, z) < s.Radius + (s.IsCity ? 110 : 45) + 5)) continue; // a town's own ground wins there
            Assert.True(Math.Abs(W.Height(x, z) - road.Heights[i]) < 0.75, $"the ground at {x:0},{z:0} is {W.Height(x, z):0.0} where the road is {road.Heights[i]:0.0}");
            onIt++;
        }
        Assert.True(onIt >= 8, $"{onIt} points of the road checked");
        var untouched = 0;
        for (var x = -1800.0; x <= 1200; x += 97)
            for (var z = -2600.0; z <= 900; z += 89)
            {
                if (W.Highways.Near(x, z) < 200) continue;
                Assert.Equal(W.Lie(x, z), W.Highways.Shape(x, z, W.Lie(x, z)));
                untouched++;
            }
        Assert.True(untouched > 50, $"{untouched} points off the roads checked");
    }

    [Fact]
    public void A_carter_has_a_way_by_road_from_the_yard_to_every_place_a_road_reaches()
    {
        foreach (var r in W.Highways.Roads)
        {
            var way = W.Highways.WayTo(r.To);
            Assert.NotNull(way);
            Assert.Equal(Highways.Yard, way!.Line[0]);
            Assert.Equal(r.Line[^1], way.Line[^1]);
            Assert.True(way.Length >= r.Length, $"the way to {r.ToName} is shorter than its own road");
            // Every point of the way is on some road, or on the yard's approach to one.
            for (var i = 0; i < way.Line.Length; i++)
            {
                var (x, z) = way.Line[i];
                var onRoad = W.Highways.Near(x, z) < Highways.Half + 1 || Northwood.OnRoad(x, z) || Math.Sqrt(x * x + z * z) < 125;
                Assert.True(onRoad, $"the way to {r.ToName} leaves the roads at {x:0},{z:0}");
            }
            if (r.Spur) Assert.True(way.Line.Any(p => Math.Abs(p.X - r.Line[0].X) < 1 && Math.Abs(p.Z - r.Line[0].Z) < 1), $"the way to {r.ToName} never reaches its spur's junction");
        }
        Assert.Null(W.Highways.WayTo("nowhere"));
        // The village up the wood's own road, and Druitt at the end of the west road, have ways too.
        var village = W.Highways.WayTo("city:hollowheart")!;
        Assert.Equal(Northwood.VillageGate, village.Line[^1]);
        Assert.All(Northwood.Hearts, t => Assert.All(village.Line, p => Assert.True(Math.Sqrt(Math.Pow(p.X - t.X, 2) + Math.Pow(p.Z - t.Z, 2)) > t.Radius * Northwood.RootSpread, "the way to the village runs into a great tree")));
        Assert.All(village.Line.Skip(1), p => Assert.True(Northwood.OnRoad(p.X, p.Z)));
        var druitt = W.Find("city:druitt")!;
        var toDruitt = W.Highways.WayTo(druitt.Id, druitt.X, druitt.Z)!;
        Assert.Equal(Northwood.WestRoad[^1], toDruitt.Line[^1]);
    }

    [Fact]
    public void A_road_knows_where_along_it_you_are_and_the_point_any_distance_along()
    {
        var road = W.Highways.Roads.OrderByDescending(r => r.Length).First();
        var (x, z) = road.At(road.Length / 2);
        var (off, along, _) = road.Nearest(x + 3, z);
        Assert.True(off <= 3.001 && Math.Abs(along - road.Length / 2) < Highways.Half + 3, $"{off:0.0} m off, {along:0} along of {road.Length:0}");
        Assert.Equal(road.Line[0], road.At(-5));
        Assert.Equal(road.Line[^1], road.At(road.Length + 5));
    }
}
