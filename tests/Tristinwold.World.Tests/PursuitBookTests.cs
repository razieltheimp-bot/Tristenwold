using Tristinwold.Gameplay;
using Tristinwold.Deviant;

namespace Tristinwold.World.Tests;

/// <summary>Feats counted as they're done, treasure hunts that lead somewhere, and all of it saved.</summary>
public class PursuitBookTests
{
    [Fact]
    public void Feats_come_once_as_the_counts_rise()
    {
        var book = new PursuitBook();
        Assert.Contains(book.Count("fish"), f => f.Id == "first_catch");
        Assert.Empty(book.Count("fish")); // not twice
        var at99 = book.Count("fish", 97);
        Assert.Empty(at99);
        Assert.Contains(book.Count("fish"), f => f.Id == "angler");
        Assert.Contains(book.Set("best_trade", 10), f => f.Id == "master_of_a_trade");
        Assert.Empty(book.Set("best_trade", 4)); // counts never go down
        Assert.Equal(10, book.Counters["best_trade"]);
        Assert.Equal(5 + 20 + 30, book.Points);
    }

    [Fact]
    public void A_treasure_hunt_leads_from_landmark_to_landmark_on_dry_land()
    {
        var world = WorldGen.Default;
        var hunt = Treasure.NewHunt(world, new Rng(12), 0, 0);
        Assert.InRange(hunt.Clues.Count, 2, 4);
        foreach (var c in hunt.Clues)
        {
            Assert.True(world.Height(c.X, c.Z) > WorldGen.WaterLevel, c.Text);
            Assert.Contains(c.Landmark, c.Text);
            Assert.InRange(Math.Sqrt((c.X - c.LandmarkX) * (c.X - c.LandmarkX) + (c.Z - c.LandmarkZ) * (c.Z - c.LandmarkZ)), 10, 400);
        }
        Assert.Equal(hunt.Clues.Count, hunt.Clues.Select(c => c.Landmark).Distinct().Count());
        Assert.Equal("You're very close.", Treasure.Warmth(5));
        var (crowns, items) = Treasure.Reward(new Rng(1), hunt.Clues.Count);
        Assert.True(crowns > 0 && items.Count >= 2 && items.All(i => Items.Get(i.Id) is not null));
    }

    [Fact]
    public void Pursuits_are_saved_with_the_company()
    {
        var book = new PursuitBook();
        book.Plots["allotment_1"] = new Planting { Seed = "potato_seed", PlantedAt = 50 };
        book.House.Owned = true;
        book.House.Built = 2;
        book.House.Chest["oak_log"] = 7;
        book.Count("fires");
        book.Hunt = Treasure.NewHunt(WorldGen.Default, new Rng(3), 0, 0);
        book.Snares.Add(new Snare { Id = "s1", X = 1, Z = 2, Biome = "Marsh", ReadyAt = 99 });
        var json = CompanySave.Capture(1777, 0, new ExploredMap(), new QuestLog(), pursuits: book).ToJson();
        var back = CompanySave.FromJson(json).Pursuits!;
        Assert.Equal("potato_seed", back.Plots["allotment_1"].Seed);
        Assert.Equal(2, back.House.Built);
        Assert.Equal(7, back.House.Chest["oak_log"]);
        Assert.Contains("first_fire", back.Feats);
        Assert.Equal(book.Hunt.Clues.Count, back.Hunt!.Clues.Count);
        Assert.Equal("Marsh", back.Snares.Single().Biome);
    }
}
