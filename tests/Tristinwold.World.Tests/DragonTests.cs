using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.World.Tests;

/// <summary>Ancient dragons, their lands, their kin, and their wars.</summary>
public class DragonTests
{
    static readonly WorldGen World = WorldGen.Default;
    static readonly List<Ancient> Many = Dragons.Near(World, 0, 0, 40000).ToList();

    [Fact]
    public void Ancients_live_far_from_home_on_dry_land_and_come_in_every_colour()
    {
        Assert.True(Many.Count > 20, $"only {Many.Count} ancients");
        Assert.All(Many, a =>
        {
            Assert.True(a.DistanceTo(0, 0) >= Dragons.NearestToHome, $"{a.FullName} lairs too close to Tristinwold");
            Assert.True(World.Height(a.X, a.Z) > WorldGen.WaterLevel, $"{a.FullName} lairs in the water");
            Assert.InRange(a.Territory, 1800, 2700);
        });
        Assert.Equal(Enum.GetValues<DragonColour>().Length, Many.Select(a => a.Colour).Distinct().Count());
        Assert.Equal(Many, Dragons.Near(World, 0, 0, 40000).ToList()); // the same every time
    }

    [Fact]
    public void Every_colour_is_a_dragon_kind_and_metallics_are_wary()
    {
        foreach (var c in Enum.GetValues<DragonColour>())
        {
            var kind = Bestiary.Get(Dragons.KindFor(c));
            Assert.Equal(Dragons.KindFor(c), kind.Id);
            Assert.Equal(Dragons.IsMetallic(c), kind.Wary);
            Assert.Equal(c, Dragons.ColourOf(kind.Id));
        }
        Assert.True(Dragons.Scale(DragonSize.Ancient).Height > 3 * Dragons.Scale(DragonSize.Normal).Height);
    }

    [Fact]
    public void Dragons_of_the_lords_colour_roam_its_land()
    {
        var lord = Many.First();
        var flights = Wildlife.Near(World, lord.X, lord.Z, lord.Territory * 0.7).Where(h => h.KindId.StartsWith("dragon_")).ToList();
        Assert.NotEmpty(flights);
        // Its kin follow it; a few have fallen to the Rift and follow no one.
        Assert.All(flights, f => Assert.Equal(Dragons.IsCorrupted(f.KindId) && !lord.Corrupted ? Dragons.Rift : Dragons.TerritoryAt(World, f.X, f.Z)!.Id, f.Faction));
        var lair = Dragons.Lair(lord).ToList();
        Assert.Contains(lair, h => h.Size == DragonSize.Ancient && h.Name == lord.FullName && h.KindId == lord.KindId);
    }

    [Fact]
    public void Ancients_go_to_war_some_seasons_and_lead_their_dragons_at_the_border()
    {
        var start = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
        var wars = Enumerable.Range(0, 40).SelectMany(i => Dragons.WarsNear(World, start.AddHours(i * Dragons.SeasonHours), 0, -12000, 15000)).ToList();
        Assert.NotEmpty(wars);
        Assert.Contains(wars, w => w.A.Metallic != w.B.Metallic);
        foreach (var w in wars.Take(10))
        {
            Assert.True(w.A.DistanceTo(w.B.X, w.B.Z) <= Dragons.WarReach);
            // The front lies between the two lairs.
            Assert.True(w.A.DistanceTo(w.FrontX, w.FrontZ) < w.A.DistanceTo(w.B.X, w.B.Z));
            var bands = Dragons.Warbands(w).ToList();
            Assert.Contains(bands, b => b.Faction == w.A.Id && b.Foe == w.B.Id);
            Assert.Contains(bands, b => b.Faction == w.B.Id && b.Foe == w.A.Id);
        }
        // Every company sees the same wars at the same time.
        var t = start.AddHours(7);
        Assert.Equal(Dragons.WarsNear(World, t, 0, -12000, 15000).Select(w => w.Id), Dragons.WarsNear(World, t, 0, -12000, 15000).Select(w => w.Id));
    }
}
