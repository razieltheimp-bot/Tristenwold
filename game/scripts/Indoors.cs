using Godot;
using Tristinwold.Deviant;

namespace Tristinwold.Game;

/// <summary>Going in and out of buildings: E at any door, and the room behind it (see <see cref="Indoors"/>).</summary>
public partial class Main
{
    Indoors? _indoors;
    Vector3 _outside;

    public Indoors? IndoorsNode => _indoors;

    (string Verb, Action Act)? IndoorAction(Vector3 pos)
    {
        if (_indoors is not null) return pos.DistanceTo(_indoors.WayOut) < 2.4f ? ("step outside", GoOutside) : null;
        if (_cave is not null || _atSea is not null || _player.Mount is not null || Buildings.DoorNear(pos) is not { } door) return null;
        return ($"go inside the {Indoors.Called(door.Kind)}", () => GoInside(door.Kind, door.Seed, door.Step));
    }

    void GoInside(string kind, int seed, Vector3 step)
    {
        _outside = step;
        _indoors = new Indoors(this, kind, seed);
        AddChild(_indoors);
        _player.GlobalPosition = _indoors.WayIn;
        _player.Rotation = Vector3.Zero;
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
        Sound.Ui("equip", -8);
    }

    void GoOutside()
    {
        _indoors?.QueueFree();
        _indoors = null;
        City.Below = null;
        City.BelowName = null;
        _terrain.EnsureAround(_outside, 1);
        _player.GlobalPosition = _outside with { Y = Mathf.Max(_outside.Y, City.FloorAt(_outside)) + 0.4f }; // a door may be up a tree
        _player.Velocity = Vector3.Zero;
        _player.ResetPhysicsInterpolation();
    }

    /// <summary>A room doesn't outlast the hero leaving it some other way (the ring), nor a game that was closed in it.</summary>
    void TendIndoors()
    {
        TendSkyboat(GetProcessDeltaTime());
        TendShaft();
        TendCrypt();
        var above = _player.GlobalPosition.Y > Indoors.Threshold;
        if (!above && _indoors is not null)
        {
            _indoors.QueueFree();
            _indoors = null;
        }
        else if (above && _indoors is null && !_player.Downed)
        {
            _outside = City.PlayerStart;
            GoOutside();
        }
    }

    public void GoInsideForTest(string kind, int seed, Vector3 step) => GoInside(kind, seed, step);
    public void GoOutsideForTest() => GoOutside();
}

/// <summary>
/// The room behind a door. It's built when the hero steps in, played out high above the land like a voyage, with the
/// sky shut out: walls and a roof, a floor underfoot, a fire, and what that kind of building would have in it.
/// </summary>
public partial class Indoors(Main main, string kind, int seed) : Node3D
{
    public bool IsTavern => kind == "tavern";
    /// <summary>Where a tavern's business is: the bar, the card table, the dice table and the hearth.</summary>
    public Vector3 Bar { get; private set; }
    public Vector3 CardTable => Stage + new Vector3(2, 0, -2);
    public Vector3 DiceTable => Stage + new Vector3(4.2f, 0, 3);
    public Vector3 Fireside { get; private set; }

    public const float Threshold = 3000;
    static readonly Vector3 Stage = new(0, 3400, 0);

    /// <summary>The floor plan of each kind that has an inside: wide, deep and high.</summary>
    static (float Wide, float Deep, float High) Plan(string kind) => kind switch
    {
        "tavern" or "longhall" => (14, 16, 3.8f),
        "smithy" => (10, 11, 3.8f),
        "barn" => (12, 15, 5),
        "church" => (11, 22, 7),
        "keep" => (18, 18, 6),
        "tower" => (8, 8, 4),
        "hollow" => (9, 9, 3.2f),
        "mineshaft" => (12, 32, 4.5f),
        "crypt" => (14, 66, 5),
        _ => (8, 9, 3),
    };

