using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// Work in and around Tristinwold, and the city's economy (see <see cref="CityEconomy"/>):
/// <list type="bullet">
/// <item>Fishing: with a rod, face open water (the piers, the shore, a lake or river) and press E; when the float goes
/// under, press E again. The harbour, fresh water and the cold north each have their own fish.</item>
/// <item>Mining down the Greyhollow mine west of the walls (stone, iron, coal, silver, and gold in the deep), and at
/// outcrops up on the crags (silver and gold); woodcutting at Hollin's woodlot (pine, oak, yew). Each needs its tool.</item>
/// <item>Cooking at a tavern's kitchen: the Gallows Perch and the Salt Barrel.</item>
/// <item>Day work: carrying cargo on the docks for Haskel Brine, and running letters for the Exchange.</item>
/// <item>The Exchange: Factor Ottilie Marsh's market report, the contracts for what's short, the city's works.</item>
/// </list>
/// Every shop's prices for the goods the city trades in follow the economy, and everything a hero sells or buys moves it.
/// </summary>
public partial class Main
{
    CityEconomy _city = CityEconomy.Start(0);
    Workings _workings = null!;
    double _cityTick;
    bool _working;

    public CityEconomy Economy => _city;

    /// <summary>On a server, the city is everybody's: the server's copy, as it last sent it.</summary>
    bool _sharedCity;
    public bool SharedCity => _sharedCity;

    /// <summary>The server's city, in place of ours: all but our own name at the Exchange, and the day's timing kept to our clock.</summary>
    void TakeCity(CitySnapshot s)
    {
        var renown = _city.Renown;
        _city = s.City;
        _city.Renown = renown;
        _city.NextDayAt = _guilds.PlaySeconds + Math.Max(0, s.City.NextDayAt - s.Seconds);
        _trade.Roads = _city.Roads; // the roads are the city's: everyone's
        _sharedCity = true;
    }

    /// <summary>Does something to the city here and now, and on the server too.</summary>
    public void DoCity(CityAction a)
    {
        _city.Apply(a);
        Share(a);
    }

    /// <summary>Something already done to our copy of the city, told to the server.</summary>
    public void Report(CityAction a) => Share(a);

    /// <summary>What we just did to our copy of the city, done to the server's too (on a server).</summary>
    void Share(CityAction a)
    {
        if (_sharedCity && _client?.Status == ClientStatus.Connected) _client.Send(new CityTrade(a));
    }
    public Workings WorkingsNode => _workings;

    public static readonly Vector3 QuarryAt = new((float)Greyhollow.QuarryX, 0, (float)Greyhollow.QuarryZ), WoodlotAt = new(-92, 0, -118);
    /// <summary>Where the cargo waits (the end of the middle pier), and where it goes (the warehouse by the east gate).</summary>
    public static readonly Vector3 CrateStack = new(136, 0, 0), Warehouse = new(90, 0, -9);
    public static readonly Vector3 FactorAt = new(25, 0, -25), ForemanAt = new(104, 0, 1.5f);

    /// <summary>What a shopkeeper charges this hero: their people's markup, less what the city's regard is worth.</summary>
    double ShopMarkup => _player.Markup * (1 - _city.Discount) * _standing.PriceFactor * PerkShop;
    /// <summary>What a shop would charge this hero for something today (for the self-test).</summary>
    public int ShopPriceFor(Item item) => Shops.Charge(_city.Price(item), ShopMarkup);

    void AddTownWork()
    {
        Townsfolk(new Npc("tristinwold/factor", "Factor Ottilie Marsh", Sex.Female, 47, "factor of the Exchange", "city:tristinwold", "Tristinwold",
            "Ottilie keeps the Exchange's ledgers, posts its contracts and counts the city's coin. She knows what everything costs, and why, and what it'll cost tomorrow.",
            ["money", "gossip"], "dry", "Prices, contracts, the city's works: if it's bought or sold in Tristinwold, it crosses my desk.", FactorAt.X, FactorAt.Z), "scholar");
        Townsfolk(new Npc("tristinwold/foreman", "Haskel Brine", Sex.Male, 50, "foreman of the docks", "city:tristinwold", "Tristinwold",
            "Haskel has shifted cargo on these piers for thirty years, and has the back to prove it. He pays by the crate, and he pays on the nail.",
            ["the sea", "gossip"], "gruff", "Strong arms? There's always cargo.", ForemanAt.X, ForemanAt.Z), "worker");
        var keepers = new (Shop Shop, Vector3 At, string Look, Sex Sex, int Age, string Who, string Story, string Temper, bool Stall)[]
        {
            (Shops.Fishmonger, new(64, 0, -13), "townsfolk", Sex.Male, 61, "fishmonger", "Bryn Saltmarsh fished the harbour for forty years before his knees gave out. Now he sells what the boats bring, and teaches anyone who'll listen how to catch their own.", "cheerful", true),
            (Shops.Quarry, QuarryAt + new Vector3(7, 0, 5), "worker", Sex.Female, 45, "quarrymaster of Greyhollow", "Dagna Flint has dug stone and ore out of Greyhollow since the walls were half the height. She can tell iron from tin by the sound it makes.", "blunt", false),
            (Shops.Woodlot, WoodlotAt + new Vector3(6, 0, 6), "hunter", Sex.Male, 39, "forester", "Hollin Ashby marks the trees that can come down and plants two for each one that does. The shipwrights swear by his oak.", "warm", false),
            (Shops.SaltBarrel, new(40, 0, 69), "drinker", Sex.Female, 52, "cook of the Salt Barrel", "Fenna Rook has fed sailors, Tongs and the Watch at the same tables for twenty years, and nobody's ever started a fight over her stew.", "warm", false),
        };
        foreach (var (shop, at, look, sex, age, who, story, temper, stall) in keepers)
        {
            Townsfolk(new Npc(shop.Id, shop.Keeper, sex, age, who, "city:tristinwold", "Tristinwold", story, ["trade", "the road"], temper, shop.Greeting, at.X, at.Z), look);
            if (stall) Market.Stall(this, at, shop);
        }

        // The quarry: the mound over the mine's mouth, and stone and tools about. The veins are down the mine now (see Cave).
        City.Zones.Add(new Zone("The Greyhollow Quarry", QuarryAt, 22, 9));
        City.Zones.Add(new Zone("Hollin's Woodlot", WoodlotAt, 24, 9));
        AddMineMound();
        AddDrownedChapel();
        AddCarpentersYard();
        AddForest();
        // The quarry's faces: broken rock round the back, cut back where the stone's been taken, and a gravel floor.
        for (var i = 0; i < 9; i++)
        {
            var a = Mathf.DegToRad(-175 + i * 21f);
            var at = QuarryAt + new Vector3(Mathf.Cos(a) * 16, 0, Mathf.Sin(a) * 14);
            if (i == 4) continue; // the mine's mouth
            Models.Place(this, $"medieval/nature/rock_single_{(char)('A' + i % 5)}.gltf", at with { Y = City.FloorAt(at) - 0.3f }, 7 + i % 3 * 1.5f, i * 1.7f);
        }
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 15, BottomRadius = 15, Height = 0.06f, RadialSegments = 48 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("#8f8a80"), AlbedoTexture = Effects.Dot, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1,
            },
            Position = QuarryAt with { Y = City.FloorAt(QuarryAt) + 0.04f }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        foreach (var (path, off, scale) in new[] { ("medieval/props/resource_stone.gltf", new Vector3(-8, 0, 6), 5f), ("medieval/props/wheelbarrow.gltf", new Vector3(9, 0, 1), 5f),
                     ("medieval/props/resource_stone.gltf", new Vector3(10, 0, 8), 4f), ("medieval/props/pallet.gltf", new Vector3(5, 0, 9), 5f) })
            Models.Place(this, path, QuarryAt + off, scale, off.X);
        foreach (var (path, off, scale) in new[] { ("medieval/props/resource_lumber.gltf", new Vector3(10, 0, 3), 5f), ("medieval/props/resource_lumber.gltf", new Vector3(11, 0, 9), 5f),
                     ("medieval/props/wheelbarrow.gltf", new Vector3(3, 0, 10), 5f), ("ph:stump.glb", new Vector3(-3, 0, 12), 0.8f) })
            Models.Place(this, path, WoodlotAt + off, scale, off.Z);
        Sign(QuarryAt + new Vector3(-13, 4, 8), "Greyhollow Quarry");
        Sign(WoodlotAt + new Vector3(0, 7, 0), "Hollin's Woodlot");

