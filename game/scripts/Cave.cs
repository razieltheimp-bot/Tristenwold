using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The Greyhollow mine (see <see cref="Greyhollow"/>): a cutting through the mound at the quarry leads to chambers and
/// drifts going down, where the quarry's veins are worked now. Dagna Flint's work (see <see cref="MineBook"/>) ends at
/// the old face: break it, hold the dead off Tam Rusk, and the deep is open.
/// </summary>
public partial class Main
{
    MineBook _mine = new();
    Cave? _cave;
    double _mineTick;

    public MineBook MineLog => _mine;
    public Cave? CaveNode => _cave;
    public bool InMine => _cave is not null;

    /// <summary>The door in the mound: the mine building's mouth, at the back of the quarry.</summary>
    public static Vector3 MineDoor => QuarryAt + new Vector3(0, 0, -16.1f);

    /// <summary>The mine's mouth at the quarry: an open doorway at the end of the cutting through the mound (see <see cref="Greyhollow.Hill"/>).</summary>
    void AddMineMound()
    {
        var mouth = MineDoor with { Y = City.FloorAt(MineDoor) };
        Buildings.Place(this, "mine_portal", mouth, 0, 7);
        // The dark at the end of the timber sets, with nothing lit behind it.
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(3.4f, 3.4f) }, Position = mouth + new Vector3(0, 1.6f, -5.3f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Black, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        });
        AddChild(new Label3D { Text = "Greyhollow Mine", Position = mouth + new Vector3(0, 3.9f, 0.3f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
    }

    /// <summary>What there is to do with E at the mine: the door, the way up, the old face.</summary>
    (string Verb, Action Act)? MineAction(Vector3 pos)
    {
        if (_cave is null) return pos.DistanceTo(MineDoor with { Y = pos.Y }) < 5 ? ("go down the mine", EnterMine) : null;
        if (pos.DistanceTo(Cave.WayUp) < 4) return ("climb up to daylight", () => _ = LeaveMine());
        if (_mine.Broken || _working || pos.DistanceTo(Cave.FaceAt) > 5) return null;
        return _mine.Stage == MineStage.AtTheFace
            ? ("break through the old face", BreakFace)
            : ("listen at the old face", () => _hud.Toast("Old stone, walled up with care. Something behind it is very quiet. This is Dagna's to say.", 6));
    }

    /// <summary>Down through the doorway: there's no door, so walking in is enough (see <see cref="TendMine"/>).</summary>
    void EnterMine()
    {
        if (_cave is not null) return;
        if (_player.Mount is not null) { Sound.Ui("error"); _hud.Toast("You can't ride down a mine. Leave your mount in the quarry.", 4); return; }
        GoDown();
        _player.GlobalPosition = Cave.Landing;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        Save();
    }

    void GoDown()
    {
        _mine.Reload();
        _cave = new Cave(this, _mine);
        AddChild(_cave);
    }

    async Task LeaveMine()
    {
        _cave?.QueueFree();
        _cave = null;
        City.Below = null;
        City.BelowName = null;
        var out_ = MineDoor + new Vector3(0, 0, 3);
        _terrain.EnsureAround(out_, 1);
        _player.GlobalPosition = out_ with { Y = City.FloorAt(out_) + 0.5f };
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        Save();
        await Task.CompletedTask;
    }

    /// <summary>The cave, kept with the hero: built when a saved game wakes down there, and gone when the ring takes them home.</summary>
    void TendMine(double delta)
    {
        if ((_mineTick -= delta) > 0) return;
        _mineTick = 0.25;
        var under = _player.GlobalPosition.Y is > Cave.Threshold and < Indoors.Threshold;
        if (_cave is null && _player.Mount is null && !_player.Downed && _player.GlobalPosition.DistanceTo(MineDoor + new Vector3(0, City.FloorAt(MineDoor), -3.4f)) < 1.8f) { EnterMine(); return; }
        if (under && _cave is null && !_player.Downed) GoDown();
        if (!under && _cave is not null)
        {
            if (_mine.Stage == MineStage.Breached)
            {
                _mine.Lost();
                _hud.Toast("You came round on the surface. The miners dragged Tam out and walled the face up behind him. Break it again when you're ready.", 8);
            }
            _cave.QueueFree();
            _cave = null;
            City.Below = null;
            City.BelowName = null;
        }
        _cave?.Tend(0.25);
    }

    /// <summary>The pick against the old face, and the wall comes down.</summary>
    async void BreakFace()
    {
        if (_player.Pack.Count("pickaxe") == 0) { Sound.Ui("error"); _hud.Toast("You'll want a pickaxe for that. Dagna Flint sells them.", 5); return; }
        _working = true;
        _player.Face(Cave.FaceAt);
        var held = Hands()?.Carry("pick");
        for (var i = 0; i < 3; i++)
        {
            Hands()?.Perform("2H_Melee_Attack_Chop", 0.7, 1.3f);
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this)) return;
            Sound.At("melee/hit_metal", Cave.FaceAt, -2, 0.6f);
            Effects.Impact(this, Cave.FaceAt + Vector3.Up, Cave.FaceAt - _player.GlobalPosition, DamageKind.Physical, false);
        }
        held?.QueueFree();
        _working = false;
        _cave?.Breach();
        Save();
    }

    /// <summary>A vein worked: Dagna's counting, while she's watching.</summary>
    void MineWorked(WorkSpot spot)
    {
        if (spot.At.Y <= Cave.Threshold || !_mine.VeinWorked()) return;
        _hud.Toast("That's enough to satisfy Dagna. Go and hear what she wants done at the old face.", 7);
        Sound.Ui("quest_1");
    }

    /// <summary>What Dagna Flint has to say about the mine.</summary>
    IEnumerable<(string, Action)> MineTalk(Person person)
    {
        if (person.Npc.Id != Shops.Quarry.Id) yield break;
        switch (_mine.Stage)
        {
            case MineStage.None:
                yield return ("Is there work down the mine?", () =>
                {
                    _mine.Hire();
                    Say(person, $"The quarry's dug out; it's all underground now. Through the cutting in the mound. Work {MineBook.VeinsToProve} veins for me and I'll know you can swing a pick. Then we'll talk about the old face.");
                    Save();
                });
                break;
            case MineStage.Hired:
                yield return ("How am I doing?", () => Say(person, $"{_mine.Worked} of {MineBook.VeinsToProve} veins. Keep swinging. Pickaxe, if you've not got one: I sell them."));
                break;
            case MineStage.AtTheFace:
                yield return (_mine.Failed > 0 ? "We lost it last time. Tell me again." : "What's at the old face?", () => Say(person,
                    "Past the Silver Cut there's a wall my grandmother's crew put up, and nobody ever said why. The ore runs behind it. Tam Rusk's down there shoring the drift. " +
                    "Take your pick to it. And if something comes through, stay between it and Tam: he can't run, not with his leg."));
                break;
            case MineStage.Breached:
                yield return ("There are dead things in your mine.", () => Say(person, "Then why are you up here? Get back down to Tam!"));
                break;
            case MineStage.Held:
                yield return ("It's done. Tam's safe.", () =>
                {
                    var pay = _mine.Collect() ?? 0;
                    _player.Crowns += pay;
                    Take([("rough_garnet", 2), ("rough_sapphire", 1)]);
                    var up = _player.Pack.Practise(Job.Mining, 60);
                    Sound.Ui("coin");
                    Say(person, $"So that's why she walled it up. {pay} crowns, and these came out of the rubble. The deep's yours to work, if you've the stomach: there's gold at the bottom, and more of those things between you and it.");
                    if (up is { } level) _hud.Toast($"Mining rises to {level}!", 4);
                    Save();
                });
                break;
            default:
                yield return ("What's down in the deep?", () => Say(person, "Barrows older than the city, a cloister of bones, and the gold seam under it all. They come back, those things. Take a good blade."));
                break;
        }
    }

    public void EnterMineForTest() => EnterMine();
    public Task LeaveMineForTest() => LeaveMine();
    public void BreakFaceForTest() => BreakFace();
    public bool DagnaLives => _townsfolk.Any(p => p.Npc.Id == Shops.Quarry.Id);
}

