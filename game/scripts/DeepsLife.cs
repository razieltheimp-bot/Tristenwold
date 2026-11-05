using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The Deep Trenches and the Dark Depths (see <see cref="Deeps"/>), under the mines. At the back of any mine there's a
/// crack going down, and Greyhollow's opens past the old face: below is the network's trench, with its dwarf city and
/// a deep road up to every mine over it, and at the far end one tunnel down to the Depths, where the Rift burns and
/// Duragon, lord over the Seven, sits by it. From the Depths a way climbs to every trench in the world. Every so often
/// the demons raid upward (see <see cref="DemonRaids"/>): the dwarf city first, then out of a mine and onto the surface,
/// where three times in ten heaven's host comes down on them, and on everything near them.
/// </summary>
public partial class Main
{
    readonly Dictionary<int, Trench> _trenches = [];
    Trench? _deepTrench;
    readonly List<Person> _deepFolk = [];
    readonly List<Enemy> _deepFoes = [];
    Enemy? _duragon;
    string? _raidMet;
    readonly List<Enemy> _raidBand = [];
    double _heavenIn = -1, _deepTick;

    static readonly string Hell = "hell", Heaven = "heaven", Deep = "deep";

    /// <summary>A trench room by its seed: the room only knows the seed it was built from.</summary>
    Trench TrenchOf(int seed) => _trenches.TryGetValue(seed, out var t) ? t : Deeps.Get(_world, 0, 0);

    /// <summary>Where a mine's mouth is on the surface, to come up at.</summary>
    Vector3 MouthOf(Settlement? mine) => mine is null ? MineDoor + new Vector3(0, 0, 3) : new Vector3((float)mine.X + 4, 0, (float)mine.Z + 4);

    (string Verb, Action Act)? DeepAction(Vector3 pos)
    {
        // Greyhollow, past the old face: the way down to the trench under Tristinwold.
        if (_cave is not null && _mine.Broken && pos.DistanceTo(Cave.FaceAt) < 5)
            return ("open the dwarf door down to the Deep Trenches", () => { var out_ = MineDoor + new Vector3(0, 0, 3); _ = LeaveMine(); _outside = out_; GoDeep("trench", Deeps.Get(_world, 0, 0)); });
        if (_indoors is null) return null;
        var stage = Indoors.StageAt;
        var deep = _indoors.Kind switch { "mineshaft" => 36f, "trench" => 110f, "depths" => 150f, _ => 0f };
        var back = stage + new Vector3(0, 0, -deep / 2 + 2.5f);
        switch (_indoors.Kind)
        {
            case "mineshaft" when pos.DistanceTo(_indoors.DeepDoorAt with { Y = pos.Y }) < 3.2f && Shafts.TryGetValue(_indoors.Seed, out var mine):
                return ("open the dwarf door down to the Deep Trenches", () => GoDeep("trench", Deeps.Under(_world, mine.X, mine.Z)));
            case "trench" when pos.DistanceTo(back with { Y = pos.Y }) < 4:
                return ("take the tunnel down to the Dark Depths", () => GoDeep("depths", null));
            case "trench" when pos.DistanceTo(DeepRoadSign with { Y = pos.Y }) < 3:
                return ("take the deep road to another mine", DeepRoads);
            case "depths" when pos.DistanceTo(DeepRoadSign with { Y = pos.Y }) < 3:
                return ("climb up into another trench", ClimbToTrench);
        }
        return null;
    }

    /// <summary>The signpost just inside the way in: the deep road's arms in a trench, the ways up in the Depths.</summary>
    Vector3 DeepRoadSign => Indoors.StageAt + new Vector3(4, 0, (_indoors?.Kind == "depths" ? 150 : 110) / 2f - 8);

    /// <summary>Down a level: the room above goes at once (so it can't undo the new one's sky), and the new one's built.</summary>
    void GoDeep(string kind, Trench? trench)
    {
        if (_player.Mount is not null) { Sound.Ui("error"); _hud.Toast("Not on a mount. Leave it up top.", 4); return; }
        if (trench is not null) { _trenches[trench.Seed] = trench; _deepTrench = trench; }
        if (_indoors is { } old) { RemoveChild(old); old.QueueFree(); }
        _indoors = new Indoors(this, kind, trench?.Seed ?? 0);
        AddChild(_indoors);
        _player.GlobalPosition = _indoors.WayIn;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        Sound.Ui("equip", -8);
        _hud.Toast(kind == "trench" ? $"Down and down, into the Deep Trenches: the old dwarf roads, and the lamps of {TrenchOf(trench!.Seed).City}." : "Down a tunnel that goes on longer than any tunnel should, and out into the Dark Depths. The air is hot, and far off there's a red light like a wound.", 8);
    }

