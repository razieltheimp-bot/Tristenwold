using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Life in the Northwood (see <see cref="Northwood"/>): Hollowheart, the village grown up through its greatest trees;
/// the logging camp at the wood's edge; the four guardians, who leave you be unless you've cut their trees and walk their
/// ground; the corruption round the one that's turned; and the dragon under the western front that sets it all going.
/// </summary>
public partial class Main
{
    ForestBook _forest = new();
    Forest? _forestNode;
    const string Elder = "hollowheart/oruun", Druid = "hollowheart/maren", Sprout = "hollowheart/thistle", Shroomie = "hollowheart/morel", Leader = "hollowheart/wukang", Rafikki = "northwood/rafikki";

    public ForestBook ForestLog => _forest;
    public Forest? ForestNode => _forestNode;

    void AddForest()
    {
        var (vx, vz) = (Northwood.VillageX, Northwood.VillageZ);
        Townsfolk(new Npc(Elder, "Oruun Greybrow", Sex.Male, 70, "elder of Hollowheart", "hollowheart", "Hollowheart",
            "Oruun has sat at the root of the Heartwood longer than anyone can say. He has a top-knot gone grey, a string of river-pearls, and a way of looking at you until you remember your manners.",
            ["the forest", "old battles"], "dry", "Sit. The tree isn't going anywhere, and neither are you.", vx + 16, vz + 14), "ape_elder");
        Townsfolk(new Npc(Leader, "Lu Wukang", Sex.Male, 41, "king of the ape people", "hollowheart", "Hollowheart",
            "Lu Wukang sits at the top of every argument in Hollowheart with his red-and-gold staff across his knees. He has broken more jaws than treaties, and he guards the wood's secrets better than either.",
            ["old battles", "the forest"], "proud", "You're a long way from your walls, little cousin.", vx + 24, vz + 6), "ape_warrior");
        Townsfolk(new Npc(Rafikki, "Rafikki", Sex.Male, 66, "a druid nobody speaks of", "northwood", "the Northwood",
            "A baboon-faced ape in a nest of old branches, staff wrapped in rag, butterflies coming and going from his fur. The apes of Hollowheart drove him out and won't say his name. He says the wood has a lord, and the lord is sleeping.",
            ["the forest", "dreams"], "sly", "Ah! Somebody with an axe in their eyes. Come up, come up.", Northwood.RafikkiX, Northwood.RafikkiZ), "ape_elder");
        Townsfolk(new Npc(Druid, "Maren of the Roots", Sex.Female, 52, "druid of the Northwood", "hollowheart", "Hollowheart",
            "Maren came to the wood to study it and was, she says, studied back. She tends what the guardians can't reach.",
            ["the forest", "herbs"], "kindly", "Mind where you step. Most of it's listening.", vx - 18, vz + 12), "priest");
        Townsfolk(new Npc(Sprout, "Thistle-in-Spring", Sex.Female, 30, "speaker of the plant folk", "hollowheart", "Hollowheart",
            "Thistle grew up, in the most exact sense, at the foot of the guardian she prays to. She doesn't blink, and asks a lot of questions about fire.",
            ["the forest", "dreams"], "warm", "You smell of the road. Is it nice, the road?", vx + 5, vz + 22), "spriggan");
        Townsfolk(new Npc(Shroomie, "Old Morel", Sex.Male, 40, "keeper of the undergrowth", "hollowheart", "Hollowheart",
            "Morel runs the village's cellar-market from a damp hollow, and remembers everything anyone has ever spilled.",
            ["trade", "the forest"], "gruff", "Spores are free. Everything else, you pay.", vx - 7, vz + 24), "shroomie");
        City.Zones.Add(new Zone("Hollowheart", new Vector3((float)vx, 0, (float)vz), 64, 10));
        City.Zones.Add(new Zone("The Strand", new Vector3(-980, 0, -260), 120, 9));
        City.Zones.Add(new Zone("The Logging Camp", new Vector3((float)Northwood.CampX, 0, (float)Northwood.CampZ), 38, 9));
        Sign(new Vector3((float)Northwood.CampX, 5, (float)Northwood.CampZ), "The Logging Camp");
        AddChild(_forestNode = new Forest(this));
    }