/// <summary>
/// The inside of the mine, built when the hero goes down. It's played out far above the land, like a voyage, with the
/// rock closed in all round so nothing else can be seen; a floor hook tells everything standing in it where the ground is.
/// </summary>
public partial class Cave(Main main, MineBook book) : Node3D
{
    public static readonly Vector3 Stage = new(Main.QuarryAt.X, 2400, Main.QuarryAt.Z);
    /// <summary>Anything higher than this is in the mine.</summary>
    public const float Threshold = 2000;
    const float Cell = 2;
    const int X0 = -38, X1 = 38, Z0 = -124, Z1 = 12, Chunk = 16; // in cells

    /// <summary>A point of the mine (metres from the adit), in the world, on its floor.</summary>
    public static Vector3 At(double x, double z) => new(Stage.X + (float)x, Stage.Y + (float)Greyhollow.Floor(x, z), Stage.Z + (float)z);

    public static Vector3 Landing => At(0, 2) + Vector3.Up * 0.5f;
    public static Vector3 WayUp => At(0, 6);
    public static Vector3 FaceAt => At(Greyhollow.Face.X, Greyhollow.Face.Z);
    public static Vector3 TamAt => At(Greyhollow.Room("old_face").X - 4, Greyhollow.Room("old_face").Z - 3);
    public static Vector3 InRoom(string id) => At(Greyhollow.Room(id).X, Greyhollow.Room(id).Z);