    void DeepRoads()
    {
        if (_deepTrench is not { } t) return;
        var ways = t.Mines.Select(m => ($"Up to {m.Name}", (Action)(() => { _outside = MouthOf(m); GoOutside(); _hud.Toast($"The deep road climbs, and climbs, and comes out at the back of {m.Name}.", 6); }))).ToList();
        if (t.Home) ways.Insert(0, ("Up to the Greyhollow mine, under Tristinwold", () => { _outside = MouthOf(null); GoOutside(); }));
        ways.Add(("Stay down here.", _hud.EndTalk));
        _hud.Talk($"The Deep Road, {t.City}", "A dwarf-cut signpost, with a lamp in a cage on top. Each arm points up a road to a mine.", [.. ways]);
    }

    void ClimbToTrench()
    {
        var from = _deepTrench ?? Deeps.Get(_world, 0, 0);
        var ways = Deeps.Around(_world, from.I * Deeps.Cell, from.J * Deeps.Cell, 3).Take(9).Select(t =>
            ($"Up to {t.City}{(t.Home ? ", under Tristinwold" : t.Mines.Count > 0 ? $", under {t.Mines[0].Name}" : "")}", (Action)(() =>
            {
                _outside = MouthOf(t.Home ? null : t.Mines.FirstOrDefault());
                GoDeep("trench", t);
            }))).ToList();
        ways.Add(("Stay down here.", _hud.EndTalk));
        _hud.Talk("The Dark Depths", "Tunnels climb away in every direction, and the dwarves have cut their marks by each: the name of the trench it reaches.", [.. ways]);
    }

    // ---- building the deep rooms ------------------------------------------------------------------------------------

