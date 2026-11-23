using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// What people say: heroes' deeds and the deviants' (see <see cref="Gameplay.Rumour"/>), told by the folk who live near where
/// they happened, a hero described and never named. And the bounty boards, at the Watch House in Tristinwold and in
/// every colony's yard: the heroes with a price on their head, how they look, and where they were last heard of.
/// </summary>
public partial class Main
{
    /// <summary>How far news travels: the folk this far from where it happened have heard of it.</summary>
    const double RumourReach = 2500;

    /// <summary>How this hero looks to a stranger.</summary>
    string Look => Gameplay.Rumour.Describe(_player.Appearance ?? Avatar.For(_player.Class.Id), _player.Class.Name); // no look picked: the class's own

    /// <summary>The town nearest a spot, for the telling ("round Larkmere").</summary>
    string? TownNear(Vector3 p) => _world.SettlementsNear(p.X, p.Z, 3000).OrderBy(s => s.DistanceTo(p.X, p.Z)).FirstOrDefault()?.Name;

    /// <summary>
    /// Something this hero did, for the folk round about to talk of: where they are (or <paramref name="at"/>), how they
    /// look, and what they did. The same <paramref name="key"/> again is the same story counted up.
    /// </summary>
    void Spread(string key, string did, bool bad, string? many = null, Vector3? at = null)
    {
        var p = at ?? _player.GlobalPosition;
        DoCity(CityAction.Spread(new Gameplay.Rumour
        {
            Key = $"{key}|{_player.Company}", By = _player.Company, Who = Look, Did = did, Many = many,
            X = p.X, Z = p.Z, Area = City.AreaAt(p), Town = TownNear(p), Bad = bad,
        }));
    }

    /// <summary>
    /// A deed on the hero's name (see <see cref="Standing"/>), talked of where it was done: every wicked one, and good
    /// turns worth a story. Killing the folk's animals is counted up on its own (see <see cref="Poached"/>).
    /// </summary>
    void RumourOf(int size, string deed)
    {
        if (deed.Length == 0 || size is > 0 and < 2) return;
        if (deed.StartsWith("killed somebody's", StringComparison.Ordinal) || deed.StartsWith("poached somebody's", StringComparison.Ordinal)) return;
        var did = deed.StartsWith("were ", StringComparison.Ordinal) ? "was " + deed[5..] : deed;
        Spread($"deed|{deed}", did, size < 0);
    }

    /// <summary>Paid thieves to rob a town's stores: word gets round who paid, and there's a price on the payer's head.</summary>
    void HiredRobbery(string town)
    {
        _standing.Evil(3, $"paid thieves to rob the stores at {town}");
        _standing.Wanted(Standing.HiredRobberyBounty);
        SyncOutlaw();
        _hud.Toast($"Word gets round {town} that somebody paid for it, and how they looked. {Standing.HiredRobberyBounty} crowns on your head ({_standing.Bounty} in all).", 8);
    }

    // ---- the telling ----

    /// <summary>What each of the folk has already told this hero, so they move on to the next story.</summary>
    readonly Dictionary<string, HashSet<string>> _toldBy = [];

    /// <summary>The freshest story from near this person's home that they haven't told yet, as they'd tell it; or null.</summary>
    string? HeardHereabouts(Npc npc)
    {
        var here = new Vector3((float)npc.X, 0, (float)npc.Z);
        var area = City.AreaAt(here);
        var day = _city.Day;
        var told = _toldBy.TryGetValue(npc.Id, out var t) ? t : _toldBy[npc.Id] = [];
        double Far(Gameplay.Rumour r) => r.X is { } x && r.Z is { } z ? Math.Sqrt((x - npc.X) * (x - npc.X) + (z - npc.Z) * (z - npc.Z)) : r.Area == area ? 0 : double.MaxValue;
        var pick = _city.Rumours
            .Where(r => r.Day >= day - Gameplay.Rumour.KeepDays && Far(r) <= RumourReach && !told.Contains($"{r.Key}#{r.Count}"))
            .OrderBy(r => Far(r) / RumourReach + (day - r.Day) * 0.25 - (r.Bad ? 0.2 : 0)).FirstOrDefault();
        if (pick is null) return null;
        told.Add($"{pick.Key}#{pick.Count}");
        var where = pick.X is { } px && pick.Z is { } pz ? Direction(here, (px, pz)) is var d && d == "here" ? "right here" : $"{d} of here"
            : $"out in {pick.Area}";
        var when = (day - pick.Day) switch { <= 0 => "today", 1 => "yesterday", _ => "a few days back" };
        var line = $"{(pick.Bad ? "Keep this to yourself, but they" : "They")} say {pick.Telling}. That was {where}, {when}.";
        if (pick.By is { } by && by.Equals(_player.Company, StringComparison.OrdinalIgnoreCase))
            line += pick.Bad ? " ...You've a look of them about you, now I think of it." : " Sounds a lot like you, now I look at you.";
        return line;
    }

    /// <summary>For the self-test: what this person would tell of next.</summary>
    public string? HeardHereaboutsForTest(Npc npc) => HeardHereabouts(npc);

    // ---- the bounty boards ----

    /// <summary>The board by the Watch House, and one in each colony's yard (drawn while the hero's near).</summary>
    static Vector3 TownBoardAt => WatchHouseAt + new Vector3(3.4f, 0, 0.6f); // worked out when asked: WatchHouseAt lives in another file
    Node3D? _townBoard;
    readonly Dictionary<string, (Node3D Root, Vector3 At)> _colonyBoards = [];
    double _boardsAt;

