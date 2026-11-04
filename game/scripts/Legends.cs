using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The two great blades. Rubilacxe stands in the stone at Tristinwold's heart, for the one who is good and has brought down
/// the fifth guardian. Arachnicalibur hangs at the Dread Pirate Soren's hip, for the wicked hand that can take it from him
/// when he comes ashore to lay siege to a town. There's one of each on a server, and when they're gone, they're gone.
/// </summary>
public partial class Main
{
    /// <summary>Where the stone stands, in the middle of the town.</summary>
    public static readonly Vector3 BladeStone = new(64, 0, -2);
    Node3D? _stoneBlade;
    Legends? _legends;

    public Legends? LegendsNode => _legends;

    void AddLegends()
    {
        AddChild(_legends = new Legends(this));
        Sign(BladeStone + Vector3.Up * 5, "The Stone");
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#6e6c66"), Roughness = 1 };
        var at = BladeStone with { Y = City.FloorAt(BladeStone) };
        var body = new StaticBody3D { Position = at };
        body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.25f, Height = 1.7f, RadialSegments = 14, Rings = 6 }, MaterialOverride = stone, Position = Vector3.Up * 0.5f, Scale = new Vector3(1.2f, 1, 1) });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.25f, Height = 1.4f }, Position = Vector3.Up * 0.6f });
        AddChild(body);
        var steel = new StandardMaterial3D { AlbedoColor = new Color("#c8d4e0"), Metallic = 0.9f, Roughness = 0.25f };
        var gold = new StandardMaterial3D { AlbedoColor = new Color("#d8b050"), Metallic = 0.85f, Roughness = 0.3f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color("#2e2820"), Roughness = 0.8f };
        _stoneBlade = new Node3D { Position = at + Vector3.Up * 1.1f, Rotation = new Vector3(0.1f, 0, -0.08f) };
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.09f, 1.5f, 0.02f) }, MaterialOverride = steel, Position = Vector3.Up * 0.65f });
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.06f, 0.08f) }, MaterialOverride = gold, Position = Vector3.Up * 1.42f });
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.028f, Height = 0.36f, RadialSegments = 10 }, MaterialOverride = dark, Position = Vector3.Up * 1.62f });
        _stoneBlade.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.075f, Height = 0.15f, RadialSegments = 12, Rings = 8 }, MaterialOverride = gold, Position = Vector3.Up * 1.84f });
        _stoneBlade.AddChild(new OmniLight3D { Position = Vector3.Up * 1, LightColor = new Color("#d8e8ff"), LightEnergy = 1.1f, OmniRange = 7 });
        AddChild(_stoneBlade);
    }

    /// <summary>Why this hero can't draw the sword, or null if they can.</summary>
    public string? WhyNotBlade() =>
        !_forest.DragonSlayer ? "It doesn't so much as shift. Cut in the stone, worn nearly smooth: \"Who ends the fifth guardian may try me.\""
        : _standing.Outlaw || _standing.Repute < Legend.BladeRepute ? "It moves. Then it goes cold in your hand, and sets like iron in ice. Whatever is in it knows what you've done, and wants better."
        : null;

    (string Verb, Action Act)? BladeAction(Vector3 pos)
    {
        if (_city.BladeHolder is not null || pos.DistanceTo(BladeStone with { Y = pos.Y }) > 3.2f) return null;
        return ("try to draw the sword from the stone", DrawBlade);
    }

    public void DrawBlade()
    {
        if (WhyNotBlade() is { } why) { Sound.Ui("error"); _hud.Toast(why, 9); return; }
        DoCity(CityAction.Blade(Legend.Rubilacxe, _player.Company));
        Take([(Legend.Rubilacxe, 1), (Legend.RubilacxeScabbard, 1)]);
        _standing.Good(20, "drew Rubilacxe from the stone");
        Sound.Ui("quest");
        _hud.Toast("The stone lets go as if it had only been waiting to be asked. Rubilacxe comes up into the light, and every bell in Tristinwold rings at once with no hand on the rope.", 12);
        Save();
    }

    /// <summary>The stone's blade shows till it's drawn, whoever drew it.</summary>
    void TendStone()
    {
        if (_stoneBlade is not null) _stoneBlade.Visible = _city.BladeHolder is null;
    }
}

