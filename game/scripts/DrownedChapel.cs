using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The Drowned Chapel: out past the walls on the north shore, what's left of the old Temple of the Tide's church, the
/// sea at its back and a few houses gone to rot around it. Its roof fell in long ago. Under the altar, steps go down
/// into the crypts the tide got into, three vaults deep, and the drowned dead keep them. Clear them and the
/// Temple pays; give it a few days and they're back.
/// </summary>
public partial class Main
{
    public static readonly Vector3 ChapelAt = new(118, 0, -182);
    const int CryptRefills = 3;
    readonly List<Enemy> _cryptFoes = [];

    /// <summary>Which way the ruin faces: its door towards the city.</summary>
    static float ChapelYaw => Mathf.Atan2(-ChapelAt.X, -ChapelAt.Z);

    /// <summary>Where the steps go down, behind the altar, and where you come back up.</summary>
    static Vector3 CryptStair => ChapelAt + new Vector3(0, 0, -7.5f).Rotated(Vector3.Up, ChapelYaw);
    static Vector3 CryptStep => ChapelAt + new Vector3(0, 0, -4.5f).Rotated(Vector3.Up, ChapelYaw);

    string CryptKey => $"crypt:{_city.Day / CryptRefills}";
    public bool CryptCleared => _pursuits.Counters.ContainsKey(CryptKey);