        // The docks: cargo waiting at the end of the middle pier, and the warehouse it goes to.
        for (var i = 0; i < 5; i++)
            Models.Place(this, "ph:crate.glb", CrateStack + new Vector3(i % 2 * 0.9f, 0, i / 2 * 0.8f - 0.8f), 1.3f, i * 0.3f, solid: false);
        for (var i = 0; i < 4; i++)
            Models.Place(this, i % 2 == 0 ? "ph:crate.glb" : "ph:barrel.glb", Warehouse + new Vector3(-1.5f + i, 0, -2.2f), 1.2f, i, solid: false);
        Sign(CrateStack + new Vector3(0, 3, 0), "Cargo");
        Sign(Warehouse + new Vector3(0, 3.2f, -2), "Warehouse");

        Board(FactorAt + new Vector3(-1.6f, 0, -1.6f));
        Ships();

        _workings = new Workings(this);
        AddChild(_workings);
    }

    /// <summary>
    /// Ships at anchor in the harbour: two great merchantmen, a smaller trader and a pinnace (Poly Haven's scanned
    /// Dutch ships), riding the swell. Only where the files are (they're left out of the phone build).
    /// </summary>
    void Ships()
    {
        foreach (var (file, at, yaw) in new[] { ("ph:ship_large.glb", new Vector3(205, 0, -55), 0.4f), ("ph:ship.glb", new Vector3(225, 0, 50), -1.1f),
                     ("ph:ship_large.glb", new Vector3(285, 0, 10), 2.0f), ("ph:pinnace.glb", new Vector3(165, 0, 25), 1.6f) })
        {
            if (OS.HasFeature("mobile") || Models.Scene(file)?.Instantiate() is not Node3D ship) continue;
            var hold = new Node3D { Position = at with { Y = (float)WorldGen.WaterLevel - 1.6f }, Rotation = new Vector3(0, yaw, 0) };
            AddChild(hold);
            hold.AddChild(ship);
            // Riding the swell: a slow roll and pitch, each ship its own.
            var t = hold.CreateTween().SetLoops();
            var (roll, pitch, period) = (0.025f + at.X % 3 * 0.005f, 0.012f, 4.5f + at.Z % 2);
            t.TweenProperty(ship, "rotation", new Vector3(pitch, 0, roll), period).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(ship, "rotation", new Vector3(-pitch, 0, -roll), period).SetTrans(Tween.TransitionType.Sine);
        }
    }

    /// <summary>The Exchange's market board: a notice board on two posts, papered with prices and contracts.</summary>
    void Board(Vector3 at)
    {
        var root = new StaticBody3D { Position = at with { Y = City.FloorAt(at) }, Rotation = new Vector3(0, Mathf.Atan2(at.X, at.Z), 0) };
        AddChild(root);
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#6a4a2e"), Roughness = 0.9f };
        foreach (var x in new[] { -1.1f, 1.1f })
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 2.4f, 0.14f) }, MaterialOverride = wood, Position = new Vector3(x, 1.2f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.4f, 1.3f, 0.08f) }, MaterialOverride = wood, Position = new Vector3(0, 1.65f, 0) });
        var rng = new RandomNumberGenerator { Seed = 7 };
        for (var i = 0; i < 9; i++)
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(rng.RandfRange(0.3f, 0.45f), rng.RandfRange(0.35f, 0.5f), 0.01f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(i % 3 == 0 ? "#efe2c0" : "#e6d6b0"), Roughness = 1 },
                Position = new Vector3(-0.9f + i % 5 * 0.45f, 1.95f - i / 5 * 0.6f + rng.RandfRange(-0.05f, 0.05f), 0.05f),
                RotationDegrees = new Vector3(0, 0, rng.RandfRange(-6, 6)),
            });
        root.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.4f, 2.4f, 0.3f) }, Position = new Vector3(0, 1.2f, 0) });
        AddChild(new Label3D
        {
            Text = "The Exchange: prices, contracts, works", Position = at with { Y = City.FloorAt(at) + 2.9f }, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 40, OutlineSize = 10, PixelSize = 0.012f, Modulate = new Color("#ffd27a"),
        });
    }

    void Sign(Vector3 at, string text) => AddChild(new Label3D
    {
        Text = text, Position = at with { Y = at.Y + City.FloorAt(at) }, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FontSize = 48, OutlineSize = 10, PixelSize = 0.014f, Modulate = new Color("#ffd27a"),
    });

    /// <summary>The city's days turn over as you play; what happens is in the log, and the big things on screen.</summary>
    void TendCity(double delta)
    {
        if ((_cityTick -= delta) > 0) return;
        _cityTick = 1;
        if (_sharedCity && _client?.Status != ClientStatus.Connected) _sharedCity = false; // off the server: the city goes on here
        if (_sharedCity) return; // the server runs the days, and says what happened
        foreach (var news in _city.Advance(_guilds.PlaySeconds))
        {
            _hud.Log($"Tristinwold: {news}");
            if (!news.EndsWith("that's over now.")) _hud.Toast($"News from the Exchange: {news}", 8);
        }
    }

    /// <summary>Lines for the HUD's tracker: dock work and an errand under way.</summary>
    IEnumerable<string> TownLines(Vector3 pos)
    {
        if (_shift is { } s)
            yield return _holding
                ? $"Dock work: carry the crate to the warehouse by the east gate ({Direction(pos, (Warehouse.X, Warehouse.Z))}). {s.Left} to go, {_city.DockWage} crowns each."
                : $"Dock work: lift a crate at the end of the middle pier ({Direction(pos, (CrateStack.X, CrateStack.Z))}). {s.Left} to go, {_city.DockWage} crowns each.";
        if (_errand is { } e && IsInstanceValid(e.To))
            yield return $"Errand: take the Exchange's letter to {e.To.Npc.Name} ({Direction(pos, (e.To.GlobalPosition.X, e.To.GlobalPosition.Z))}), {e.Pay} crowns.";
    }

    /// <summary>What there is to do here with E, besides talking and taking: fish, mine, fell, lift and set down cargo.</summary>
    (string Verb, Action Act)? TownAction(Vector3 pos)
    {
        if (_shift is not null && !_holding && pos.DistanceTo(CrateStack with { Y = pos.Y }) < 3.5f) return ("lift a crate", LiftCrate);
        if (_holding && pos.DistanceTo(Warehouse with { Y = pos.Y }) < 4) return ("set the crate down", SetCrateDown);
        if (_working) return null;
        if (_workings.Within(pos) is { } spot)
        {
            var (job, tool) = spot.IsTree ? (Job.Woodcutting, "woodcutters_axe") : (Job.Mining, "pickaxe");
            var verb = spot.IsTree ? $"fell the {Gathering.Tree(spot.Kind).Name.ToLowerInvariant()}" : $"mine the {Gathering.Vein(spot.Kind).Name.ToLowerInvariant()}";
            return (_player.Pack.Count(tool) > 0 ? verb : $"{verb} (needs a {Items.Get(tool)!.Name.ToLowerInvariant()})", () => Work(spot, job, tool));
        }
        if (_player.Pack.Count("fishing_rod") > 0 && WaterAhead() is { } water) return ("cast a line", () => Cast(water.At, water.Kind));
        return null;
    }

    // ---- fishing ----------------------------------------------------------------------------------------------

    enum Line { None, Waiting, Bite }

    Line _line;
    double _lineTimer;
    Vector3 _castFrom, _bobberAt;
    Water _water;
    Node3D? _bobber, _rod;
    MeshInstance3D? _lineMesh;

    public bool LineOut => _line != Line.None;
    public bool Biting => _line == Line.Bite;

    /// <summary>The water the hero faces, within a cast (or to either side, on a pier), and what kind it is.</summary>
    public (Vector3 At, Water Kind)? WaterAhead()
    {
        var p = _player.GlobalPosition;
        if (p.Y - WorldGen.WaterLevel > 4 || _player.Swimming) return null;
        var forward = -_player.GlobalTransform.Basis.Z with { Y = 0 };
        foreach (var dir in new[] { forward, forward.Rotated(Vector3.Up, 0.6f), forward.Rotated(Vector3.Up, -0.6f) })
            for (var d = 2.5f; d <= 9; d += 1)
            {
                var q = p + dir.Normalized() * d;
                if (_world.Height(q.X, q.Z) > WorldGen.WaterLevel - 0.35 || City.OnPier(q)) continue;
                var kind = _world.BiomeAt(q.X, q.Z) is Biome.Tundra or Biome.Snowfield ? Water.Cold
                    : new Vector2(q.X - 200, q.Z).Length() < 260 ? Water.Sea : Water.Fresh;
                return (q with { Y = (float)WorldGen.WaterLevel + 0.02f }, kind);
            }
        return null;
    }

    /// <summary>E while fishing: strike when the float's under, else reel in.</summary>
    (string Verb, Action Act)? FishingAction() => _line switch
    {
        Line.Bite => ("STRIKE!", Strike),
        Line.Waiting => ("reel in", () => EndLine("You reel in.")),
        _ => null,
    };

    void Cast(Vector3 at, Water kind)
    {
        if (_tracker.InFight) { _hud.Toast("Not with enemies about.", 3); return; }
        _player.Face(at);
        Hands()?.Perform("Throw", 0.6, 1.2f);
        _rod = Hands()?.Carry("rod");
        (_line, _water, _bobberAt, _castFrom) = (Line.Waiting, kind, at, _player.GlobalPosition);
        _lineTimer = Fishing.BiteDelay(_player.Pack.Level(Job.Fishing), _lootRng) * WeatherBite;
        _bobber = new Node3D { Position = at };
        var red = new StandardMaterial3D { AlbedoColor = new Color("#d83a2a"), Roughness = 0.5f };
        var white = new StandardMaterial3D { AlbedoColor = new Color("#f2ece0"), Roughness = 0.5f };
        _bobber.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f }, MaterialOverride = white });
        _bobber.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f, IsHemisphere = true }, MaterialOverride = red, Position = new Vector3(0, 0.005f, 0) });
        AddChild(_bobber);
        _lineMesh = new MeshInstance3D { Mesh = new ImmediateMesh(), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.85f, 0.7f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_lineMesh);
        Sound.At("melee/swing", _player.GlobalPosition + Vector3.Up, -6, 0.7f);
        GetTree().CreateTimer(0.4).Timeout += () => { if (_line != Line.None) { Sound.At("move/land", at, -10, 1.6f); Ripple(at); } };
        _hud.Toast($"You cast into the {(kind == Water.Sea ? "harbour" : kind == Water.Cold ? "icy water" : "water")}. Wait for the float to go under, then press E.", 4);
    }

    /// <summary>Each frame the line's out: the float bobs, the line hangs from the rod's tip, and the fish bites (or gets away).</summary>
    void TendLine(double delta)
    {
        if (_line == Line.None) return;
        if (_player.GlobalPosition.DistanceTo(_castFrom) > 2 || _player.Downed || _tracker.InFight) { EndLine(_tracker.InFight ? "You drop the rod to fight." : "You reel in."); return; }
        var t = (float)Time.GetTicksMsec() / 1000;
        if (_bobber is not null)
            _bobber.Position = _bobberAt + Vector3.Up * (_line == Line.Bite ? -0.12f + 0.04f * Mathf.Sin(t * 30) : 0.02f * Mathf.Sin(t * 2.2f));
        if (_lineMesh?.Mesh is ImmediateMesh mesh && _bobber is not null)
        {
            var tip = _rod is not null ? _rod.GlobalTransform * new Vector3(0, 2.15f, 0.68f) : _player.GlobalPosition + Vector3.Up * 2.4f;
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
            for (var i = 0; i <= 12; i++)
            {
                var k = i / 12f;
                mesh.SurfaceAddVertex(tip.Lerp(_bobber.GlobalPosition, k) + Vector3.Down * Mathf.Sin(k * Mathf.Pi) * 0.4f); // a little sag
            }
            mesh.SurfaceEnd();
        }
        if ((_lineTimer -= delta) > 0) return;
        if (_line == Line.Waiting)
        {
            _line = Line.Bite;
            _lineTimer = Fishing.Window(_player.Pack.Level(Job.Fishing));
            Sound.At("magic/poison", _bobberAt, -4, 1.3f);
            Ripple(_bobberAt);
            _hud.Toast(TouchMode ? "A bite! Tap Use!" : "A bite! Press E!", 1.5);
        }
        else
        {
            _line = Line.Waiting;
            _lineTimer = Fishing.BiteDelay(_player.Pack.Level(Job.Fishing), _lootRng);
            _hud.Toast("It got away. The float's back up: wait for another bite.", 3);
        }
    }

    void Strike()
    {
        if (_line != Line.Bite) return;
        var level = _player.Pack.Level(Job.Fishing);
        var id = Fishing.Catch(_water, level, _lootRng);
        _player.Pack.Add(id);
        var up = _player.Pack.Practise(Job.Fishing, Fishing.Xp(id));
        Hands()?.Perform("Cheer", 0.7);
        Sound.At("move/land", _bobberAt, -4, 1.4f);
        Sound.Ui("pickup");
        Ripple(_bobberAt);
        var item = Items.Get(id)!;
        if (id != "old_boot") Did("fish");
        if (id == "pearl") Did("pearl");
        EndLine(id == "old_boot" ? "You haul in... an old boot." : $"You land {(item.Name.StartsWith('A') ? "an" : "a")} {item.Name.ToLowerInvariant()}! ({_city.Offer(item)} crowns at market today.)" +
            (up is { } l ? $" Fishing rises to {l}!" : ""));
        Save();
    }

    void EndLine(string? say)
    {
        _line = Line.None;
        _bobber?.QueueFree();
        _lineMesh?.QueueFree();
        _rod?.QueueFree();
        (_bobber, _lineMesh, _rod) = (null, null, null);
        if (say is not null) _hud.Toast(say, 4);
    }

    void Ripple(Vector3 at)
    {
        var mat = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1, 0.6f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1, Rings = 32, RingSegments = 4 }, MaterialOverride = mat, Position = at + Vector3.Up * 0.03f, Scale = new Vector3(0.1f, 0.05f, 0.1f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(ring);
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "scale", new Vector3(1.2f, 0.05f, 1.2f), 0.9);
        tw.TweenProperty(mat, "albedo_color", new Color(1, 1, 1, 0), 0.9);
        tw.Chain().TweenCallback(Callable.From(ring.QueueFree));
    }

    Figure? Hands() => _player.Figure as Figure;

    static string TradeName(Job j) => j switch { Job.Taming => "Beast taming", _ => j.ToString() };

    // ---- mining and woodcutting -------------------------------------------------------------------------------

    async void Work(WorkSpot spot, Job job, string tool)
    {
        if (_player.Pack.Count(tool) == 0)
        {
            Sound.Ui("error");
            _hud.Toast(job == Job.Mining ? "You need a pickaxe: Dagna Flint sells them at the Greyhollow quarry." : "You need a woodcutter's axe: Hollin Ashby sells them at his woodlot.", 5);
            return;
        }
        var level = _player.Pack.Level(job);
        var need = job == Job.Mining ? Gathering.Vein(spot.Kind).Level : Gathering.Tree(spot.Kind).Level;
        if (level < need) { Sound.Ui("error"); _hud.Toast($"That takes {job.ToString().ToLowerInvariant()} {need}; you're {level}.", 4); return; }
        _working = true;
        _player.Face(spot.At);
        var held = Hands()?.Carry(job == Job.Mining ? "pick" : "axe");
        var strokes = Gathering.Strokes(level);
        for (var i = 0; i < strokes; i++)
        {
            Hands()?.Perform("2H_Melee_Attack_Chop", 0.7, 1.3f);
            await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this)) return;
            var hit = spot.At + Vector3.Up * (spot.IsTree ? 1.1f : 0.6f);
            Sound.At(spot.IsTree ? "melee/arrow_hit" : "melee/hit_metal", hit, -4, spot.IsTree ? 0.8f : 0.7f);
            Effects.Impact(this, hit, hit - _player.GlobalPosition, DamageKind.Physical, false);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        }
        held?.QueueFree();
        _working = false;
        List<(string Id, int N)> got;
        int xp;
        if (spot.IsTree)
        {
            var tree = Gathering.Tree(spot.Kind);
            got = [(tree.Log, Gathering.Logs(level, _lootRng))];
            xp = Gathering.WoodXp(tree);
        }
        else
        {
            var vein = Gathering.Vein(spot.Kind);
            got = Gathering.Mine(vein, level, _lootRng);
            xp = Gathering.MiningXp(vein);
        }
        foreach (var (id, n) in got) _player.Pack.Add(id, n);
        var up = _player.Pack.Practise(job, xp);
        _herbs.Worked(spot.Id, _guilds.PlaySeconds);
        _workings.Worked(spot);
        if (!spot.IsTree) MineWorked(spot);
        ForestWorked(spot, spot.At);
        Did(spot.IsTree ? "trees" : "veins");
        Sound.Ui("pickup");
        _hud.Toast($"{(spot.IsTree ? "Timber!" : "You break it open:")} {Loot.Describe(got)}.{(up is { } l ? $" {job} rises to {l}!" : "")}", 4);
        Save();
    }

    public bool Working => _working;

    // ---- dock work --------------------------------------------------------------------------------------------

    (int Left, int Done)? _shift;
    bool _holding;
    Node3D? _crate;

    public bool OnShift => _shift is not null;
    public bool Holding => _holding;

    void StartShift(Person foreman)
    {
        if (_shift is not null) { Say(foreman, $"You've {_shift.Value.Left} crates still to shift. Get on with it."); return; }
        _shift = (6, 0);
        Say(foreman, $"Six crates off the end of the middle pier, into the warehouse by the gate. {_city.DockWage} crowns a crate, and five more when you're done. Lift with your legs.");
    }

    void LiftCrate()
    {
        _holding = true;
        _player.Burdened = true;
        if (Models.Scene("ph:crate.glb")?.Instantiate() is Node3D crate)
        {
            crate.Scale = Vector3.One * 1.1f;
            crate.Position = new Vector3(0, 1.0f, -0.6f);
            _player.AddChild(crate);
            _crate = crate;
        }
        Sound.At("move/land", _player.GlobalPosition, -6, 0.8f);
    }

    void SetCrateDown()
    {
        if (_shift is not { } s) return;
        _holding = false;
        _player.Burdened = false;
        _crate?.QueueFree();
        _crate = null;
        var wage = (int)Math.Round(_city.DockWage * PerkPay);
        _player.Crowns += wage;
        // Whatever was in it is in the city's stores now.
        var cargo = _lootRng.Pick(new[] { Good.Fish, Good.Timber, Good.Iron, Good.Leather, Good.Ale, Good.Bread, Good.Herbs });
        _city.Stock[cargo] = _city.Stock.GetValueOrDefault(cargo) + 2;
        Share(CityAction.Cargo(cargo, 2));
        _city.Renown += 1;
        s = (s.Left - 1, s.Done + 1);
        Did("crates");
        Sound.Ui("coin", -2);
        if (s.Left > 0) { _shift = s; _hud.Toast($"+{wage} crowns ({CityEconomy.Info(cargo).Name}). {s.Left} to go.", 3); }
        else
        {
            _shift = null;
            _player.Crowns += 5;
            _hud.Toast($"+{wage} crowns, and Haskel's five for a full shift. Good work.", 5);
        }
        Save();
    }

    // ---- errands for the Exchange -----------------------------------------------------------------------------

    (Person To, int Pay)? _errand;
    public Person? ErrandTo => _errand?.To;

    void OfferErrand(Person factor)
    {
        if (_errand is { } e && IsInstanceValid(e.To)) { Say(factor, $"You've my letter for {e.To.Npc.Name} still. They're {Direction(factor.GlobalPosition, (e.To.GlobalPosition.X, e.To.GlobalPosition.Z))} of here."); return; }
        var within = _townsfolk.Concat(_places.People).Where(p => IsInstanceValid(p) && p != factor && !p.Captive && new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z).Length() < City.OuterRadius + 30).ToList();
        if (within.Count == 0) { Say(factor, "Nothing to carry today."); return; }
        var to = within[_lootRng.Next(within.Count)];
        var pay = 6 + (int)(to.GlobalPosition.DistanceTo(factor.GlobalPosition) / 10);
        _errand = (to, pay);
        Say(factor, $"A sealed letter for {to.Npc.Name}, {to.Npc.Occupation}. Don't open it. {pay} crowns when it's delivered.");
    }

    /// <summary>Talking to the one an errand's for: the letter's delivered and paid for.</summary>
    bool DeliverErrand(Person person)
    {
        if (_errand is not { } e || e.To != person) return false;
        _errand = null;
        _player.Crowns += (int)Math.Round(e.Pay * PerkPay);
        _city.Renown += 2;
        Sound.Ui("coin");
        _hud.Toast($"Letter delivered: +{e.Pay} crowns.", 4);
        Did("letters");
        Save();
        return true;
    }

    // ---- the Exchange -----------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> TownTalk(Person person)
    {
        var npc = person.Npc;
        if (_errand is { } e && e.To == person) yield return ("A letter for you, from the Exchange.", () => { DeliverErrand(person); Say(person, "Ah. Thank you. Tell Ottilie I'll answer by the morning."); });
        switch (npc.Id)
        {
            case "tristinwold/factor":
                yield return ("How's trade in the city?", () => _hud.Talk(Who(npc), _city.Report(), ("Back", () => Say(person, "Anything else?"))));
                yield return ($"What's wanted? ({_city.Contracts.Count} contract{(_city.Contracts.Count == 1 ? "" : "s")})", () => ShowContracts(person));
                yield return (_city.Work is { } w ? $"What's the city building? ({w.Title})" : "What's the city building?", () => ShowWorks(person));
                yield return ("Any errands?", () => OfferErrand(person));
                yield return ("What does the Exchange think of me?", () => Say(person,
                    $"You're {_city.RenownTitle} ({_city.Renown} to your name in my ledger). " +
                    (_city.Discount > 0 ? $"Shopkeepers know it: {_city.Discount:P0} off, and a better price for what you sell." : "Do the city some good, and the shopkeepers will hear of it.")));
                break;
            case "tristinwold/foreman":
                yield return (_shift is null ? $"Any work on the docks? ({_city.DockWage} crowns a crate)" : "About the cargo...", () => StartShift(person));
                yield return ("How's the harbour?", () => Say(person, Harbour()));
                break;
            case "tristinwold/corva":
                yield return ("Can I use your kitchen?", () => ShowCraft(Job.Cooking, () => Say(person, "Mind the good knife.")));
                yield return ("An ale, please. (4 crowns)", () => BuyOne(person, "ale", 4));
                break;
        }
        if (Shops.Of(npc.Id) is { Trade: { } trade } && trade != Job.Cooking)
            yield return ($"Teach me about {trade.ToString().ToLowerInvariant()}.", () => Say(person, Advice(trade)));
    }

    string Harbour()
    {
        var fish = _city.Factor(Good.Fish);
        var events = _city.Running.Select(r => CityEconomy.Event(r.Id)).Where(e => e.Id is "storm" or "shoal" or "caravan" or "shipyard").Select(e => e.Title.ToLowerInvariant()).ToList();
        return (events.Count > 0 ? $"There's {string.Join(" and ", events)}. " : "") +
               (fish > 1.3 ? "Fish is dear: the boats aren't bringing enough in." : fish < 0.8 ? "Fish is cheap: the market's swimming in it." : "The boats are in and out, steady.") +
               $" Cargo pays {_city.DockWage} a crate right now." + (_city.WorksDone.Contains("harbour_wall") ? " And since the wall's been mended, we lose fewer boats." : " If the city ever mends the harbour wall, we'd lose fewer boats.");
    }

    string Advice(Job trade) => trade switch
    {
        Job.Fishing => "Get a rod, face open water and cast (E). Watch the float: when it goes under, strike (E again), quick. Herring and mackerel in the harbour, " +
                       "perch and carp in the lakes, char in the cold north; the better you get, the rarer what bites: lobster, salmon, pike, even pearls. " +
                       $"You're a level {_player.Pack.Level(Job.Fishing)} angler. Sell to me, or cook it at the Salt Barrel.",
        Job.Mining => "A pick, and the veins down the mine, through the cutting in the mound: grey stone, rust-red iron, black coal, and a silver seam for the skilled. Out on the crags " +
                      $"there's silver and gold. The smelters take the ore, and a forge turns two into an ingot. You're a level {_player.Pack.Level(Job.Mining)} miner.",
        Job.Woodcutting => "An axe, and the trees with the red band: pine for anyone, oak once you've the knack, yew for the skilled. Shipwrights and " +
                           $"builders want the timber, and the city's always building. You're a level {_player.Pack.Level(Job.Woodcutting)} woodcutter.",
        Job.Farming => "Buy seed from me, take any empty bed (E) and plant it. Potatoes come up in a few minutes; onions and cabbages take longer; the herbs " +
                       "(emberleaf, bluebell, bloodcap, frostbloom, silverthorn) want a surer hand and come up as herbs for the alchemists. Come back when it's ripe and dig it up. " +
                       $"You're a level {_player.Pack.Level(Job.Farming)} grower. Buy a house and you can put a garden in, too.",
        Job.Crafting => "Silver and gold from the forge, a cut stone if you've one, and my bench: rings and amulets that make your blows land harder or turn others' aside. " +
                        $"The loom's for linen: tunics and jerkins. Ask to use the bench any time. You're a level {_player.Pack.Level(Job.Crafting)} crafter.",
        Job.Agility => "Six obstacles: the log, the net, the swing, the stones, the wall, and the ladder and slide. Take them in order for a whole lap, and the bonus. " +
                       $"Fall off and you start again. The quicker you get, the quicker you are everywhere. You're a level {_player.Pack.Level(Job.Agility)} tumbler.",
        _ => "",
    };

    void BuyOne(Person person, string id, int price)
    {
        if (!Serves(person)) return;
        var cost = Shops.Charge(price, ShopMarkup * RepBuy(person));
        if (_player.Crowns < cost) { Sound.Ui("error"); Say(person, "Coin first, love."); return; }
        _player.Crowns -= cost;
        _player.Pack.Add(id);
        _city.Bought(id, cost);
        Share(CityAction.Bought(id, cost));
        Sound.Ui("coin");
        Say(person, "There you go.");
    }

    void ShowContracts(Person factor)
    {
        var options = _city.Contracts.Select(c =>
        {
            var have = _player.Pack.Contents.Where(x => CityEconomy.GoodOf(x.Item.Id) == c.Good).Sum(x => x.N);
            return ($"{Cap(c.Buyer)} want {c.Left} {CityEconomy.Info(c.Good).Name} at {c.PricePer} each, +{c.Bonus} when it's all in; {Math.Max(0, c.Expires - _city.Day + 1)} day(s) left. (You have {have}.)",
                (Action)(() =>
                {
                    // The cheapest of the good go first: nobody wants a golden carp in a fish order by mistake.
                    var ids = _player.Pack.Contents.Where(x => CityEconomy.GoodOf(x.Item.Id) == c.Good && !_player.Pack.Worn.ContainsValue(x.Item.Id))
                        .OrderBy(x => x.Item.Value).SelectMany(x => Enumerable.Repeat(x.Item.Id, x.N)).Take(c.Left).ToList();
                    if (ids.Count == 0) { Sound.Ui("error"); Say(factor, $"You've no {CityEconomy.Info(c.Good).Name} on you."); return; }
                    var finishing = ids.Count >= c.Left;
                    var paid = _city.Deliver(c, ids);
                    Share(CityAction.Deliver(c.Id, ids));
                    foreach (var id in ids) _player.Pack.Take(id);
                    _player.Crowns += (int)Math.Round(paid * PerkPay);
                    Sound.Ui("coin");
                    Save();
                    _hud.Toast($"Delivered {ids.Count}: +{paid} crowns.{(finishing ? " Contract filled!" : "")}", 5);
                    if (finishing) Did("contracts");
                    ShowContracts(factor);
                }));
        }).ToList();
        options.Add(("Back", () => Say(factor, "Anything else?")));
        _hud.Talk(Who(factor.Npc), _city.Contracts.Count == 0 ? "Nothing's short today. Come back tomorrow." :
            "What the city's short of, and who's paying over the odds for it. Bring it here; I pay as it comes in.", [.. options]);
    }

    void ShowWorks(Person factor)
    {
        if (_city.Work is not { } work)
        {
            Say(factor, $"Everything the council planned is built: {string.Join(", ", CityEconomy.Works.Select(w => w.Title.ToLowerInvariant()))}. The city's never looked better.");
            return;
        }
        var needs = _city.WorkNeeds.ToList();
        var coin = work.Crowns - _city.WorkCrowns;
        var text = $"{work.Title}. Still wanted: {(needs.Count == 0 ? "no more goods" : string.Join(", ", needs.Select(n => $"{n.N} {CityEconomy.Info(n.Good).Name}")))}" +
                   $"{(coin > 0 ? $", and {coin} crowns" : "")}. The city puts in its spare stock and coin each day; anything you give speeds it along, and the city remembers. " +
                   (_city.WorksDone.Count > 0 ? $"Already built: {string.Join(", ", _city.WorksDone.Select(id => CityEconomy.Works.First(w => w.Id == id).Title.ToLowerInvariant()))}." : "");
        var options = new List<(string, Action)>();
        foreach (var (good, n) in needs)
        {
            var have = _player.Pack.Contents.Where(x => CityEconomy.GoodOf(x.Item.Id) == good).ToList();
            if (have.Count == 0) continue;
            options.Add(($"Give {CityEconomy.Info(good).Name} ({have.Sum(x => x.N)} carried, {n} wanted)", () =>
            {
                var ids = have.OrderBy(x => x.Item.Value).SelectMany(x => Enumerable.Repeat(x.Item.Id, x.N)).Take(n).ToList();
                var taken = _city.Donate(ids, out var done);
                Share(CityAction.Donate(ids.Take(taken).ToList()));
                foreach (var id in ids.Take(taken)) _player.Pack.Take(id);
                if (taken > 0) { Did("donations"); WellDone(1); }
                Sound.Ui("pickup");
                _hud.Toast($"You give {taken} {CityEconomy.Info(good).Name} to the works.{(done is not null ? " " + done : "")}", 6);
                Save();
                ShowWorks(factor);
            }));
        }
        foreach (var gift in new[] { 25, 100 }.Where(g => coin > 0 && _player.Crowns >= g))
            options.Add(($"Give {Math.Min(gift, coin)} crowns", () =>
            {
                var taken = _city.DonateCrowns(Math.Min(gift, coin), out var done);
                Share(CityAction.DonateCrowns(taken));
                _player.Crowns -= taken;
                if (taken > 0) { Did("donations"); WellDone(1); }
                Sound.Ui("coin");
                if (done is not null) _hud.Toast(done, 8);
                Save();
                ShowWorks(factor);
            }));
        options.Add(("Back", () => Say(factor, "Anything else?")));
        _hud.Talk(Who(factor.Npc), text, [.. options]);
    }

    // ---- for the self-test ------------------------------------------------------------------------------------

    public void StartShiftForTest() => _shift = (6, 0);
    public void LiftForTest() => LiftCrate();
    public void SetDownForTest() => SetCrateDown();
    public void CastForTest((Vector3 At, Water Kind) w) => Cast(w.At, w.Kind);
    public void StrikeForTest() => Strike();
    public void CityForTest(CityAction a) { _city.Apply(a); Share(a); }
    public void WorkForTest(WorkSpot spot) => Work(spot, spot.IsTree ? Job.Woodcutting : Job.Mining, spot.IsTree ? "woodcutters_axe" : "pickaxe");
    public void ErrandForTest(Person factor) => OfferErrand(factor);
    public bool DeliverForTest(Person to) => DeliverErrand(to);
}

