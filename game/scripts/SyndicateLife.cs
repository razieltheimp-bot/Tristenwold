using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Korrin's Iron Syndicate in the world (see <see cref="Syndicate"/>), while he holds Tristinwold. Supply depots on the
/// three roads out of the city, each with a captain and his guard, in Korrin's black and violet; squads hunting the
/// country round the city; fences in the city's alleys selling contraband at ruinous prices, or for a favour; Garrick
/// at the yard with the runs that break the blockade (food smuggled in, a caravan pulled out of an ambush); and
/// assassins for whoever burns a depot. Kill a captain and his men break and run: they only ever followed him out of
/// fear.
/// </summary>
public partial class Main
{
    static readonly (string Id, string Name, Vector3 At)[] Depots =
    [
        ("depot:north", "the north road depot", new(-176, 0, -218)),
        ("depot:west", "the west road depot", new(-420, 0, -112)),
        ("depot:south", "the south downs depot", new(-60, 0, 380)),
    ];
    static readonly Vector3 SmuggleCache = new(-520, 0, 170), PinnedCaravan = new(-260, 0, -66);
    static readonly (string Id, Vector3 At)[] Fences = [("syndicate:fence_docks", new(84, 0, 14)), ("syndicate:fence_west", new(-92, 0, 22))];

    string? _depotHere;
    Node3D? _depotSite, _fortress, _cacheSite, _pinnedSite;
    Person? _captain;
    Enemy? _captainBody;
    readonly List<Enemy> _depotGuard = [];
    readonly List<Enemy> _squad = [];
    readonly List<Person> _fences = [];
    double _squadAt = 240, _assassinsAt = -1;
    /// <summary>The run Garrick's given this hero: 1, to the cache; 2, back with the food; 10, to the pinned caravan.</summary>
    int _run;
    readonly List<Enemy> _runFoes = [];

    void TendSyndicate()
    {
        var p = _player.GlobalPosition;
        if (!_city.KorrinHolds)
        {
            if (_depotHere is not null) ClearDepot();
            foreach (var f in _fences.Where(IsInstanceValid)) Dismiss(f);
            _fences.Clear();
            _run = 0;
            return;
        }
        TendFences(p);
        TendDepots(p);
        TendSquads(p);
        TendAssassins(p);
        TendRuns(p);
    }

    // ---- the depots and their captains ----------------------------------------------------------------------------

