using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// What a class is worth beyond its blade (see <see cref="Perks"/>), and the artificers' trades in particular. Every
/// class has a trade perk that touches the markets: what it sells for, what it gathers, what it's paid, what it's
/// charged, what a load fetches away from home. An alchemist steadies a town's market with reagents (once a day,
/// through whoever keeps the market's book: Garrick Wain for the towns, Marit Hale or a port's harbour master for the
/// ports); an ether-aeronaut fits ships with modules at Osric Tarr's yard; a saboteur breaks spare gear down for
/// Nell Corran's black market, and plants a tracker on a parked wagon at the caravan yard, follows it up the road,
/// and springs an ambush on it.
/// </summary>
public partial class Main
{
    List<Enemy>? _ambush;
    Node3D? _ambushWagon;
    double _artificerTick;

    TradePerk Perk => Perks.Of(_player.Class.Id);
    public (string Verb, Action Act)? ArtificerActionForTest(Vector3 pos) => ArtificerAction(pos);
    public static Vector3 ParkedWagonAt => YardWagon;
    double PerkSell => Perk.Sell;
    double PerkGather => Perk.Gather;
    double PerkPay => Perk.Pay;
    double PerkShop => Perk.Shop;
    double PerkTrade => Perk.Trade;

    /// <summary>How many of a gathered thing, by the class's trade: a huntsman's or a refiner's extra, now and then.</summary>
    int Gathered(int n) => n <= 0 ? n : (int)Math.Floor(n * PerkGather + (_lootRng.NextDouble() < n * PerkGather % 1 ? 1 : 0));

    /// <summary>Reagents an alchemist spends to steady a market: three herbs, or two ores.</summary>
    const int SteadyHerbs = 3, SteadyOres = 2;

    IEnumerable<(string, Action)> ArtificerTalk(Person person)
    {
        var id = person.Npc.Id;
        var c = _player.Class.Id;
        if (c == "Alchemist")
        {
            if (id == Master)
                foreach (var m in _trade.Towns.Values.Where(m => m.Steadied != _city.Day).OrderBy(m => m.Name))
                    yield return ($"Steady {m.Name}'s market with reagents ({SteadyHerbs} herbs or {SteadyOres} ores; once a day).", () => Steady(person, m));
            if (id == HarbourMaster || _atSea?.Agent == person)
                foreach (var port in Seas.Ports.Where(p => _atSea?.Agent != person || _sea.Voyage?.Port == p.Id))
                {
                    var m = _sea.Market(port, _city.Day);
                    if (m.Steadied != _city.Day) yield return ($"Steady {port.Name}'s market with reagents ({SteadyHerbs} herbs or {SteadyOres} ores; once a day).", () => Steady(person, m));
                }
        }
        else if (c == "EtherAeronaut" && id == Shipwright)
        {
            foreach (var (module, kind) in SeaBook.ModuleKinds.Where(k => !_sea.HasModule(k.Key)))
                yield return ($"Fit {kind.Name} ({kind.Price} crowns): {kind.Text}.", () => FitModule(person, module));
            if (_sea.Modules.Count > 0) yield return ("What have I fitted?", () => Say(person, $"Fitted and sailing with you: {string.Join(", ", _sea.Modules.Select(m => SeaBook.ModuleKinds[m].Name))}."));
        }
        else if (c == "Saboteur" && id == Smuggler)
        {
            var spare = _player.Pack.Contents.Where(x => x.Item.Kind == ItemKind.Gear && !_player.Pack.Worn.ContainsValue(x.Item.Id)).ToList();
            if (spare.Count > 0)
                yield return ($"Break my spare gear down for parts ({spare.Sum(x => x.N)} pieces; the black market's price).", () => Scrap(person, spare));
            else yield return ("Anything to break down?", () => Say(person, "Bring me gear you don't wear, wagon iron, hull fittings. I pay for parts, not questions."));
        }
    }