/// <summary>
/// The Dread Pirate Soren ashore: while a town's under siege, he and his crew are at its gates for whoever comes. Like
/// Turlin, he won't stay dead, and each time less of the man comes back. He's the whole server's enemy, so what's left of
/// him is too. At a quarter of his blood he goes for his ship, and that only drives him off; killing him takes catching him.
/// One siege in three, Lu Wukong comes with him, on the town's side: another who won't stay dead, and doesn't leave when he's
/// hurt. Help him hold, and when it's over he'll offer you a berth (see <see cref="PirateCrew.DrunkenMonkey"/>).
/// </summary>
public partial class Legends(Main main) : Node
{
    const string Reckoning = "reckoning", Monkey = "monkey";
    Enemy? _soren, _lu;
    readonly List<Enemy> _crew = [], _hands = [];
    double _tick, _leaving;
    Vector3 _away;
    bool _hunting, _struck, _lingering, _offered;

    public Enemy? Soren => _soren;
    public Enemy? Lu => _lu;
    public IReadOnlyList<Enemy> Hands => _hands;
    public IReadOnlyList<Enemy> Crew => _crew;
    /// <summary>The chance the black blade drops (the self-test sets it, to see it happen).</summary>
    public double WebChance { get; set; } = Legend.WebChance;

    public override void _Ready()
    {
        Enemy.Slain += OnSlain;
        main.PlayerNode.WentDown += OnHeroDown;
    }

    public override void _ExitTree()
    {
        Enemy.Slain -= OnSlain;
        main.PlayerNode.WentDown -= OnHeroDown;
    }

    public override void _Process(double delta)
    {
        WatchSoren(delta);
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var city = main.Economy;
        if (_lingering) { TendOffer(); return; }
        if (_hunting)
        {
            if (_soren is null || new Vector2(main.PlayerNode.GlobalPosition.X - _soren.GlobalPosition.X, main.PlayerNode.GlobalPosition.Z - _soren.GlobalPosition.Z).Length() > 220) Clear();
            return;
        }
        if (city.Siege is not { } siege)
        {
            if (LuStanding) { SorenGone(); return; }
            Clear();
            TendHunt();
            return;
        }
        if (_soren is not null) return;
        var (x, z) = siege.Town == ReputationBook.Capital ? (150.0, 0.0) : main.World.Find(siege.Town) is { } town ? (town.X, town.Z) : (double.NaN, 0);
        if (double.IsNaN(x)) return;
        var at = new Vector3((float)x, 0, (float)z);
        var p = main.PlayerNode.GlobalPosition;
        if (new Vector2(p.X - at.X, p.Z - at.Z).Length() > 150 || !main.TerrainNode.HasGround(at)) return;
        Muster(at + new Vector3(0, 0, 40));
    }

    /// <summary>Whoever took his blade doesn't get to keep it in peace: he comes for them, out where there are no walls.</summary>
    void TendHunt()
    {
        var hero = main.PlayerNode;
        if (main.Economy.WebHolder != hero.Company || main.GuildBook.PlaySeconds < main.ForestLog.SorenHuntAt) return;
        var p = hero.GlobalPosition;
        if (hero.Downed || hero.InFight || main.HudNode.Talking || City.Below is not null || City.Afloat is not null || main.InTownForTurlin(p) || GD.Randf() >= 1f / 45) return;
        Hunt();
    }

    void Hunt()
    {
        var hero = main.PlayerNode;
        main.ForestLog.SorenHuntAt = main.GuildBook.PlaySeconds + Legend.SorenHuntEvery;
        _hunting = true;
        Muster(hero.GlobalPosition + (-hero.GlobalBasis.Z with { Y = 0 }).Normalized() * 18);
        main.HudNode.Toast("He's come for what's his. The blade at your hip starts to hum, and the strands of the Web all pull one way. Lose to him, and he'll take it back.", 10);
        _soren?.Provoke(hero);
    }

