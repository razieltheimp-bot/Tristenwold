using System.Text.Json;
using Tristinwold.Gameplay;
using Tristinwold.World;
using Xunit;

namespace Tristinwold.World.Tests;

public class LairTests
{
    [Fact]
    public void Every_lair_is_on_dry_open_ground_well_apart_from_towns_and_each_other()
    {
        var w = WorldGen.Default;
        foreach (var r in Raids.All)
        {
            Assert.InRange(r.DistanceTo(0, 0), 200, 1200);
            for (var i = 0; i < 8; i++)
            {
                var (x, z) = (r.X + 20 * Math.Cos(i * Math.PI / 4), r.Z + 20 * Math.Sin(i * Math.PI / 4));
                Assert.True(w.Height(x, z) > WorldGen.WaterLevel + 0.5, $"{r.Name} is wet at {x:0},{z:0}");
            }
            Assert.DoesNotContain(w.SettlementsNear(r.X, r.Z, 300), s => s.DistanceTo(r.X, r.Z) < s.Radius + 40);
            Assert.All(Raids.All.Where(o => o != r), o => Assert.True(o.DistanceTo(r.X, r.Z) > 150, $"{r.Name} crowds {o.Name}"));
            Assert.Equal(r.BossKind, Bestiary.Get(r.BossKind).Id);
            Assert.Equal(r.ThrallKind, Bestiary.Get(r.ThrallKind).Id);
            Assert.NotEmpty(r.Moves);
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.True(r.MinLevel <= r.MaxLevel);
        }
        Assert.Equal(Raids.All.Count, Raids.All.Select(r => r.Id).Distinct().Count());
        Assert.Contains(Raids.All, r => r.Site == LairSite.Cave);
        Assert.Contains(Raids.All, r => r.Site == LairSite.Ruin);
        Assert.Contains(Raids.All, r => r.Site == LairSite.Dungeon);
    }

    [Fact]
    public void The_further_out_the_lair_the_stronger_the_boss_and_the_richer_the_hoard()
    {
        var byDistance = Raids.All.OrderBy(r => r.DistanceTo(0, 0)).ToList();
        foreach (var r in Raids.All.Where(r => r != Raids.AshenCrypt))
        {
            Assert.True(Raids.Boss(r, r.MaxLevel).Health > Raids.Boss(Raids.AshenCrypt, 6).Health, r.Name);
            // A hero below the lair's level still meets the boss at its level: these are stronger mini bosses.
            Assert.Equal(Raids.Boss(r, 1), Raids.Boss(r, r.MinLevel));
            Assert.True(Raids.Daunting(r, 1) || r.MinLevel <= 2);
        }
        Assert.True(Raids.Reward(Raids.StormEyrie, 15, false).Xp > Raids.Reward(Raids.GoblinWarren, 6, false).Xp);
        Assert.Contains(Raids.GoblinWarren, Raids.For(5, 0, 0));
        Assert.DoesNotContain(Raids.StormEyrie, Raids.For(5, 0, 0));
    }

    [Fact]
    public void A_boss_that_beats_the_company_takes_a_name_and_roams_and_a_new_nameless_one_moves_in()
    {
        var r = Raids.GoblinWarren;
        var book = new RaidBook();
        var roamer = book.Escaped(r, 1000, 42);

        Assert.False(string.IsNullOrWhiteSpace(roamer.Name));
        Assert.DoesNotContain(r.Boss, roamer.Title, StringComparison.OrdinalIgnoreCase); // it's a creature of its own now
        Assert.StartsWith(roamer.Name, roamer.Title);
        Assert.Equal("Dorval's Minion", Roaming.MinionOf("Dorval the Grinning, the Ravager"));
        Assert.Equal(Roaming.NameFor(42), roamer.Name); // the same seed, the same name
        // A moment later a new one has moved in (long before the lair's usual cooldown).
        Assert.False(book.Ready(r, 1000 + RaidBook.MoveInSeconds - 1));
        Assert.True(book.Ready(r, 1000 + RaidBook.MoveInSeconds));
        Assert.True(RaidBook.MoveInSeconds < r.CooldownSeconds / 4);
        Assert.Equal(0, book.ClearsOf(r));

        // Powered up past its lair, and more with every company it beats.
        Assert.True(Roaming.Numbers(roamer, 6).Health > Raids.Boss(r, 6).Health);
        Assert.True(Roaming.Numbers(roamer, 6).Damage > Raids.Boss(r, 6).Damage);
        var before = Roaming.Numbers(roamer, 6);
        roamer.Wins++;
        Assert.True(Roaming.Numbers(roamer, 6).Health > before.Health);
        Assert.True(Roaming.Bounty(roamer, 6).Xp > Raids.Reward(r, 6, firstTime: true).Xp);

        book.Slain(roamer);
        Assert.Empty(book.Roamers);
    }