    void Steady(Person keeper, TownMarket m)
    {
        var herbs = _player.Pack.Contents.Where(x => x.Item.Kind == ItemKind.Herb).Sum(x => x.N);
        var ores = _player.Pack.Contents.Where(x => x.Item.Id is "iron_ore" or "copper_ore" or "silver_ore" or "coal").Sum(x => x.N);
        if (herbs < SteadyHerbs && ores < SteadyOres) { Say(keeper, $"Reagents first: {SteadyHerbs} herbs or {SteadyOres} ores, and I'll put your salts and your tinctures where {m.Name} needs them."); return; }
        var spent = new List<string>();
        var left = herbs >= SteadyHerbs ? SteadyHerbs : 0;
        foreach (var x in _player.Pack.Contents.Where(x => x.Item.Kind == ItemKind.Herb).ToList())
            while (left > 0 && _player.Pack.Take(x.Item.Id)) { left--; spent.Add(x.Item.Name); }
        if (herbs < SteadyHerbs)
        {
            left = SteadyOres;
            foreach (var id in new[] { "iron_ore", "copper_ore", "silver_ore", "coal" })
                while (left > 0 && _player.Pack.Take(id)) { left--; spent.Add(Items.Get(id)?.Name ?? id); }
        }
        if (!m.Steady(_city.Day)) { Say(keeper, $"{m.Name}'s book is steadied for today already."); return; }
        _standing.Good(1, $"steadied {m.Name}'s market");
        Sound.Ui("quest", -4);
        Say(keeper, $"Done: {string.Join(", ", spent)} into salts, tinctures and a quiet word in the right ears. {m.Name}'s gluts and shortages ease, and its prices with them, for the day.");
        _hud.Toast($"Market Transmutation: {m.Name}'s prices steady toward what they usually are.", 7);
        Save();
    }

    void FitModule(Person osric, string module)
    {
        var crowns = _player.Crowns;
        if (_sea.Fit(module, ref crowns) is not { } price) { Say(osric, $"Brass isn't free. {SeaBook.ModuleKinds[module].Price} crowns, and you'll fit it yourself."); return; }
        _player.Crowns = crowns;
        Sound.Ui("coin", -2);
        Say(osric, $"{price} crowns, and a day of your own hands. {char.ToUpper(SeaBook.ModuleKinds[module].Name[0])}{SeaBook.ModuleKinds[module].Name[1..]} it is: it goes aboard whatever you sail on.");
        _hud.Toast($"Fitted: {SeaBook.ModuleKinds[module].Name}. {SeaBook.ModuleKinds[module].Text}.", 8);
        Save();
    }

    void Scrap(Person nell, List<(Item Item, int N)> spare)
    {
        var crowns = 0;
        foreach (var (item, n) in spare)
            for (var i = 0; i < n && _player.Pack.Take(item.Id); i++) crowns += (int)Math.Round(_city.Offer(item) * PerkSell * 1.2);
        _player.Crowns += crowns;
        Sound.Ui("coin", -2);
        Say(nell, $"Springs, plates, good steel and a buckle or two. {crowns} crowns, and nobody asks where the parts go.");
        _hud.Toast($"Scrapped for the black market: +{crowns} crowns.", 6);
        Save();
    }

    // ---- the tracker and the ambush ------------------------------------------------------------------

    /// <summary>A saboteur by a parked wagon plants a tracker; by the tracked wagon on the road, springs the ambush.</summary>
    (string Verb, Action Act)? ArtificerAction(Vector3 pos)
    {
        var saboteur = _player.Class.Id == "Saboteur";
        if (!saboteur && !Shadows.In(ShadowLine.Highwayman)) return null;
        var t = _standing.Tracker;
        if (t is null || t.Lost(_guilds.PlaySeconds))
        {
            if (saboteur && GetChildren().OfType<Wagon>().Any(w => w.Parked && w.GlobalPosition.DistanceTo(pos) < 5) && NextBound() is { } bound)
                return ($"plant a tracker on the wagon (bound for {bound.Name})", () => Plant(bound));
            return null;
        }
        if (!t.Sprung && !t.Arrived(_guilds.PlaySeconds) && _ambush is null)
        {
            var (x, z) = t.Where(_guilds.PlaySeconds);
            if (new Vector2((float)x - pos.X, (float)z - pos.Z).Length() < 18) return ("spring the ambush on the wagon", () => Spring(t));
        }
        return null;
    }