    /// <summary>A trench: a long cavern with pillars of living rock, the dwarf city in the middle, the deep road's sign, and the tunnel down.</summary>
    public void FurnishTrench(Indoors room, int seed)
    {
        var t = TrenchOf(seed);
        _deepTrench = t;
        var stage = Indoors.StageAt;
        var rng = new Rng((ulong)seed ^ 0xD3E9);
        var rock = new StandardMaterial3D { AlbedoColor = new Color("#4a4440"), Roughness = 1 };
        // The city's hall, in the middle of the tunnels: pillars of living rock down both sides, the houses between.
        for (var z = -14f; z < 24; z += 12)
            foreach (var side in new[] { -1f, 1f })
                room.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.4f, BottomRadius = 1.9f, Height = 14 }, MaterialOverride = rock, Position = stage + new Vector3(side * 15.5f, 7, z) });
        for (var i = 0; i < 6; i++)
        {
            var at = stage + new Vector3(i % 2 == 0 ? -9 : 9, 0, -11 + i / 2 * 11);
            Buildings.Place(room, i == 2 ? "smithy" : "house", at, i % 2 == 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2, seed + i);
        }
        foreach (var z in new[] { -12f, 6f, 20f })
        {
            room.AddChild(Placed(Lairfolk.Light("#ffb060", 1.8f, 18, flicker: 0.1f), stage + new Vector3(0, 9, z)));
            room.AddChild(Placed(Lairfolk.Motes(new Color(0.6f, 0.8f, 1f, 0.5f), 0.05f, 20, new Vector3(10, 4, 4), 0.1f), stage + new Vector3(0, 6, z)));
        }
        room.AddChild(Placed(Lairfolk.Light("#ffb060", 1.4f, 14, flicker: 0.1f), stage + new Vector3(0, 4, 49))); // the way in
        // The side rooms off the old roads: a storeroom, a shrine to the ancestors, a grotto of glowing caps.
        foreach (var (m, i) in room.SideRooms.Select((m, i) => (m, i)))
        {
            var (at, size) = room.RoomSpan(m);
            switch (i % 3)
            {
                case 0:
                    Models.Place(room, "dungeon/keg.glb", at + new Vector3(-size.X / 2 + 1.4f, 0, -1), 0.8f);
                    Models.Place(room, "dungeon/keg.glb", at + new Vector3(-size.X / 2 + 1.4f, 0, 0.4f), 0.8f);
                    Models.Place(room, "ph:crate.glb", at + new Vector3(size.X / 2 - 1.6f, 0, 0), 1.1f, 0.5f);
                    room.Glow(at + Vector3.Up * 3, "#ffb060", 1.2f, 12);
                    break;
                case 1:
                    room.Block(at + new Vector3(0, 0.6f, -size.Y / 2 + 1.2f), new Vector3(2.4f, 1.2f, 1), Surface.Stone);
                    room.Block(at + new Vector3(0, 2.4f, -size.Y / 2 + 0.8f), new Vector3(1.4f, 2.4f, 0.4f), Surface.Stone, solid: false);
                    room.Glow(at + new Vector3(0, 2, -size.Y / 2 + 2), "#a0c8ff", 1.4f, 12);
                    break;
                default:
                    var cap = new StandardMaterial3D { AlbedoColor = new Color("#60e0c8"), EmissionEnabled = true, Emission = new Color("#30c0a0"), EmissionEnergyMultiplier = 1.6f };
                    for (var k = 0; k < 7; k++)
                    {
                        var p = at + new Vector3(((float)rng.NextDouble() - 0.5f) * (size.X - 2), 0, ((float)rng.NextDouble() - 0.5f) * (size.Y - 2));
                        var h = 0.4f + (float)rng.NextDouble() * 1.2f;
                        room.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.09f, Height = h }, MaterialOverride = rock, Position = p + Vector3.Up * h / 2 });
                        room.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.35f + h * 0.2f, Height = 0.3f + h * 0.15f }, MaterialOverride = cap, Position = p + Vector3.Up * h });
                    }
                    room.Glow(at + Vector3.Up * 2, "#40e0c0", 1.2f, 12);
                    break;
            }
        }
        room.AddChild(new Label3D { Text = t.City, Position = stage + new Vector3(0, 8, -6), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 96, OutlineSize = 16, PixelSize = 0.02f, Modulate = new Color("#ffd27a") });
        room.AddChild(new Label3D { Text = "The Deep Road\n(to the mines above)", Position = DeepRoadSign + new Vector3(0, 2.4f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#e8d8b0") });
        room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 2.2f, 0.2f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#5a4030") }, Position = DeepRoadSign + new Vector3(0, 1.1f, 0) });
        // The tunnel down, at the far end, with the Depths' red light coming up it.
        var mouth = stage + new Vector3(0, 0, -53);
        room.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(5, 5) }, Position = mouth + new Vector3(0, 2.5f, -1.6f), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#1a0606"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        room.AddChild(Placed(Lairfolk.Light("#ff3020", 2.2f, 10, flicker: 0.3f), mouth + new Vector3(0, 1, -0.5f)));
        room.AddChild(new Label3D { Text = "Down to the Dark Depths", Position = mouth + new Vector3(0, 5.5f, -1.5f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#ff8a6a") });
        // The dwarves of the city.
        (string Role, string Job, string Look, Vector3 At)[] folk =
        [
            ("thane", $"thane of {t.City}", "aldric", stage + new Vector3(0, 0, -4)),
            ("smith", $"smith of {t.City}", "worker", stage + new Vector3(-5, 0, -10)),
            ("brewer", $"brewer of {t.City}", "drinker", stage + new Vector3(5, 0, 2)),
            ("lorekeeper", $"keeper of the ancestors' hall in {t.City}", "scholar", stage + new Vector3(-4, 0, 10)),
            ("guard", $"of the guard of {t.City}", "guard", stage + new Vector3(3, 0, 20)),
        ];
        foreach (var (role, job, look, at) in folk)
        {
            var sex = rng.Chance(0.5) ? Sex.Male : Sex.Female;
            var name = role == "thane" ? t.Thane : Names.Person(rng, sex);
            var npc = new Npc($"deep:{t.Id}:{role}", name, sex, 60 + rng.Next(140), job, t.Id, t.City, $"{name} of {t.City}, in the Deep Trenches, where the dwarves have always been.", ["the deep", "the ancestors"], "gruff",
                "Up from the surface? It's been a while since one of you came down the old roads.", at.X, at.Z, Race: "dwarf");
            _deepFolk.Add(MeetAt(npc, look, stage.Y));
        }
        // A raid at the gates: demons in the streets, and the city's guard holding them.
        if (_city.Demons is { Surfaced: false } r && r.Trench == t.Id) PeopleTheRaid(stage, room);
    }

    void PeopleTheRaid(Vector3 stage, Indoors room)
    {
        var level = Math.Max(8, _player.Hero.Level);
        for (var i = 0; i < 7; i++)
        {
            var at = stage + new Vector3(-4.5f + i % 4 * 3, 0.4f, -17 + i / 4 * 3); // in at the far end of the city's hall
            var e = Enemy.Create(Bestiary.Get(i < 3 ? "demon_grunt" : "imp"), at, Northwood.DangerFor(level), look: i < 3 ? "demon_grunt" : null);
            (e.Faction, e.Foe, e.Roam) = (Hell, Deep, 10);
            AddEnemy(e);
            e.Provoke(_player);
            _deepFoes.Add(e);
        }
        for (var i = 0; i < 3; i++)
        {
            var at = stage + new Vector3(-3 + i * 3, 0.4f, -6);
            var e = Enemy.Create(Bestiary.Get("bandit_chief"), at, Northwood.DangerFor(level - 2), look: "warmaster");
            e.Stats.Name = $"Dwarf of {_deepTrench!.City}";
            (e.Allied, e.Faction, e.Foe, e.Roam) = (true, Deep, Hell, 12);
            AddEnemy(e);
            e.RefreshPlate();
            _deepFoes.Add(e);
        }
        _hud.Toast($"{_deepTrench!.City} is burning. Demons up out of the Depths are in the streets, and the dwarves are holding the gate hall. Help them!", 9);
    }

    /// <summary>The Dark Depths: a great cavern, red-lit, with the Rift at the far end and Duragon on his throne of bone before it.</summary>
    public void FurnishDepths(Indoors room)
    {
        var stage = Indoors.StageAt;
        var rock = new StandardMaterial3D { AlbedoColor = new Color("#2a2020"), Roughness = 1 };
        var rng = new Rng(0xDEE9);
        // Fangs of rock about the Rift's cavern (the burrows up from it are the warren, see Indoors.CutWarren).
        for (var i = 0; i < 12; i++)
        {
            var at = stage + new Vector3(rng.Next(56) - 28, 0, -72 + rng.Next(42));
            if (Mathf.Abs(at.X - stage.X) < 22 && at.Z < stage.Z - 46) continue; // not in the Rift, nor on the throne
            if (Mathf.Abs(at.X - stage.X) < 6) continue;
            room.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 1.4f + rng.Next(3), Height = 4 + rng.Next(12) }, MaterialOverride = rock, Position = at + Vector3.Up * 2 });
        }
        room.AddChild(Placed(Lairfolk.Light("#ff6030", 1.4f, 16, flicker: 0.2f), stage + new Vector3(0, 4, 66))); // the way in
        // The burrows' side rooms: a pit of bones, a pool of fire, a nest with something in it.
        foreach (var (m, i) in room.SideRooms.Select((m, i) => (m, i)))
        {
            var (at, size) = room.RoomSpan(m);
            switch (i % 3)
            {
                case 0:
                    var bones = new StandardMaterial3D { AlbedoColor = new Color("#d8d0c0"), Roughness = 0.8f };
                    for (var k = 0; k < 14; k++)
                        room.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 0.6f + (float)rng.NextDouble() }, MaterialOverride = bones,
                            Position = at + new Vector3(((float)rng.NextDouble() - 0.5f) * (size.X - 3), 0.1f, ((float)rng.NextDouble() - 0.5f) * (size.Y - 3)), Rotation = new Vector3(Mathf.Pi / 2, (float)rng.NextDouble() * 3, 0) });
                    room.Glow(at + Vector3.Up * 3, "#ff8050", 1, 12);
                    break;
                case 1:
                    var fire = new StandardMaterial3D { AlbedoColor = new Color("#ff5010"), EmissionEnabled = true, Emission = new Color("#ff3000"), EmissionEnergyMultiplier = 2.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
                    room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(size.X * 0.5f, 0.06f, size.Y * 0.5f) }, MaterialOverride = fire, Position = at + new Vector3(0, 0.05f, 0) });
                    room.Glow(at + Vector3.Up * 2, "#ff4010", 2, 14);
                    break;
                default:
                    for (var k = 0; k < 2; k++)
                    {
                        var e = Enemy.Create(Bestiary.Get(k == 0 ? "imp" : "dire_imp"), at + new Vector3(k * 2 - 1, 0.4f, 0), Northwood.DangerFor(Math.Max(10, _player.Hero.Level)));
                        (e.Faction, e.Roam) = (Hell, 4);
                        AddEnemy(e);
                        _deepFoes.Add(e);
                    }
                    room.Glow(at + Vector3.Up * 3, "#ff6030", 1, 12);
                    break;
            }
        }
        // The Rift: a wound in the floor that goes down forever, burning.
        var rift = stage + new Vector3(0, 0.05f, -58);
        var burn = new StandardMaterial3D { AlbedoColor = new Color("#ff3010"), EmissionEnabled = true, Emission = new Color("#ff2a10"), EmissionEnergyMultiplier = 3, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(40, 0.1f, 10) }, MaterialOverride = burn, Position = rift });
        room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3, 18, 0.4f) }, MaterialOverride = burn, Position = rift + new Vector3(0, 9, -4) });
        room.AddChild(Placed(Lairfolk.Light("#ff3010", 4, 60, flicker: 0.35f), rift + new Vector3(0, 6, 0)));
        room.AddChild(Placed(Lairfolk.Motes(new Color(1f, 0.35f, 0.1f, 0.9f), 0.2f, 120, new Vector3(20, 2, 5), 3f), rift + Vector3.Up));
        room.AddChild(new Label3D { Text = "The Rift", Position = rift + new Vector3(0, 12, -3), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 120, OutlineSize = 18, PixelSize = 0.025f, Modulate = new Color("#ff8a6a") });
        room.AddChild(new Label3D { Text = "Ways up\n(to every trench)", Position = DeepRoadSign + new Vector3(0, 2.4f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#e8d8b0") });
        room.AddChild(Placed(Lairfolk.Light("#ffb060", 1.4f, 10), DeepRoadSign + new Vector3(0, 3, 0)));
        // The Host on this side of it: brutes and imps, and their lord.
        for (var i = 0; i < 8; i++)
        {
            var at = stage + new Vector3(-16 + i % 4 * 10, 0.4f, -32 - i / 4 * 7); // in the Rift's cavern, between it and the burrows
            var e = Enemy.Create(Bestiary.Get(i % 3 == 0 ? "demon_grunt" : i % 3 == 1 ? "imp" : "dire_imp"), at, Northwood.DangerFor(Math.Max(12, _player.Hero.Level + 2)), look: i % 3 == 0 ? "demon_grunt" : null);
            (e.Faction, e.Roam) = (Hell, 10);
            AddEnemy(e);
            _deepFoes.Add(e);
        }
        if (!_city.DuragonDown)
        {
            var throne = rift + new Vector3(0, 0, 8);
            var bone = new StandardMaterial3D { AlbedoColor = new Color("#d8d0c0"), Roughness = 0.8f };
            room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3, 1, 2.4f) }, MaterialOverride = bone, Position = throne + new Vector3(0, 0.5f, -1) });
            room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3, 4, 0.5f) }, MaterialOverride = bone, Position = throne + new Vector3(0, 2, -2) });
            var level = _city.DuragonLevel;
            _duragon = Enemy.Create(Bestiary.Get("duragon"), throne + new Vector3(0, 0.6f, 1), Northwood.DangerFor(level), look: "duragon");
            _duragon.Stats.Name = $"Duragon, Lord of the Seven (level {level})";
            (_duragon.Faction, _duragon.Roam) = (Hell, 12);
            AddEnemy(_duragon);
            _duragon.Scale = Vector3.One * 1.45f;
            _duragon.RefreshPlate();
            Aura(_duragon, new Color("#ff3030"), 4);
            _deepFoes.Add(_duragon);
        }
    }

    public void LeftTheDeep()
    {
        foreach (var p in _deepFolk.Where(IsInstanceValid)) Dismiss(p);
        _deepFolk.Clear();
        foreach (var e in _deepFoes.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
        _deepFoes.Clear();
        _duragon = null;
    }

    // ---- the dwarves ---------------------------------------------------------------------------------------------

    IEnumerable<(string, Action)> DeepTalk(Person person)
    {
        var id = person.Npc.Id;
        if (!id.StartsWith("deep:") || _deepTrench is not { } t) yield break;
        var role = id[(id.LastIndexOf(':') + 1)..];
        var raid = _city.Demons is { Surfaced: false } r && r.Trench == t.Id;
        switch (role)
        {
            case "thane":
                yield return ("Where do the dwarves come from?", () => Say(person, $"\"Here. Every dwarf on your surface, the mountain clans and the ones in your towns alike, came up out of these roads, a long time ago. Some of us stayed. {t.City} has stood since before your city had a name.\""));
                yield return ("What's below?", () => Say(person, "\"The Dark Depths. One dark under all the world, and every trench has a road down to it. There's a rift down there now, under your Tristinwold, and the hells are coming through it. They come up our roads first. They always come up our roads first.\""));
                if (raid) yield return ("How can I help?", () => Say(person, "\"Get into the gate hall and kill every demon you find. My guard's holding, but not for long.\""));
                break;
            case "smith":
                yield return ("What do you sell?", () => DeepWares(person, ["iron_ingot", "silver_ingot", "gold_ingot", "warding_draught"]));
                break;
            case "brewer":
                yield return ("Something to drink?", () => DeepWares(person, ["ale", "honey_cake", "stamina_brew"]));
                break;
            case "lorekeeper":
                yield return ("Tell me about the ancestors.", () => Say(person, "\"They dug down, not up. Down to where the stone is warm and the gems grow. Then they found what's under the stone, and they built the gates, and they've been holding them since. Your Immortals aren't the only ones who don't stay dead, you know: our ancestors walk the halls down here on the bad nights.\""));
                yield return ("Who is Duragon?", () => Say(person, "\"The lord over the Seven. The Sins: Pride, Wrath, Envy, Greed, Sloth, Gluttony, Lust. He sits by the Rift and waits for them. One of them's already out. You'll have heard of him: gold armour, big moustache, very pleased with himself.\""));
                break;
        }
    }

    void DeepWares(Person person, string[] wares)
    {
        var options = wares.Select(id => Items.Get(id)).OfType<Item>().Select(item =>
        {
            var price = _city.StorePrice(item);
            return ($"{item.Name}, {price} crowns", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); _hud.Toast("You can't afford that.", 3); return; }
                _player.Crowns -= price;
                _player.Pack.Add(item.Id);
                Sound.Ui("coin");
                Save();
                DeepWares(person, wares);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "\"Mind the roads going up.\"")));
        _hud.Talk($"{person.Npc.Name} ({_player.Crowns} crowns)", "Dwarf-made. It'll outlast you.", [.. options]);
    }

    // ---- the raids ------------------------------------------------------------------------------------------------

    void TendDeeps(double delta)
    {
        TendHeaven(delta);
        if ((_deepTick -= delta) > 0) return;
        _deepTick = 1;
        // Down in a trench under attack: clear the streets, and the raid's broken.
        if (_indoors?.Kind == "trench" && _city.Demons is { Surfaced: false } r && r.Trench == _deepTrench?.Id && _deepFoes.Count > 0
            && _deepFoes.Where(e => IsInstanceValid(e) && e.Faction == Hell).All(e => e.State == EnemyState.Dead))
        {
            DoCity(CityAction.RaidEnded(r.Id, _player.Company, true));
            RaidPaid($"The last demon in {r.City} falls. The dwarves drag the bodies back down the tunnel and shout your name after them.");
            _deepFoes.Clear();
        }
        TendSurfaceRaid();
    }

    /// <summary>Starts the next raid when one's due: up the network nearest this hero, out of one of its mines.</summary>
    void BeginRaidIfDue()
    {
        if (_city.Demons is not null || _city.Day < _city.NextRaid) return;
        var p = _player.GlobalPosition;
        var trenches = Deeps.Around(_world, p.X, p.Z, 1).Where(t => t.Mines.Count > 0 || t.Home).ToList();
        if (trenches.Count == 0) return;
        var t = trenches[_city.Day % trenches.Count];
        var mine = t.Mines.Count > 0 && !(t.Home && _city.Day % 2 == 0) ? t.Mines[_city.Day % t.Mines.Count] : null;
        var mouth = MouthOf(mine);
        var town = mine is null ? null : _world.SettlementsNear(mine.X, mine.Z, 1600).Where(s => s.Kind is SettlementKind.Town or SettlementKind.Hamlet or SettlementKind.City && !s.Hostile).MinBy(s => s.DistanceTo(mine.X, mine.Z));
        DoCity(CityAction.RaidBegan(new DemonRaid
        {
            Id = $"raid-{t.Id}-{_city.Day}", Trench = t.Id, City = t.City, Mine = mine?.Id ?? "greyhollow", MineName = mine?.Name ?? "the Greyhollow mine",
            X = mouth.X, Z = mouth.Z, Town = mine is null ? ReputationBook.Capital : town?.Id ?? "", TownName = mine is null ? "Tristinwold" : town?.Name ?? "the country round it",
        }));
    }

    /// <summary>A raid on the surface: the demons by the mine they broke out of, and if heaven answers, heaven.</summary>
    void TendSurfaceRaid()
    {
        if (_city.Demons is not { Surfaced: true } r || _indoors is not null || _cave is not null)
        {
            if (_raidBand.Count > 0 && _city.Demons is null) ClearRaidBand();
            return;
        }
        var p = _player.GlobalPosition;
        var at = new Vector3((float)r.X, 0, (float)r.Z);
        var far = new Vector2(p.X - at.X, p.Z - at.Z).Length();
        if (_raidMet == r.Id)
        {
            if (far > 260) { ClearRaidBand(); _raidMet = null; return; }
            if (_raidBand.Count > 0 && _raidBand.Where(e => IsInstanceValid(e) && e.Faction == Hell).All(e => e.State == EnemyState.Dead))
            {
                DoCity(CityAction.RaidEnded(r.Id, _player.Company, !r.Heaven));
                if (!r.Heaven) RaidPaid($"The last of the demons out of {r.MineName} goes down. {r.TownName} won't burn today.");
                else _hud.Toast($"The demons are gone. So is most of the country round {r.MineName}: heaven's host doesn't care what it burns.", 10);
                ClearRaidBand();
            }
            return;
        }
        if (far > 160) return;
        _raidMet = r.Id;
        _terrain.EnsureAround(at, 1);
        var level = Math.Max(10, _player.Hero.Level + 1);
        for (var i = 0; i < 8; i++)
        {
            var spot = at + new Vector3(Mathf.Cos(i * 0.8f) * 8, 0, Mathf.Sin(i * 0.8f) * 8);
            var kind = i < 3 ? "demon_grunt" : i == 3 ? "dire_imp" : "imp";
            var e = Enemy.Create(Bestiary.Get(kind), spot with { Y = City.FloorAt(spot) + 0.5f }, Northwood.DangerFor(level), look: kind == "demon_grunt" ? kind : null);
            (e.Faction, e.Roam) = (Hell, 16);
            AddEnemy(e);
            e.Provoke(_player);
            _raidBand.Add(e);
        }
        _hud.Toast($"Demons! Up out of {r.MineName} from the Dark Depths, brutes with bone axes and imps by the dozen, making for {r.TownName}.", 9);
        if (r.Heaven) _heavenIn = 7;
    }

    /// <summary>The gates of heaven open: a pillar of light, and the host comes down it, and kills the demons, and anyone else in the way.</summary>
    void TendHeaven(double delta)
    {
        if (_heavenIn < 0 || (_heavenIn -= delta) > 0) return;
        _heavenIn = -1;
        if (_city.Demons is not { Surfaced: true } r) return;
        var at = new Vector3((float)r.X, 0, (float)r.Z);
        var pillar = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 6, BottomRadius = 4, Height = 220 }, Position = at with { Y = City.FloorAt(at) + 110 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 0.95f, 0.75f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color("#fff0c0"), EmissionEnergyMultiplier = 3 },
        };
        AddChild(pillar);
        GetTree().CreateTimer(40).Timeout += () => { if (IsInstanceValid(pillar)) pillar.QueueFree(); };
        for (var i = 0; i < 5; i++)
        {
            var spot = at + new Vector3(Mathf.Cos(i * 1.25f) * 6, 0, Mathf.Sin(i * 1.25f) * 6);
            var floor = City.FloorAt(spot) + 0.5f;
            var angel = Enemy.Create(Bestiary.Get("angel"), spot with { Y = floor + 40 }, Northwood.DangerFor(Math.Max(14, _player.Hero.Level + 4)), look: "angel");
            (angel.Faction, angel.Foe, angel.Roam, angel.Expires) = (Heaven, Hell, 30, 90);
            AddEnemy(angel);
            Wings(angel);
            Aura(angel, new Color("#fff0c0"), 3);
            var down = angel.CreateTween();
            down.TweenProperty(angel, "global_position:y", floor, 3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            angel.Provoke(_player); // they don't care who's in the way
            _raidBand.Add(angel);
        }
        _hud.Toast("The sky splits. A pillar of light comes down over the mine, and down it come winged things in gold and white with bows of light and burning swords. They kill the demons. They kill everything else too. RUN.", 12);
        _hud.Log($"Heaven's host has come down over {r.MineName}.");
    }

    /// <summary>White wings at an angel's back.</summary>
    static void Wings(Node3D angel)
    {
        var feather = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 0.96f, 0.92f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled, EmissionEnabled = true, Emission = new Color("#fff8e0"), EmissionEnergyMultiplier = 0.6f };
        foreach (var side in new[] { -1f, 1f })
            angel.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1.8f, 2.4f) }, MaterialOverride = feather, Position = new Vector3(side * 1.0f, 1.9f, -0.35f), RotationDegrees = new Vector3(0, side * 35, side * -20) });
    }

    void ClearRaidBand()
    {
        foreach (var e in _raidBand.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) e.QueueFree();
        _raidBand.Clear();
    }

    void RaidPaid(string said)
    {
        var level = _player.Hero.Level;
        var pay = 300 + 30 * level;
        _player.Crowns += pay;
        GainXp(120 + 20 * level);
        Sound.Ui("quest");
        _hud.Toast($"{said} (+{pay} crowns)", 10);
        Save();
    }

    void OnDeepSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim != _duragon) return;
        _duragon = null;
        DoCity(CityAction.DuragonFell(_player.Company));
        var level = _player.Hero.Level;
        _player.Crowns += 2000 + 80 * level;
        GainXp(400 + 50 * level);
        Sound.Ui("coin", -2);
        _hud.Toast("Duragon falls into his own Rift, laughing, and the fire swallows him. He'll climb back out. But not today.", 10);
        Save();
    }

    void OnDeepWin()
    {
        if (_duragon is { } d && IsInstanceValid(d) && d.State != EnemyState.Dead && d.GlobalPosition.DistanceTo(_player.GlobalPosition) < 60)
        {
            DoCity(CityAction.DuragonWon());
            _hud.Toast("Duragon lifts you by the throat and looks at you a long while with his red eyes. \"Not yet. You'll be more use to me later.\" Then he lets go.", 9);
        }
    }

    // ---- for the self-test ---------------------------------------------------------------------------------------

    public void GoDeepForTest(string kind, Trench? trench) => GoDeep(kind, trench);
    public (string Verb, Action Act)? DeepActionForTest(Vector3 at) => DeepAction(at);
    public Trench? DeepTrenchForTest => _deepTrench;
    public IReadOnlyList<Person> DeepFolkForTest => _deepFolk;
    public IReadOnlyList<Enemy> DeepFoesForTest => _deepFoes;
    public Enemy? DuragonForTest => _duragon;
    public IReadOnlyList<Enemy> RaidBandForTest => _raidBand;
    public void BeginRaidForTest() => BeginRaidIfDue();
    public void TendDeepsForTest() { _deepTick = 0; TendDeeps(0.01); }
    public void HeavenNowForTest() { _heavenIn = 0.001; TendHeaven(0.01); }
    public Vector3 DeepRoadSignForTest => DeepRoadSign;
}