    void TendBoards()
    {
        if (_clock - _boardsAt < 2) return;
        _boardsAt = _clock;
        _tracker.Look ??= Look; // the deviants' stories of this hero's fights describe them, too
        _townBoard ??= BoardNode(TownBoardAt with { Y = City.FloorAt(TownBoardAt) }, 0);
        var pos = _player.GlobalPosition;
        var colonies = _city.Halls.Values.Where(h => h.Kind == HallKind.Colony).ToDictionary(h => h.Key);
        foreach (var key in _colonyBoards.Keys.ToList())
            if (!colonies.TryGetValue(key, out var h) || new Vector2((float)h.X - pos.X, (float)h.Z - pos.Z).Length() > 500)
            {
                _colonyBoards[key].Root.QueueFree();
                _colonyBoards.Remove(key);
            }
        foreach (var (key, h) in colonies)
        {
            if (_colonyBoards.ContainsKey(key) || new Vector2((float)h.X - pos.X, (float)h.Z - pos.Z).Length() > 400) continue;
            var (at, yaw, _) = ColonyStall(h);
            var spot = at + new Basis(Vector3.Up, yaw) * new Vector3(-3.6f, 0, 0.4f);
            spot = spot with { Y = City.FloorAt(spot) };
            _colonyBoards[key] = (BoardNode(spot, yaw), spot);
        }
    }

    /// <summary>A board on two posts, with "Bounty Board" over it.</summary>
    Node3D BoardNode(Vector3 at, float yaw)
    {
        var root = new Node3D { Position = at, Rotation = new Vector3(0, yaw, 0) };
        AddChild(root);
        var wood = BuildingMaterials.For(Surface.Planks);
        foreach (var x in new[] { -0.9f, 0.9f })
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 2.3f, 0.14f) }, MaterialOverride = wood, Position = new Vector3(x, 1.15f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.1f, 1.2f, 0.08f) }, MaterialOverride = wood, Position = new Vector3(0, 1.55f, 0.05f) });
        // Notices pinned to it.
        foreach (var (x, y) in new[] { (-0.55f, 1.75f), (0.1f, 1.6f), (0.6f, 1.8f), (-0.2f, 1.3f) })
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.38f, 0.48f, 0.01f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#e8dcb8") }, Position = new Vector3(x, y, 0.1f) });
        root.AddChild(new Label3D { Text = "Bounty Board", Position = new Vector3(0, 2.65f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 44, OutlineSize = 10, PixelSize = 0.01f, Modulate = new Color("#ffd27a") });
        return root;
    }

    (string Verb, Action Act)? BoardAction(Vector3 pos)
    {
        bool Near(Vector3 at) => new Vector2(at.X - pos.X, at.Z - pos.Z).Length() < 3.2f;
        if (_townBoard is not null && Near(TownBoardAt) || _colonyBoards.Values.Any(b => Near(b.At)))
            return ("read the bounty board", () => _hud.Talk("Bounty Board", BoardText(), [("Leave it.", _hud.EndTalk)]));
        return null;
    }

    /// <summary>
    /// What's pinned up: every hero with a price on their head, biggest first, with how they look and where they were
    /// last heard of (from the talk about them). Whoever brings one down collects.
    /// </summary>
    public string BoardText()
    {
        SyncOutlaw();
        var wanted = _city.Outlaws.Where(o => o.Value > 0).OrderByDescending(o => o.Value).Take(10).ToList();
        if (wanted.Count == 0) return "Nobody's wanted just now. There's nothing up but an old notice about a lost goat.";
        var day = _city.Day;
        var lines = new List<string> { "WANTED, by order of the Watch. Bring them down and the bounty is yours." };
        foreach (var (who, bounty) in wanted)
        {
            var theirs = _city.Rumours.Where(r => r.By is { } b && b.Equals(who, StringComparison.OrdinalIgnoreCase)).ToList();
            var looks = theirs.LastOrDefault(r => r.Who is not null)?.Who;
            var last = theirs.LastOrDefault(r => r.Town is not null || r.Area is not null);
            var seen = last is null ? "" : $" Last heard of {(last.Town is { } town ? $"round {town}" : $"out in {last.Area}")}, {((day - last.Day) switch { <= 0 => "today", 1 => "yesterday", _ => "a few days back" })}.";
            var you = who.Equals(_player.Company, StringComparison.OrdinalIgnoreCase) ? " (That's you.)" : "";
            lines.Add($"{who}: {bounty} crowns.{you}{(looks is null ? "" : $" {char.ToUpperInvariant(looks[0])}{looks[1..]}.")}{seen}");
        }
        return string.Join("\n", lines);
    }
}

public partial class Main
{
    public string LookForTest => Look;
    public void HiredRobberyForTest(string town) => HiredRobbery(town);
    /// <summary>For the self-test: a hero cut down, as if this one had (or hadn't) struck first.</summary>
    public void MurderedForTest(string victim, bool mine, bool asleep, bool outlaw)
    {
        _firstBlow[victim] = (mine, asleep);
        Murdered(victim, outlaw);
    }
    public (string Verb, Action Act)? BoardActionForTest(Vector3 at) => BoardAction(at);
    public static Vector3 TownBoardForTest => TownBoardAt;
    public bool TownBoardUpForTest => _townBoard is not null;
}