/// <summary>Somewhere to mine or fell: a mine vein, a wild outcrop, or a woodlot tree.</summary>
public sealed record WorkSpot(string Id, string Kind, bool IsTree, Vector3 At);

/// <summary>
/// The veins of the mine and the crags and the trees of the woodlot, shown near the hero. A worked one is gone (a
/// felled tree leaves a stump) until it comes back.
/// </summary>
public partial class Workings(Main main) : Node3D
{
    readonly Dictionary<string, (WorkSpot Spot, Node3D Body)> _shown = [];
    double _tick;

    public const float Reach = 2.8f;

    /// <summary>The mine's veins in its chambers' walls, and the woodlot's marked trees in rows.</summary>
    public static readonly IReadOnlyList<WorkSpot> Fixed = Build();

    static List<WorkSpot> Build()
    {
        var spots = new List<WorkSpot>();
        foreach (var seam in Greyhollow.Seams)
        {
            var (x, z, _) = Greyhollow.SeamAt(seam);
            spots.Add(new WorkSpot(seam.Id, seam.Kind, false, Cave.At(x, z)));
        }
        // The logging camp: a stand in rows at the wood's edge, cut and cut again.
        for (var i = 0; i < 16; i++)
            spots.Add(new WorkSpot($"camp_{i}", i % 5 == 4 ? "oak" : "pine", true, new Vector3((float)Northwood.CampX - 18 + i % 4 * 12, 0, (float)Northwood.CampZ - 15 + i / 4 * 10)));
        string[] trees = ["pine", "pine", "oak", "pine", "oak", "yew", "pine", "oak", "pine", "oak", "yew", "pine"];
        for (var i = 0; i < trees.Length; i++)
            spots.Add(new WorkSpot($"woodlot_{i}", trees[i], true, Main.WoodlotAt + new Vector3(-9 + i % 4 * 5.5f + (i / 4 % 2) * 2.5f, 0, -8 + i / 4 * 6)));
        return spots;
    }

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var p = main.PlayerNode.GlobalPosition;
        var now = main.GuildBook.PlaySeconds;
        var below = main.InMine; // the mine's veins down the mine, and everything else on the surface
        var near = Fixed.Where(s => s.At.Y > Cave.Threshold == below && s.At.DistanceTo(p with { Y = s.At.Y }) < 110)
            .Concat(below ? [] : Outcrops.Near(main.World, p.X, p.Z, 110).Select(o => new WorkSpot(o.Id, o.VeinId, false, new Vector3((float)o.X, 0, (float)o.Z))))
            .ToDictionary(s => s.Id);
        foreach (var id in _shown.Keys.Where(id => !near.ContainsKey(id) || Ready(near[id], now) != !_shown[id].Body.HasMeta("stump")).ToList())
        {
            _shown[id].Body.QueueFree();
            _shown.Remove(id);
        }
        foreach (var (id, spot) in near)
        {
            if (_shown.ContainsKey(id) || !below && !main.TerrainNode.HasGround(spot.At)) continue;
            var ready = Ready(spot, now);
            if (!ready && !spot.IsTree) continue; // a worked vein is just rock until it comes back
            var at = below ? spot.At : spot.At with { Y = City.FloorAt(spot.At) };
            var body = ready ? (spot.IsTree ? Tree(spot) : Vein(spot)) : Stump(spot);
            body.Position = at;
            _shown[id] = (spot, body);
        }
    }

    /// <summary>The camp's stand grows back every other day; everything else in eight minutes.</summary>
    bool Ready(WorkSpot s, double now) => main.HerbLog.Ready(s.Id, now, s.Id.StartsWith("camp_") ? 2 * CityEconomy.DaySeconds : Gathering.RegrowSeconds);

    /// <summary>The vein or tree within reach of a spot, if any (not a stump).</summary>
    public WorkSpot? Within(Vector3 at) => _shown.Values.Where(v => !v.Body.HasMeta("stump") && v.Body.GlobalPosition.DistanceTo(at with { Y = v.Body.GlobalPosition.Y }) < Reach + (v.Spot.IsTree ? 0.2f : 0.6f))
        .OrderBy(v => v.Body.GlobalPosition.DistanceTo(at)).Select(v => v.Spot).FirstOrDefault();

    public IEnumerable<WorkSpot> Shown => _shown.Values.Where(v => !v.Body.HasMeta("stump")).Select(v => v.Spot);

    /// <summary>A vein's been worked, or a tree felled: the tree topples (and leaves a stump), the vein crumbles.</summary>
    public void Worked(WorkSpot s)
    {
        if (!_shown.Remove(s.Id, out var v)) return;
        if (s.IsTree)
        {
            var away = (v.Body.GlobalPosition - main.PlayerNode.GlobalPosition) with { Y = 0 };
            var axis = away.LengthSquared() > 0.01f ? away.Normalized().Cross(Vector3.Up) : Vector3.Right;
            var tw = v.Body.CreateTween();
            tw.TweenProperty(v.Body, "quaternion", new Quaternion(-axis, Mathf.DegToRad(85)), 1.3).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(() => { Sound.At("magic/fire_boom", v.Body.GlobalPosition, -2, 0.5f); Effects.Smoke(main, v.Body.GlobalPosition + away.Normalized() * 3, 1.2f); }));
            tw.TweenInterval(0.8);
            tw.TweenCallback(Callable.From(v.Body.QueueFree));
            _shown[s.Id] = (s, Stump(s));
            _shown[s.Id].Body.Position = s.At with { Y = City.FloorAt(s.At) };
        }
        else
        {
            Effects.Burst(main, v.Body.GlobalPosition + Vector3.Up * 0.5f, DamageKind.Physical, 1.4f);
            v.Body.QueueFree();
        }
    }

    Node3D Vein(WorkSpot s)
    {
        var vein = Gathering.Vein(s.Kind);
        var n = new Node3D { Name = s.Id };
        AddChild(n);
        n.Position = s.At with { Y = City.FloorAt(s.At) };
        var rng = new RandomNumberGenerator { Seed = (ulong)WorldGen.StableHash(s.Id) };
        var size = 1.9f + rng.Randf() * 0.5f;
        Models.Place(n, "ph:boulder_small.glb", Vector3.Zero, size, rng.Randf() * Mathf.Tau);
        // The ore showing in the rock: chunks of colour, and a glint for silver and gold.
        var shine = s.Kind is "silver" or "gold";
        var ore = new StandardMaterial3D
        {
            AlbedoColor = new Color(vein.Colour), Roughness = shine ? 0.25f : 0.8f, Metallic = shine ? 0.8f : 0.1f,
            EmissionEnabled = shine, Emission = new Color(vein.Colour), EmissionEnergyMultiplier = 0.25f,
        };
        for (var i = 0; i < 9; i++)
        {
            var a = rng.Randf() * Mathf.Tau;
            var r = rng.RandfRange(0.15f, 0.55f);
            n.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = Vector3.One * rng.RandfRange(0.12f, 0.24f) * size },
                MaterialOverride = ore,
                Position = new Vector3(Mathf.Cos(a) * r * size * 0.9f, size * rng.RandfRange(0.3f, 0.5f), Mathf.Sin(a) * r * size * 0.45f),
                RotationDegrees = new Vector3(rng.RandfRange(0, 90), rng.RandfRange(0, 90), rng.RandfRange(0, 90)),
            });
        }
        n.AddChild(new Label3D
        {
            Text = vein.Name, Position = Vector3.Up * (size + 0.6f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, OutlineSize = 8,
            PixelSize = 0.01f, Modulate = new Color(1, 1, 1, 0.8f), VisibilityRangeEnd = 18,
        });
        return n;
    }

    Node3D Tree(WorkSpot s)
    {
        var timber = Gathering.Tree(s.Kind);
        var n = new StaticBody3D { Name = s.Id };
        AddChild(n);
        n.Position = s.At with { Y = City.FloorAt(s.At) };
        var variant = (int)(WorldGen.StableHash(s.Id) % Foliage.Variants);
        if (Foliage.Available)
            n.AddChild(new MeshInstance3D { Mesh = Foliage.Tree(s.Kind == "pine" ? Foliage.Kind.Fir : s.Kind == "yew" ? Foliage.Kind.Autumn : Foliage.Kind.Green, variant, main.TerrainNode.Fancy), Scale = Vector3.One * (s.Kind == "oak" ? 1.0f : 0.9f) });
        else Models.Place(n, "medieval/nature/tree_single_A.gltf", Vector3.Zero, 3, 0, solid: false);
        n.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.35f, Height = 4 }, Position = Vector3.Up * 2 });
        // The forester's red band: this one can come down.
        n.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.18f, CapTop = false, CapBottom = false },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#b8302a"), Roughness = 0.9f }, Position = Vector3.Up * 1.4f,
        });
        n.AddChild(new Label3D
        {
            Text = timber.Name, Position = Vector3.Up * 2.6f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, OutlineSize = 8,
            PixelSize = 0.01f, Modulate = new Color(1, 1, 1, 0.8f), VisibilityRangeEnd = 18,
        });
        return n;
    }

    Node3D Stump(WorkSpot s)
    {
        var n = new Node3D { Name = s.Id + "_stump" };
        n.SetMeta("stump", true);
        AddChild(n);
        Models.Place(n, "ph:stump.glb", Vector3.Zero, 0.5f, WorldGen.StableHash(s.Id) % 6, solid: false);
        return n;
    }
}
