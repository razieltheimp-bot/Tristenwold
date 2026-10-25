using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// What the company carries, finds, makes and trades: loot from what it kills (pelts from wolves, scales and hearts
/// from dragons, trophies and gear from the lairs' hoards); herbs picked in the wild; the pack (I), where gear is
/// put on and potions drunk; the market on the Gallows Perch road: Hollis's provisions, Hulda Ironhand's smithy and
/// forge, Old Saffron's remedies and her bench; and the trades, herbalism and smithing, that get better with use.
/// </summary>
public partial class Main
{
    HerbBook _herbs = new();
    HerbPatch _herbPatch = null!;
    readonly Tristinwold.Nemesis.Rng _lootRng = new((ulong)DateTime.UtcNow.Ticks);

    public Pack PackOf => _player.Pack;
    public HerbBook HerbLog => _herbs;

    // ---- loot -----------------------------------------------------------------------------------

    /// <summary>A kill's spoils into the pack.</summary>
    void Spoils(Enemy victim)
    {
        var drop = Loot.Roll(victim.Kind.Id, _lootRng, victim.Young);
        if (drop.Items.Count == 0 && drop.Septims == 0) return;
        Take(drop.Items);
        _player.Septims += drop.Septims;
        var what = Loot.Describe(drop.Items) + (drop.Septims > 0 ? $"{(drop.Items.Count > 0 ? ", " : "")}{drop.Septims} septims" : "");
        _hud.Log($"Looted: {what}");
        if (victim.IsDragon) _hud.Toast($"The dragon's hoard and body: {what}.", 9);
    }

    /// <summary>Into the pack, all of it.</summary>
    public void Take(IEnumerable<(string Id, int N)> items)
    {
        foreach (var (id, n) in items) _player.Pack.Add(id, n);
    }

    // ---- herbs ----------------------------------------------------------------------------------

    /// <summary>Picks the plant within reach, if there is one (E does this; the self-test calls it).</summary>
    public bool PickHere()
    {
        if (_herbPatch.Within(_player.GlobalPosition) is not { } plant) return false;
        Pick(plant);
        return true;
    }

    public HerbPatch HerbPatchNode => _herbPatch;

    /// <summary>Picks a wild plant: herbs into the pack (more as you get better at it), and it grows back in a while.</summary>
    void Pick(Plant plant)
    {
        var pack = _player.Pack;
        var n = Herbs.Yield(pack.Level(Job.Herbalism), _lootRng.NextDouble());
        pack.Add(plant.HerbId, n);
        _herbs.Picked(plant, _guilds.PlaySeconds);
        _herbPatch.Picked(plant);
        var up = pack.Practise(Job.Herbalism, 5);
        _hud.Toast($"Picked {Items.Count(plant.HerbId, n).ToLowerInvariant()}.{(up is { } level ? $" Herbalism rises to {level}!" : "")}", 3);
    }

    // ---- the pack (I) ---------------------------------------------------------------------------

    public void ShowPack()
    {
        var pack = _player.Pack;
        var worn = string.Join(", ", new[] { Slot.Weapon, Slot.Offhand, Slot.Head, Slot.Body, Slot.Cloak }
            .Select(s => pack.WornIn(s) is { } i ? i.Name : null).Where(n => n is not null));
        var text = $"{_player.Septims} septims, {_player.Meat} meat. Wearing: {(worn.Length > 0 ? worn : "nothing extra")} " +
                   $"(+{pack.Armor:0} armour, +{pack.Damage:P0} damage).\n" +
                   $"Herbalism {pack.Level(Job.Herbalism)}, smithing {pack.Level(Job.Smithing)}, beast taming {pack.Level(Job.Taming)}.{(_player.Buffs is { } b ? $" Now: {b}." : "")}\n" +
                   (pack.Items.Count == 0 ? "Your pack is empty." : string.Join(", ", pack.Contents.Select(c => Items.Count(c.Item.Id, c.N))));
        var options = new List<(string, Action)>();
        foreach (var slot in new[] { Slot.Weapon, Slot.Offhand, Slot.Head, Slot.Body, Slot.Cloak })
            if (pack.WornIn(slot) is { } on) options.Add(($"Take off {on.Name}", () => { pack.Unequip(slot); Redress(); ShowPack(); }));
        foreach (var (item, n) in pack.Contents)
        {
            if (item.Wearable) options.Add(($"Wear {item.Name} ({item.Text})", () => { pack.Equip(item.Id); Redress(); ShowPack(); }));
            else if (item.Usable) options.Add(($"{(item.Kind == ItemKind.Food ? "Eat" : "Drink")} {item.Name} ({n}): {item.Text}", () => { Consume(item); ShowPack(); }));
        }
        if (pack.Count("alchemist_kit") > 0) options.Add(("Brew potions (alchemist's kit)", () => ShowCraft(Job.Herbalism, ShowPack)));
        options.Add(("Close", _hud.EndTalk));
        _hud.Talk("Your pack", text, [.. options]);
    }