    void TendDepots(Vector3 p)
    {
        if (_depotHere is { } id && (Depots.First(d => d.Id == id).At is var here && new Vector2(p.X - here.X, p.Z - here.Z).Length() > 200 || _city.DepotsBurned.Contains(id))) ClearDepot();
        if (_depotHere is not null || _indoors is not null || _cave is not null) return;
        foreach (var (did, name, at) in Depots)
        {
            if (_city.DepotsBurned.Contains(did) || new Vector2(p.X - at.X, p.Z - at.Z).Length() > 160) continue;
            _depotHere = did;
            _terrain.EnsureAround(at, 1);
            _depotSite = new Node3D { Position = at with { Y = City.FloorAt(at) } };
            AddChild(_depotSite);
            for (var i = 0; i < 8; i++)
                Models.Place(_depotSite, i % 2 == 0 ? "ph:crate.glb" : "ph:barrel.glb", new Vector3(-3 + i % 4 * 1.6f, 0, -2 + i / 4 * 1.6f), 1.3f, i, solid: false);
            for (var i = 0; i < 4; i++) _depotSite.AddChild(Banner(new Vector3(Mathf.Cos(i * Mathf.Pi / 2) * 7, 0, Mathf.Sin(i * Mathf.Pi / 2) * 7)));
            _depotSite.AddChild(new Label3D { Text = $"Iron Syndicate: {name}", Position = new Vector3(0, 4.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.01f, Modulate = new Color("#c8a0ff") });
            var bought = _city.CaptainsBought.Contains(did);
            _captain = MeetAt(new Npc($"syndicate:{did}", $"Captain {Names.Person(new Rng((ulong)WorldGen.StableHash(did)), Sex.Male)}", Sex.Male, 41, $"captain of {name}, for the Iron Syndicate", "wilds", name,
                "A scarred man in black leather with a violet sash, who runs this depot for Korrin and doesn't look as if he enjoys it.", ["money", "staying alive"], "sly",
                bought ? "\"Keep your voice down. You never paid me anything.\"" : "\"Turn round. Whatever you're selling, the Emperor isn't buying.\"", at.X + 2, at.Z + 3), "bandit_chief");
            if (!bought)
                for (var i = 0; i < 4; i++)
                {
                    var spot = at + new Vector3(Mathf.Cos(i * 1.57f + 0.7f) * 5, 0, Mathf.Sin(i * 1.57f + 0.7f) * 5);
                    _depotGuard.Add(Thug(i == 0 ? "bandit_chief" : i % 2 == 0 ? "bandit" : "dock_thug", spot, "Iron Syndicate guard", provoke: false));
                }
            _hud.Toast(bought ? $"{name}: the captain you bought keeps his men away from you." : $"{name}: Korrin's supplies, stacked high, with a captain and his guard on them.", 7);
            return;
        }
    }

    void ClearDepot()
    {
        foreach (var e in _depotGuard.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
        _depotGuard.Clear();
        if (_captain is not null && IsInstanceValid(_captain)) Dismiss(_captain);
        if (_captainBody is not null && IsInstanceValid(_captainBody) && _captainBody.State != EnemyState.Dead) _captainBody.QueueFree();
        if (_depotSite is not null && IsInstanceValid(_depotSite)) _depotSite.QueueFree();
        (_captain, _captainBody, _depotSite, _depotHere) = (null, null, null, null);
    }

    /// <summary>A thug of the Iron Syndicate, in Korrin's colours, at about the hero's strength.</summary>
    Enemy Thug(string kind, Vector3 at, string name, bool provoke = true)
    {
        var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(Math.Max(4, _player.Hero.Level)));
        e.Stats.Name = name;
        (e.Faction, e.Roam) = ("syndicate", 12);
        AddEnemy(e);
        e.RefreshPlate();
        Lairfolk.Recoat(e, m => m.AlbedoColor = m.AlbedoColor.Lerp(new Color("#3a2a50"), 0.45f)); // black and violet
        if (provoke) e.Provoke(_player);
        return e;
    }

    /// <summary>Near Korrin, his men fight harder, and his violet light is on them.</summary>
    static void Emboldened(Enemy e)
    {
        e.Stats.DamageMult *= 1.25;
        Aura(e, new Color("#a050ff"), 2);
    }

    /// <summary>A post with Korrin's black and violet hanging from it.</summary>
    static Node3D Banner(Vector3 at)
    {
        var post = new Node3D { Position = at };
        post.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 4 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#2a2420") }, Position = new Vector3(0, 2, 0) });
        post.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 1.6f, 0.04f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#2a1a3a"), Roughness = 0.9f }, Position = new Vector3(0.48f, 3, 0) });
        post.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.25f, 0.05f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#8a50d8"), EmissionEnabled = true, Emission = new Color("#6a30c0") }, Position = new Vector3(0.48f, 3.3f, 0) });
        return post;
    }

    /// <summary>A town his thugs hold: his banners all round it, and a fortress feel to the streets.</summary>
    void Fortify(Settlement town)
    {
        _fortress = new Node3D { Position = new Vector3((float)town.X, 0, (float)town.Z) };
        AddChild(_fortress);
        var r = (float)Math.Max(18, town.Radius * 0.8);
        for (var i = 0; i < 10; i++)
        {
            var at = new Vector3(Mathf.Cos(i * Mathf.Tau / 10) * r, 0, Mathf.Sin(i * Mathf.Tau / 10) * r);
            var b = Banner(at);
            b.Position = at with { Y = City.FloorAt(_fortress.Position + at) };
            _fortress.AddChild(b);
        }
        _fortress.AddChild(new Label3D { Text = $"{town.Name}\nheld by the Iron Syndicate", Position = new Vector3(0, 12, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 72, OutlineSize = 14, PixelSize = 0.02f, Modulate = new Color("#c8a0ff") });
    }

    IEnumerable<(string, Action)> SyndicateTalk(Person person)
    {
        var id = person.Npc.Id;
        if (id.StartsWith("syndicate:fence")) { foreach (var o in FenceTalk(person)) yield return o; yield break; }
        if (person != _captain || _depotHere is not { } depot) yield break;
        if (_city.CaptainsBought.Contains(depot))
        {
            yield return ("Where are the patrols?", () => Say(person, $"He leans close. \"Squads go out from the other depots on the roads, every few hours. {string.Join(" ", Depots.Where(d => d.Id != depot && !_city.DepotsBurned.Contains(d.Id)).Select(d => $"Out of {d.Name}, {Direction(person.GlobalPosition, (d.At.X, d.At.Z))} of here."))} Don't tell anyone where you heard it.\""));
            yield break;
        }
        yield return ($"Make it worth his while ({Syndicate.Bribe} crowns)", () =>
        {
            if (_player.Crowns < Syndicate.Bribe) { Sound.Ui("error"); Say(person, "\"That's not enough to die for.\""); return; }
            _player.Crowns -= Syndicate.Bribe;
            DoCity(CityAction.CaptainBought(depot));
            foreach (var e in _depotGuard.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
            _depotGuard.Clear();
            Sound.Ui("coin");
            Save();
            Say(person, "He takes the purse without looking at it. \"My lads stay off this ground, and you didn't get this from me.\" He tells you where the other depots send their patrols. \"If he ever finds out, I'm dead, and so's everyone here. So he won't.\"");
        });
        yield return ("Draw on him.", () =>
        {
            _hud.EndTalk();
            var at = person.GlobalPosition;
            Dismiss(person);
            _captain = null;
            _captainBody = Thug("bandit_chief", at, "Iron Syndicate captain");
            _captainBody.Scale = Vector3.One * 1.15f;
            Emboldened(_captainBody);
            foreach (var e in _depotGuard.Where(IsInstanceValid)) e.Provoke(_player);
            _hud.Toast("The captain draws, and shouts, and his guard come running.", 6);
        });
    }

    (string Verb, Action Act)? SyndicateAction(Vector3 pos)
    {
        if (!_city.KorrinHolds || _indoors is not null) return null;
        if (_depotHere is { } depot && _depotSite is not null && _captain is null && _depotGuard.All(e => !IsInstanceValid(e) || e.State == EnemyState.Dead)
            && pos.DistanceTo(_depotSite.GlobalPosition with { Y = pos.Y }) < 6)
            return ("set the depot alight", () => BurnDepot(depot));
        if (_run == 1 && _cacheSite is not null && pos.DistanceTo(SmuggleCache with { Y = pos.Y }) < 4)
            return ("take the smuggled food", TakeCache);
        return null;
    }

    void BurnDepot(string depot)
    {
        DoCity(CityAction.DepotBurned(depot, _player.Company));
        if (_depotSite is not null) Effects.Burst(this, _depotSite.GlobalPosition + Vector3.Up, DamageKind.Fire, 6);
        GainXp(80 + 10 * _player.Hero.Level);
        _assassinsAt = _guilds.PlaySeconds + 120 + GD.Randi() % 180;
        _squadAt += 240;
        _hud.Toast("The depot goes up like a torch. Prices in the region will ease a little, and there'll be fewer patrols on the roads. Korrin will have heard by morning. He'll want to know who.", 10);
        ClearDepot();
        Save();
    }

    /// <summary>Kill a captain and the rest break: they followed him out of fear, not love.</summary>
    void OnSyndicateSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim != _captainBody) return;
        _captainBody = null;
        var at = victim.GlobalPosition;
        var fled = 0;
        foreach (var e in Enemies().Where(e => e.Faction == "syndicate" && e.State != EnemyState.Dead && e.GlobalPosition.DistanceTo(at) < 45).ToList())
        {
            e.LoseTrack(30);
            e.Wander(e.GlobalPosition + (e.GlobalPosition - at).Normalized() * 80);
            var gone = e;
            GetTree().CreateTimer(6).Timeout += () => { if (IsInstanceValid(gone) && gone.State != EnemyState.Dead) gone.QueueFree(); };
            fled++;
        }
        _depotGuard.Clear();
        if (fled > 0) _hud.Toast($"The captain's down, and {fled} of his men throw down their weapons and run. They only ever followed him out of fear.", 8);
    }

    // ---- squads and assassins ---------------------------------------------------------------------------------------

    void TendSquads(Vector3 p)
    {
        if (_guilds.PlaySeconds < _squadAt || _squad.Any(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) return;
        if (_player.Downed || _hud.Talking || _indoors is not null || _cave is not null || _atSea is not null || InTownForTurlin(p) || p.Length() > 2500) return;
        if (Depots.Any(d => _city.CaptainsBought.Contains(d.Id) && new Vector2(p.X - d.At.X, p.Z - d.At.Z).Length() < 200)) return; // a bought captain's ground
        _squad.Clear();
        _squadAt = _guilds.PlaySeconds + (300 + GD.Randi() % 180) * (1 + _city.DepotsBurned.Count);
        var dir = (-_player.GlobalBasis.Z with { Y = 0 }).Normalized();
        for (var i = 0; i < 4; i++)
            _squad.Add(Thug(i == 0 ? "bandit_chief" : i % 2 == 0 ? "bandit" : "dock_thug", p + dir * 24 + new Vector3(i * 2.5f - 4, 0, 0), i == 0 ? "Iron Syndicate sergeant" : "Iron Syndicate thug"));
        _hud.Toast("A squad in black and violet comes up the road at a run, in step. The Iron Syndicate: they've been looking for you.", 7);
    }

    void TendAssassins(Vector3 p)
    {
        if (_assassinsAt < 0 || _guilds.PlaySeconds < _assassinsAt || _player.Downed || _indoors is not null || _cave is not null) return;
        _assassinsAt = -1;
        for (var i = 0; i < 2; i++)
        {
            var e = Enemy.Create(Bestiary.Get("drowned_hand"), (p + new Vector3(i * 4 - 2, 0, -12)) with { Y = City.FloorAt(p) + 0.5f }, Northwood.DangerFor(_player.Hero.Level + 4), look: "Assassin");
            e.Stats.Name = "Korrin's assassin";
            (e.Faction, e.Roam) = ("syndicate", 20);
            AddEnemy(e);
            e.RefreshPlate();
            Emboldened(e);
            e.Provoke(_player);
        }
        _hud.Toast("Two shapes step out of nowhere with knives already moving. Korrin found out who burned his depot.", 8);
    }

    // ---- the black market --------------------------------------------------------------------------------------------

    void TendFences(Vector3 p)
    {
        if (_fences.Count > 0 || p.Length() > 400) return;
        foreach (var (id, at) in Fences)
            _fences.Add(MeetAt(new Npc(id, id.EndsWith("docks") ? "Slip Marrow" : "Nettie Crook", id.EndsWith("docks") ? Sex.Male : Sex.Female, 35, "fence, in a dark alley", "city:tristinwold", "Tristinwold",
                "Sells what the Syndicate's taken off the stalls, to whoever can pay what the Syndicate charges. Or does a favour instead.", ["money"], "sly", "\"Psst. You look hungry. Everyone's hungry. I can help, for a price.\"", at.X, at.Z), "Assassin"));
    }

    static readonly string[] Contraband = ["travel_rations", "stamina_brew", "mana_tonic", "elixir_of_might", "warding_draught", "iron_ingot"];

    IEnumerable<(string, Action)> FenceTalk(Person person)
    {
        yield return ("What have you got?", () => FenceWares(person));
        yield return ("I can't pay. What about a favour?", () => _hud.Talk(Who(person.Npc), "\"Put a brick through the Watch-house window tonight, and the next thing off the cart's yours. They'll put a price on your head. Small one.\"",
            ("Do it.", () =>
            {
                _standing.Wanted(25);
                var item = Contraband[(int)(GD.Randi() % Contraband.Length)];
                _player.Pack.Add(item);
                Save();
                Say(person, $"\"Lovely.\" A {Items.Get(item)!.Name} changes hands. Somewhere a window breaks, and the Watch have your description. (Bounty +25)");
            }), ("No.", () => Say(person, "\"Suit yourself. Starve honest.\""))));
    }

    void FenceWares(Person person)
    {
        var options = Contraband.Select(id => Items.Get(id)!).Select(item =>
        {
            var price = (int)Math.Ceiling(Shops.Charge(_city.Price(item), ShopMarkup) * 1.5);
            return ($"{item.Name}, {price} crowns", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); _hud.Toast("You can't afford that.", 3); return; }
                _player.Crowns -= price;
                _player.Pack.Add(item.Id);
                Sound.Ui("coin");
                Save();
                FenceWares(person);
            }));
        }).ToList();
        options.Add(("Never mind.", () => Say(person, "\"Prices only go up, friend.\"")));
        _hud.Talk($"{person.Npc.Name} ({_player.Crowns} crowns)", "Contraband, at what the Syndicate charges, and a bit for me.", [.. options]);
    }

    // ---- the runs that break the blockade ---------------------------------------------------------------------------

    static readonly string[] FedStock = ["elixir_of_might", "warding_draught", "bear_cloak", "sapphire", "honey_cake", "travel_rations"];

    IEnumerable<(string, Action)> BlockadeTalk(Person person)
    {
        if (_run == 0)
        {
            yield return ("Is there any way to get food in?", () =>
            {
                _run = 1;
                MakeCache();
                Say(person, $"Garrick drops his voice. \"There's a load of flour and salt meat hidden {Direction(person.GlobalPosition, (SmuggleCache.X, SmuggleCache.Z))} of here, in the old sheepfold. Bring it in past his squads and I'll get it to the bakers. He'll take it out on somebody, mind. He always does.\"");
            });
            yield return ("Any caravans in trouble?", () =>
            {
                _run = 10;
                Say(person, $"\"One of ours is pinned on the west road, {Direction(person.GlobalPosition, (PinnedCaravan.X, PinnedCaravan.Z))} of here. Syndicate thugs all round it. Break them and bring her in.\"");
            });
        }
        if (_run == 2)
            yield return ("I've brought the food.", () =>
            {
                _run = 0;
                DoCity(CityAction.Smuggled(_player.Company));
                GainXp(100 + 15 * _player.Hero.Level);
                Sound.Ui("quest");
                Save();
                Say(person, "Garrick has it off your hands and away before anyone's looked twice. \"The starving merchants'll open their good stock now, and the Watch'll fight. And he'll hang somebody for it. You knew that.\"");
            });
        if (_city.FedUntil > _city.Day)
            yield return ("What's the good stock?", () => FedWares(person));
    }

    void FedWares(Person person)
    {
        var options = FedStock.Select(id => Items.Get(id)!).Select(item =>
        {
            var price = Shops.Charge(_city.Price(item), _player.Markup);
            return ($"{item.Name}, {price} crowns", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); return; }
                _player.Crowns -= price;
                _player.Pack.Add(item.Id);
                Sound.Ui("coin");
                Save();
                FedWares(person);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "\"Spend it while it's here.\"")));
        _hud.Talk($"The merchants who were starving ({_player.Crowns} crowns)", "Fair prices, the best stock in the city, for as long as the food lasts.", [.. options]);
    }

    void MakeCache()
    {
        _terrain.EnsureAround(SmuggleCache, 1);
        _cacheSite = new Node3D { Position = SmuggleCache with { Y = City.FloorAt(SmuggleCache) } };
        AddChild(_cacheSite);
        for (var i = 0; i < 4; i++) Models.Place(_cacheSite, "ph:crate.glb", new Vector3(i % 2 * 1.2f, 0, i / 2 * 1.2f), 1.1f, i, solid: false);
    }

    void TakeCache()
    {
        _run = 2;
        if (_cacheSite is not null && IsInstanceValid(_cacheSite)) _cacheSite.QueueFree();
        _cacheSite = null;
        var p = _player.GlobalPosition;
        for (var i = 0; i < 4; i++) _runFoes.Add(Thug(i == 0 ? "bandit_chief" : "bandit", p + new Vector3(18, 0, i * 3 - 4), i == 0 ? "Iron Syndicate sergeant" : "Iron Syndicate thug"));
        _hud.Toast("You shoulder the sacks, and a whistle goes up from the ridge. They were waiting. Get it back to Garrick!", 8);
    }

    void TendRuns(Vector3 p)
    {
        if (_run != 10) return;
        var far = new Vector2(p.X - PinnedCaravan.X, p.Z - PinnedCaravan.Z).Length();
        if (_pinnedSite is null)
        {
            if (far > 140) return;
            _terrain.EnsureAround(PinnedCaravan, 1);
            var wagon = new Wagon(this, "horse") { Parked = true };
            AddChild(wagon);
            wagon.GlobalPosition = PinnedCaravan with { Y = City.FloorAt(PinnedCaravan) };
            _pinnedSite = wagon;
            for (var i = 0; i < 5; i++) _runFoes.Add(Thug(i == 0 ? "bandit_chief" : i % 2 == 0 ? "bandit" : "dock_thug", PinnedCaravan + new Vector3(Mathf.Cos(i * 1.25f) * 7, 0, Mathf.Sin(i * 1.25f) * 7), i == 0 ? "Iron Syndicate sergeant" : "Iron Syndicate thug"));
            _hud.Toast("The caravan's wagon, slewed across the road, and Syndicate thugs all round it.", 7);
            return;
        }
        if (_runFoes.Any(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) return;
        _run = 0;
        _runFoes.Clear();
        DoCity(CityAction.Rescued(_player.Company));
        GainXp(90 + 12 * _player.Hero.Level);
        _player.Crowns += 150 + 15 * _player.Hero.Level;
        Sound.Ui("quest");
        var site = _pinnedSite;
        _pinnedSite = null;
        GetTree().CreateTimer(8).Timeout += () => { if (IsInstanceValid(site)) site.QueueFree(); };
        _hud.Toast("The last of them goes down. The drover climbs back up, white-faced, and takes her on into the yard. Something's getting through.", 9);
        Save();
    }

    /// <summary>On the tracker: how far his hand reaches, and the run in hand.</summary>
    IEnumerable<string> KorrinLines(Vector3 pos)
    {
        if (_city.KorrinHolds)
            yield return $"KORRIN'S INFLUENCE {_city.KorrinInfluence}%: prices x{_city.KorrinMarkup:0.0}; he holds Tristinwold{(_city.KorrinTowns.Count > 0 ? $" and {_city.KorrinTowns.Count} town{(_city.KorrinTowns.Count == 1 ? "" : "s")}" : "")}";
        if (_run == 1) yield return $"Smuggling: fetch the food from the old sheepfold ({Direction(pos, (SmuggleCache.X, SmuggleCache.Z))})";
        if (_run == 2) yield return $"Smuggling: get the food to Garrick at the caravan yard ({Direction(pos, (YardAt.X, YardAt.Z))})";
        if (_run == 10) yield return $"Caravan rescue: break the ambush on the west road ({Direction(pos, (PinnedCaravan.X, PinnedCaravan.Z))})";
        if (_city.Demons is { } r) yield return r.Surfaced ? $"DEMONS out of {r.MineName}, making for {r.TownName}{(r.Heaven ? ". The sky is opening" : "")}" : $"Demons are raiding {r.City}, in the Deep Trenches under {r.MineName}";
    }

    // ---- for the self-test ---------------------------------------------------------------------------------------

    public static (string Id, string Name, Vector3 At)[] DepotsForTest => Depots;
    public Person? CaptainForTest => _captain;
    public Enemy? CaptainBodyForTest => _captainBody;
    public IReadOnlyList<Enemy> DepotGuardForTest => _depotGuard;
    public IReadOnlyList<Person> FencesForTest => _fences;
    public (string Verb, Action Act)? SyndicateActionForTest(Vector3 at) => SyndicateAction(at);
    public void TendSyndicateForTest() => TendSyndicate();
    public int SmuggleRunForTest => _run;
    public bool AssassinsComingForTest => _assassinsAt >= 0;
}
