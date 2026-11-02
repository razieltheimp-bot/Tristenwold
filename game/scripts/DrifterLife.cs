using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The drifters (see <see cref="Drifters"/>): six incomplete immortals out in the world, each at their own camp or
/// moving between a round of them. Anyone can sit and talk, buy what they carry, fetch what they want, train with them,
/// spar, or fight them to the death (they're back the next day, stronger). And anyone can hire them, though never to
/// walk at their side: to fall on a town, to go after another hero, or to guard or raid a wagon or a ship. Every job
/// is on the server, so the hero it's aimed at meets them wherever they are: on the road, at their wagon, on deck.
/// They fight at their own level, ten at the least, and it grows like any deviant's: with every hero they beat, and
/// every time they come back from the dead.
/// </summary>
public partial class Main
{
    readonly Dictionary<string, (Person Who, int Camp, Node3D Site)> _drifterFolk = [];
    readonly Dictionary<string, Vector3> _drifterSpots = [];
    /// <summary>The drifter in a fight here, what job brought them (if any), and whether they're on the hero's side.</summary>
    (Enemy Body, Drifter Who, Hire? Job, bool Ally, Person? Left)? _drifterFight;
    readonly List<Enemy> _drifterCalled = [];
    /// <summary>The jobs already played out here, this session: a town's attacker comes once to each hero.</summary>
    readonly HashSet<string> _drifterStruck = [];
    double _drifterTick, _drifterHuntAt = 90;
    bool _drifterChanged;

    /// <summary>How a drifter looks, by their look: Broly's rage turns his hair green.</summary>
    public static Avatar? DrifterAvatar(string look) =>
        look == "drifter_broly_rage" ? Drifters.Get("broly")!.Avatar with { Hair = "Moss" } : Drifters.All.FirstOrDefault(d => d.Look == look)?.Avatar ?? UndyingAvatar(look);

    public static string DrifterNpc(Drifter d) => $"drifter:{d.Id}";
    static Drifter? DrifterOf(Person p) => p.Npc.Id.StartsWith("drifter:") ? Drifters.Get(p.Npc.Id[8..]) : null;

    void TendDrifters(double delta)
    {
        if ((_drifterTick -= delta) > 0) return;
        _drifterTick = 1;
        PlaceDrifters();
        TendDrifterFight();
        TendHires();
    }

    // ---- where they are ---------------------------------------------------------------------------------------

    /// <summary>Somewhere dry near the camp, clear of every village: the same spot every time.</summary>
    Vector3 DrifterSpot(Drifter d, int camp)
    {
        var key = $"{d.Id}:{camp}";
        if (_drifterSpots.TryGetValue(key, out var at)) return at;
        var (x0, z0, _) = d.Camps[camp];
        var best = (X: x0, Z: z0);
        for (var i = 0; i < 120; i++)
        {
            var (x, z) = (x0 + Math.Cos(i * 2.4) * i * 5, z0 + Math.Sin(i * 2.4) * i * 5);
            if (_world.Height(x, z) < WorldGen.WaterLevel + 1.5) continue;
            if (_world.SettlementsNear(x, z, 300).Any(s => s.DistanceTo(x, z) < s.Radius + 40)) continue;
            best = (x, z);
            break;
        }
        return _drifterSpots[key] = new Vector3((float)best.X, 0, (float)best.Z);
    }

    /// <summary>Each drifter stands at today's camp while there's someone near to see them, and isn't there while they're dead or fighting.</summary>
    void PlaceDrifters()
    {
        var p = _player.GlobalPosition;
        foreach (var d in Drifters.All)
        {
            var camp = d.Camps.Length == 1 ? 0 : ((_city.Day % d.Camps.Length) + d.Camps.Length) % d.Camps.Length;
            var spot = DrifterSpot(d, camp);
            var far = new Vector2(p.X - spot.X, p.Z - spot.Z).Length();
            var away = _city.DrifterDown(d.Id) || _drifterFight is { Ally: false } f && f.Who == d || _duel is { } du && du.Rival.Npc.Id == DrifterNpc(d);
            var here = _drifterFolk.TryGetValue(d.Id, out var stood) && IsInstanceValid(stood.Who);
            if (here && (away || far > 340 || stood.Camp != camp))
            {
                if (_duel is { } duel && duel.Rival == stood.Who) continue;
                Dismiss(stood.Who);
                if (IsInstanceValid(stood.Site)) stood.Site.QueueFree();
                _drifterFolk.Remove(d.Id);
                here = false;
            }
            if (here || away || far > 260) continue;
            _terrain.EnsureAround(spot, 1);
            var a = d.Avatar;
            var npc = new Npc(DrifterNpc(d), d.Name, a.Female ? Sex.Female : Sex.Male, d.Id == "irohe" ? 62 : 30, $"{d.Title}, an incomplete immortal", "wilds", d.CampOn(_city.Day).Place,
                d.Story, ["old stories", "the road"], d.Leaning == Leaning.Good ? "warm" : "guarded", d.Greeting, spot.X, spot.Z, Race: a.Race);
            var who = MeetAt(npc, d.Look);
            who.Scale = Vector3.One * d.Size;
            _drifterFolk[d.Id] = (who, camp, DrifterCamp(d, spot));
        }
    }