    /// <summary>A tree fallen in the wood: the guardians notice, even at the camp.</summary>
    void ForestWorked(WorkSpot spot, Vector3 at)
    {
        if (!spot.IsTree || !Northwood.Inside(at.X, at.Z) && !spot.Id.StartsWith("camp_")) return;
        if (_forest.TreesCut++ == 0) _hud.Toast("Somewhere deep in the Northwood, something very large turns its head.", 7);
    }

    /// <summary>What a tree in the corrupted ground gives: its logs are rotten through, and they burn foul.</summary>
    public bool OnRotten(Vector3 at) => Northwood.Corrupted(at.X, at.Z);

    IEnumerable<(string, Action)> ForestTalk(Person person)
    {
        var id = person.Npc.Id;
        if (id == Shroomie) foreach (var o in SellHollow(person)) yield return o;
        if (id == Elder)
        {
            var c = _city;
            if (_forest.Stage == ForestStage.None)
                yield return ("What's wrong with the wood?", () =>
                {
                    Say(person, $"One of the guardians has turned. {Northwood.Guardians.First(g => g.Corrupted).Name}, who mended the east of the wood, is rotting it instead. It pains me to say this to a stranger: end it. It grows back. It always has. That's the mercy in it, and the horror.");
                    _forest.Stage = ForestStage.Asked;
                    Save();
                });
            else if (_forest.Stage == ForestStage.Slain)
                yield return ("It's done.", () =>
                {
                    _player.Crowns += ForestBook.Pay;
                    _forest.Stage = ForestStage.Asked;
                    _forest.Ended++;
                    Sound.Ui("coin");
                    Say(person, $"Not done. Never done. It'll stand up again, and we'll ask again. {ForestBook.Pay} crowns, and our thanks, such as they are.");
                    Save();
                });
            else yield return ("Tell me about the guardians.", () => Say(person, "Four. They mended this wood before there was a Tristinwold. The plant folk pray to them. They'll leave you be, unless you've taken an axe to the trees. Then don't walk their ground."));
            if (c.LairOpen) yield return ("Something opened in the west.", () => Say(person, c.DragonFree ? "It's out. Everything we were afraid of is out." : "We felt it. There's a barrier on that place. The guardians ARE the barrier. Do not kill another. Do you hear me? Not one."));
        }
        if (id == Leader)
        {
            yield return ("Where are the other guardians?", () => Say(person, "Where they've always been, and that's all I'll say. Ask me again and I'll answer with the staff. There are some roads you don't walk a stranger down."));
            yield return ("Who is Rafikki?", () => Say(person, "We don't say that name. He was the wisest of us, and he listened to something under the mountains until there was no room left in him for anything else."));
        }
        if (id == Rafikki)
        {
            yield return ("What do you want?", () => Say(person, "To wake my lord! He sleeps under the western front, walled in by the four, and they will not let him rise. Kill the rotten one, and the door opens. Kill one more, a true one, and the wall comes down. Oh, it'll be wonderful. Everything will burn green."));
            yield return (_forest.Told ? "Remind me where they stand." : "Where are the true guardians?", () =>
            {
                _forest.Told = true;
                foreach (var g in Northwood.Guardians.Where(x => !x.Corrupted)) _hud.Log($"{g.Name}: {Direction(person.GlobalPosition, (g.X, g.Z))}");
                Say(person, string.Join(" ", Northwood.Guardians.Where(g => !g.Corrupted).Select(g => $"{g.Name}, {Direction(person.GlobalPosition, (g.X, g.Z))}.")) + " And they don't go empty-handed to the grave. Oh no. They've been growing treasures in their hearts since before your city had walls.");
                _standing.Evil(5, "bargained with Rafikki");
                Save();
            });
        }
        if (id == Druid)
        {
            if (_forest.Blighted)
                yield return ($"Get the rot out of me. ({ForestBook.CurePrice} crowns)", () =>
                {
                    if (_player.Crowns < ForestBook.CurePrice) { Say(person, "It's not the crowns I want, it's the roots they buy. Come back with them, and soon."); return; }
                    _player.Crowns -= ForestBook.CurePrice;
                    _forest.Blighted = false;
                    Sound.Ui("potion");
                    Say(person, "Hold still. It'll hurt, and then it won't.");
                    Save();
                });
            foreach (var pet in CompanionBodies.Where(b => b.Bond is { } bond && (bond.Blighted || bond.Rotted) && b.Leader == _player && b.GlobalPosition.DistanceTo(person.GlobalPosition) < 20).ToList())
                yield return ($"Cure {pet.Bond!.Name}. ({ForestBook.CurePrice} crowns)", () =>
                {
                    if (_player.Crowns < ForestBook.CurePrice) { Say(person, "Bring what it costs, and quickly."); return; }
                    _player.Crowns -= ForestBook.CurePrice;
                    pet.Cured();
                    Sound.Ui("potion");
                    Say(person, $"There. {pet.Bond!.Name} knows you again. Give it a few days before you ask it to be brave.");
                    Save();
                });
        }
        if (id == Druid) yield return ("How do I find my way through the wood?", () => Say(person, "Keep to the road and nothing older than a bear will trouble you. Leave it and the paths will turn you round: they're meant to. The guardians are at the ends of the small ones."));
        if (id == Sprout) yield return ("Who do your people pray to?", () => Say(person, string.Join(", ", Northwood.Guardians.Select(g => g.Name)) + ". We'd thank you not to kill them. It wouldn't feel like thanks."));
    }

