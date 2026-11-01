using System.Text.Json;
using Tristinwold.Deviant;

namespace Tristinwold.World.Tests;

/// <summary>The main story: the hero is an Immortal, made under the Needle, brought back by their ring.</summary>
public class ImmortalTests
{
    static Settlement Place(SettlementKind kind, bool hostile = false) =>
        new WorldGen(seed: 7).SettlementsNear(0, 0, 40000).First(s => s.Kind == kind && s.Hostile == hostile);

    [Fact]
    public void A_hero_can_make_a_home_of_a_town_but_not_of_somewhere_that_wants_them_dead()
    {
        var (saga, town) = (new ImmortalSaga(), Place(SettlementKind.Town));
        Assert.Null(saga.Home);
        Assert.True(saga.Settle(town));
        Assert.False(saga.Settle(town));
        Assert.Equal((town.Id, town.Name), (saga.Home!.Id, saga.Home.Name));
        var copy = System.Text.Json.JsonSerializer.Deserialize<ImmortalSaga>(System.Text.Json.JsonSerializer.Serialize(saga))!;
        Assert.Equal(saga.Home, copy.Home);
    }

    [Fact]
    public void A_sailor_wakes_on_the_quay_nearest_home()
    {
        var saga = new ImmortalSaga();
        var docks = new List<(double X, double Z)> { (97, -36), (-900, -262), (640, -600) };
        Assert.Equal(0, saga.NearestDock(docks, (78, -6)));
        saga.Home = new Hometown("a", "Eastward", 700, -500);
        Assert.Equal(2, saga.NearestDock(docks, (78, -6)));
        saga.Home = new Hometown("b", "Westward", -1200, 0);
        Assert.Equal(1, saga.NearestDock(docks, (78, -6)));
    }

    [Fact]
    public void The_road_runs_from_waking_to_warden_of_the_rings()
    {
        var w = WorldGen.Default;
        var s = new ImmortalSaga();
        Assert.Null(s.Objective);
        Assert.True(s.Begin());
        Assert.False(s.Begin());
        Assert.Contains("Mother Corva", s.Objective);

        // Talking to the wrong person doesn't move it on.
        Assert.Null(s.Talked(ImmortalSaga.Ilse));
        Assert.Contains("made", s.Talked(ImmortalSaga.Corva));
        Assert.Equal(Chapter.Archive, s.Chapter);
        Assert.Contains("Nine woke", s.Talked(ImmortalSaga.Ilse));
        Assert.Equal(Chapter.Proving, s.Chapter);

        // Prove the ring holds: fall, and come back.
        var rng = new Rng(1);
        Assert.Contains("It holds", s.Returned(rng));
        Assert.Equal(Chapter.Breach, s.Chapter);
        Assert.Equal(1, s.Returns);

        // Only the Host counts at the breach.
        Assert.False(s.Slain("wolf"));
        for (var i = 0; i < ImmortalSaga.BreachToClose - 1; i++) Assert.False(s.Slain(i % 2 == 0 ? "imp" : "dire_imp"));
        Assert.Contains($"{ImmortalSaga.BreachToClose - 1}/{ImmortalSaga.BreachToClose}", s.Objective);
        Assert.True(s.Slain("imp"));
        Assert.Equal(Chapter.Dragonfire, s.Chapter);
        Assert.NotNull(s.Moved);
        Assert.NotNull(s.Goal(w, (0, 0), (0, 0), 0, 0)); // there's a dragon the Rift took, somewhere out there

        // An ordinary red dragon isn't one the Rift took.
        Assert.False(s.Slain("dragon_red"));
        Assert.True(s.Slain("dragon_red_corrupt"));
        Assert.Equal(Chapter.Warden, s.Chapter);
        Assert.Contains(ImmortalSaga.Title, s.Talked(ImmortalSaga.Corva));
        Assert.Equal(Chapter.Done, s.Chapter);
        Assert.Null(s.Objective);
    }

    [Fact]
    public void Mother_Corva_keeps_count_of_every_return()
    {
        var s = new ImmortalSaga { Chapter = Chapter.Done };
        var rng = new Rng(4);
        for (var i = 0; i < 12; i++) Assert.False(string.IsNullOrWhiteSpace(s.Returned(rng)));
        Assert.Equal(12, s.Returns);
        Assert.DoesNotContain("{n}", s.Returned(rng));
        var copy = JsonSerializer.Deserialize<ImmortalSaga>(JsonSerializer.Serialize(s))!;
        Assert.Equal(13, copy.Returns);
        Assert.Equal(Chapter.Done, copy.Chapter);
    }

    [Fact]
    public void The_breach_opens_on_dry_ground_beyond_the_walls_clear_of_villages_and_lairs()
    {
        var w = WorldGen.Default;
        var (x, z) = ImmortalSaga.BreachSite(w);
        var d = Math.Sqrt(x * x + z * z);
        Assert.InRange(d, 400, 1400);
        Assert.True(w.Height(x, z) > WorldGen.WaterLevel + 1.9);
        Assert.DoesNotContain(w.SettlementsNear(x, z, 300), s => s.DistanceTo(x, z) < s.Radius + 60);
        Assert.Equal((x, z), ImmortalSaga.BreachSite(w));
    }
}