    void Redress()
    {
        _player.Dress();
        Save();
    }

    void Consume(Item item)
    {
        if (item.Use is null || !_player.Pack.Take(item.Id)) return;
        _player.Consume(item.Use);
        _hud.Toast($"You {(item.Kind == ItemKind.Food ? "eat" : "drink")} the {item.Name.ToLowerInvariant()}.", 3);
    }

    /// <summary>H: the best healing potion in the pack, quickly.</summary>
    void QuickDrink()
    {
        var pack = _player.Pack;
        var best = new[] { "greater_healing", "healing_draught", "travel_rations", "bread" }.FirstOrDefault(id => pack.Count(id) > 0);
        if (best is null) { _hud.Toast("Nothing to heal you in your pack. Buy healing draughts from Hollis or Old Saffron, or brew them.", 4); return; }
        Consume(Items.Get(best)!);
    }

    // ---- making things --------------------------------------------------------------------------

    /// <summary>The recipes of a trade, those you can make now first.</summary>
    void ShowCraft(Job job, Action back)
    {
        var pack = _player.Pack;
        var level = pack.Level(job);
        var known = Recipes.For(job).Where(r => r.Level <= level + 1).OrderBy(r => !pack.Has(r.Needs) || r.Level > level).ThenBy(r => r.Level).ToList();
        var text = $"{job} {level} ({pack.JobXp.GetValueOrDefault(job)}/{(level < Pack.MaxJobLevel ? Pack.XpFor(level + 1) : pack.JobXp.GetValueOrDefault(job))} xp). " +
                   (job == Job.Smithing ? "Smelt scrap into iron, iron and coal into steel; tan hides into leather; forge weapons and armour." : "Brew potions from the herbs you pick in the wild.");
        var options = known.Select(r =>
        {
            var needs = string.Join(", ", r.Needs.Select(n => $"{Items.Count(n.Id, n.N)} ({pack.Count(n.Id)})"));
            var label = r.Level > level ? $"[{job} {r.Level}] {Items.Count(r.Makes, r.Count)}: {needs}"
                : pack.Has(r.Needs) ? $"Make {Items.Count(r.Makes, r.Count)} from {needs}" : $"({Items.Count(r.Makes, r.Count)}: needs {needs})";
            return (label, (Action)(() =>
            {
                var before = pack.Level(job);
                if (!pack.Make(r)) { _hud.Toast(r.Level > level ? $"You need {job.ToString().ToLowerInvariant()} {r.Level} for that." : "You don't have everything for that.", 3); return; }
                _hud.Toast($"You make {Items.Count(r.Makes, r.Count).ToLowerInvariant()}.{(pack.Level(job) > before ? $" {job} rises to {pack.Level(job)}!" : "")}", 4);
                Save();
                ShowCraft(job, back);
            }));
        }).ToList();
        options.Add(("Back", back));
        _hud.Talk(job == Job.Smithing ? "The forge" : "Brewing", text, [.. options]);
    }

    // ---- shops ----------------------------------------------------------------------------------

    /// <summary>What a shopkeeper offers besides talk: buying, selling, and their forge or bench.</summary>
    IEnumerable<(string, Action)> ShopTalk(Person person)
    {
        if (Shops.All.FirstOrDefault(s => s.Id == person.Npc.Id) is not { } shop) yield break;
        if (shop.Beasts)
        {
            yield return ("Show me your animals.", () => ShowMenagerie(person));
            yield return ("I need meat for my beasts.", () => Buy(person, shop));
            yield return ("How do I tame one in the wild?", () => Say(person,
                "Find a young one with none of its grown kin close by: they won't let you near it, and they'll come for you. " +
                "Go up to it and press E (Use on a phone), with a bit of meat in your pack if you can. Rangers never fail; " +
                "the rest of us get better at it. Every beast you tame, feed and raise teaches you beast taming: the better " +
                "you are, the more often they take to you, the faster they learn, and the less I charge you. " +
                "The only thing you can't tame is a grown beast. Or a dragon, unless you find the egg yourself."));
            yield break;
        }
        yield return ("Let me see what you have.", () => Buy(person, shop));
        yield return ("I've things to sell.", () => Sell(person, shop));
        if (shop.Forge) yield return ("Can I use your forge?", () => ShowCraft(Job.Smithing, () => Say(person, "Mind the anvil.")));
        if (shop.Bench) yield return ("Can I use your bench?", () => ShowCraft(Job.Herbalism, () => Say(person, "Wash the mortar after.")));
    }