    /// <summary>He beat the one who took it: he takes his blade and his scabbard back, and the Web is his again.</summary>
    void OnHeroDown()
    {
        if (!_hunting || _soren is not { } e || !IsInstanceValid(e) || e.State == EnemyState.Dead) return;
        var hero = main.PlayerNode;
        if (e.GlobalPosition.DistanceTo(hero.GlobalPosition) > 60 || main.Economy.WebHolder != hero.Company) return;
        foreach (var (slot, id) in new[] { (Slot.Weapon, Legend.Arachnicalibur), (Slot.Neck, Legend.ArachnicaliburScabbard) })
        {
            if (hero.Pack.WornIn(slot)?.Id == id) hero.Pack.Unequip(slot);
            hero.Pack.Take(id);
        }
        hero.Fit();
        hero.Dress();
        main.DoCity(CityAction.WebLost(hero.Company));
        main.HudNode.Toast("Soren stands over you and takes the black blade from your hand, and the scabbard from your back, without any hurry at all. It was only ever on loan.", 11);
        Clear();
        main.Save();
    }

    /// <summary>He isn't reckless: hurt badly, he makes for his ship, and you've a few seconds to finish him.</summary>
    void WatchSoren(double delta)
    {
        if (_soren is not { } e || !IsInstanceValid(e) || e.State == EnemyState.Dead) return;
        if (_leaving > 0)
        {
            e.GlobalPosition += (_away * 12 + Vector3.Up * 3) * (float)delta;
            if ((_leaving -= delta) > 0) return;
            var earned = _hunting || _struck;
            if (!_hunting) main.DoCity(CityAction.SiegeBroken(earned ? main.PlayerNode.Company : Legend.WukongName, false));
            if (earned) main.PlayerNode.Crowns += Legend.SiegeBounty / 2;
            main.HudNode.Toast(_hunting ? $"He's gone, for now, and the blade's still yours. He'll be back for it. +{Legend.SiegeBounty / 2} crowns."
                : earned ? $"He's gone, and the siege with him: the town's safe for now. He wasn't killed, so he'll be back as he was. +{Legend.SiegeBounty / 2} crowns."
                : "Lu Wukong has driven him off, and the siege with him. The town's safe for now.", 9);
            e.QueueFree();
            (_soren, _leaving, _hunting) = (null, 0, false);
            SorenGone();
            main.Save();
            return;
        }
        if (e.Stats.Health > e.Stats.MaxHealth * Legend.SorenFleeAt || !e.FightingCompany && !LuStanding) return;
        _leaving = Legend.SorenFlightSeconds;
        e.Carried = true;
        var away = e.GlobalPosition - main.PlayerNode.GlobalPosition;
        _away = new Vector3(away.X, 0, away.Z).Normalized();
        main.HudNode.Toast($"{Legend.FormOf(main.Economy.SorenChanges).Retreat} Bring him down now, or he's away!", 7);
    }

    void Muster(Vector3 at)
    {
        var form = Legend.FormOf(main.Economy.SorenChanges);
        var danger = WorldGen.Danger(at.X, at.Z) * (1 + 0.08 * main.PlayerNode.Hero.Level);
        _soren = Enemy.Create(Bestiary.Get("dread_soren"), at with { Y = City.FloorAt(at) + 0.5f }, danger * 1.4 * Legend.SorenStrength(main.Economy.SorenDeaths), look: Look(main.Economy));
        Dress(_soren, main.Economy);
        _soren.Roam = 16;
        _soren.Struck = (_, _, _, _) => _struck = true;
        main.AddEnemy(_soren);
        _soren.Scale = Vector3.One * form.Size;
        for (var i = 0; i < 6; i++)
        {
            var spot = at + new Vector3(Mathf.Cos(i * 1.05f) * 7, 0, Mathf.Sin(i * 1.05f) * 7);
            var e = Enemy.Create(Bestiary.Get(i == 0 ? "pirate_captain" : "pirate"), spot with { Y = City.FloorAt(spot) + 0.5f }, danger);
            e.Roam = 12;
            main.AddEnemy(e);
            _crew.Add(e);
        }
        main.HudNode.Toast(form.Hail, 11);
        main.HudNode.Log($"Soren: {form.Hail}");
        if (!_hunting && main.Economy.Siege is { Lu: true }) MusterLu(at, danger);
    }