    /// <summary>The ruin, the rotten houses round it, and the dark of the stairwell.</summary>
    void AddDrownedChapel()
    {
        var floor = City.FloorAt(ChapelAt);
        var root = new Node3D { Name = "DrownedChapel", Position = ChapelAt with { Y = floor }, Rotation = new Vector3(0, ChapelYaw, 0) };
        AddChild(root);
        var rng = new Rng(0xD20C);
        const float wide = 10, deep = 22, thick = 0.8f;
        void Stone(Vector3 at, Vector3 size, float tilt = 0)
        {
            var body = new StaticBody3D { Position = at, Rotation = new Vector3(tilt, 0, tilt * 0.6f) };
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = BuildingMaterials.For(Surface.Stone) });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            root.AddChild(body);
            Buildings.Mould(body);
        }
        // The flagged floor, sunk a little and heaved by the years.
        Stone(new Vector3(0, -0.45f, 0), new Vector3(wide, 1, deep));
        // The long walls: broken off at every height, with gaps where they've come down altogether.
        foreach (var side in new[] { -1f, 1f })
            for (var z = -deep / 2 + 1.5f; z < deep / 2 - 1; z += 3)
            {
                if (rng.Chance(0.22)) continue; // fallen
                var h = 1.2f + (float)rng.NextDouble() * 4.6f;
                Stone(new Vector3(side * (wide / 2 - thick / 2), h / 2 - 0.5f, z), new Vector3(thick, h + 1, 2.9f));
            }
        // The front, with its doorway and half its gable; the east end standing taller round an empty window.
        foreach (var x in new[] { -3.4f, 3.4f })
            Stone(new Vector3(x, 2.3f, deep / 2 - thick / 2), new Vector3(3.2f, 5.6f, thick));
        Stone(new Vector3(-1.2f, 5.6f, deep / 2 - thick / 2), new Vector3(2.4f, 1.2f, thick));
        foreach (var x in new[] { -3.2f, 3.2f })
            Stone(new Vector3(x, 3.2f, -deep / 2 + thick / 2), new Vector3(3.6f, 7.4f, thick));
        Stone(new Vector3(0, 6.4f, -deep / 2 + thick / 2), new Vector3(2.8f, 1, thick));
        // The bell tower, a stump now, and the bell long gone.
        Stone(new Vector3(wide / 2 + 1.4f, 3.2f, deep / 2 - 2), new Vector3(3, 7.4f, 3));
        // The altar, and what's come down out of the roof and walls round it.
        Stone(new Vector3(0, 0.5f, -deep / 2 + 3), new Vector3(2.6f, 1.1f, 1.1f));
        for (var i = 0; i < 9; i++)
        {
            var at = new Vector3((float)rng.NextDouble() * (wide - 2) - wide / 2 + 1, 0.1f, (float)rng.NextDouble() * (deep - 4) - deep / 2 + 2);
            if (at.DistanceTo(new Vector3(0, 0.1f, -7.5f)) < 2.2f) continue; // the stairwell stays clear
            var s = 0.5f + (float)rng.NextDouble() * 0.9f;
            Stone(at, new Vector3(s * 1.4f, s, s), (float)rng.NextDouble() * 0.5f);
        }
        // The stairwell behind the altar: the top steps, then dark.
        for (var i = 0; i < 3; i++)
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2, 0.2f, 0.6f) }, MaterialOverride = BuildingMaterials.For(Surface.DarkStone), Position = new Vector3(0, 0.02f - i * 0.22f, -6.6f - i * 0.55f) });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(2, 0.05f, 1.4f) }, Position = new Vector3(0, -0.6f, -8.4f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Black, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        });
        root.AddChild(new OmniLight3D { Position = new Vector3(0, 0.6f, -7.8f), LightColor = new Color("#6ad0c8"), LightEnergy = 0.8f, OmniRange = 5 });
        root.AddChild(new Label3D { Text = "The Drowned Chapel", Position = new Vector3(0, 8.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
        // The houses that were the chapel's parish, gone to rot.
        foreach (var (dx, dz, kind) in new[] { (-17f, 4f, "cottage"), (-15f, -12f, "ruin"), (17f, -9f, "cottage") })
        {
            var at = ChapelAt + new Vector3(dx, 0, dz).Rotated(Vector3.Up, ChapelYaw);
            var house = Buildings.Place(this, kind, at with { Y = City.FloorAt(at) - 0.15f }, ChapelYaw + (float)rng.NextDouble() * 0.6f - 0.3f, (int)(dx * 7 + dz), Surface.Thatch, 0.9f, door: false);
            Buildings.Mould(house);
        }
    }

    (string Verb, Action Act)? ChapelAction(Vector3 pos)
    {
        if (_indoors is not null || _player.Mount is not null || pos.DistanceTo(CryptStair with { Y = pos.Y }) > 2.6f) return null;
        return ("go down into the crypt", () => GoInside("crypt", 0xD20C, CryptStep));
    }

    /// <summary>The drowned dead in the crypt, set out as the hero comes down: three vaults of them, and the singer at the back.</summary>
    public void PeopleTheCrypt(Vector3 stage, float deep)
    {
        if (CryptCleared) return;
        var danger = WorldGen.Danger(ChapelAt.X, ChapelAt.Z) * 1.25 + _player.Hero.Level * 0.03;
        void Raise(string kind, float x, float z)
        {
            var e = Enemy.Create(Bestiary.Get(kind), stage + new Vector3(x, 0.3f, z), danger);
            AddEnemy(e);
            _cryptFoes.Add(e);
        }
        var first = deep / 2 - 16;
        foreach (var x in new[] { -3f, 0f, 3f }) Raise("hollow_soldier", x, first);
        Raise("draugr", -3, 0);
        Raise("draugr", 3, 0);
        Raise("hollow_soldier", -1, -2);
        Raise("hollow_soldier", 1, -2);
        var back = -deep / 2 + 6;
        Raise("draugr_overlord", -2, back);
        Raise("sea_wraith", 2, back - 1);
    }

    public void LeftTheCrypt()
    {
        foreach (var e in _cryptFoes.Where(IsInstanceValid)) e.QueueFree();
        _cryptFoes.Clear();
    }

    void TendCrypt()
    {
        if (_cryptFoes.Count == 0 || _cryptFoes.Any(e => IsInstanceValid(e) && e.State != EnemyState.Dead)) return;
        _cryptFoes.Clear();
        _pursuits.Counters[CryptKey] = 1;
        var pay = 80 + 12 * _player.Hero.Level;
        _player.Crowns += pay;
        _player.Pack.Add("ancient_coin", 3);
        GainXp(60 + 8 * _player.Hero.Level);
        Repute.Deed(3, ReputationBook.Capital);
        Sound.Ui("quest");
        _hud.Toast($"The singing stops. The crypt is quiet, for now. The Temple of the Tide sends {pay} crowns after you, and you find three old coins in the silt.", 9);
        Save();
    }

    /// <summary>For the self-test.</summary>
    public IReadOnlyList<Enemy> CryptFoes => _cryptFoes;
    public void EnterCryptForTest() => GoInside("crypt", 0xD20C, CryptStep);
    public (string Verb, Action Act)? ChapelActionForTest(Vector3 at) => ChapelAction(at);
    public static Vector3 CryptStairForTest => CryptStair;
}