    public static string Called(string kind) => kind switch
    {
        "longhall" => "long hall", "smithy" => "smithy", "barn" => "barn", "church" => "church", "keep" => "keep", "tower" => "tower",
        "tavern" => "tavern", "cottage" => "cottage", "hollow" => "living hollow", "mineshaft" => "mine", "crypt" => "Drowned Chapel's crypt", _ => "house",
    };

    public Vector3 WayIn => Stage + new Vector3(0, 0.6f, Plan(kind).Deep / 2 - 1.6f);
    public Vector3 WayOut => Stage + new Vector3(0, 0, Plan(kind).Deep / 2 - 0.6f);

    public override void _Ready()
    {
        var (wide, deep, high) = Plan(kind);
        City.Below = p => p.Y > Threshold ? Stage.Y : null;
        City.BelowName = p => p.Y > Threshold ? $"Inside the {Called(kind)}" : null;
        City.ShutOutSky(new Color("#b0a090"), 0.3f, 0.002f);
        var shaft = kind is "mineshaft" or "crypt";
        var stone = kind is "church" or "keep" or "tower" or "smithy";
        Slab(Vector3.Down * 0.1f, new Vector3(wide, 0.2f, deep), shaft ? Surface.DarkStone : stone ? Surface.Stone : Surface.Planks);
        Slab(Vector3.Up * (high + 0.1f), new Vector3(wide, 0.2f, deep), shaft ? Surface.DarkStone : Surface.Timber);
        var walls = shaft ? Surface.DarkStone : stone ? Surface.Stone : kind == "hollow" ? Surface.Timber : Surface.Plaster;
        foreach (var side in new[] { -1, 1 })
        {
            Slab(new Vector3(side * (wide / 2 + 0.1f), high / 2, 0), new Vector3(0.2f, high, deep), walls);
            Slab(new Vector3(0, high / 2, side * (deep / 2 + 0.1f)), new Vector3(wide + 0.4f, high, 0.2f), walls);
        }
        Part(new Vector3(0, 1.25f, deep / 2 - 0.02f), new Vector3(1.5f, 2.5f, 0.08f), Surface.Planks); // the door, shut behind you
        foreach (var x in new[] { -wide / 4, wide / 4 })
            for (var z = -deep / 2 + 3; z < deep / 2; z += 5)
                Part(new Vector3(x, high - 0.1f, z), new Vector3(0.28f, 0.28f, 0.28f), Surface.Timber);
        Light(new Vector3(0, high - 0.6f, 0), 1.6f, Math.Max(wide, deep) * 1.1f);
        Furnish(wide, deep, high);
    }

    public override void _ExitTree()
    {
        City.Below = null;
        City.BelowName = null;
        City.LetInSky();
        if (kind == "mineshaft") main.LeftTheShaft();
        if (kind == "crypt") main.LeftTheCrypt();
    }

    static Material Photo(Surface s)
    {
        if (BuildingMaterials.For(s).Duplicate() is not StandardMaterial3D m) return BuildingMaterials.For(s);
        m.Uv1Triplanar = true; // boxes here have no measured UVs: lay the photograph on by the world's metres
        m.Uv1WorldTriplanar = true;
        m.Uv1Scale = Vector3.One * 0.45f;
        return m;
    }