    IEnumerable<(string, Action)> SellHollow(Person person)
    {
        yield return ("What have you got?", () =>
        {
            var offers = new[] { "healing_draught", "stamina_brew", "bread", "pine_log", "traveller_cloak" }.Select(i => Items.Get(i)!).Select(item => ($"{item.Name}, {item.Value} crowns", (Action)(() =>
            {
                if (_player.Crowns < item.Value) { Sound.Ui("error"); return; }
                _player.Crowns -= item.Value;
                _player.Pack.Add(item.Id);
                Sound.Ui("coin");
            }))).Append(("That's all.", (Action)_hud.EndTalk)).ToArray();
            _hud.Talk("Morel's Hollow", "Everything here grew here. Don't ask from what.", offers);
        });
    }
}

/// <summary>Hollowheart itself, and what walks the wood round it.</summary>
public partial class Forest(Main main) : Node3D
{
    readonly Dictionary<string, Enemy> _guardians = [];
    readonly Dictionary<string, double> _slainAt = [];
    Enemy? _dragon;
    double _tick;
    bool _toldOpen, _toldFree;

    public IReadOnlyDictionary<string, Enemy> Guardians => _guardians;
    public Enemy? Dragon => _dragon;

    public override void _Ready()
    {
        Enemy.Slain += OnSlain;
        Enemy.Landed += OnLanded;
        BuildVillage();
        var lair = new Vector3((float)Northwood.LairX, 0, (float)Northwood.LairZ);
        Buildings.Place(this, "mine_portal", lair with { Y = City.FloorAt(lair) }, 0.7f, 3);
    }

    public override void _ExitTree()
    {
        Enemy.Slain -= OnSlain;
        Enemy.Landed -= OnLanded;
    }

    static Material Bark()
    {
        var m = (BuildingMaterials.For(Surface.Timber).Duplicate() as StandardMaterial3D) ?? new StandardMaterial3D();
        m.Uv1Triplanar = true;
        m.Uv1WorldTriplanar = true;
        m.Uv1Scale = Vector3.One * 0.22f;
        m.AlbedoColor = new Color("#8a7458");
        m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return m;
    }

