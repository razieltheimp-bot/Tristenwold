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
        Assert.Contains(r.Boss, roamer.Title);
        Assert.Equal(Roaming.NameFor(42), roamer.Name); // the same seed, the same name
        // The lair stands empty until the timer runs out; then a new one is there.
        Assert.False(book.Ready(r, 1000 + r.CooldownSeconds - 1));
        Assert.True(book.Ready(r, 1000 + r.CooldownSeconds));
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
            Assert.True(Math.Sqrt(x * x + z * z) < Roaming.FarthestOut + 50);
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
}