    bool LuStanding => _lu is not null && IsInstanceValid(_lu) && _lu.State != EnemyState.Dead;

    /// <summary>He lands between Soren and the town with his crew about him, and the two crews go for each other.</summary>
    void MusterLu(Vector3 soren, double danger)
    {
        var deaths = main.Economy.WukongDeaths;
        var form = Legend.WukongOf(deaths);
        foreach (var e in _crew.Append(_soren!)) (e.Faction, e.Foe) = (Reckoning, Monkey);
        var at = soren + new Vector3(0, 0, -16);
        _lu = Enemy.Create(Bestiary.Get("lu_wukong"), at with { Y = City.FloorAt(at) + 0.5f }, danger * 1.2 * Legend.WukongStrength(deaths), look: LuLook(main.Economy));
        Stand(_lu);
        _lu.Scale = Vector3.One * form.Size;
        for (var i = 0; i < 5; i++)
        {
            var spot = at + new Vector3(Mathf.Cos(i * 1.26f) * 5, 0, Mathf.Sin(i * 1.26f) * 5);
            var e = Enemy.Create(Bestiary.Get("monkey_hand"), spot with { Y = City.FloorAt(spot) + 0.5f }, danger);
            Stand(e);
            _hands.Add(e);
        }
        main.HudNode.Toast(form.Hail, 11);
        main.HudNode.Log($"Lu Wukong: {form.Hail}");
    }

    void Stand(Enemy e)
    {
        (e.Allied, e.Faction, e.Foe, e.Roam) = (true, Monkey, Reckoning, 16);
        main.AddEnemy(e);
        e.RefreshPlate();
    }

    /// <summary>Soren's gone: his crew go with him, and if Lu's still on his feet he has a word with whoever fought beside him.</summary>
    void SorenGone()
    {
        foreach (var e in _crew.Where(IsInstanceValid)) e.QueueFree();
        _crew.Clear();
        if (_soren is not null && IsInstanceValid(_soren)) _soren.QueueFree();
        _soren = null;
        if (LuStanding) _lingering = true;
    }

    void TendOffer()
    {
        var hero = main.PlayerNode;
        if (!LuStanding || hero.Downed || _lu!.GlobalPosition.DistanceTo(hero.GlobalPosition) > 100) { LuSails(); return; }
        if (_offered || main.HudNode.Talking) return;
        _offered = true;
        foreach (var e in _hands.Where(IsInstanceValid).Append(_lu)) e.LoseTrack(30);
        if (main.Sea.Outlaw && main.Sea.Sworn.Good)
        {
            hero.Crowns += Legend.MonkeyShare;
            main.HudNode.Toast($"{Legend.WukongName} tosses you a purse off the top of what Soren dropped: a hand's share, +{Legend.MonkeyShare} crowns. The patched sail stands out of the bay.", 9);
            LuSails();
            main.Save();
            return;
        }
        var rank = main.Sea.Rank == SeaRank.Landsman ? "" : " (the Brine Company will strike you off its books)";
        main.HudNode.Talk($"{Legend.WukongName}, of {PirateCrew.DrunkenMonkey.Ship}", Legend.WukongOf(main.Economy.WukongDeaths).Offer,
            ($"I'll sail with you.{rank}", () =>
            {
                main.Sea.JoinCrew(PirateCrew.DrunkenMonkey);
                main.Standing.Good(5, "signed aboard the Drunken Monkey");
                main.HudNode.EndTalk();
                main.HudNode.Toast($"You're a swab of {PirateCrew.DrunkenMonkey.Ship} now. When you want to go out after pirates, find Nell Corran by the south pier.", 10);
                LuSails();
                main.Save();
            }),
            ("Not today.", () =>
            {
                main.HudNode.EndTalk();
                main.HudNode.Toast("The patched sail stands out of the bay, and the singing goes with it. The berth will keep till next time.", 7);
                LuSails();
            }));
    }

    void LuSails()
    {
        (_lingering, _offered) = (false, false);
        Clear();
    }

    public void HuntForTest() => Hunt();

