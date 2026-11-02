using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The carpenters (see <see cref="Carpentry"/>): their yard out past Hollin's woodlot, their master, and the work he
/// hands out. Any hero can join them: take a job, bring the planks, and put the house up at the edge of the town that
/// wants it, frame, walls and roof, and the town has the house for everyone on the server.
/// </summary>
public partial class Main
{
    public static readonly Vector3 CarpentersYard = new(-175, 0, -105);
    const string MasterCarpenter = "tristinwold/carpenter";
    Node3D? _site;
    bool _framing;

    int CarpentryDone => _pursuits.Counters.GetValueOrDefault("carpentry:done");

    /// <summary>The job in hand, if any: the town, what's to be done there, and how far it's got.</summary>
    (string Town, CarpentryWork Work, int Stage)? CarpentryJob
    {
        get
        {
            var bits = _pursuits.Carpentry.Split('|');
            return bits.Length == 3 && Enum.TryParse<CarpentryWork>(bits[1], out var w) && int.TryParse(bits[2], out var stage) ? (bits[0], w, stage) : null;
        }
        set => _pursuits.Carpentry = value is { } j ? $"{j.Town}|{j.Work}|{j.Stage}" : "";
    }

    void AddCarpentersYard()
    {
        var at = CarpentersYard with { Y = City.FloorAt(CarpentersYard) };
        Buildings.Place(this, "barn", at + new Vector3(0, 0, -8), 0, 21, Surface.Shingle);
        for (var i = 0; i < 4; i++)
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4, 0.3f, 1.2f) }, MaterialOverride = BuildingMaterials.For(Surface.Planks), Position = at + new Vector3(6, 0.15f + i * 0.3f, 2 + (i % 2) * 0.2f) });
        Models.Place(this, "ph:stump.glb", at + new Vector3(-5, 0, 3), 1.2f, 0.4f, solid: false);
        AddChild(new Label3D { Text = "The Carpenters' Yard", Position = at + new Vector3(0, 6, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
        Townsfolk(new Npc(MasterCarpenter, "Abel Joist", Sex.Male, 52, "master carpenter", ReputationBook.Capital, "Tristinwold",
            "Has built half the houses outside the walls and mended the other half. Takes on anyone who can hold a saw straight.",
            ["timber", "a square corner"], "gruff", "Measure twice. What do you want?", at.X + 2, at.Z + 3), "worker");
    }

    /// <summary>The places the carpenters work: Tristinwold, and the towns the wagons reach.</summary>
    IEnumerable<(string Id, string Name)> CarpentryTowns => JobTowns.Select(t => (t.Id, t.Name)).Prepend((ReputationBook.Capital, "Tristinwold"));

    IEnumerable<(string, Action)> CarpentryTalk(Person person)
    {
        if (person.Npc.Id != MasterCarpenter) yield break;
        var done = CarpentryDone;
        yield return ("How do I stand?", () => Say(person, done == 0 ? "You don't, yet. Take a job, and we'll see." : $"You're a {Carpentry.Rank(done)}: {done} houses up. {(done < Carpentry.MasterAt ? $"{(done < Carpentry.JourneymanAt ? Carpentry.JourneymanAt : Carpentry.MasterAt) - done} more and you'll be a {Carpentry.Rank(done < Carpentry.JourneymanAt ? Carpentry.JourneymanAt : Carpentry.MasterAt)}." : "There's no more I can teach you.")}"));
        yield return ("Can I use the sawhorse?", () => ShowCraft(Job.Building, () => Say(person, "Leave it as you found it.")));
        if (CarpentryJob is { } job)
        {
            var name = CarpentryTowns.FirstOrDefault(t => t.Id == job.Town).Name ?? job.Town;
            yield return ($"About the job in {name}...", () => Say(person, $"{Carpentry.Say(job.Work, name)}. {(job.Stage == 0 ? $"Take {Carpentry.Planks} planks out to the site, the far side of its wall, and get the frame up." : $"You've {Carpentry.Stages - job.Stage} of it left to do.")}"));
            yield return ("I'll have to leave that job.", () => { CarpentryJob = null; _site?.QueueFree(); _site = null; Save(); Say(person, "Somebody else will finish it."); });
            yield break;
        }
        var work = CarpentryTowns.Select(t => (t.Id, t.Name, Work: Carpentry.Wanted(_city, t.Id))).Where(t => t.Work is not null).ToList();
        if (work.Count == 0) { yield return ("Any work?", () => Say(person, "Nobody's building just now: nowhere's burned, nowhere's doing well enough to grow. Ask me in a few days.")); yield break; }
        foreach (var (id, name, w) in work.Take(6))
            yield return ($"{Carpentry.Say(w!.Value, name)}. ({Carpentry.Pay(done, w.Value)} crowns; bring {Carpentry.Planks} planks)", () =>
            {
                CarpentryJob = (id, w.Value, 0);
                Save();
                Say(person, $"Good. The site's out past {name}'s wall: you'll find the pegs in. Frame, walls and roof, and come back for your pay. No, the pay comes to you. Go on.");
            });
    }

    /// <summary>Where a town's next house goes up: just outside its wall, on the side away from the capital's road.</summary>
    Vector3? SiteOf(string town)
    {
        if (town == ReputationBook.Capital) return new Vector3(-75, 0, -82);
        if (_world.Find(town) is not { } s) return null;
        var home = new Vector2((float)-s.X, (float)-s.Z).Normalized();
        var side = new Vector2(-home.Y, home.X);
        return new Vector3((float)s.X + side.X * (float)(s.Radius + 14), 0, (float)s.Z + side.Y * (float)(s.Radius + 14));
    }

    /// <summary>The site of the job in hand, drawn as far as it's got: pegs, then a frame, then walls, then a roof going on.</summary>
    void TendSite()
    {
        if (CarpentryJob is not { } job || SiteOf(job.Town) is not { } at || _player.GlobalPosition.DistanceTo(at with { Y = _player.GlobalPosition.Y }) > 140)
        {
            _site?.QueueFree();
            _site = null;
            return;
        }
        if (_site is not null && (int)_site.GetMeta("stage") == job.Stage) return;
        _site?.QueueFree();
        _site = new Node3D();
        _site.SetMeta("stage", job.Stage);
        AddChild(_site);
        _site.GlobalPosition = at with { Y = City.FloorAt(at) };
        var timber = BuildingMaterials.For(Surface.Timber);
        foreach (var (x, z) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
            _site.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.25f, job.Stage == 0 ? 0.6f : 3.2f, 0.25f) }, MaterialOverride = timber, Position = new Vector3(x, job.Stage == 0 ? 0.3f : 1.6f, z) });
        if (job.Stage >= 2)
            foreach (var (x, z, w, d) in new[] { (0f, -3f, 6f, 0.2f), (0f, 3f, 6f, 0.2f), (-3f, 0f, 0.2f, 6f), (3f, 0f, 0.2f, 6f) })
                _site.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w, 2.8f, d) }, MaterialOverride = BuildingMaterials.For(Surface.Planks), Position = new Vector3(x, 1.4f, z) });
        _site.AddChild(new Label3D { Text = "Building site", Position = Vector3.Up * 4.5f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, OutlineSize = 10, PixelSize = 0.01f, Modulate = new Color("#ffd27a") });
    }

    (string Verb, Action Act)? SiteAction(Vector3 pos)
    {
        if (_framing || CarpentryJob is not { } job || _site is null || pos.DistanceTo(_site.GlobalPosition with { Y = pos.Y }) > 6) return null;
        var what = job.Stage switch { 0 => "put the frame up", 1 => "board the walls", _ => "get the roof on" };
        return (job.Stage == 0 ? $"{what} ({Carpentry.Planks} planks)" : what, () => Frame(job));
    }

    async void Frame((string Town, CarpentryWork Work, int Stage) job)
    {
        if (job.Stage == 0 && _player.Pack.Count("plank") < Carpentry.Planks) { Sound.Ui("error"); _hud.Toast($"You'll want {Carpentry.Planks} planks. Saw them at Abel's sawhorse, or your own.", 5); return; }
        if (job.Stage == 0) _player.Pack.Take("plank", Carpentry.Planks);
        _framing = true;
        _player.Face(_site!.GlobalPosition);
        var held = Hands()?.Carry("pick");
        for (var i = 0; i < 3; i++)
        {
            Hands()?.Perform("2H_Melee_Attack_Chop", 0.7, 1.1f);
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this)) return;
            Sound.At("melee/hit_metal", _site?.GlobalPosition ?? _player.GlobalPosition, -4, 0.6f);
        }
        held?.QueueFree();
        _framing = false;
        if (job.Stage + 1 < Carpentry.Stages)
        {
            CarpentryJob = job with { Stage = job.Stage + 1 };
            Save();
            TendSite();
            return;
        }
        CarpentryJob = null;
        _site?.QueueFree();
        _site = null;
        var pay = Carpentry.Pay(CarpentryDone, job.Work);
        DoCity(CityAction.Carpentered(job.Town, job.Work, _player.Company));
        _pursuits.Counters["carpentry:done"] = CarpentryDone + 1;
        _player.Crowns += pay;
        GainXp(30 + 5 * _player.Hero.Level);
        Repute.Deed(2, job.Town);
        Sound.Ui("quest");
        _hud.Toast($"The roof's on. {pay} crowns from Abel, and the town has its house. You're a {Carpentry.Rank(CarpentryDone)}.", 8);
        Save();
    }

    /// <summary>For the self-test.</summary>
    public (string Town, CarpentryWork Work, int Stage)? CarpentryJobForTest { get => CarpentryJob; set => CarpentryJob = value; }
    public Vector3? SiteForTest(string town) => SiteOf(town);
    public void TendSiteForTest() => TendSite();
    public (string Verb, Action Act)? SiteActionForTest(Vector3 at) => SiteAction(at);
    public bool FramingForTest => _framing;
}