    /// <summary>Where the next wagon out of the yard is bound: the nearest town the city trades with, or the nearest town.</summary>
    Settlement? NextBound() =>
        _trade.Towns.Values.Select(m => _world.Find(m.Id)).OfType<Settlement>().OrderBy(s => s.DistanceTo(YardAt.X, YardAt.Z)).FirstOrDefault()
        ?? _world.SettlementsNear(0, 0, 5000).Where(s => s.Kind == SettlementKind.Town).OrderBy(s => s.DistanceTo(0, 0)).FirstOrDefault();

    void Plant(Settlement bound)
    {
        _standing.Tracker = new WagonTracker { TownId = bound.Id, TownName = bound.Name, X = bound.X, Z = bound.Z, FromX = YardAt.X, FromZ = YardAt.Z, PlantedAt = _guilds.PlaySeconds };
        Sound.Ui("quest", -5);
        _hud.Toast($"A tracker under the wagon's bed, ticking. It's bound for {bound.Name}; the tracker shows where it is on the road. Meet it there, away from the walls, and spring the ambush.", 10);
        Save();
    }

    void Spring(WagonTracker t)
    {
        var (x, z) = t.Where(_guilds.PlaySeconds);
        var at = new Vector3((float)x, 0, (float)z);
        _terrain.EnsureAround(at, 1);
        at.Y = City.FloorAt(at);
        var wagon = new Wagon(this, "horse") { Parked = true };
        AddChild(wagon);
        wagon.GlobalPosition = at;
        _ambushWagon = wagon;
        _ambush = [];
        for (var i = 0; i < 2; i++)
        {
            var spot = at + new Vector3(i == 0 ? 3 : -3, 0, 2);
            var g = Enemy.Create(Bestiary.Get("watchman"), spot with { Y = City.FloorAt(spot) + 0.5f }, look: "guard");
            g.Stats.Name = "Caravan guard";
            AddEnemy(g);
            g.RefreshPlate();
            g.Provoke(_player);
            _ambush.Add(g);
        }
        t.Sprung = true;
        _hud.Toast($"The wagon pulls up where the tracker said it would, and its guards come off the box with steel out. Bring them down, and the load is yours.", 8);
        Save();
    }

    void TendArtificer(double delta)
    {
        if ((_artificerTick -= delta) > 0) return;
        _artificerTick = 1;
        if (_ambush is null) return;
        if (_ambush.Any(g => IsInstanceValid(g) && g.State != EnemyState.Dead)) return;
        // The guards are down: the load is yours, and the city will hear.
        var take = 60 + 12 * _player.Hero.Level;
        _player.Crowns += take;
        var t = _standing.Tracker;
        if (t is not null && _trade.Towns.TryGetValue(t.TownId, out var m)) m.Plundered(_city.Day);
        _standing.Evil(5, $"ambushed a wagon bound for {t?.TownName ?? "the road"}");
        var seen = _standing.Caught(Standing.AmbushSeen, Standing.RaidBounty, _lootRng, sure: CaughtSure);
        Sound.Ui("coin", -2);
        _hud.Toast(seen
            ? $"The wagon's strongbox and its best loads: {take} crowns. The driver got away and knew your face: {Standing.RaidBounty} crowns on your head ({_standing.Bounty} in all)."
            : $"The wagon's strongbox and its best loads: {take} crowns, and nobody left to say who took them.", 9);
        if (_ambushWagon is { } w && IsInstanceValid(w)) GetTree().CreateTimer(20).Timeout += () => { if (IsInstanceValid(w)) w.QueueFree(); };
        _ambush = null;
        _ambushWagon = null;
        _standing.Tracker = null;
        Save();
    }

    IEnumerable<string> ArtificerLines(Vector3 pos)
    {
        if (_standing.Tracker is not { } t || t.Lost(_guilds.PlaySeconds)) yield break;
        var (x, z) = t.Where(_guilds.PlaySeconds);
        var d = new Vector2((float)x - pos.X, (float)z - pos.Z).Length();
        yield return t.Sprung ? $"The ambush: bring the wagon's guards down"
            : t.Arrived(_guilds.PlaySeconds) ? $"Tracker: the wagon is at {t.TownName}'s market; it'll be gone soon"
            : $"Tracker: the wagon is on the road to {t.TownName}, {d:0} m away ({Direction(pos, (x, z))})";
    }
}