    [Fact]
    public void A_roamer_wanders_the_wilds_from_its_lair_never_into_towns_or_water()
    {
        var w = WorldGen.Default;
        var r = Raids.HollowBastion;
        var roamer = new RaidBook().Escaped(r, 0, 7);
        Assert.Equal((r.X, r.Z), roamer.Where(w, 0));
        var travelled = 0.0;
        var last = roamer.Where(w, 0);
        for (var t = 60.0; t < 4 * 3600; t += 60)
        {
            var (x, z) = roamer.Where(w, t);
            travelled += Math.Sqrt((x - last.X) * (x - last.X) + (z - last.Z) * (z - last.Z));
            last = (x, z);
            Assert.True(Math.Sqrt((x - r.X) * (x - r.X) + (z - r.Z) * (z - r.Z)) < Roaming.FarthestOut + 50);
            Assert.True(Math.Sqrt(x * x + z * z) > Roaming.NearestToCity - 50);
        }
        Assert.True(travelled > 3000, $"it only went {travelled:0} m");
        Assert.True(roamer.Where(w, 10_000) != (r.X, r.Z));
        // Every company sees it in the same place, and a fresh copy of it (from a save) agrees.
        var copy = JsonSerializer.Deserialize<Roamer>(JsonSerializer.Serialize(roamer))!;
        Assert.Equal(roamer.Where(w, 9000), copy.Where(w, 9000));
        Assert.Equal(roamer.Title, copy.Title);
        Assert.DoesNotContain("Story", JsonSerializer.Serialize(roamer)); // its lair isn't copied into every save
        var book = new RaidBook();
        book.Roamers.Add(roamer);
        var save = CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), new QuestLog(), raids: book).ToJson());
        Assert.Equal(roamer.Name, save.Raids!.Roamers.Single().Name);
    }

    [Fact]
    public void Past_the_nine_the_far_country_has_lairs_for_ever_and_harder_the_further_out()
    {
        var w = WorldGen.Default;
        Assert.Empty(FarLairs.Near(w, 0, 0, FarLairs.Beyond - 100)); // near home, only the nine
        foreach (var (x, z) in new[] { (6000.0, 0.0), (-20_000.0, 15_000.0), (0.0, -60_000.0) })
        {
            var found = FarLairs.Near(w, x, z, 3000).ToList();
            Assert.NotEmpty(found);
            foreach (var r in found)
            {
                Assert.True(w.Height(r.X, r.Z) > WorldGen.WaterLevel + 1.4, $"{r.Name} is in the water");
                Assert.DoesNotContain(w.SettlementsNear(r.X, r.Z, 300), s => s.DistanceTo(r.X, r.Z) < s.Radius + 80);
                Assert.StartsWith("the ", r.Name);
                Assert.Contains(r.Name, r.Story);
                // The same lair every time it's asked for, by its id too (a roamer from it, saved, finds its way home).
                Assert.Equal(r, Raids.Get(r.Id));
                Assert.Equal(r.Id, FarLairs.Near(w, r.X, r.Z, 1).Single().Id);
            }
        }
        // Further out, harder: a hero who's outgrown everything near home has somewhere to go.
        var near = FarLairs.Near(w, 0, -3500, 2000).Min(r => r.MinLevel); // north: the ocean lies east
        var far = FarLairs.Near(w, 40_000, 0, 3000).Min(r => r.MinLevel);
        Assert.True(far > near + 20, $"{near} near, {far} far");
        Assert.True(FarLairs.Near(w, 40_000, 0, 3000).All(r => r.MaxLevel > Raids.All.Max(o => o.MaxLevel)));
    }

    [Fact]
    public void A_roamer_from_a_far_lair_wanders_out_there_not_back_to_town()
    {
        var w = WorldGen.Default;
        var lair = FarLairs.Near(w, 9000, 4000, 3000).First();
        var roamer = new RaidBook().Escaped(lair, 0, 11);
        Assert.Equal(lair, roamer.Lair);
        var moved = false;
        for (var t = 600.0; t < 6 * 3600; t += 600)
        {
            var (x, z) = roamer.Where(w, t);
            moved |= (x, z) != (lair.X, lair.Z);
            Assert.True(Math.Sqrt((x - lair.X) * (x - lair.X) + (z - lair.Z) * (z - lair.Z)) < Roaming.FarthestOut + 50);
        }
        Assert.True(moved);
    }
}
