using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Nemesis;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// A raid's place and its fight (see <see cref="Raids"/>): the Ashen Crypt, a scorched ring of broken pillars
/// around a sarcophagus, mist on the ground and torches that never go out. Come close while it's kindled and the
/// thralls claw up out of the ash; put them down and the Cinder Warden rises. He bursts into flame around himself
/// (a moment's warning: step away), hurls his burning skull's fire at anyone who keeps their distance, and calls more
/// thralls as he weakens. Beat him and his hoard is yours; the crypt falls quiet, and in a while it rekindles.
/// If the company falls or flees, the dead lie down again and wait.
/// </summary>
public partial class Crypt(Main main, Raid raid) : Node3D
{
    enum Phase { Waiting, Thralls, Boss, Spoils }

    Phase _phase;
    Node3D? _site;
    readonly List<Enemy> _thralls = [];
    Enemy? _boss;
    double _flame = 4, _flare = 3, _telegraph, _check;
    int _summons;

    public Pickup? Hoard { get; private set; }
    public Enemy? Warden => _boss;
    public IReadOnlyList<Enemy> Thralls => _thralls;
    public bool Fighting => _phase is Phase.Thralls or Phase.Boss;

    Vector3 Centre => new((float)raid.X, City.FloorAt(new Vector3((float)raid.X, 0, (float)raid.Z)), (float)raid.Z);

    bool Ready => main.RaidLog.Ready(raid, main.GuildBook.PlaySeconds);

    public override void _Process(double delta)
    {
        var p = main.PlayerNode;
        var d = new Vector2(p.GlobalPosition.X - (float)raid.X, p.GlobalPosition.Z - (float)raid.Z).Length();
        if (_site is null && d < 350 && main.TerrainNode.HasGround(Centre)) Build();
        else if (_site is not null && d > 500 && _phase == Phase.Waiting) { _site.QueueFree(); _site = null; }
        if (_site is null) return;

        switch (_phase)
        {
            case Phase.Waiting when Ready && d < 40 && !p.Downed:
                Wake();
                break;
            case Phase.Thralls or Phase.Boss when p.Downed || d > 120:
                Reset(p.Downed ? "The ash settles over you. The dead lie down again, and wait." : "You leave the crypt behind. The dead lie down again, and wait.");
                break;
            case Phase.Thralls when _thralls.All(t => !IsInstanceValid(t) || t.State == EnemyState.Dead):
                RaiseWarden();
                break;
            case Phase.Boss when _boss is null || !IsInstanceValid(_boss) || _boss.State == EnemyState.Dead:
                Fallen();
                break;
        }
    }

    // ---- the place ------------------------------------------------------------------------------