    /// <summary>What's round a drifter: a fire, always, and for Irohe his kettle on it and Zuko at his feet.</summary>
    Node3D DrifterCamp(Drifter d, Vector3 at)
    {
        var site = new Node3D { Position = at with { Y = City.FloorAt(at) } };
        AddChild(site);
        var fire = new Node3D { Position = new Vector3(2.2f, 0, 1.2f) };
        site.AddChild(fire);
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#4a3424"), Roughness = 1 };
        for (var i = 0; i < 4; i++)
            fire.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = 0.9f }, MaterialOverride = wood, Position = new Vector3(0, 0.08f, 0), RotationDegrees = new Vector3(90, i * 45, 0) });
        fire.AddChild(Lairfolk.Light("#ff9a3a", 1.6f, 7, flicker: 0.35f));
        fire.AddChild(Lairfolk.Motes(new Color(1f, 0.6f, 0.2f, 0.9f), 0.08f, 24, new Vector3(0.2f, 0.1f, 0.2f), 1.4f));
        if (d.Id == "irohe")
        {
            var iron = new StandardMaterial3D { AlbedoColor = new Color("#2a2a2c"), Metallic = 0.6f, Roughness = 0.5f };
            fire.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.16f, Height = 0.26f }, MaterialOverride = iron, Position = new Vector3(0, 0.32f, 0) }); // the kettle
            fire.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.03f, Height = 0.14f }, MaterialOverride = iron, Position = new Vector3(0.15f, 0.38f, 0), RotationDegrees = new Vector3(0, 0, -55) });
            if (Creature.Create("dog.glb", 0.9f, new Color("#1c1a1c"), yaw: 90) is { } zuko)
            {
                zuko.Position = new Vector3(-1.4f, 0, 0.8f);
                zuko.RotationDegrees = new Vector3(0, 60, 0);
                site.AddChild(zuko);
            }
            site.AddChild(new Label3D { Text = "Zuko", Position = new Vector3(-1.4f, 1.3f, 0.8f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 24, OutlineSize = 6, PixelSize = 0.006f, Modulate = new Color("#d8c8a8") });
        }
        return site;
    }

    // ---- talk -------------------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> DrifterTalk(Person person)
    {
        if (DrifterOf(person) is not { } d) yield break;
        var counters = _pursuits.Counters;
        yield return ("How are you still alive?", () => Say(person, d.Undying));
        if (d.Id == "irohe")
            yield return ("Share a cup of tea by the fire", () => DrifterHelp(person, d));
        else if (d.Help is not null)
            yield return counters.GetValueOrDefault($"drifter_help:{d.Id}", -1) == _city.Day
                ? ("Could you help me?", () => Say(person, "Once a day, friend. Come back tomorrow."))
                : ("Could you help me?", () => DrifterHelp(person, d));
        yield return ("Will you train me?", () => DrifterLesson(person, d));
        if (counters.GetValueOrDefault($"drifter_quest:{d.Id}") == 0)
        {
            var want = Items.Get(d.Wants)!;
            yield return _player.Pack.Count(d.Wants) >= d.WantsN
                ? ($"Give {d.Name} {d.WantsN} {want.Name} ({d.WantsPay} crowns)", () => DrifterQuestDone(person, d))
                : ("Is there anything you need?", () => Say(person, d.Ask));
        }
        if (d.Wares.Length > 0) yield return ("What have you got?", () => DrifterWares(person, d));
        yield return ("I have work for you.", () => DrifterHireMenu(person, d));
        yield return ("Spar with me.", () => DrifterSpar(person, d));
        yield return ("Fight me. To the death.", () => _hud.Talk(Who(person.Npc), d.Leaning == Leaning.Good ? $"{d.Name} looks at you a long while. \"If you must. I'll come back. You might not.\"" : $"{d.Name} smiles, and stands. \"I'll come back. Will you?\"",
            ("Draw.", () => DrifterFight(person, d)), ("No. Forget it.", () => Say(person, "Wise."))));
    }

    void DrifterHelp(Person person, Drifter d)
    {
        switch (d.Id)
        {
            case "gornok": _player.Bless(600, AutoStatus.Protect, AutoStatus.Shell); break;
            case "ren": _player.Bless(600, AutoStatus.Haste, AutoStatus.Regen); break;
            case "yokin": _player.Stats.Heal(1e9); _player.Bless(600, AutoStatus.Protect); break;
            case "irohe":
                _player.Stats.Restore(1e9, 1e9);
                _player.Bless(600, AutoStatus.Regen);
                _pursuits.RestedUntil = _guilds.PlaySeconds + Housing.RestedSeconds;
                if (_player.Pack.Count("travellers_tea") < 3) _player.Pack.Add("travellers_tea");
                break;
        }
        _pursuits.Counters[$"drifter_help:{d.Id}"] = _city.Day;
        Save();
        Say(person, d.Help ?? "");
    }

    /// <summary>A lesson: once for each level you reach, for a fee, and the first from a teacher of your own way teaches you a secret of it (a skill point).</summary>
    void DrifterLesson(Person person, Drifter d)
    {
        if (!d.Teaches.Contains(_player.Class.Id)) { Say(person, d.NotTaught); return; }
        var key = $"drifter_lesson:{d.Id}";
        var level = _player.Hero.Level;
        if (_pursuits.Counters.GetValueOrDefault(key) >= level) { Say(person, "You've had today's lesson for where you are. Go and use it. Come back when you've grown."); return; }
        var fee = 20 + 10 * level;
        _hud.Talk(Who(person.Npc), $"A lesson costs {fee} crowns. Not for me: for the fire, and the tea.",
            ($"Pay {fee} crowns", () =>
            {
                if (_player.Crowns < fee) { Sound.Ui("error"); Say(person, "Come back when your purse is as ready as you are."); return; }
                _player.Crowns -= fee;
                _pursuits.Counters[key] = level;
                var xp = 40 + 12 * level;
                GainXp(xp);
                _player.Stats.Restore(1e9, 1e9);
                var secret = $"drifter_secret:{d.Id}";
                var first = _pursuits.Counters.GetValueOrDefault(secret) == 0;
                if (first) { _pursuits.Counters[secret] = 1; _player.Hero.Points++; }
                Save();
                Say(person, $"{d.Lesson} (+{xp} experience{(first ? "; and a secret of the way: a skill point to spend (K)" : "")})");
            }), ("Not now.", () => Say(person, "The lesson will wait. So will I.")));
    }

    void DrifterQuestDone(Person person, Drifter d)
    {
        if (!_player.Pack.Take(d.Wants, d.WantsN)) return;
        _player.Crowns += d.WantsPay;
        _pursuits.Counters[$"drifter_quest:{d.Id}"] = 1;
        GainXp(30 + 8 * _player.Hero.Level);
        Sound.Ui("coin");
        Save();
        Say(person, $"{d.Thanks} (+{d.WantsPay} crowns)");
    }

    void DrifterWares(Person person, Drifter d)
    {
        var options = d.Wares.Select(id => Items.Get(id)).OfType<Item>().Select(item =>
        {
            var price = _city.StorePrice(item);
            return ($"{item.Name}, {price} crowns: {item.Text}", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); _hud.Toast("You can't afford that.", 3); return; }
                _player.Crowns -= price;
                _player.Pack.Add(item.Id);
                Sound.Ui("coin");
                Save();
                DrifterWares(person, d);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "Safe roads.")));
        _hud.Talk($"{d.Name} ({_player.Crowns} crowns)", "What I carry, I'll part with.", [.. options]);
    }

    // ---- hiring ------------------------------------------------------------------------------------------------

    /// <summary>Every hero the server knows by name: outlaws, hall-founders, shopkeepers, councillors. Not this one.</summary>
    IEnumerable<string> KnownHeroes() =>
        _city.Outlaws.Keys.Concat(_city.Halls.Values.Select(h => h.Owner)).Concat(_city.Stores.Values.Select(s => s.Owner))
            .Concat(_city.Councils.Values.SelectMany(c => c.Heroes)).Where(n => n.Length > 0 && n != _player.Company).Distinct().OrderBy(n => n).Take(12);

    void DrifterHireMenu(Person person, Drifter d)
    {
        if (_city.HireOf(d.Id) is { } busy) { Say(person, $"I'm already paid to {Drifters.Job(busy.Kind, busy.TargetName)}. When that's done, ask me again."); return; }
        var options = new List<(string, Action)>();
        void Offer(HireKind k, string label) => options.Add(($"{label} ({Drifters.Price(k)} crowns, {Drifters.Days(k)} days)", () => DrifterTarget(person, d, k)));
        Offer(HireKind.GuardCaravan, "Ride guard on my wagon");
        Offer(HireKind.GuardShip, "Sail guard on my ship");
        Offer(HireKind.Hero, d.Leaning == Leaning.Good ? "Bring in an outlaw" : "Go after a hero");
        Offer(HireKind.RaidCaravan, "Take someone's wagon on the road");
        Offer(HireKind.RaidShip, "Board someone's ship at sea");
        if (d.Leaning != Leaning.Good) Offer(HireKind.Town, "Fall on a town");
        options.Add(("Never mind.", () => Say(person, "As you like.")));
        _hud.Talk($"{d.Name} ({_player.Crowns} crowns)", d.Leaning == Leaning.Good
            ? "I'll guard what's yours, and I'll go after an outlaw. Nothing else."
            : "I'll do most things, for the right price. I won't walk at your side. I'm not a pet.", [.. options]);
    }

    void DrifterTarget(Person person, Drifter d, HireKind k)
    {
        if (Drifters.Guarding(k)) { DrifterHire(person, d, k, _player.Company, _player.Company); return; }
        var targets = k == HireKind.Town
            ? new[] { (Id: ReputationBook.Capital, Name: "Tristinwold") }.Concat(JobTowns.Select(m => (m.Id, m.Name))).ToList()
            : KnownHeroes().Where(n => d.Leaning != Leaning.Good || _city.Outlaws.ContainsKey(n)).Select(n => (Id: n, Name: n)).ToList();
        if (targets.Count == 0) { Say(person, d.Leaning == Leaning.Good ? "I don't know of any outlaws worth the walk." : "I don't know anyone by that description."); return; }
        var options = targets.Select(t => ($"{t.Name}{(_city.Outlaws.TryGetValue(t.Id, out var b) ? $" ({b} crowns on their head)" : "")}", (Action)(() => DrifterHire(person, d, k, t.Id, t.Name)))).ToList();
        options.Add(("Never mind.", () => Say(person, "As you like.")));
        _hud.Talk($"{d.Name}: {(k == HireKind.Town ? "which town" : "who")}?", "Name them.", [.. options]);
    }

    void DrifterHire(Person person, Drifter d, HireKind k, string target, string name)
    {
        if (Drifters.Refuses(d, k, target, _player.Company, _city.Outlaws) is { } no) { Say(person, no.Length > 0 ? no : "No."); return; }
        var price = Drifters.Price(k);
        if (_player.Crowns < price) { Sound.Ui("error"); Say(person, $"That's {price} crowns. Come back when you have it."); return; }
        var hire = new Hire { Id = $"{d.Id}-{_city.Day}-{(uint)GD.Randi() % 100000}", Drifter = d.Id, Kind = k, Target = target, TargetName = name, By = _player.Company };
        DoCity(CityAction.Hired(hire));
        if (_city.HireOf(d.Id)?.Id != hire.Id) { Say(person, "Something's changed. Ask me again in a moment."); return; }
        _player.Crowns -= price;
        Sound.Ui("coin");
        Save();
        Say(person, Drifters.Guarding(k)
            ? $"Done. For {Drifters.Days(k)} days, if anyone comes for your {(k == HireKind.GuardShip ? "ship" : "wagon")}, I'll be there."
            : $"Done. I'll {Drifters.Job(k, name)}. You never met me.");
    }

    // ---- fights ------------------------------------------------------------------------------------------------

    /// <summary>A drifter's body, at their own level (grown like any deviant's): on the hero's side, or against them.</summary>
    Enemy RaiseDrifter(Drifter d, Vector3 at, bool ally, Hire? job, Person? left = null, bool exact = false)
    {
        var level = _city.DrifterLevel(d.Id);
        var e = Enemy.Create(Bestiary.Get(d.Kind), exact ? at : at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(level), look: d.Look);
        e.Stats.Name = $"{d.Name}, {d.Title} (level {level})";
        e.Roam = 14;
        if (ally) { e.Leader = _player; e.Expires = 150; }
        AddEnemy(e);
        e.Scale = Vector3.One * d.Size;
        e.RefreshPlate();
        if (!ally) e.Provoke(_player);
        ClearDrifterFight();
        _drifterFight = (e, d, job, ally, left);
        _drifterChanged = false;
        return e;
    }

    void DrifterFight(Person person, Drifter d)
    {
        _hud.EndTalk();
        var at = person.GlobalPosition;
        person.Visible = false;
        person.CollisionLayer = 0;
        RaiseDrifter(d, at, false, null, person);
        _player.Face(at);
        _hud.Toast($"{d.Name} rises to meet you. To the death!", 6);
    }

    void DrifterSpar(Person person, Drifter d)
    {
        if (_duel is not null) return;
        _hud.EndTalk();
        var level = _city.DrifterLevel(d.Id);
        var at = person.GlobalPosition;
        var foe = Enemy.Create(Bestiary.Get(d.Kind), at with { Y = City.FloorAt(at) + 0.3f }, look: d.Look);
        foe.Spar(d.Name, 220 + 30 * level, 11 + 2 * level);
        AddEnemy(foe);
        foe.Scale = Vector3.One * d.Size;
        person.Visible = false;
        person.CollisionLayer = 0;
        foe.Provoke(_player);
        _duel = (foe, person, won =>
        {
            if (won) GainXp(25 + 6 * _player.Hero.Level);
            _hud.Toast(won ? $"{d.Name} steps back and bows. \"Good.\"" : $"{d.Name} helps you up. \"Again, when you're ready.\"", 6);
        });
        _player.Face(at);
        _hud.Toast($"{d.Name} squares up. First to yield loses.", 5);
    }

    /// <summary>A spar with a drifter over: they step back to their fire.</summary>
    void DrifterDuelOver(Enemy foe, Person rival) => GetTree().CreateTimer(2.5).Timeout += () =>
    {
        if (IsInstanceValid(foe)) foe.QueueFree();
        if (IsInstanceValid(rival)) { rival.Visible = true; rival.CollisionLayer = 1; }
    };

    void ClearDrifterFight()
    {
        foreach (var e in _drifterCalled.Where(IsInstanceValid)) e.QueueFree();
        _drifterCalled.Clear();
        if (_drifterFight is { } f && IsInstanceValid(f.Body) && f.Body.State != EnemyState.Dead) f.Body.QueueFree();
        if (_drifterFight?.Left is { } left && IsInstanceValid(left)) { left.Visible = true; left.CollisionLayer = 1; }
        _drifterFight = null;
    }

    /// <summary>A fight with a drifter going on: their second wind (Irohe's fire, Broly's rage), and whether it's still a fight.</summary>
    void TendDrifterFight()
    {
        if (_drifterFight is not { } f) return;
        if (!IsInstanceValid(f.Body) || f.Body.State == EnemyState.Dead) { if (f.Ally) _drifterFight = null; return; }
        var e = f.Body;
        if (!f.Ally && e.GlobalPosition.DistanceTo(_player.GlobalPosition) > 160)
        {
            // Got clear of them. A job they were paid for isn't done: they'll be back.
            if (f.Job is { } job) { _drifterStruck.Remove(job.Id); _drifterHuntAt = _guilds.PlaySeconds + 300; }
            _hud.Toast($"You've lost {f.Who.Name}, for now.", 5);
            ClearDrifterFight();
            return;
        }
        if (_drifterChanged || e.Stats.Health > e.Stats.MaxHealth * (f.Who.Id == "broly" ? 0.5 : 0.6)) return;
        switch (f.Who.Id)
        {
            case "irohe":
                _drifterChanged = true;
                e.Reshape("elemental_fire");
                e.Scale = Vector3.One * f.Who.Size * 1.3f;
                Effects.Burst(this, e.GlobalPosition + Vector3.Up, DamageKind.Fire, 4);
                _hud.Toast("Irohe breathes in, slow and deep, and breathes out fire: he stands up out of it a giant of living flame. \"Now we see what you have learned.\"", 9);
                for (var i = 0; i < 2; i++) CallSerpent(e, f.Ally, i);
                break;
            case "broly":
                _drifterChanged = true;
                e.Reshape("drifter_broly_rage");
                e.Scale = Vector3.One * f.Who.Size * 1.4f;
                e.Stats.Heal(e.Stats.MaxHealth * 0.25);
                Effects.Burst(this, e.GlobalPosition + Vector3.Up, DamageKind.Poison, 5);
                _hud.Toast("Broly screams, and keeps screaming. His hair goes green as poison and stands up like a flame, and he grows. Whatever was holding him back, it isn't any more.", 9);
                break;
        }
    }

    /// <summary>Irohe's fire made a serpent: a long dragon of flame that coils up out of the ground and goes for whoever he's fighting.</summary>
    void CallSerpent(Enemy irohe, bool ally, int n)
    {
        var at = irohe.GlobalPosition + new Vector3(n == 0 ? 4 : -4, 0, 2);
        var danger = Northwood.DangerFor(_city.DrifterLevel("irohe")) * 0.7;
        var serpent = Enemy.Create(Bestiary.Get("flame_serpent"), at with { Y = City.FloorAt(at) + 0.8f }, danger, look: "flame_serpent");
        serpent.Expires = 40;
        if (ally) serpent.Leader = _player;
        AddEnemy(serpent);
        if (!ally) serpent.Provoke(_player);
        _drifterCalled.Add(serpent);
        Effects.Burst(this, at + Vector3.Up, DamageKind.Fire, 2.5f);
    }

    void OnDrifterSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        _drifterCalled.Remove(victim);
        if (_drifterFight is not { } f || f.Body != victim) return;
        var d = f.Who;
        _drifterFight = null;
        foreach (var e in _drifterCalled.Where(IsInstanceValid)) e.QueueFree();
        _drifterCalled.Clear();
        if (f.Left is { } left && IsInstanceValid(left)) { left.Visible = true; left.CollisionLayer = 1; }
        if (f.Ally)
        {
            DoCity(CityAction.DrifterFell(d.Id, ""));
            _hud.Toast($"{d.Name} falls defending you. They'll be back tomorrow, but the job's over.", 7);
            Save();
            return;
        }
        DoCity(CityAction.DrifterFell(d.Id, _player.Company));
        var level = _player.Hero.Level;
        var purse = 150 + 20 * level;
        _player.Crowns += purse;
        GainXp(60 + 15 * level);
        Sound.Ui("coin", -2);
        _hud.Toast($"{d.Fell} (+{purse} crowns) They'll be back tomorrow, stronger.", 10);
        _hud.Log($"Drifters: {d.Name} killed. {d.Back}");
        Save();
    }

    /// <summary>The hero went down to a drifter: what they were paid for is done, and they take their due.</summary>
    void OnDrifterWin()
    {
        if (_drifterFight is not { Ally: false } f || !IsInstanceValid(f.Body) || f.Body.State == EnemyState.Dead) return;
        if (f.Body.GlobalPosition.DistanceTo(_player.GlobalPosition) > 60) return;
        var d = f.Who;
        var cost = "";
        DoCity(CityAction.DrifterWon(d.Id, f.Job?.Id));
        if (f.Job is not null)
        {
            if (d.Leaning == Leaning.Good && _standing.Bounty > 0)
            {
                var paid = Math.Min(_player.Crowns, _standing.Bounty);
                _player.Crowns -= paid;
                _standing.Bounty = 0;
                cost = $" They take you in: your bounty's settled ({paid} crowns out of your purse).";
            }
            else
            {
                var cut = _player.Crowns * 15 / 100;
                _player.Crowns -= cut;
                cost = cut > 0 ? $" They take {cut} crowns off you for their trouble." : "";
            }
        }
        _hud.Toast(d.Won + cost, 10);
        GetTree().CreateTimer(3).Timeout += ClearDrifterFight;
        Save();
    }

    // ---- the jobs, as they come to this hero -------------------------------------------------------------------

    /// <summary>Whatever's been paid for against this hero, met where they are: on the road, at their wagon, or in a town paid for.</summary>
    void TendHires()
    {
        if (_drifterFight is not null || _duel is not null || _player.Downed || _hud.Talking || City.Below is not null || City.Afloat is not null || _atSea is not null) return;
        var p = _player.GlobalPosition;
        foreach (var job in _city.LiveHires.ToList())
        {
            if (_drifterStruck.Contains(job.Id) || Drifters.Get(job.Drifter) is not { } d) continue;
            switch (job.Kind)
            {
                case HireKind.Hero when job.Target == _player.Company:
                    if (_guilds.PlaySeconds < _drifterHuntAt || _player.InFight || InTownForTurlin(p) || GD.Randf() >= 1f / 30) continue;
                    Strike(job, d, p + (-_player.GlobalBasis.Z with { Y = 0 }).Normalized() * 20, d.Hail);
                    return;
                case HireKind.RaidCaravan when job.Target == _player.Company:
                    if (_wagon is null || _trade.Run is null && _trade.Loaded <= 0 || new Vector2(_wagon.GlobalPosition.X, _wagon.GlobalPosition.Z).Length() < 400) continue;
                    Strike(job, d, _wagon.GlobalPosition + new Vector3(16, 0, 10), $"{d.Hail} They want the wagon.");
                    return;
                case HireKind.Town:
                    var (x, z, r) = job.Target == ReputationBook.Capital ? (0.0, 0.0, 220.0) : _world.Find(job.Target) is { } s ? (s.X, s.Z, s.Radius + 40) : (double.NaN, 0.0, 0.0);
                    if (double.IsNaN(x) || new Vector2(p.X - (float)x, p.Z - (float)z).Length() > r) continue;
                    Strike(job, d, p + (-_player.GlobalBasis.Z with { Y = 0 }).Normalized() * 18, $"{d.Name} is in {job.TargetName}, paid to fall on it, and the streets are emptying. {d.Hail}");
                    return;
            }
        }
    }

    void Strike(Hire job, Drifter d, Vector3 at, string hail)
    {
        _drifterStruck.Add(job.Id);
        _terrain.EnsureAround(at, 1);
        RaiseDrifter(d, at, false, job);
        _hud.Toast(hail, 10);
        _hud.Log($"Drifters: {d.Name}, paid to {Drifters.Job(job.Kind, job.TargetName)}.");
    }

    /// <summary>Trouble at the wagon or on deck: whoever this hero paid to guard it is there. True if someone came.</summary>
    public bool DrifterGuards(HireKind kind, Vector3 at, bool exact = false)
    {
        if (_drifterFight is not null || _city.LiveHires.FirstOrDefault(h => h.Kind == kind && h.Target == _player.Company) is not { } job || Drifters.Get(job.Drifter) is not { } d) return false;
        RaiseDrifter(d, at, true, job, exact: exact);
        _hud.Toast($"{d.Name} is there, as paid: {(kind == HireKind.GuardShip ? "over the rail" : "out of nowhere")} and into them.", 7);
        if (d.Id == "irohe") for (var i = 0; i < 2; i++) CallSerpent(_drifterFight!.Value.Body, true, i);
        return true;
    }

    /// <summary>Boarders over the rail: if someone's paid a drifter to board this hero's ship, they come with them.</summary>
    public bool DrifterBoards(Vector3 at)
    {
        if (_drifterFight is not null || _city.LiveHires.FirstOrDefault(h => h.Kind == HireKind.RaidShip && h.Target == _player.Company && !_drifterStruck.Contains(h.Id)) is not { } job || Drifters.Get(job.Drifter) is not { } d) return false;
        _drifterStruck.Add(job.Id);
        RaiseDrifter(d, at, false, job, exact: true);
        _hud.Toast($"{d.Name} comes over the rail with them. {d.Hail}", 9);
        return true;
    }

    // ---- for the self-test -------------------------------------------------------------------------------------

    public Person? DrifterForTest(string id) => _drifterFolk.TryGetValue(id, out var s) && IsInstanceValid(s.Who) ? s.Who : null;
    public Vector3 DrifterSpotForTest(string id) => DrifterSpot(Drifters.Get(id)!, 0);
    public Enemy? DrifterFoeForTest => _drifterFight?.Body;
    public void PlaceDriftersForTest() => PlaceDrifters();
    public void TendDriftersForTest() { TendDrifterFight(); TendHires(); }
    public IEnumerable<(string, Action)> DrifterTalkForTest(Person p) => DrifterTalk(p);
}
