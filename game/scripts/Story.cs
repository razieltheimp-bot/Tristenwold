using Godot;
using Tristinwold.Gameplay;
using Tristinwold.Deviant;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The main story, The Rings of Tristinwold (see <see cref="ImmortalSaga"/>): the hero is an Immortal, made under the
/// Needle and bound by an iron ring to the Wake beneath the Gallows Perch. Mother Corva tells them what they are;
/// Sister Ilse shows them the Magi's ledger; they fall, and the ring brings them back; they close a breach the Host
/// has torn open beyond the walls, and bring down a dragon the Rift has taken.
/// </summary>
public partial class Main
{
    ImmortalSaga _story = new();
    (double X, double Z)? _storyGoal;
    double _lastStory;
    HostBreach? _breach;
    readonly Rng _storyRng = new((ulong)Random.Shared.NextInt64());

    public ImmortalSaga Story => _story;
    public HostBreach? BreachNode => _breach;

    static (double X, double Z) CorvaAt => (City.GallowsPerch.X - 2.5, City.GallowsPerch.Z + 11);
    static readonly (double X, double Z) IlseAt = (-34, 6);

    /// <summary>A new hero opens their eyes at the Gallows Perch, and the story begins (an old save's hero joins it too).</summary>
    void BeginStory()
    {
        _player.Woke += OnWoke;
        Ageing.Wake(_player.Hero, _player.Race.Id, _city.Day); // they woke at about twenty, in their people's years
        if (_story.Begin()) _hud.Toast(ImmortalSaga.Opening, 16);
    }

    /// <summary>The ring brought the hero back.</summary>
    void OnWoke()
    {
        SeaWoke();
        IsleWoke();
        _hud.Toast(_story.Returned(_storyRng), 12);
        Save();
    }

    void StorySlain(Enemy victim)
    {
        if (!_story.Slain(victim.Kind.Id)) return;
        if (_story.Moved is { } said) _hud.Toast(said, 14);
        _hud.Log($"The Rings of Tristinwold: {_story.Objective}");
        Save();
    }

    IEnumerable<(string, Action)> StoryTalk(Person person)
    {
        var id = person.Npc.Id;
        if (id is not (ImmortalSaga.Corva or ImmortalSaga.Ilse)) yield break;
        var (ask, then) = (_story.Chapter, id) switch
        {
            (Chapter.Waking, ImmortalSaga.Corva) => ("What am I?", "Sit. Drink. Then go and find Sister Ilse."),
            (Chapter.Archive, ImmortalSaga.Ilse) => ("Mother Corva says I was made here. What was done to me?", "Go on. Find out if it holds."),
            (Chapter.Warden, ImmortalSaga.Corva) => ("The dragon is dead.", "Now. Drink."),
            _ => ((string?)null, ""),
        };
        if (ask is not null)
            yield return (ask, () =>
            {
                if (_story.Talked(id) is not { } told) return;
                _hud.Talk(Who(person.Npc), told, ("...", () => Say(person, then)));
                if (_story.Chapter == Chapter.Done) _hud.Toast($"You are a {ImmortalSaga.Title}.", 10);
                Save();
            });
        if (id == ImmortalSaga.Corva && _story.Chapter > Chapter.Waking)
            yield return ("About the Wake...", () => Say(person,
                $"It's under this floor. Don't go looking. When one of you dies, the ring pulls you back down to it, and it makes you again, and up you come, here. " +
                $"You've come back {_story.Returns} time{(_story.Returns == 1 ? "" : "s")}. I write every one down. Somebody ought to."));
        if (id == ImmortalSaga.Ilse && _story.Chapter > Chapter.Archive)
            yield return ("Why make us at all?", () => Say(person,
                "Because the Host can't abide a thing that won't stay dead. A city can raise ten thousand spears, and the Host will kill every one of them, once. " +
                "And because of the dragons the Rift has taken. Nobody born has ever beaten one. Somebody who can come back, and try again, and learn, might."));
    }

