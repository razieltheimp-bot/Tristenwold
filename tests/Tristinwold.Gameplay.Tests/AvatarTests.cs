using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

/// <summary>A hero's look, apart from their class.</summary>
public class AvatarTests
{
    [Fact]
    public void Every_class_starts_with_its_own_figure()
    {
        Assert.Equal(new Avatar("Knight", "Knight", "Knight", "Knight", "Knight_Helmet", "Natural", "Natural", "Average"), Avatar.For("Paladin"));
        Assert.Equal("Mage_Hat", Avatar.For("Wizard").Hat);
        Assert.All(Classes.All, c => Assert.Contains(Avatar.For(c.Id).Body, Avatar.Figures));
    }

    [Fact]
    public void A_look_survives_being_saved_and_read_back()
    {
        var mixed = new Avatar("Skeleton_Mage", "Knight", "Barbarian", "Rogue", "Mage_Hat", "Crimson", "Tan", "Tall");
        Assert.Equal(mixed, Avatar.Parse(mixed.Encode()));
        var rng = new Random(4);
        for (var i = 0; i < 50; i++) { var a = Avatar.Random(rng); Assert.Equal(a, Avatar.Parse(a.Encode())); }
    }

    [Fact]
    public void A_bad_or_missing_look_falls_back_to_the_class()
    {
        Assert.Null(Avatar.Parse(null));
        Assert.Null(Avatar.Parse(""));
        var odd = Avatar.Parse("head=Dragon;body=Mage;nonsense;height=Giant", "Barbarian")!;
        Assert.Equal("Barbarian", odd.Head);
        Assert.Equal("Mage", odd.Body);
        Assert.Equal("Average", odd.Height);
        Assert.Equal(0.8f, (Avatar.For("Mage") with { Height = "Tall" }).Scale);
        Assert.Equal("#ffffff", Avatar.For("Mage").OutfitHex);
    }

    [Fact]
    public void Race_dragon_blood_and_the_extras_are_kept_with_the_look()
    {
        var a = Avatar.For("Barbarian") with { Race = "dragonkind", Dragon = "Gold", Half = "dwarf", Build = "Broad", Beard = "Braided", Hair = "Copper", Horns = "Ram" };
        var back = Avatar.Parse(a.Encode())!;
        Assert.Equal(a, back);
        Assert.Equal("Gold half-dragon dwarf", back.Kind);
        Assert.Equal("Red dragonkind", (Avatar.For("Mage") with { Race = "dragonkind" }).Kind);
        Assert.True((Avatar.For("Mage") with { Race = "dwarf" }).Scale < Avatar.For("Mage").Scale);   // dwarves are short
        Assert.True((Avatar.For("Mage") with { Race = "dwarf", Build = "Broad" }).Width > 1.2f);      // and broad
        Assert.Equal("human", Avatar.Parse("head=Mage")!.Race);      // looks saved before races were human
        Assert.Null(Avatar.Parse("head=Mage")!.Blood);
        Assert.Equal(Races.Get("orc").Skin, (Avatar.For("Mage") with { Race = "orc" }).SkinHex); // green unless picked otherwise
    }

    [Fact]
    public void Every_people_has_a_gift_and_a_cost_and_dragonkind_mixes_half_with_any()
    {
        Assert.Equal(["human", "orc", "goblin", "elf", "dwarf", "half_giant", "dragonkind"], Races.All.Select(r => r.Id));
        Assert.All(Races.All, r => { Assert.NotEmpty(r.Good); Assert.NotEmpty(r.Bad); });
        Assert.True(Races.Get("half_giant").Size > Races.Get("human").Size && Races.Get("goblin").Size < Races.Get("dwarf").Size);

        var orc = new Combatant("Grak", 100);
        var (health, mana, _) = Races.Apply(orc, Races.Get("orc"), null);
        Assert.True(health > 1 && mana < 1 && orc.DamageMult > 1 && orc.SpellPower < 1);

        // A half-dragon elf: half the elf's magic, half the dragon's toughness, the elf's ears.
        var half = (Avatar.For("Wizard") with { Race = "dragonkind", Dragon = "White", Half = "elf" });
        Assert.Equal("elf", half.People.Ears);
        Assert.True(half.People.Mana > 1 && half.People.Mana < Races.Get("elf").Mana);
        Assert.True(half.People.Health > Races.Get("elf").Health && half.People.Health < Races.Get("dragonkind").Health);

        var c = new Combatant("Ysolde", 100);
        Races.Apply(c, half.People, half.Blood, half.PureDragon);
        Assert.Equal(50 + 15 / 2.0, c.Resist[Tristinwold.Nemesis.DamageKind.Frost]); // white dragons breathe frost; half an elf's resistance too
        Assert.True(c.Resist[Tristinwold.Nemesis.DamageKind.Fire] < 0);              // and can't stand fire
        Races.Apply(c, half.People, half.Blood, half.PureDragon);                    // again, as when gear changes
        Assert.Equal(57.5, c.Resist[Tristinwold.Nemesis.DamageKind.Frost]);          // not piled up

        var pure = new Combatant("Vyr", 100);
        Races.Apply(pure, Races.Get("dragonkind"), Races.Dragon("Red"), pure: true);
        Assert.Equal(75, pure.Resist[Tristinwold.Nemesis.DamageKind.Fire]);
        Assert.True(Races.Prices(Races.Get("human")) < 1 && Races.Prices(Races.Get("dragonkind")) > 1.1);
    }
}