    void Part(Vector3 at, Vector3 size, Surface s) =>
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Photo(s), Position = Stage + at });

    void Slab(Vector3 at, Vector3 size, Surface s)
    {
        var body = new StaticBody3D { Position = Stage + at };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Photo(s) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    void Sign(Vector3 at, string text) => AddChild(new Label3D { Text = text, Position = Stage + at, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 36, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#ffd27a") });

    void Light(Vector3 at, float energy, float reach, string colour = "#ffc890") =>
        AddChild(new OmniLight3D { Position = Stage + at, LightColor = new Color(colour), LightEnergy = energy, OmniRange = reach, OmniAttenuation = 1.1f });

    void Put(string path, float x, float z, float scale, float yaw = 0, bool solid = true, float y = 0) =>
        Models.Place(this, path, Stage + new Vector3(x, y, z), scale, yaw, solid);

    /// <summary>A stone hearth against the back wall, with its fire going.</summary>
    void Hearth(float deep)
    {
        var z = -deep / 2 + 0.7f;
        Slab(new Vector3(0, 0.5f, z), new Vector3(2.6f, 1, 1.1f), Surface.Stone);
        Part(new Vector3(0, 2.2f, z - 0.1f), new Vector3(1.6f, 2.4f, 0.8f), Surface.Stone);
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.14f, 0.7f) }, Position = Stage + new Vector3(0, 1.06f, z + 0.1f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffb040"), EmissionEnabled = true, Emission = new Color("#ff6a10"), EmissionEnergyMultiplier = 2.5f },
        });
        Light(new Vector3(0, 1.8f, z + 1), 2.2f, 10, "#ff9a4a");
    }

    void Furnish(float wide, float deep, float high)
    {
        var rng = new Rng((ulong)Math.Abs(seed) + 11);
        switch (kind)
        {
            case "mineshaft":
                for (var z = -deep / 2 + 4; z < deep / 2 - 3; z += 7) // props, a lamp to each pair, and the odd cart and heap
                {
                    foreach (var side in new[] { -1f, 1f })
                        Slab(new Vector3(side * (wide / 2 - 0.3f), high / 2, z), new Vector3(0.4f, high, 0.4f), Surface.Timber);
                    Part(new Vector3(0, high - 0.2f, z), new Vector3(wide, 0.35f, 0.4f), Surface.Timber);
                    Light(new Vector3(wide / 2 - 0.8f, 2.4f, z), 0.9f, 9);
                    Put("ph:crate.glb", -wide / 2 + 1.2f, z + 2, 1, (float)rng.NextDouble() * Mathf.Tau);
                }
                Sign(new Vector3(0, 3, deep / 2 - 3), "Mind your head");
                main.PeopleTheShaft(this, seed, Stage + new Vector3(0, 0.4f, -deep / 2 + 5));
                break;
            case "crypt":
                foreach (var z in new[] { deep / 6, -deep / 6 }) // the vaults' walls, each with an arch through the middle
                    foreach (var side in new[] { -1f, 1f })
                        Slab(new Vector3(side * (wide / 4 + 0.9f), high / 2, z), new Vector3(wide / 2 - 1.8f, high, 0.6f), Surface.DarkStone);
                for (var z = -deep / 2 + 4; z < deep / 2 - 3; z += 6)
                    foreach (var x in new[] { -wide / 2 + 1.6f, wide / 2 - 1.6f })
                    {
                        Slab(new Vector3(x, 0.45f, z), new Vector3(1.1f, 0.9f, 2.3f), Surface.Stone); // a tomb, its lid long off
                        Light(new Vector3(x * 0.6f, 2.6f, z), 0.7f, 8, "#5ac8c0");
                    }
                AddChild(new MeshInstance3D // the tide's left a hand of black water over everything
                {
                    Mesh = new BoxMesh { Size = new Vector3(wide - 0.4f, 0.04f, deep - 0.4f) }, Position = Stage + new Vector3(0, 0.12f, 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.3f, 0.32f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, Metallic = 0.3f },
                });
                Sign(new Vector3(0, 3, deep / 2 - 3), "Here the drowned keep");
                main.PeopleTheCrypt(Stage, deep);
                break;
            case "tavern" or "longhall":
                Hearth(deep);
                Slab(new Vector3(-wide / 2 + 1, 0.55f, -1), new Vector3(1, 1.1f, 7), Surface.Timber); // the bar
                Bar = Stage + new Vector3(-wide / 2 + 2, 0, -1);
                Fireside = Stage + new Vector3(0, 0, -deep / 2 + 2);
                if (IsTavern)
                {
                    Sign(new Vector3(-wide / 2 + 1, 2.2f, -1), "The Bar");
                    Sign(new Vector3(2, 1.9f, -2), "Twenty-One");
                    Sign(new Vector3(4.2f, 1.9f, 3), "Doubles");
                }
                Put("dungeon/keg.glb", -wide / 2 + 0.6f, 3, 0.7f);
                Put("dungeon/keg.glb", -wide / 2 + 0.6f, 4.2f, 0.7f);
                foreach (var (x, z) in new[] { (2f, -2f), (4.2f, 3f), (0.2f, 2f) })
                {
                    Put("dungeon/table_medium.glb", x, z, 0.8f);
                    Put("dungeon/stool.glb", x - 1.3f, z, 0.7f, solid: false);
                    Put("dungeon/stool.glb", x + 1.3f, z, 0.7f, solid: false);
                }
                Guest("drinker", new Vector3(-wide / 2 + 2.2f, 0, -1), 1.6f);
                Guest("townsfolk", new Vector3(3.2f, 0, 3.8f), -2.4f);
                break;
            case "smithy":
                Hearth(deep);
                Put("dungeon/box_stacked.glb", wide / 2 - 1.2f, -2, 0.7f);
                Put("medieval/props/weaponrack.gltf", -wide / 2 + 0.9f, 0, 1.6f, 1.57f);
                Slab(new Vector3(2, 0.4f, -1), new Vector3(0.4f, 0.8f, 0.9f), Surface.DarkStone); // the anvil
                break;
            case "barn":
                Put("medieval/props/crate_long_A.gltf", -wide / 2 + 1.4f, -3, 1.6f);
                Put("medieval/props/sack.gltf", wide / 2 - 1.4f, -4, 1.6f, solid: false);
                Put("medieval/props/wheelbarrow.gltf", wide / 2 - 2, 3, 2.2f);
                break;
            case "church":
                Slab(new Vector3(0, 0.6f, -deep / 2 + 2), new Vector3(3, 1.2f, 1.2f), Surface.Stone); // the altar
                Put("dungeon/candle_lit.glb", -1, -deep / 2 + 2, 1, solid: false, y: 1.2f);
                Put("dungeon/candle_lit.glb", 1, -deep / 2 + 2, 1, solid: false, y: 1.2f);
                Light(new Vector3(0, 2.4f, -deep / 2 + 3), 2, 12, "#ffe0a0");
                for (var z = -deep / 2 + 6; z < deep / 2 - 4; z += 3)
                    foreach (var x in new[] { -2.8f, 2.8f })
                        Slab(new Vector3(x, 0.3f, z), new Vector3(3.4f, 0.6f, 0.6f), Surface.Timber); // the pews
                break;
            case "keep" or "tower":
                Put("dungeon/banner_red.glb", -wide / 2 + 0.5f, 0, 1.4f, 1.57f, solid: false, y: 1.2f);
                Put("dungeon/table_long_decorated_A.glb", 0, -2, 0.9f);
                Put("dungeon/chest.glb", wide / 2 - 1.3f, -deep / 2 + 1.4f, 0.8f);
                Light(new Vector3(0, 2.4f, -2), 1.6f, 11);
                break;
            default:
                Hearth(deep);
                Slab(new Vector3(wide / 2 - 1.1f, 0.3f, deep / 2 - 3), new Vector3(1.6f, 0.6f, 2.4f), Surface.Planks); // the bed
                Part(new Vector3(wide / 2 - 1.1f, 0.68f, deep / 2 - 3.2f), new Vector3(1.4f, 0.14f, 1.9f), Surface.Plaster);
                Put("dungeon/table_medium.glb", -wide / 2 + 1.7f, 0, 0.75f);
                Put("dungeon/chair.glb", -wide / 2 + 3.1f, 0, 0.7f, -1.57f, solid: false);
                Put(rng.Chance(0.5) ? "dungeon/chest.glb" : "dungeon/trunk_medium_A.glb", wide / 2 - 1, -deep / 2 + 1.4f, 0.7f);
                Put("dungeon/shelf_small.glb", -wide / 2 + 0.5f, -deep / 2 + 1.6f, 0.7f, 1.57f);
                break;
        }
    }

    void Guest(string look, Vector3 at, float yaw)
    {
        var guest = Body.For(look);
        AddChild(guest);
        guest.GlobalPosition = Stage + at;
        guest.Rotation = new Vector3(0, yaw, 0);
    }
}
