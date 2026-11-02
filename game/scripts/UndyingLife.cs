using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Korrin and Ronacse (see <see cref="Undying"/>) in the world. Korrin floats in his tank in the warehouse by the east
/// gate until somebody pulls the lever; then he holds court on the dock road with his thugs round him, the stalls and
/// the caravan yard are shut, and his men take a town every other day. He fights twice: put him down and he gets up
/// as something worse, whole again. Ronacse keeps his vigil by the Host's breach in the wilds, a paladin in gold to
/// look at, and goes looking for heroes worth humbling; he calls meteors down on whoever he's fighting, and when he
/// stops pretending, he's a demon with the sun at his back.
/// </summary>
public partial class Main
{
    /// <summary>The tank, in the warehouse by the east gate; and where he sits when the city is his, on the dock road.</summary>
    static Vector3 KorrinTank => Warehouse + new Vector3(-5.5f, 0, 2.5f);
    static readonly Vector3 KorrinSeat = new(108, 0, -4);

    Node3D? _tank;
    Node3D? _tankSleeper;
    Enemy? _korrin;
    bool _korrinFinal;
    readonly List<Enemy> _korrinCourt = [];
    string? _korrinPatrolled;
    readonly List<Enemy> _korrinPatrol = [];

    Person? _pride;
    Enemy? _prideBody;
    bool _prideUnbound;
    double _meteorIn = 6, _prideHuntAt = 20 * 60, _undyingTick;
    readonly List<Node3D> _meteors = [];

    /// <summary>The looks of the worst of them, for the bodies they're built on.</summary>
    static Avatar? UndyingAvatar(string look) => look switch
    {
        "korrin" => Undying.KorrinLook,
        "korrin_final" => Undying.KorrinFinalLook,
        "ronacse" => Undying.PrideLook,
        "ronacse_pride" => Undying.PrideUnboundLook,
        "demon_grunt" => GruntLook,
        "duragon" => DuragonLook,
        "angel" => AngelLook,
        _ => null,
    };

    /// <summary>The demons' brutes: red-skinned and horned, furred and huge, with bone axes. Their lord: pale, ram-horned, red-eyed, cloaked in red. Heaven's host: golden, bare to the waist, in white.</summary>
    static readonly Avatar GruntLook = Avatar.For("Barbarian") with
    {
        Race = Races.Dragonkind, Dragon = "Red", Half = "none", Horns = "Long", Build = "Broad", Height = "Tall", Top = "Class", Bottom = "Russet", Wear = "Class", Outfit = "Natural", Eyes = "Red", Style = "Mane", Hair = "Brown",
    };
    static readonly Avatar DuragonLook = Avatar.For("Barbarian") with
    {
        Race = Races.Dragonkind, Dragon = "White", Half = "elf", Horns = "Ram", Skin = "Ashen", Eyes = "Red", Style = "None", Build = "Broad", Height = "Tall", Top = "Class", Bottom = "Crimson", Wear = "Class", Outfit = "Natural",
    };
    static readonly Avatar AngelLook = Avatar.For("Paladin") with
    {
        Race = "human", Skin = "Tan", Hair = "Honey", Style = "Short04", Build = "Broad", Height = "Tall", Top = "Class", Bottom = "White", Wear = "Class", Outfit = "Natural", Eyes = "Amber", Headwear = "none",
    };

    void TendUndying(double delta)
    {
        TendMeteors(delta);
        if ((_undyingTick -= delta) > 0) return;
        _undyingTick = 1;
        TendTank();
        TendKorrinCourt();
        TendKorrinTowns();
        TendSyndicate();
        TendPride();
    }

    // ---- the tank -----------------------------------------------------------------------------------------------

