using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>Working at a forge: what comes off it is one of a kind (see <see cref="Forging"/>), and every town's forge has its own hand in it.</summary>
public partial class Main
{
    /// <summary>The forge the hero's working at: the one in the town they're standing in, or a camp's if they're in the wilds.</summary>
    ForgeHouse ForgeHere()
    {
        var place = TownHere(_player.GlobalPosition) ?? "wilds";
        return Forging.ForgeAt(place, _world.Find(place)?.Name);
    }

    /// <summary>Turns what a recipe just made into a piece of its own.</summary>
    Item ForgeUnique(Recipe r, Job job)
    {
        var forge = ForgeHere();
        var serial = Forging.Serial(r.Makes, BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0), forge.Id, forge.Name, _player.Pack.Level(job), _player.Hero.Level, _player.Company);
        _player.Pack.Take(r.Makes);
        _player.Pack.Add(serial);
        return Items.Get(serial)!;
    }

    /// <summary>Any town's smith will let a hero at the fire, not only Hulda.</summary>
    IEnumerable<(string, Action)> ForgeTalk(Person person)
    {
        if (Shops.All.Any(s => s.Id == person.Npc.Id) || !person.Npc.Occupation.Contains("smith", StringComparison.OrdinalIgnoreCase)) yield break;
        yield return ("Can I use your forge?", () => { if (Serves(person)) OfferForge(person, "Mind the tongs."); });
    }
}