    void Build()
    {
        _site = new Node3D { Name = "AshenCrypt" };
        AddChild(_site);
        var c = Centre;
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#4a4642"), Roughness = 0.95f };
        var scorch = new StandardMaterial3D { AlbedoColor = new Color("#1e1a17"), Roughness = 1 };
        // The ground scorched black, darkest at the middle: a skin laid over the land, following its every rise.
        _site.AddChild(Scorch(c, 22));
        // The dais and the warden's sarcophagus.
        var dais = new StaticBody3D { Position = c + Vector3.Up * 0.3f };
        dais.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(7, 0.6f, 7) }, MaterialOverride = stone });
        dais.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(7, 0.6f, 7) } });
        dais.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.9f, 2.8f) }, MaterialOverride = scorch, Position = new Vector3(0, 0.75f, -2.2f) });
        _site.AddChild(dais);
        // A ring of pillars, some broken; torches between.
        for (var i = 0; i < 10; i++)
        {
            var a = i * Mathf.Tau / 10;
            var at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 15;
            var broken = i % 3 == 1;
            var height = broken ? 2.5f + i % 2 : 7;
            var pillar = new StaticBody3D { Position = at with { Y = City.FloorAt(at) + height / 2 } };
            pillar.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.75f, Height = height, RadialSegments = 10 }, MaterialOverride = stone });
            pillar.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.7f, Height = height } });
            if (!broken) pillar.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.5f, 1.8f) }, MaterialOverride = stone, Position = Vector3.Up * height / 2 });
            _site.AddChild(pillar);
            if (broken) Models.Place(_site, "dungeon/rubble_large.glb", at + new Vector3(1.2f, City.FloorAt(at) - at.Y, 0.8f), 0.35f, a, solid: false);
            if (i % 2 == 0)
            {
                var torch = at.Lerp(c, 0.12f);
                _site.AddChild(new OmniLight3D { Position = torch with { Y = City.FloorAt(torch) + 3 }, LightColor = new Color("#ff8a3a"), LightEnergy = 1.6f, OmniRange = 9 });
                var flame = new Node3D { Position = torch with { Y = City.FloorAt(torch) + 2.6f } };
                flame.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.08f, Height = 2.4f }, MaterialOverride = scorch, Position = Vector3.Down * 1.3f });
                flame.AddChild(Fire(0.18f, 20));
                _site.AddChild(flame);
            }
        }
        // Mist lying on the ground.
        _site.AddChild(Mist(c));
    }

    static MeshInstance3D Scorch(Vector3 c, float radius)
    {
        const int n = 28;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int i, int j)
        {
            var (x, z) = ((i / (float)n * 2 - 1) * radius, (j / (float)n * 2 - 1) * radius);
            var at = c + new Vector3(x, 0, z);
            return new Vector3(x, City.FloorAt(at) - c.Y + 0.06f, z);
        }
        Color Shade(Vector3 p)
        {
            var d = new Vector2(p.X, p.Z).Length() / radius;
            return new Color(0.09f, 0.075f, 0.065f, Mathf.Clamp(1.15f - d * d * 1.2f, 0, 0.92f));
        }
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                foreach (var (a, b) in new[] { (0, 0), (1, 0), (1, 1), (0, 0), (1, 1), (0, 1) })
                {
                    var p = Point(i + a, j + b);
                    st.SetColor(Shade(p));
                    st.SetNormal(Vector3.Up);
                    st.AddVertex(p);
                }
        return new MeshInstance3D
        {
            Mesh = st.Commit(), Position = c, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1 },
        };
    }

    static CpuParticles3D Fire(float size, int amount) => new()
    {
        Mesh = new QuadMesh
        {
            Size = Vector2.One * size * 2,
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            },
        },
        Amount = amount, Lifetime = 0.7, Direction = Vector3.Up, Spread = 15, Gravity = new Vector3(0, 1.2f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.5f,
        ColorRamp = new Gradient { Offsets = [0, 0.5f, 1], Colors = [new Color(1, 0.9f, 0.5f), new Color(1, 0.45f, 0.1f, 0.8f), new Color(0.4f, 0.05f, 0, 0)] },
    };

    static CpuParticles3D Mist(Vector3 centre) => new()
    {
        Position = centre + Vector3.Up * 0.6f,
        Mesh = new QuadMesh
        {
            Size = Vector2.One * 4.5f,
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Effects.Dot,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            },
        },
        Amount = 28, Lifetime = 9, Preprocess = 9, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(18, 0.3f, 18),
        Direction = Vector3.Right, Spread = 180, Gravity = Vector3.Zero, InitialVelocityMin = 0.1f, InitialVelocityMax = 0.4f,
        ScaleAmountMin = 0.8f, ScaleAmountMax = 1.6f,
        ColorRamp = new Gradient { Offsets = [0, 0.3f, 0.7f, 1], Colors = [new Color(0.8f, 0.82f, 0.85f, 0), new Color(0.8f, 0.82f, 0.85f, 0.16f), new Color(0.8f, 0.82f, 0.85f, 0.16f), new Color(0.8f, 0.82f, 0.85f, 0)] },
    };

    // ---- the fight ------------------------------------------------------------------------------

    Enemy Rise(string kind, Vector3 at, (double Health, double Damage) numbers, string clip, double delay)
    {
        var e = Enemy.Create(Bestiary.Get(kind), at with { Y = City.FloorAt(at) + 0.4f }, look: kind);
        e.Tune(numbers.Health, numbers.Damage);
        e.Roam = 6;
        main.AddEnemy(e);
        e.Figure.Perform(clip, delay, 1);
        Turn(e, main.PlayerNode.GlobalPosition);
        GetTree().CreateTimer(delay).Timeout += () => { if (IsInstanceValid(e) && e.State != EnemyState.Dead) e.Provoke(main.PlayerNode); };
        return e;
    }

    void Wake()
    {
        _phase = Phase.Thralls;
        var level = main.PlayerNode.Hero.Level;
        main.HudNode.Toast(Raids.Outgrown(raid, level)
            ? $"The ash of {raid.Name} stirs, half-heartedly. You've grown past this place; there's little here for you now."
            : $"The ash of {raid.Name} stirs. Something claws up out of the ground...", 9);
        for (var i = 0; i < raid.Thralls; i++)
        {
            var a = i * Mathf.Tau / raid.Thralls + 0.4f;
            _thralls.Add(Rise("cinder_thrall", Centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 8, Raids.Thrall(raid, level), "Spawn_Ground_Skeletons", 1.6));
        }
    }

    void RaiseWarden()
    {
        _phase = Phase.Boss;
        _summons = 0;
        (_flame, _flare, _telegraph) = (5, 3, 0);
        var level = main.PlayerNode.Hero.Level;
        _boss = Rise("cinder_warden", Centre + new Vector3(0, 0.6f, -1), Raids.Boss(raid, level), "Skeletons_Awaken_Standing", 2.4);
        _boss.Stats.Name = raid.Boss;
        _boss.Roam = 4;
        _boss.Think = Think;
        _boss.RefreshPlate();
        Effects.Burst(GetParent(), Centre + Vector3.Up * 2, DamageKind.Fire, 4);
        main.HudNode.Toast($"{raid.Boss} rises from the ash. \"{raid.Taunt}\"", 9);
    }

    /// <summary>The Warden's own moves, every moment he fights.</summary>
    void Think(Enemy warden, double delta)
    {
        var p = main.PlayerNode;
        var d = warden.GlobalPosition.DistanceTo(p.GlobalPosition);
        var damage = warden.Kind.Damage * warden.Stats.DamageMult;

        // Flame burst: he rears up, burning brighter, then fire bursts all around him. Step away in time.
        if (_telegraph > 0)
        {
            if ((_telegraph -= delta) <= 0)
            {
                Effects.Burst(GetParent(), warden.GlobalPosition + Vector3.Up * 0.5f, DamageKind.Fire, 6);
                if (d < 6.5f && !p.Downed) p.TakeHit(damage * 1.6, DamageKind.Fire, AttackKind.Spell, false, warden);
                foreach (var c in p.Companions().Where(c => c.GlobalPosition.DistanceTo(warden.GlobalPosition) < 6.5f).ToList()) c.TakeHitFrom(warden, damage * 1.6, false, DamageKind.Fire);
            }
            return;
        }
        if ((_flame -= delta) <= 0 && d < 7)
        {
            _flame = 9;
            _telegraph = 1.4;
            warden.Figure.Perform("Spellcast_Raise", 1.4, 1);
            Effects.Rise(warden, DamageKind.Fire, 3);
            main.HudNode.Toast($"{raid.BossName} burns brighter...", 2);
            return;
        }
        // Skull flare: fire hurled at anyone who keeps their distance.
        if ((_flare -= delta) <= 0 && d > 8 && d < 30)
        {
            _flare = 5;
            Turn(warden, p.GlobalPosition);
            warden.Figure.Perform("Spellcast_Shoot", 0.8, 1.2f);
            Effects.Bolt(GetParent(), warden.GlobalPosition + Vector3.Up * 3, p.GlobalPosition + Vector3.Up, DamageKind.Fire, 0.4f);
            p.TakeHit(damage, DamageKind.Fire, AttackKind.Spell, false, warden);
        }
        // As he weakens, he calls more of the dead.
        var share = warden.Stats.Health / warden.Stats.MaxHealth;
        if ((_summons == 0 && share < 0.66) || (_summons == 1 && share < 0.33))
        {
            _summons++;
            warden.Figure.Perform("Spellcast_Summon", 1.2, 1);
            main.HudNode.Toast($"{raid.BossName} calls the dead!", 4);
            for (var i = 0; i < 2; i++)
            {
                var a = (float)GD.RandRange(0, Mathf.Tau);
                _thralls.Add(Rise("cinder_thrall", warden.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5, Raids.Thrall(raid, p.Hero.Level), "Spawn_Ground_Skeletons", 1.2));
            }
        }
    }

    void Fallen()
    {
        _phase = Phase.Spoils;
        foreach (var t in _thralls.Where(t => IsInstanceValid(t) && t.State != EnemyState.Dead)) t.Collapse();
        main.HudNode.Toast($"{raid.Boss} crumbles into ash and embers. His hoard lies open on the dais.", 9);
        Hoard = new Pickup { QuestId = raid.Id, Item = "the Warden's hoard" };
        AddChild(Hoard);
        Hoard.GlobalPosition = Centre + new Vector3(0, 0.6f, 1.5f);
    }

    /// <summary>The hoard taken: the reward, and the crypt falls quiet until it rekindles.</summary>
    public (int Xp, int Septims) Loot()
    {
        var first = main.RaidLog.ClearsOf(raid) == 0;
        var reward = Raids.Reward(raid, main.PlayerNode.Hero.Level, first);
        main.RaidLog.Cleared(raid, main.GuildBook.PlaySeconds);
        Hoard?.QueueFree();
        Hoard = null;
        _phase = Phase.Waiting;
        _thralls.Clear();
        _boss = null;
        return reward;
    }

    void Reset(string why)
    {
        foreach (var t in _thralls.Where(IsInstanceValid)) t.QueueFree();
        if (_boss is not null && IsInstanceValid(_boss)) _boss.QueueFree();
        _thralls.Clear();
        _boss = null;
        _phase = Phase.Waiting;
        main.HudNode.Toast(why, 7);
    }

    /// <summary>A line for the quest tracker, for heroes it was made for.</summary>
    public string? Tracker(Vector3 from, Func<Vector3, (double, double), string> direction)
    {
        if (Raids.Outgrown(raid, main.PlayerNode.Hero.Level)) return null;
        var left = main.RaidLog.SecondsLeft(raid, main.GuildBook.PlaySeconds);
        var state = Fighting ? "the dead are up" : left > 0 ? $"rekindles in {Math.Ceiling(left / 60)} min" : "kindled";
        return $"Raid: {Cap(raid.Name)}, {state} ({direction(from, (raid.X, raid.Z))})";
    }

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    static void Turn(Node3D who, Vector3 toward)
    {
        var to = toward - who.GlobalPosition;
        to.Y = 0;
        if (to.LengthSquared() > 0.01f) who.LookAt(who.GlobalPosition + to, Vector3.Up);
    }
}