    /// <summary>The story's line on the tracker, and the breach while it's open.</summary>
    void TendStory()
    {
        if (_clock - _lastStory < 2) return;
        _lastStory = _clock;
        var at = _player.GlobalPosition;
        _storyGoal = _story.Goal(_world, CorvaAt, IlseAt, at.X, at.Z);
        if (_story.Chapter == Chapter.Breach && _breach is null)
        {
            var (x, z) = ImmortalSaga.BreachSite(_world);
            _breach = new HostBreach(this, new Vector3((float)x, 0, (float)z));
            AddChild(_breach);
        }
        else if (_story.Chapter != Chapter.Breach && _breach is not null)
        {
            _breach.Close();
            _breach = null;
        }
    }

    string? StoryLine(Vector3 pos) => _story.Objective is { } next
        ? $"The Rings of Tristinwold: {next}{(_storyGoal is { } g ? $" ({Direction(pos, g)})" : "")}"
        : null;
}

/// <summary>
/// Where the Hollow Host has torn the world open, out past the walls: a wound of violet light that imps crawl out of,
/// and now and then something worse. It keeps coming while the hero is near and the breach is open.
/// </summary>
public partial class HostBreach(Main main, Vector3 at) : Node3D
{
    readonly List<Enemy> _host = [];
    double _next;

    /// <summary>Where it stands, on the ground (still known once it's closed).</summary>
    public Vector3 At => _ground;
    Vector3 _ground = at;
    public IReadOnlyList<Enemy> Host => _host;

    public override void _Ready()
    {
        Position = _ground = at with { Y = City.FloorAt(at) };
        var violet = new Color("#a050ff");
        var wound = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 1.6f, OuterRadius = 2.3f, Rings = 32, RingSegments = 12 },
            Position = new Vector3(0, 2.6f, 0), Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = violet, EmissionEnabled = true, Emission = violet, EmissionEnergyMultiplier = 3 },
        };
        AddChild(wound);
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.6f, Height = 0.05f },
            Position = new Vector3(0, 2.6f, 0), Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0, 0.08f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        });
        AddChild(new OmniLight3D { Position = new Vector3(0, 2.6f, 0), LightColor = violet, LightEnergy = 3, OmniRange = 16 });
        AddChild(new Label3D { Text = "A breach of the Hollow Host", Position = new Vector3(0, 5.6f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, Modulate = violet, OutlineSize = 8 });
    }

    public override void _Process(double delta)
    {
        GetChild<Node3D>(0).RotateObjectLocal(Vector3.Up, (float)delta * 0.6f);
        _host.RemoveAll(e => !IsInstanceValid(e) || e.State == EnemyState.Dead);
        var p = main.PlayerNode;
        if ((_next -= delta) > 0 || p.Downed || p.GlobalPosition.DistanceTo(GlobalPosition) > 90 || _host.Count >= 4) return;
        _next = 4;
        // Mostly imps; every few, a dire imp.
        var kind = main.Story.HostSlain % 4 == 3 && _host.All(e => e.Kind.Id != "dire_imp") ? "dire_imp" : "imp";
        var off = new Vector3((float)GD.RandRange(-3.0, 3.0), 0, (float)GD.RandRange(-3.0, 3.0));
        var spot = GlobalPosition + off;
        var e = Enemy.Create(Bestiary.Get(kind), spot with { Y = City.FloorAt(spot) + 0.5f }, WorldGen.Danger(spot.X, spot.Z));
        main.AddEnemy(e);
        e.Provoke(p);
        _host.Add(e);
        Effects.Rise(e, DamageKind.Fire, 1.2f);
    }

    /// <summary>Shut: the light goes out and the Host that came through goes back with it.</summary>
    public void Close()
    {
        foreach (var e in _host.Where(IsInstanceValid)) e.QueueFree();
        QueueFree();
    }
}