    /// <summary>The look of him as he is now: the sea's work is built into his body (see Garments.Soren).</summary>
    public static string Look(CityEconomy city) => $"soren_{Math.Min(city.SorenChanges, Legend.SorenForms.Count - 1)}";

    /// <summary>He sees blows coming, while the blade and its scabbard are still his.</summary>
    public static void Dress(Enemy soren, CityEconomy city) => soren.Foresees = city.WebHolder is null ? Legend.SorenForesees : 0;

    /// <summary>The look of Lu as he is now: how much of him is ape (see Garments.Wukong).</summary>
    public static string LuLook(CityEconomy city) => $"lu_{Math.Min(city.WukongDeaths, Legend.WukongForms.Count - 1)}";

    void Clear()
    {
        foreach (var e in _crew.Concat(_hands).Where(IsInstanceValid)) e.QueueFree();
        _crew.Clear();
        _hands.Clear();
        foreach (var e in new[] { _soren, _lu })
            if (e is not null && IsInstanceValid(e)) e.QueueFree();
        (_soren, _lu, _leaving, _hunting, _struck) = (null, null, 0, false, false);
    }

    /// <summary>Lu's down, at a siege or on a pirate's deck. It isn't the end of him, and each time it costs him more.</summary>
    void LuFell(Enemy lu, Enemy? killer, bool byCompany)
    {
        var form = Legend.WukongOf(main.Economy.WukongDeaths);
        main.DoCity(CityAction.WukongKilled(byCompany ? main.PlayerNode.Company : killer?.Stats.Name ?? "The Dread Pirate Soren"));
        main.HudNode.Toast(form.Lost, 11);
        main.HudNode.Log($"Lu Wukong: {form.Lost}");
        if (main.AtSeaNode is { } at) at.FlagSlain = true;
        if (byCompany) main.Standing.Evil(10, "killed Lu Wukong");
        if (lu != _lu) return;
        _lu = null;
        foreach (var e in _hands.Where(IsInstanceValid)) e.QueueFree();
        _hands.Clear();
        main.HudNode.Toast("His crew get him onto their shoulders and go down to the boats, singing the part he's forgotten. There's nobody between Soren and the town now.", 9);
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim.Kind.Id == "lu_wukong") { LuFell(victim, killer, byCompany); return; }
        if (victim != _soren) return;
        var form = Legend.FormOf(main.Economy.SorenChanges);
        var earned = byCompany || _struck || _hunting;
        main.DoCity(_hunting ? CityAction.SorenKilled(main.PlayerNode.Company) : CityAction.SiegeBroken(earned ? main.PlayerNode.Company : Legend.WukongName, true));
        (_soren, _leaving, _hunting) = (null, 0, false);
        SorenGone();
        if (!earned)
        {
            main.HudNode.Toast(form.Lost, 11);
            main.Save();
            return;
        }
        main.PlayerNode.Crowns += Legend.SiegeBounty;
        var rng = new Tristinwold.Deviant.Rng((ulong)GD.Randi() << 32 | GD.Randi());
        var plunder = Legend.Plunder(rng);
        var blade = main.Economy.WebHolder is null && rng.Chance(WebChance);
        if (blade)
        {
            main.DoCity(CityAction.Blade(Legend.Arachnicalibur, main.PlayerNode.Company));
            main.ForestLog.SorenHuntAt = main.GuildBook.PlaySeconds + Legend.SorenHuntEvery;
            plunder.AddRange([(Legend.Arachnicalibur, 1), (Legend.ArachnicaliburScabbard, 1)]);
        }
        main.Take(plunder);
        main.HudNode.Toast(form.Lost, 11);
        main.HudNode.Toast(blade
            ? $"And there, in the wet: Arachnicalibur and its scabbard. It lets you take it, the way a web lets a fly land. +{Legend.SiegeBounty} crowns, and the plunder of a dozen towns."
            : $"The plunder of a dozen towns spills across the stones, and +{Legend.SiegeBounty} crowns. His blade went with him.", 10);
        main.HudNode.Log($"Taken from Soren: {string.Join(", ", plunder.Select(x => Items.Count(x.Id, x.N)))}.");
        main.Save();
    }
}
