using Tristinwold.Gameplay;
using Tristinwold.World;
using Xunit;

namespace Tristinwold.World.Tests;

public class HerbTests
{
    [Fact]
    public void Herbs_grow_wild_by_the_kind_of_land_never_in_town_and_grow_back()
    {
        var w = WorldGen.Default;
        var plants = Herbs.Near(w, 0, 0, 1500).ToList();
        Assert.True(plants.Count > 100);
        Assert.All(plants, p => Assert.True(Math.Sqrt(p.X * p.X + p.Z * p.Z) > 110 && Items.Get(p.HerbId)?.Kind == ItemKind.Herb, p.Id));
        Assert.True(plants.Select(p => p.HerbId).Distinct().Count() >= 4);
        Assert.Equal(plants.Select(p => p.Id), Herbs.Near(w, 0, 0, 1500).Select(p => p.Id)); // the same for everyone
        var book = new HerbBook();
        var p = plants[0];
        Assert.True(book.Ready(p, 100));
        book.Picked(p, 100);
        Assert.False(book.Ready(p, 100 + Herbs.RegrowSeconds - 1));
        Assert.True(book.Ready(p, 100 + Herbs.RegrowSeconds));
        Assert.True(Herbs.Yield(10, 0.01) > Herbs.Yield(1, 0.01));
    }

    [Fact]
    public void The_pack_and_the_herbs_are_saved_with_the_company()
    {
        var pack = new Pack();
        pack.Add("wolf_pelt", 3);
        pack.Add("iron_helm");
        pack.Equip("iron_helm");
        pack.Practise(Job.Herbalism, 50);
        var herbs = new HerbBook();
        herbs.PickedAt["herb_1_2_0"] = 42;
        var save = CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), new QuestLog(), pack: pack, herbs: herbs).ToJson());
        Assert.Equal(3, save.Pack!.Count("wolf_pelt"));
        Assert.Equal("iron_helm", save.Pack.Worn[Slot.Head]);
        Assert.Equal(50, save.Pack.JobXp[Job.Herbalism]);
        Assert.Equal(42, save.Herbs!.PickedAt["herb_1_2_0"]);
    }

    [Fact]
    public void Where_the_hero_stood_is_saved_and_old_saves_have_no_spot()
    {
        var save = CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), new QuestLog(), where: new Spot(-300.5f, 12.25f, 80f, 1.5f)).ToJson());
        Assert.Equal(new Spot(-300.5f, 12.25f, 80f, 1.5f), save.Where);
        Assert.Null(CompanySave.FromJson("""{"seed":1,"crowns":5}""").Where);
    }
}