    /// <summary>A brass-bound tank of green light as tall as two men, with a figure floating in it, a plate, and a lever.</summary>
    void BuildTank()
    {
        _tank = new Node3D { Position = KorrinTank with { Y = City.FloorAt(KorrinTank) } };
        AddChild(_tank);
        var brass = new StandardMaterial3D { AlbedoColor = new Color("#8a6a30"), Metallic = 0.8f, Roughness = 0.35f };
        var glass = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 1f, 0.6f, 0.28f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f,
            EmissionEnabled = true, Emission = new Color("#3aff8a"), EmissionEnergyMultiplier = 0.6f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _tank.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.2f, BottomRadius = 1.3f, Height = 0.5f }, MaterialOverride = brass, Position = new Vector3(0, 0.25f, 0) });
        _tank.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.1f, BottomRadius = 1.1f, Height = 3.2f }, MaterialOverride = glass, Position = new Vector3(0, 2.1f, 0), Name = "Glass" });
        _tank.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.0f, BottomRadius = 1.25f, Height = 0.45f }, MaterialOverride = brass, Position = new Vector3(0, 3.9f, 0) });
        for (var i = 0; i < 4; i++)
            _tank.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 3.6f }, MaterialOverride = brass, Position = new Vector3(Mathf.Cos(i * Mathf.Pi / 2 + 0.78f) * 1.15f, 2.1f, Mathf.Sin(i * Mathf.Pi / 2 + 0.78f) * 1.15f) });
        _tank.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 1.1f, 0.12f) }, MaterialOverride = brass, Position = new Vector3(1.6f, 0.8f, 0.4f), RotationDegrees = new Vector3(0, 0, -20), Name = "Lever" });
        _tank.AddChild(new Label3D { Text = "SUBJECT ONE", Position = new Vector3(0, 0.36f, 1.32f), FontSize = 32, OutlineSize = 6, PixelSize = 0.006f, Modulate = new Color("#e8d8a0") });
        _tank.AddChild(Placed(Lairfolk.Light("#3aff8a", 1.4f, 7, flicker: 0.05f), new Vector3(0, 2.2f, 0)));
        _tank.AddChild(Lairfolk.Motes(new Color(0.6f, 1f, 0.7f, 0.6f), 0.05f, 30, new Vector3(0.8f, 1.4f, 0.8f), 0.4f));
        _tankSleeper = Figure.Create("korrin");
        _tankSleeper.Position = new Vector3(0, 0.9f, 0);
        _tankSleeper.RotationDegrees = new Vector3(6, 180, 0);
        _tank.AddChild(_tankSleeper);
        var bob = _tankSleeper.CreateTween().SetLoops();
        bob.TweenProperty(_tankSleeper, "position:y", 1.1f, 3).SetTrans(Tween.TransitionType.Sine);
        bob.TweenProperty(_tankSleeper, "position:y", 0.9f, 3).SetTrans(Tween.TransitionType.Sine);
        Gild(_tankSleeper, new Color("#e8e8f2"));
    }

    static Node3D Placed(Node3D node, Vector3 at) { node.Position = at; return node; }

    /// <summary>The tank as things are: him asleep in it, or the glass in shards and nobody home.</summary>
    void TendTank()
    {
        if (_tank is null) BuildTank();
        var awake = _city.KorrinFree;
        if (_tankSleeper is not null && IsInstanceValid(_tankSleeper)) _tankSleeper.Visible = !awake;
        if (_tank!.GetNodeOrNull<MeshInstance3D>("Glass") is { } g) g.Visible = !awake;
    }

    (string Verb, Action Act)? TankAction(Vector3 pos)
    {
        if (_indoors is not null || _player.Mount is not null || pos.DistanceTo(KorrinTank with { Y = pos.Y }) > 3.2f) return null;
        if (_city.KorrinFree) return ("look at the broken tank", () => _hud.Talk("The tank", "The glass is in pieces on the floor and the green has drained away. Whatever was in here isn't any more. The brass plate still says SUBJECT ONE.", ("Leave it.", _hud.EndTalk)));
        return ("look at the tank", LookAtTank);
    }

    void LookAtTank() => _hud.Talk("The tank",
        "A tank of green light, as tall as two men, bound in brass and humming. Someone floats inside it, asleep: a half-dragon elf, pale-skinned, long-horned, in black plates of shell with white and violet over them, a long tail curled under him. His face gives away nothing at all. " +
        "The plate on the base says SUBJECT ONE. There's a lever beside it, and dust on the lever.",
        ("Pull the lever.", () => _hud.Talk("The tank", "Are you sure? There's no telling what he is, or why he's in there.",
            ("Pull it.", ReleaseKorrin), ("Leave him be.", () => _hud.Toast("You leave the lever alone. He sleeps on.", 4)))),
        ("Leave him be.", _hud.EndTalk));

    void ReleaseKorrin()
    {
        _hud.EndTalk();
        DoCity(CityAction.KorrinFreed(_player.Company));
        Effects.Burst(this, KorrinTank + Vector3.Up * 2, DamageKind.Shock, 4);
        Sound.At("creature/hurt_hero", KorrinTank, 4, 0.6f);
        _hud.Toast("The green drains. His eyes open, red. The glass bursts outward, and he steps down onto the floor dripping, and smiles, and keeps smiling. \"How kind. Tristinwold will do for a start.\" Then he's gone through the wall, laughing.", 12);
        _hud.Log("Korrin is free. Tristinwold's trade is shut.");
        Save();
    }

    // ---- his court on the dock road ------------------------------------------------------------------------------

    /// <summary>While the city's his, he holds court on the dock road with his thugs: anyone who comes near is his problem, and he's theirs.</summary>
    void TendKorrinCourt()
    {
        var p = _player.GlobalPosition;
        var near = new Vector2(p.X - KorrinSeat.X, p.Z - KorrinSeat.Z).Length();
        if (!_city.KorrinHolds || near > 200)
        {
            if (_korrin is not null || _korrinCourt.Count > 0) ClearKorrin();
            return;
        }
        if (_korrin is not null || near > 120 || _player.Downed) return;
        _korrinFinal = false;
        _korrin = RaiseKorrin("korrin", KorrinSeat);
        string[] court = ["dock_thug", "dock_thug", "bandit", "bandit", "dock_thug", "bandit_chief"];
        for (var i = 0; i < court.Length; i++)
        {
            var at = KorrinSeat + new Vector3(Mathf.Cos(i * 1.05f) * 6, 0, Mathf.Sin(i * 1.05f) * 6);
            var e = Enemy.Create(Bestiary.Get(court[i]), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(Math.Max(4, _city.KorrinLevel - 4)));
            e.Stats.Name = $"Korrin's {(court[i] == "bandit_chief" ? "enforcer" : "thug")}";
            e.Roam = 14;
            AddEnemy(e);
            e.RefreshPlate();
            Emboldened(e);
            _korrinCourt.Add(e);
        }
        _hud.Toast("On the dock road, a throne of stacked crates, and on it Korrin, with his thugs round him. \"Kneel to the Emperor of Tristinwold,\" one of them shouts, \"or don't, and we'll help you.\"", 10);
    }

    Enemy RaiseKorrin(string kind, Vector3 at)
    {
        var level = _city.KorrinLevel;
        var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(level), look: kind);
        e.Stats.Name = kind == "korrin" ? $"Korrin, Subject One (level {level})" : $"Korrin Unbound (level {level})";
        e.Roam = 18;
        AddEnemy(e);
        e.Scale = Vector3.One * (kind == "korrin" ? 1.1f : 1.35f);
        e.RefreshPlate();
        Gild(e, new Color("#e8e8f2"));
        if (kind == "korrin_final") Aura(e, new Color("#a050ff"), 3.2f);
        return e;
    }

    void ClearKorrin()
    {
        if (_korrin is not null && IsInstanceValid(_korrin) && _korrin.State != EnemyState.Dead) _korrin.QueueFree();
        foreach (var e in _korrinCourt.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
        _korrinCourt.Clear();
        _korrin = null;
    }

    /// <summary>The towns his thugs hold: they're in the streets when a hero comes.</summary>
    void TendKorrinTowns()
    {
        var p = _player.GlobalPosition;
        var held = _city.KorrinHolds ? _city.KorrinTowns.Select(id => _world.Find(id)).OfType<Settlement>().FirstOrDefault(s => s.DistanceTo(p.X, p.Z) < s.Radius + 30) : null;
        if (held?.Id == _korrinPatrolled) return;
        foreach (var e in _korrinPatrol.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
        _korrinPatrol.Clear();
        if (_fortress is not null && IsInstanceValid(_fortress)) _fortress.QueueFree();
        _fortress = null;
        _korrinPatrolled = held?.Id;
        if (held is null) return;
        for (var i = 0; i < 4; i++)
        {
            var at = p + new Vector3(Mathf.Cos(i * 1.6f) * 14, 0, Mathf.Sin(i * 1.6f) * 14);
            var e = Enemy.Create(Bestiary.Get(i == 0 ? "bandit_chief" : "dock_thug"), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(Math.Max(4, _city.KorrinLevel - 5)));
            e.Stats.Name = "Korrin's thug";
            AddEnemy(e);
            e.RefreshPlate();
            _korrinPatrol.Add(e);
        }
        Fortify(held);
        _hud.Toast($"Korrin's thugs hold {held.Name}: black and violet banners on every post, the market shut, and the people keeping their doors barred.", 8);
    }

    /// <summary>What a shopkeeper or a trader says while Korrin holds the place.</summary>
    string ShutLine => _lootRng.Pick(ShutLines);

    static readonly string[] ShutLines =
    [
        "Shut. By order of the Emperor, they say. Korrin's men take whatever's on the stall and break the stall, so I'm not putting anything on it.",
        "Not while his thugs are in the street. Come back when somebody's dealt with him.",
        "Trade? There's no trade. There's Korrin, and there's what he lets us keep.",
    ];

    // ---- Ronacse --------------------------------------------------------------------------------------------------

    Vector3 PrideVigil()
    {
        var (x, z) = ImmortalSaga.BreachSite(_world);
        return new Vector3((float)x + 22, 0, (float)z + 10);
    }

    /// <summary>He keeps his vigil by the breach, when he isn't dead or out humbling someone; and now and then he comes looking.</summary>
    void TendPride()
    {
        var p = _player.GlobalPosition;
        var vigil = PrideVigil();
        var far = new Vector2(p.X - vigil.X, p.Z - vigil.Z).Length();
        var standing = _pride is not null && IsInstanceValid(_pride);
        if (standing && (_city.PrideDown || _prideBody is not null || far > 340)) { Dismiss(_pride!); _pride = null; standing = false; }
        if (!standing && !_city.PrideDown && _prideBody is null && far < 260)
        {
            _terrain.EnsureAround(vigil, 1);
            _pride = MeetAt(new Npc("undying:ronacse", "Ronacse", Sex.Male, 40, "a paladin of the Sun, keeping vigil", "wilds", "the breach",
                "A big man in gold plate from neck to heel, with a blue shield and an axe with a blade like the sun, and a moustache he's clearly proud of. He says he's a paladin of the Sun, keeping watch over the breach so nothing else comes through. He says it often. He's very good at it, he says.",
                ["the Sun", "himself"], "proud", "\"Ah. Someone come to admire the view? Or me? Both are permitted.\"", vigil.X, vigil.Z), "ronacse");
            Gild(_pride.Figure, new Color("#d8b040"));
        }
        if (_prideBody is null && !_city.PrideDown && _player.Hero.Level >= 5 && _guilds.PlaySeconds >= _prideHuntAt && !_player.Downed && !_player.InFight && !_hud.Talking
            && City.Below is null && City.Afloat is null && _atSea is null && !InTownForTurlin(p) && GD.Randf() < 1f / 60)
        {
            _prideHuntAt = _guilds.PlaySeconds + 30 * 60;
            RaisePride(p + (-_player.GlobalBasis.Z with { Y = 0 }).Normalized() * 18, false);
            _hud.Toast("Light like noon, though it isn't. A big man in gold plate stands in the road, one finger raised to the sky. \"You. Yes, you. I've heard you think you're strong. Kneel, and I'll let you keep thinking it.\"", 10);
        }
    }

    IEnumerable<(string, Action)> PrideTalk(Person person)
    {
        if (person.Npc.Id != "undying:ronacse") yield break;
        yield return ("What are you doing out here?", () => Say(person, "\"Watching the breach, so nothing else climbs out of it. Somebody has to, and who better? Nobody, is the answer. I've checked.\""));
        yield return ("Have you always been a paladin?", () => Say(person, "He laughs, a big warm laugh, and for a moment his shadow on the ground has horns. \"Always? Nothing is always. Ask me again when you've earned the answer.\""));
        yield return ("I challenge you.", () => { _hud.EndTalk(); var at = person.GlobalPosition; Dismiss(person); _pride = null; RaisePride(at, false); _hud.Toast("\"Splendid! Nobody's asked in years.\" He lifts the axe one-handed, as if it weighs nothing.", 7); });
    }

    void RaisePride(Vector3 at, bool unbound)
    {
        var kind = unbound ? "ronacse_pride" : "ronacse";
        var level = _city.PrideLevel;
        var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(level), look: kind);
        e.Stats.Name = unbound ? $"Ronacse, Pride Unbound (level {level})" : $"Ronacse, the Sin of Pride (level {level})";
        e.Roam = 20;
        AddEnemy(e);
        e.Scale = Vector3.One * (unbound ? 1.35f : 1.1f);
        e.RefreshPlate();
        if (unbound) { Aura(e, new Color("#fff0d0"), 4f); SunBehind(e); }
        else Gild(e, new Color("#d8b040"));
        e.Provoke(_player);
        (_prideBody, _prideUnbound, _meteorIn) = (e, unbound, 4);
    }

    /// <summary>Pride's sun: a burning disc at his back, ringed in white flame.</summary>
    static void SunBehind(Node3D who)
    {
        var sun = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.3f, BottomRadius = 1.3f, Height = 0.05f }, Position = new Vector3(0, 2.6f, -0.7f), RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#f4a870"), EmissionEnabled = true, Emission = new Color("#ffb070"), EmissionEnergyMultiplier = 2.2f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        };
        who.AddChild(sun);
        who.AddChild(Placed(Lairfolk.Motes(new Color(1f, 0.95f, 0.85f, 0.95f), 0.16f, 50, new Vector3(1.4f, 1.4f, 0.3f), 1.2f), new Vector3(0, 2.6f, -0.7f)));
    }

    /// <summary>A glow round something terrible: a light, and motes rising off it.</summary>
    static void Aura(Node3D who, Color colour, float range)
    {
        who.AddChild(Placed(Lairfolk.Light("#" + colour.ToHtml(false), 2.2f, range * 2, flicker: 0.2f), new Vector3(0, 1.4f, 0)));
        who.AddChild(Placed(Lairfolk.Motes(colour, 0.1f, 40, new Vector3(0.6f, 1f, 0.6f), 1.4f), new Vector3(0, 1.2f, 0)));
    }

    /// <summary>Recolours the plate a real body wears (its garments): gold for Pride, white shell for Korrin.</summary>
    static void Gild(Node body, Color metal)
    {
        var gilt = new StandardMaterial3D { AlbedoColor = metal, Metallic = 0.85f, Roughness = 0.3f };
        foreach (var slot in body.FindChildren("Garment_*", "", true, false))
            foreach (var m in slot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                m.MaterialOverride = gilt;
    }

    /// <summary>He calls the sky down: a mark on the ground where you stand, a burning stone out of the clouds, and fire.</summary>
    void TendMeteors(double delta)
    {
        if (_prideBody is not { } e || !IsInstanceValid(e) || e.State == EnemyState.Dead || !e.FightingCompany) return;
        if ((_meteorIn -= delta) > 0) return;
        _meteorIn = _prideUnbound ? 4 : 7;
        var at = _player.GlobalPosition with { Y = City.FloorAt(_player.GlobalPosition) + 0.05f };
        var mark = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 3.6f, OuterRadius = 4.2f }, Position = at,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 0.4f, 0.1f, 0.7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color("#ff6a20") },
        };
        AddChild(mark);
        var stone = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.1f, Height = 2.2f }, Position = at + new Vector3(8, 45, -6),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#3a2010"), EmissionEnabled = true, Emission = new Color("#ff7a20"), EmissionEnergyMultiplier = 2.5f },
        };
        stone.AddChild(Lairfolk.Motes(new Color(1f, 0.6f, 0.2f, 0.9f), 0.25f, 40, new Vector3(0.6f, 0.6f, 0.6f), 2f));
        AddChild(stone);
        _meteors.Add(mark);
        _meteors.Add(stone);
        var fall = stone.CreateTween();
        fall.TweenProperty(stone, "position", at, 1.4).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        fall.TweenCallback(Callable.From(() =>
        {
            Effects.Burst(this, at + Vector3.Up, DamageKind.Fire, 5);
            Sound.At("move/land", at, 6, 0.5f);
            if (IsInstanceValid(e) && _player.GlobalPosition.DistanceTo(at) < 4.5f && !_player.Downed)
                _player.TakeHit(30 + 4 * _city.PrideLevel, DamageKind.Fire, AttackKind.Spell, false, e);
            _meteors.Remove(mark);
            _meteors.Remove(stone);
            if (IsInstanceValid(mark)) mark.QueueFree();
            if (IsInstanceValid(stone)) stone.QueueFree();
        }));
    }

    // ---- who wins ---------------------------------------------------------------------------------------------------

    void OnUndyingSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim == _korrin)
        {
            var at = victim.GlobalPosition;
            if (!_korrinFinal)
            {
                _korrinFinal = true;
                _korrin = RaiseKorrin("korrin_final", at);
                _korrin.Provoke(_player);
                Effects.Burst(this, at + Vector3.Up, DamageKind.Shock, 5);
                _hud.Toast("He goes down, and lies still, and then he laughs from the ground. The shell on him cracks and spreads, he gets up bigger, whole again, and violet light pours off him. \"Oh, good. You're fun.\"", 10);
                return;
            }
            _korrin = null;
            foreach (var e in _korrinCourt.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
            _korrinCourt.Clear();
            var level = _player.Hero.Level;
            _player.Crowns += 1000 + 50 * level;
            GainXp(200 + 30 * level);
            Sound.Ui("coin", -2);
            _hud.Talk("Korrin, beaten", "He's down, and this time he stays down, for now: breathing, smiling at the sky. You could leave him, and he'll be back in three days. Or you could drag him back to the warehouse and put him in the tank.",
                ("Drag him back into the tank.", () => { DoCity(CityAction.KorrinFell(_player.Company, true)); _hud.EndTalk(); _hud.Toast("It takes all night. He doesn't fight it: he just watches you, smiling, until the green closes over his face. The stalls open in the morning.", 10); Save(); }),
                ("Leave him.", () => { DoCity(CityAction.KorrinFell(_player.Company, false)); _hud.EndTalk(); _hud.Toast("By morning he's gone, and his thugs with him. Tristinwold trades again. He'll be back.", 9); Save(); }));
            return;
        }
        if (victim == _prideBody)
        {
            var at = victim.GlobalPosition;
            if (!_prideUnbound)
            {
                RaisePride(at, true);
                Effects.Burst(this, at + Vector3.Up, DamageKind.Fire, 6);
                _hud.Toast("The gold plate falls off him in pieces, and what's under it isn't a man. White fire runs up his arms, a sun kindles at his back, and he grins down at you with too many teeth. \"I am Pride. Did you think it would be that easy?\"", 11);
                return;
            }
            _prideBody = null;
            foreach (var m in _meteors.Where(IsInstanceValid)) m.QueueFree();
            _meteors.Clear();
            DoCity(CityAction.PrideFell(_player.Company));
            var level = _player.Hero.Level;
            _player.Crowns += 800 + 40 * level;
            GainXp(160 + 25 * level);
            Sound.Ui("coin", -2);
            _hud.Toast("Pride falls, and burns down to a figure of ash in gold plate, still pointing at the sky. A seal on the Rift mends with a sound like a bell. He'll be back.", 10);
            Save();
        }
    }

    /// <summary>The hero went down to one of them: Korrin's the stronger for it; Pride breaks a seal, and lets them live to remember.</summary>
    void OnUndyingWin()
    {
        var p = _player.GlobalPosition;
        if (_korrin is { } k && IsInstanceValid(k) && k.State != EnemyState.Dead && k.GlobalPosition.DistanceTo(p) < 60)
        {
            DoCity(CityAction.KorrinWon());
            _hud.Toast("Korrin stands over you and pats your cheek. \"Kneel next time. It saves us both the bother.\"", 9);
            GetTree().CreateTimer(3).Timeout += ClearKorrin;
        }
        if (_prideBody is { } r && IsInstanceValid(r) && r.State != EnemyState.Dead && r.GlobalPosition.DistanceTo(p) < 60)
        {
            DoCity(CityAction.PrideWon(_player.Company));
            _hud.Toast("He doesn't finish you. He lifts one finger to the sky instead. \"Remember who humbled you.\" Somewhere below the world, a seal breaks.", 10);
            var body = r;
            _prideBody = null;
            GetTree().CreateTimer(3).Timeout += () => { if (IsInstanceValid(body)) body.QueueFree(); };
        }
    }

    // ---- for the self-test -------------------------------------------------------------------------------------------

    public static Vector3 KorrinTankForTest => KorrinTank;
    public static Vector3 KorrinSeatForTest => KorrinSeat;
    public (string Verb, Action Act)? TankActionForTest(Vector3 at) => TankAction(at);
    public Enemy? KorrinForTest => _korrin;
    public bool KorrinFinalForTest => _korrinFinal;
    public int KorrinCourtForTest => _korrinCourt.Count(e => IsInstanceValid(e) && e.State != EnemyState.Dead);
    public Person? PrideForTest => _pride;
    public Enemy? PrideBodyForTest => _prideBody;
    public bool PrideUnboundForTest => _prideUnbound;
    public Vector3 PrideVigilForTest => PrideVigil();
    public void TendUndyingForTest() { _undyingTick = 0; TendUndying(0.01); }
    public int MeteorsForTest => _meteors.Count;
}