    static readonly List<(ArrayMesh Mesh, Shape3D Shape)> Shell = [];
    static Material? _rock;

    readonly List<Enemy> _dens = [], _wave = [];
    readonly Rng _rng = new((ulong)DateTime.UtcNow.Ticks);
    Node3D? _wall, _tam;
    OmniLight3D? _lamp;
    double _warned = 1;

    public IReadOnlyList<Enemy> Wave => _wave;
    public IReadOnlyList<Enemy> Dens => _dens;
    public bool WallStands => _wall is not null;

    public override void _Ready()
    {
        City.Below = p => p.Y > Threshold ? Stage.Y + (float)Greyhollow.Floor(p.X - Stage.X, p.Z - Stage.Z) : null;
        City.BelowName = p => p.Y > Threshold ? Greyhollow.NameAt(p.X - Stage.X, p.Z - Stage.Z) : null;
        City.ShutOutSky(new Color("#a89888"), 0.16f, 0.02f);
        if (Shell.Count == 0) Build();
        foreach (var (mesh, shape) in Shell)
        {
            var body = new StaticBody3D { Position = Stage };
            body.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = Rock() });
            body.AddChild(new CollisionShape3D { Shape = shape });
            AddChild(body);
        }
        Dress();
        main.PlayerNode.AddChild(_lamp = new OmniLight3D { Position = new Vector3(0, 2, 0), LightColor = new Color("#ffd8a0"), LightEnergy = 1.1f, OmniRange = 11 });
        if (!book.Broken) RaiseWall();
        Miner();
        Populate();
    }

    public override void _ExitTree()
    {
        foreach (var e in _dens.Concat(_wave).Where(IsInstanceValid)) e.QueueFree();
        _lamp?.QueueFree();
        City.Below = null;
        City.BelowName = null;
        City.LetInSky();
    }

    static Material Rock()
    {
        if (_rock is not null) return _rock;
        const string file = "res://assets/polyhaven/surfaces/dark_stone";
        var m = new StandardMaterial3D { AlbedoColor = new Color("#a89c90"), Roughness = 0.95f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        if (ResourceLoader.Exists($"{file}_diff.jpg"))
        {
            m.AlbedoTexture = GD.Load<Texture2D>($"{file}_diff.jpg");
            m.NormalEnabled = true;
            m.NormalTexture = GD.Load<Texture2D>($"{file}_nor.jpg");
            m.Uv1Triplanar = true;
            m.Uv1WorldTriplanar = true;
            m.Uv1TriplanarSharpness = 12;
            m.Uv1Scale = Vector3.One * 0.35f;
            m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        }
        return _rock = m;
    }

    // ---- the rock ----------------------------------------------------------------------------------------------

    static float Rough(int i, int j, int salt)
    {
        var n = Mathf.Sin(i * 12.9898f + j * 78.233f + salt * 37.719f) * 43758.5453f;
        return (n - Mathf.Floor(n)) * 2 - 1;
    }

    static bool OpenCell(int i, int j) => Greyhollow.Open((i + 0.5) * Cell, (j + 0.5) * Cell);

    /// <summary>A corner of the grid, at the floor or the roof; roughened so no wall shows the grid it's made on.</summary>
    static Vector3 Corner(int i, int j, bool roof)
    {
        var (x, z) = (i * Cell, j * Cell);
        var floor = (float)Greyhollow.Floor(x, z);
        var y = roof ? floor + (float)Greyhollow.Headroom(x, z) + Rough(i, j, 3) * 0.55f : floor + Rough(i, j, 4) * 0.05f;
        return new Vector3(x + Rough(i, j, 1) * 0.72f, y, z + Rough(i, j, 2) * 0.72f);
    }

    static void Quad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
    {
        // Floor, roof and walls don't share normals, or the rock's texture smears where they meet.
        st.SetSmoothGroup(facing == Vector3.Up ? 0u : facing == Vector3.Down ? 1u : 2u);
        if ((a - c).Cross(a - b).Dot(facing) < 0) (b, d) = (d, b);
        foreach (var v in new[] { a, b, c, a, c, d }) st.AddVertex(v);
    }

    /// <summary>The floor, roof and walls of every open cell, in chunks, so only what's near is drawn.</summary>
    static void Build()
    {
        for (var ci = X0; ci < X1; ci += Chunk)
            for (var cj = Z0; cj < Z1; cj += Chunk)
            {
                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);
                var any = false;
                for (var i = ci; i < Math.Min(ci + Chunk, X1); i++)
                    for (var j = cj; j < Math.Min(cj + Chunk, Z1); j++)
                    {
                        if (!OpenCell(i, j)) continue;
                        any = true;
                        Quad(st, Corner(i, j, false), Corner(i + 1, j, false), Corner(i + 1, j + 1, false), Corner(i, j + 1, false), Vector3.Up);
                        Quad(st, Corner(i, j, true), Corner(i + 1, j, true), Corner(i + 1, j + 1, true), Corner(i, j + 1, true), Vector3.Down);
                        void Wall(int i0, int j0, int i1, int j1, Vector3 facing) =>
                            Quad(st, Corner(i0, j0, false), Corner(i1, j1, false), Corner(i1, j1, true), Corner(i0, j0, true), facing);
                        if (!OpenCell(i - 1, j)) Wall(i, j, i, j + 1, Vector3.Right);
                        if (!OpenCell(i + 1, j)) Wall(i + 1, j, i + 1, j + 1, Vector3.Left);
                        if (!OpenCell(i, j - 1)) Wall(i, j, i + 1, j, Vector3.Back);
                        if (!OpenCell(i, j + 1)) Wall(i, j + 1, i + 1, j + 1, Vector3.Forward);
                    }
                if (!any) continue;
                st.GenerateNormals();
                var mesh = st.Commit();
                var shape = mesh.CreateTrimeshShape();
                shape.BackfaceCollision = true;
                Shell.Add((mesh, shape));
            }
    }

    // ---- what's in it ------------------------------------------------------------------------------------------

    void Torch(Vector3 at, float yaw, Color light)
    {
        Models.Place(this, "dungeon/torch_mounted.glb", at + Vector3.Up * 1.8f, 1.1f, yaw, solid: false);
        AddChild(new OmniLight3D { Position = at + Vector3.Up * 2.2f, LightColor = light, LightEnergy = 2.4f, OmniRange = 15, OmniAttenuation = 1.4f });
    }

    /// <summary>Torches in the chambers, props and rubble, timber sets along the drifts, and in the deep, bones.</summary>
    void Dress()
    {
        var timber = BuildingMaterials.For(Surface.Timber);
        foreach (var r in Greyhollow.Rooms)
        {
            var light = r.Deep ? new Color("#90b8d8") : new Color("#ffb060");
            foreach (var a in new[] { 1.1f, 4.2f })
            {
                var (x, z) = (r.X + Mathf.Cos(a) * (r.Radius - 1.4), r.Z + Mathf.Sin(a) * (r.Radius - 1.4));
                Torch(At(x, z), -a + Mathf.Pi / 2, light);
            }
            var (rx, rz) = (r.X + Mathf.Cos(2.5f) * (r.Radius - 2.8), r.Z + Mathf.Sin(2.5f) * (r.Radius - 2.8));
            if (r.Deep)
            {
                Models.Place(this, "dungeon/rubble_large.glb", At(rx, rz), 1.4f, (float)r.Radius, solid: false);
                for (var i = 0; i < 5; i++)
                {
                    var (a, d) = (i * 1.7f + (float)r.Radius, 3 + i * 1.4f);
                    Models.Place(this, $"halloween/bone_{(char)('A' + i % 3)}.gltf", At(r.X + Mathf.Cos(a) * d, r.Z + Mathf.Sin(a) * d), 1.3f, a, solid: false);
                }
                if (r.Id is "barrows")
                    for (var i = 0; i < 4; i++)
                        Models.Place(this, "halloween/grave_A.gltf", At(r.X - 6 + i * 4, r.Z + 7), 1.4f, 3.1f);
            }
            else
            {
                Models.Place(this, "dungeon/crates_stacked.glb", At(rx, rz), 0.6f, (float)r.Radius);
                Models.Place(this, "medieval/props/resource_stone.gltf", At(r.X - Mathf.Cos(2.5f) * (r.Radius - 3.2), r.Z - Mathf.Sin(2.5f) * (r.Radius - 3.2)), 4, (float)r.Radius * 2, solid: false);
            }
        }
        foreach (var t in Greyhollow.Tunnels)
        {
            var (a, b) = (Greyhollow.Room(t.From), Greyhollow.Room(t.To));
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var length = Math.Sqrt(dx * dx + dz * dz);
            var (ux, uz) = (dx / length, dz / length);
            var yaw = Mathf.Atan2((float)ux, (float)uz);
            for (var along = a.Radius + 4; along < length - b.Radius - 2; along += 8)
            {
                var (cx, cz) = (a.X + ux * along, a.Z + uz * along);
                foreach (var side in new[] { -1, 1 })
                    AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.28f, 3.4f, 0.28f) }, MaterialOverride = timber, Position = At(cx - uz * side * 1.9, cz + ux * side * 1.9) + Vector3.Up * 1.65f });
                AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4.2f, 0.3f, 0.3f) }, MaterialOverride = timber, Position = At(cx, cz) + Vector3.Up * 3.2f, Rotation = new Vector3(0, yaw, 0) });
            }
            AddChild(new OmniLight3D { Position = At(a.X + dx / 2, a.Z + dz / 2) + Vector3.Up * 2.2f, LightColor = t.Deep ? new Color("#90b8d8") : new Color("#ffb060"), LightEnergy = 0.8f, OmniRange = 12, OmniAttenuation = 1.6f });
        }
        // The way out: a ladder of daylight down the shaft.
        AddChild(new OmniLight3D { Position = WayUp + Vector3.Up * 3, LightColor = new Color("#dfe8ff"), LightEnergy = 2.2f, OmniRange = 12 });
        AddChild(new Label3D { Text = "The way up", Position = WayUp + Vector3.Up * 2.6f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
    }

    /// <summary>Rock and mortar across the drift to the deep.</summary>
    void RaiseWall()
    {
        var at = FaceAt;
        var wall = new StaticBody3D { Position = at + Vector3.Up * 2.2f };
        wall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(7, 5.4f, 1.6f) }, MaterialOverride = BuildingMaterials.For(Surface.Stone) });
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(7, 5.4f, 1.6f) } });
        AddChild(wall);
        _wall = wall;
    }

    void Miner()
    {
        var tam = Body.For("worker");
        AddChild(tam);
        tam.GlobalPosition = TamAt;
        tam.Rotation = new Vector3(0, 0.6f, 0);
        tam.AddChild(new Label3D { Text = "Tam Rusk", Position = Vector3.Up * 2.1f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 8, PixelSize = 0.011f, Modulate = new Color("#e8dcc4") });
        // The fall that pinned his leg: a heap over his feet and to one side, not a boulder swallowing the man.
        Models.Place(this, "dungeon/rubble_large.glb", TamAt + new Vector3(1.1f, -0.35f, 0.5f), 0.42f, 1.3f, solid: false);
        _tam = tam;
    }

    Enemy Raise(string kind, Vector3 at, double danger)
    {
        var e = Enemy.Create(Bestiary.Get(kind), at + Vector3.Up * 0.5f, danger);
        main.AddEnemy(e);
        return e;
    }

    /// <summary>What lives in the mine: rats above, and the dead past the breach once it's been won.</summary>
    void Populate()
    {
        foreach (var d in Greyhollow.Dens.Where(d => book.DeepOpen || !Greyhollow.Room(d.Room).Deep))
        {
            var r = Greyhollow.Room(d.Room);
            for (var i = 0; i < d.Count; i++)
            {
                var (a, far) = (_rng.NextDouble() * Math.Tau, _rng.NextDouble() * r.Radius * 0.55);
                _dens.Add(Raise(d.Kind, At(r.X + Math.Cos(a) * far, r.Z + Math.Sin(a) * far), d.Danger));
            }
        }
    }

    // ---- the breach --------------------------------------------------------------------------------------------

    /// <summary>The face comes down, and what was behind it comes through.</summary>
    public void Breach()
    {
        if (!book.Breach()) return;
        if (_wall is { } wall)
        {
            Effects.Burst(main, wall.GlobalPosition, DamageKind.Physical, 3);
            Effects.Smoke(main, FaceAt + Vector3.Up, 3);
            wall.QueueFree();
            _wall = null;
        }
        Sound.At("magic/fire_boom", FaceAt, 2, 0.5f);
        main.HudNode.Toast("The wall falls in, and the dark behind it moves. Tam Rusk shouts: his leg's pinned by the fall! Hold them off him!", 9);
        NextWave();
    }

    void NextWave()
    {
        _wave.Clear();
        var (x, z, _) = Greyhollow.Face;
        foreach (var (kind, count) in MineBook.Wave(book.Waves))
            for (var i = 0; i < count; i++)
            {
                var e = Raise(kind, At(x + _rng.NextDouble() * 3 - 1.5, z - 4 - _rng.NextDouble() * 5), 1);
                e.Provoke(main.PlayerNode);
                _wave.Add(e);
            }
        main.HudNode.Toast($"Wave {book.Waves + 1} of {MineBook.WaveCount} comes through the breach.", 4);
    }

    /// <summary>The fight, four times a second: the dead that reach Tam hurt him, and each wave beaten brings the next.</summary>
    public void Tend(double delta)
    {
        if (book.Stage != MineStage.Breached || _tam is null) return;
        var standing = _wave.Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead).ToList();
        if (standing.Count == 0)
        {
            if (!book.WaveBeaten()) { NextWave(); return; }
            main.HudNode.Toast("The last of them drops. The drift's quiet, and it goes on down into the dark. Tam will live. Dagna will want to hear of this.", 9);
            Sound.Ui("quest_1");
            Populate();
            main.Save();
            return;
        }
        var near = standing.Count(e => e.GlobalPosition.DistanceTo(_tam.GlobalPosition) < 11);
        if (near > 0 && book.Harm(near * 0.6 * delta)) { Lost(); return; }
        var left = book.Tam / MineBook.TamHealth;
        if (left < _warned - 0.25)
        {
            _warned = left;
            main.HudNode.Toast($"Tam's in trouble: {left:P0} left in him. Get them off him!", 4);
        }
    }

    /// <summary>The dead got to Tam: the hero's dragged clear, and the miners wall the drift up again.</summary>
    void Lost()
    {
        foreach (var e in _wave.Where(IsInstanceValid)) e.QueueFree();
        _wave.Clear();
        book.Lost();
        _warned = 1;
        RaiseWall();
        main.HudNode.Toast("The dead pull Tam under, then something drags him clear. The miners wall the drift up behind you. He lives, barely. Break the face when you're ready.", 9);
        main.Save();
    }
}
