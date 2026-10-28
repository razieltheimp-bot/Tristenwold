namespace Tristinwold.World.Tests;

/// <summary>Saves from before the coin, the third pool and a few creatures were renamed still load, with nothing lost.</summary>
public class SaveUpgradeTests
{
    [Fact]
    public void Old_saves_keep_their_coin_their_things_and_their_deviants()
    {
        var old = """{"seed":1777,"septims":321,"pack":{"items":{"skeever_tail":2,"magicka_tonic":1}},"city":{"workSeptims":40}}""";
        var save = CompanySave.FromJson(old);
        Assert.Equal(321, save.Crowns);
        Assert.Equal(2, save.Pack!.Count("rat_tail"));
        Assert.Equal(1, save.Pack.Count("mana_tonic"));
        Assert.Equal(40, save.City!.WorkCrowns);
        // JSON kept inside JSON (the server keeps its deviant world that way) is upgraded too.
        var nested = Tristinwold.Deviant.SaveUpgrade.Upgrade("""{"deviantWorldJson":"{\"stat\":\"Magicka\"}"}""");
        Assert.Contains("\\\"Mana\\\"", nested);
    }
}