    void Buy(Person person, Shop shop)
    {
        var options = shop.Sells.Select(id =>
        {
            var item = Items.Get(id);
            var (name, price, text) = id == "meat" ? ("Meat", Shops.MeatPrice, "for your beasts") : (item!.Name, Shop.Price(item), item.Text);
            return ($"{name}, {price} septims: {text}", (Action)(() =>
            {
                var (septims, meat) = (_player.Septims, _player.Meat);
                if (!Shops.Buy(shop, id, _player.Pack, ref septims, ref meat)) { _hud.Toast("You can't afford that.", 3); return; }
                (_player.Septims, _player.Meat) = (septims, meat);
                _hud.Toast($"Bought {name.ToLowerInvariant()}. ({_player.Septims} septims left)", 3);
                Save();
                Buy(person, shop);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "Come back when your purse is heavier.")));
        _hud.Talk($"{shop.Name} ({_player.Septims} septims)", shop.Greeting, [.. options]);
    }

    /// <summary>Wren's young beasts: what each will grow into, how hard it is to raise, and the price.</summary>
    void ShowMenagerie(Person person)
    {
        var level = _player.Pack.Level(Job.Taming);
        var options = Menagerie.Stock.Select(o => ($"{o.Name}, {Menagerie.Price(o, level)} septims. Danger {Menagerie.Stars(o.Danger)}, training {Menagerie.Stars(o.Training)}. {o.Text}",
            (Action)(() => { if (BuyPet(o) is not null) Say(person, "Feed it, and don't let it chew the rope."); }))).ToList();
        options.Add(("Not today.", () => Say(person, "They'll be here. Most of them.")));
        _hud.Talk($"Wren's Menagerie ({_player.Septims} septims)",
            $"Danger is what it grows into; training is how much patience it takes. Your beast taming: {level}" +
            $"{(level > 1 ? $", so {3 * (level - 1)}% off" : "")}. They follow you (up to {MaxFollowing} at a time; the rest wait at the Gallows Perch) " +
            "and grow up at your side. Dragons aren't for sale: no one sells a dragon.", [.. options]);
    }

    void Sell(Person person, Shop shop)
    {
        var options = _player.Pack.Contents.Where(c => c.Item.Id != "alchemist_kit" || c.N > 1).Select(c => ($"{c.Item.Name} ({c.N}): {Shop.Offer(c.Item)} septims each",
            (Action)(() =>
            {
                var septims = _player.Septims;
                if (Shops.Sell(c.Item.Id, _player.Pack, ref septims)) _player.Septims = septims;
                Save();
                Sell(person, shop);
            }))).ToList();
        if (_player.Pack.Contents.Any(c => c.Item.Kind is ItemKind.Material or ItemKind.Trophy))
            options.Insert(0, ("Sell every trophy and all the pelts, hides and bits (keep herbs, potions and gear)", () =>
            {
                var septims = _player.Septims;
                foreach (var (item, n) in _player.Pack.Contents.Where(c => c.Item.Kind is ItemKind.Material or ItemKind.Trophy).ToList())
                    for (var i = 0; i < n; i++) Shops.Sell(item.Id, _player.Pack, ref septims);
                _hud.Toast($"Sold for {septims - _player.Septims} septims.", 4);
                _player.Septims = septims;
                Save();
                Sell(person, shop);
            }));
        options.Add(("That's all.", () => Say(person, "Pleasure.")));
        _hud.Talk($"{shop.Name}: selling ({_player.Septims} septims)", $"{shop.Keeper} pays half what a thing's worth. Worn gear isn't for sale; take it off first.", [.. options]);
    }

    /// <summary>The market on the road in from the east gate, right by the Gallows Perch: four stalls and their keepers.</summary>
    void AddMarket()
    {
        var stalls = new (Shop Shop, Vector3 At, string Look, Sex Sex, int Age, string Who, string Story, string Temper)[]
        {
            (Shops.Provisions, new(68, 0, 13), "worker", Sex.Male, 52, "provisioner", "Hollis Brandt has sold bread and rope on the Gallows road for thirty years. He knows every company that's walked out the east gate, and which ones came back.", "warm"),
            (Shops.Smithy, new(81, 0, 14), "warmaster", Sex.Female, 44, "smith of the Anvil Gate", "Hulda Ironhand learned her trade in the Sunken Forge's shadow, from a dwarf who never said where he came from. Her forge is open to anyone who'll pay for the coal.", "blunt"),
            (Shops.Apothecary, new(94, 0, 13), "priest", Sex.Female, 71, "apothecary", "Old Saffron has gathered herbs in the wilds since before the war. She pays fair for what you pick, and teaches anyone patient enough to grind it.", "kindly"),
            (Shops.Menagerie, new(57, 0, 13), "hunter", Sex.Female, 38, "beastkeeper", "Wren Halloway was raised by a ranger who brought home every orphaned thing he found. She still does. She'll sell you anything she's raised, except the ones that won't leave her.", "dry"),
        };
        foreach (var (shop, at, look, sex, age, who, story, temper) in stalls)
        {
            Townsfolk(new Npc(shop.Id, shop.Keeper, sex, age, who, "city:tristinwold", "Tristinwold", story, ["trade", "the road"], temper, shop.Greeting, at.X, at.Z), look);
            Market.Stall(this, at, shop);
        }
    }
}