    static float Girth(float radius, float t, float round) =>
        radius * (0.42f + 0.58f * Mathf.Pow(1 - t, 1.5f)) * (1 + 1.1f * Mathf.Exp(-t * 16)) * (1 + 0.14f * Mathf.Sin(round * 5 + t * 8) * Mathf.Exp(-t * 3));

    static void Solid(Node3D parent, ArrayMesh mesh, Material m, Vector3 at)
    {
        var body = new StaticBody3D { Position = at };
        body.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = m });
        var shape = mesh.CreateTrimeshShape();
        shape.BackfaceCollision = true;
        body.AddChild(new CollisionShape3D { Shape = shape });
        parent.AddChild(body);
    }

    static ArrayMesh Grid(int rows, int columns, Func<int, int, Vector3> at)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
                foreach (var v in new[] { at(r, c), at(r, c + 1), at(r + 1, c + 1), at(r, c), at(r + 1, c + 1), at(r + 1, c) }) st.AddVertex(v);
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>
    /// A heart-tree and everything grown from it: the trunk, a ramp of living root wrapping up it from the ground, a deck
    /// grown round it, hollows bulging from its sides with their doors, and a crown of leaves over all of it.
    /// </summary>
    void Heart(Northwood.HeartTree tree, Material bark, Material leaf, Material glow, int seed)
    {
        var at = new Vector3((float)tree.X, 0, (float)tree.Z);
        at.Y = City.FloorAt(at) - 0.5f;
        var (radius, high) = ((float)tree.Radius, (float)tree.Height);
        Solid(this, Grid(26, 40, (r, c) =>
        {
            var (t, a) = (r / 26f, c * Mathf.Tau / 40);
            var g = Girth(radius, t, a);
            return new Vector3(Mathf.Cos(a) * g, t * high, Mathf.Sin(a) * g);
        }), bark, at);
        // The ramp goes round as it climbs, always just out from the trunk, from the roots to the deck.
        var climb = Northwood.DeckHeight + 0.5f;
        var turns = Mathf.Ceil(climb / (Mathf.Tau * radius * 1.3f * 0.16f));
        Solid(this, Grid((int)(turns * 44), 3, (r, c) =>
        {
            var t = r / (turns * 44);
            var a = t * turns * Mathf.Tau + seed;
            var y = t * climb;
            var inner = Girth(radius, y / high, a) - 0.3f;
            var way = c <= 1 ? inner + c * 2.9f : inner + 3.1f;
            return new Vector3(Mathf.Cos(a) * way, y + 0.5f + (c == 2 ? 0.55f : c == 3 ? 0.55f : 0), Mathf.Sin(a) * way);
        }), bark, at);
        // A deck grown round the trunk: the village's upper street.
        var deckIn = Girth(radius, Northwood.DeckHeight / high, 0) - 0.5f;
        Solid(this, Grid(2, 40, (r, c) =>
        {
            var a = c * Mathf.Tau / 40;
            var way = deckIn + r * 2.4f + (r == 2 ? 0.8f * Mathf.Sin(a * 3) : 0);
            return new Vector3(Mathf.Cos(a) * way, Northwood.DeckHeight + 0.5f - (r == 2 ? 0.25f : 0), Mathf.Sin(a) * way);
        }), bark, at);
        // Hollows bulging from the trunk: homes and shops, each with a round door and a warm light.
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4 + seed * 0.9f;
            var y = i % 2 == 0 ? 1.0f : Northwood.DeckHeight + 0.7f;
            var out_ = Girth(radius, y / high, a) + (y > 2 ? 0.2f : 0);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var pod = at + dir * out_ + Vector3.Up * (y + 1.4f);
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 2.6f, Height = 5.2f, RadialSegments = 20, Rings = 12 }, MaterialOverride = bark, Position = pod - dir * 0.5f, Scale = new Vector3(1.1f, 0.86f, 1.1f) });
            var door = new Node3D { Position = at + dir * (out_ + 2.2f) + Vector3.Up * (y + 0.05f) };
            door.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
            AddChild(door);
            door.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.75f, BottomRadius = 0.75f, Height = 0.14f, RadialSegments = 20 }, MaterialOverride = BuildingMaterials.For(Surface.Planks), Position = new Vector3(0, 1.0f, 0), Rotation = new Vector3(Mathf.Pi / 2, 0, 0), Scale = new Vector3(1, 1, 1.45f) });
            door.AddChild(new OmniLight3D { Position = new Vector3(0, 2.3f, 0.6f), LightColor = new Color("#ffd28a"), LightEnergy = 1.3f, OmniRange = 8 });
            door.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f }, MaterialOverride = glow, Position = new Vector3(0.9f, 2.1f, 0.3f) });
            Buildings.Door(door, "hollow", seed * 7 + i);
        }
        // Leaves, in great overlapping banks: the canopy the village lives under.
        for (var i = 0; i < 9; i++)
        {
            var a = i * 2.4f + seed;
            var off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (i == 0 ? 0 : radius * (1.2f + i % 3 * 0.8f));
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius * 2.4f, Height = radius * 3, RadialSegments = 14, Rings = 8 }, MaterialOverride = leaf,
                Position = at + off + Vector3.Up * (high * (0.82f + i % 3 * 0.08f)), Scale = new Vector3(1, 0.62f, 1), CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
    }

    void Bridge(Northwood.HeartTree a, Northwood.HeartTree b, Material bark)
    {
        var (from, to) = (new Vector3((float)a.X, 0, (float)a.Z), new Vector3((float)b.X, 0, (float)b.Z));
        var floor = City.FloorAt(from) - 0.5f + Northwood.DeckHeight + 0.5f;
        var way = (to - from).Normalized();
        var (p, q) = (from + way * (float)a.Radius * 0.7f, to - way * (float)b.Radius * 0.7f);
        var side = way.Cross(Vector3.Up);
        Solid(this, Grid(24, 2, (r, c) =>
        {
            var t = r / 24f;
            return p.Lerp(q, t) + side * (c - 1) * 1.4f + Vector3.Up * (floor - 0.9f * Mathf.Sin(t * Mathf.Pi) + (c != 1 ? 0.3f : 0));
        }), bark, Vector3.Zero);
    }

    void BuildVillage()
    {
        var bark = Bark();
        var leaf = new StandardMaterial3D { AlbedoColor = new Color("#3f6a34"), Roughness = 0.95f };
        var glow = new StandardMaterial3D { AlbedoColor = new Color("#b0ffc0"), EmissionEnabled = true, Emission = new Color("#60ffa0"), EmissionEnergyMultiplier = 1.6f };
        var trees = Northwood.Hearts;
        for (var i = 0; i < trees.Count; i++) Heart(trees[i], bark, leaf, glow, i + 1);
        for (var i = 1; i < trees.Count; i++) Bridge(trees[0], trees[i], bark);
        foreach (var (x, z) in new[] { (-10f, 18f), (12f, 26f), (-24f, 26f), (22f, 6f) })
            AddChild(new OmniLight3D { Position = new Vector3((float)Northwood.VillageX + x, City.FloorAt(new Vector3((float)Northwood.VillageX + x, 0, (float)Northwood.VillageZ + z)) + 3, (float)Northwood.VillageZ + z), LightColor = new Color("#a0ffc0"), LightEnergy = 1.2f, OmniRange = 14 });
    }

    public override void _Process(double delta)
    {
        if ((_tick -= delta) > 0) return;
        _tick = 1;
        var p = main.PlayerNode.GlobalPosition;
        var city = main.Economy;
        if (city.LairOpen && !_toldOpen)
        {
            _toldOpen = true;
            main.HudNode.Toast("The ground shakes, far off to the west. In the Northwood, every bird goes quiet at once.", 9);
        }
        if (city.DragonFree && !_toldFree)
        {
            _toldFree = true;
            main.HudNode.Toast("A roar rolls down out of the western hills, and the sky goes the colour of old moss. Whatever slept under the wood is free.", 12);
        }
        foreach (var g in Northwood.Guardians) Tend(g, p);
        TendBlight();
        TendFeral();
        TendDragon(p);
    }

    void Tend(Northwood.Guardian g, Vector3 p)
    {
        var at = new Vector3((float)g.X, 0, (float)g.Z);
        var flat = new Vector2(p.X - at.X, p.Z - at.Z).Length();
        var dead = !g.Corrupted && main.Economy.GuardiansSlain.Contains(g.Id);
        var regrown = !_slainAt.TryGetValue(g.Id, out var when) || main.GuildBook.PlaySeconds - when > ForestBook.Regrow;
        if (_guardians.TryGetValue(g.Id, out var e) && (!IsInstanceValid(e) || e.State == EnemyState.Dead || flat > 340))
        {
            if (IsInstanceValid(e) && flat > 340) e.QueueFree();
            _guardians.Remove(g.Id);
        }
        if (!_guardians.ContainsKey(g.Id) && !dead && regrown && flat < 240 && main.TerrainNode.HasGround(at))
        {
            e = Enemy.Create(Bestiary.Get(g.Kind), at with { Y = City.FloorAt(at) + 0.5f }, Northwood.DangerFor(10));
            e.Stats.Name = g.Name;
            e.RefreshPlate();
            e.Roam = 14;
            main.AddEnemy(e);
            _guardians[g.Id] = e;
        }
        if (!_guardians.TryGetValue(g.Id, out e)) return;
        // They bear with anyone who's kept their axe to themselves, and with anyone keeping to the road. The rotten one bears with nobody.
        var angered = g.Corrupted ? flat < 34 : main.ForestLog.TreesCut > 0 && flat < g.Territory && !Northwood.OnRoad(p.X, p.Z);
        if (angered && e.State == EnemyState.Idle) e.Provoke(main.PlayerNode);
    }

    double _nextBlight;

    /// <summary>A blow from the turned guardian may carry the rot in with it, into the hero or their beast, and it stays until a druid draws it out.</summary>
    void OnLanded(Enemy by, Enemy? on)
    {
        if (!_guardians.TryGetValue(Northwood.Guardians.First(g => g.Corrupted).Id, out var rot) || by != rot || GD.Randf() >= ForestBook.BlightChance) return;
        if (on is null)
        {
            if (main.ForestLog.Blighted) return;
            main.ForestLog.Blighted = true;
            _nextBlight = main.Clock + ForestBook.BlightSeconds;
            main.HudNode.Toast("The blow leaves black rot in the wound. It won't come out by itself: find a druid.", 8);
        }
        else if (on.Bond is { Blighted: false } bond && on.Leader == main.PlayerNode)
        {
            bond.Blighted = true;
            main.HudNode.Toast($"The rot has got into {bond.Name}. Get it to a druid before it falls.", 8);
        }
    }

    IEnumerable<Enemy> Pets() => main.Enemies().Where(e => e.Bond is not null && e.Leader == main.PlayerNode && e.State != EnemyState.Dead);

    /// <summary>Every so often the rot takes its share, of the hero and of every beast that has it, until they're cured.</summary>
    void TendBlight()
    {
        if (main.Clock < _nextBlight) return;
        _nextBlight = main.Clock + ForestBook.BlightSeconds;
        var hurt = false;
        if (main.ForestLog.Blighted && !main.PlayerNode.Downed)
        {
            main.PlayerNode.Stats.Lose(main.PlayerNode.Stats.MaxHealth * ForestBook.BlightShare);
            hurt = true;
        }
        foreach (var pet in Pets().Where(e => e.Bond!.Blighted || e.Bond.Rotted))
        {
            pet.Stats.Lose(pet.Stats.MaxHealth * ForestBook.BlightShare);
            if (pet.Stats.Health <= 0) pet.Collapse();
            hurt = true;
        }
        if (hurt) main.HudNode.Log("The rot takes its share.");
    }

    double _nextFeral = ForestBook.FeralSeconds;

    /// <summary>A bound beast doesn't stay bound: every so often the rot may rise in it again, until a druid has it out.</summary>
    void TendFeral()
    {
        if (main.Clock < _nextFeral) return;
        _nextFeral = main.Clock + ForestBook.FeralSeconds;
        foreach (var pet in Pets().Where(e => e.Bond!.Rotted && GD.Randf() < ForestBook.FeralChance).ToList())
        {
            pet.Rot(main.PlayerNode);
            main.HudNode.Toast($"{pet.Bond!.Name} goes feral! Knock it out again, and get it to a druid.", 8);
        }
    }

    void TendDragon(Vector3 p)
    {
        var city = main.Economy;
        var lair = new Vector3((float)Northwood.LairX, 0, (float)Northwood.LairZ);
        if (!city.LairOpen || city.DragonSlain) return;
        if (_dragon is not null && !IsInstanceValid(_dragon)) _dragon = null;
        if (_dragon is null)
        {
            // Asleep in the lair until the barrier breaks; after that, wherever the heroes are.
            var near = city.DragonFree ? p + new Vector3(30, 0, 30) : lair;
            if (!city.DragonFree && new Vector2(p.X - lair.X, p.Z - lair.Z).Length() > 240) return;
            _dragon = Enemy.Create(Bestiary.Get("dragon_green"), near with { Y = City.FloorAt(near) + 2 }, Northwood.DangerFor(14), size: DragonSize.Giant);
            _dragon.Stats.Name = Northwood.DragonName;
            _dragon.Stats.Grow(6);
            _dragon.RefreshPlate();
            main.AddEnemy(_dragon);
        }
        if (city.DragonFree && _dragon.State == EnemyState.Idle) _dragon.Provoke(main.PlayerNode);
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (victim == _dragon)
        {
            main.DoCity(CityAction.Guardian("dragon"));
            main.PlayerNode.Crowns += 5000;
            main.HudNode.Toast($"{Northwood.DragonName} falls, and the wood breathes out. It's over. Nobody who was here will ever forget it.", 12);
            return;
        }
        if (_guardians.FirstOrDefault(kv => kv.Value == victim).Key is not { } id)
        {
            if (Northwood.Corrupted(victim.GlobalPosition.X, victim.GlobalPosition.Z) && byCompany) main.Take([("rotwood", 1)]); // what's left of it is no use to anyone
            return;
        }
        var g = Northwood.Guardians.First(x => x.Id == id);
        _slainAt[id] = main.GuildBook.PlaySeconds;
        if (g.Corrupted)
        {
            if (main.ForestLog.Stage == ForestStage.Asked) main.ForestLog.Stage = ForestStage.Slain;
            main.HudNode.Toast($"{g.Name} falls apart in a slump of black roots. Oruun will want to know, and neither of you will enjoy it.", 8);
            main.PlayerNode.Crowns += ForestBook.Hoard / 2;
            if (!main.ForestLog.Thorn || GD.Randf() < ForestBook.ThornChance)
            {
                main.ForestLog.Thorn = true;
                main.Take([(g.Loot, 1)]);
                main.HudNode.Toast("Among the rot, one thorn that isn't dead: Blightthorn, as fine as anything a guardian ever grew.", 8);
            }
        }
        else
        {
            main.Take([(g.Loot, 1)]);
            main.PlayerNode.Crowns += ForestBook.Hoard;
            main.Standing.Evil(25, $"slew {g.Name}");
            main.HudNode.Toast($"{g.Name} comes down like a falling hill. In the heart of it: {Items.Get(g.Loot)!.Name}, and {ForestBook.Hoard} crowns of old gold. Every bird in the wood stops singing.", 11);
        }
        main.DoCity(CityAction.Guardian(id, g.Corrupted));
        main.Save();
    }

    /// <summary>The guardians' ground, with the heroes who've come to it: for the self-test.</summary>
    public void TendForTest() => _tick = 0;
}
