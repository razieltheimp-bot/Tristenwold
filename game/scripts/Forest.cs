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
    const string Elder = "hollowheart/oruun", Druid = "hollowheart/maren", Sprout = "hollowheart/thistle", Shroomie = "hollowheart/morel", Leader = "hollowheart/wukang", Raffkin = "northwood/raffkin", TurlinMan = "northwood/turlin";

    public const string TurlinId = TurlinMan, TakenId = "northwood/taken";
    public ForestBook ForestLog => _forest;
    public Forest? ForestNode => _forestNode;

    void AddForest()
    {
        _standing.Told = OnDeed;
        var (vx, vz) = (Northwood.VillageX, Northwood.VillageZ);
        Townsfolk(new Npc(Elder, "Oruun Greybrow", Sex.Male, 70, "elder of Hollowheart", "hollowheart", "Hollowheart",
            "Oruun has sat at the root of the Heartwood longer than anyone can say. He has a top-knot gone grey, a string of river-pearls, and a way of looking at you until you remember your manners.",
            ["the forest", "old battles"], "dry", "Sit. The tree isn't going anywhere, and neither are you.", vx + 16, vz + 14), "ape_elder");
        Townsfolk(new Npc(Leader, "Lu Wukang", Sex.Male, 41, "king of the Apekin", "hollowheart", "Hollowheart",
            "Lu Wukang sits at the top of every argument in Hollowheart with his red-and-gold staff across his knees. He has broken more jaws than treaties, and he guards the wood's secrets better than either.",
            ["old battles", "the forest"], "proud", "You're a long way from your walls, little cousin.", vx + 24, vz + 6), "ape_warrior");
        Townsfolk(new Npc(Raffkin, "Raffkin", Sex.Male, 66, "a druid nobody speaks of", "northwood", "the Northwood",
            "A baboon-faced ape in a nest of old branches, staff wrapped in rag, butterflies coming and going from his fur. The Apekin of Hollowheart drove him out and won't say his name. He says the wood has a lord, and the lord is sleeping.",
            ["the forest", "dreams"], "sly", "Ah! Somebody with an axe in their eyes. Come up, come up.", Northwood.RaffkinX, Northwood.RaffkinZ), "ape_elder");
        Townsfolk(new Npc(Druid, "Maren of the Roots", Sex.Female, 52, "druid of the Northwood", "hollowheart", "Hollowheart",
            "Maren came to the wood to study it and was, she says, studied back. She tends what the guardians can't reach.",
            ["the forest", "herbs"], "kindly", "Mind where you step. Most of it's listening.", vx - 18, vz + 12), "priest");
        Townsfolk(new Npc(Sprout, "Thistle-in-Spring", Sex.Female, 30, "speaker of the Photosynthens", "hollowheart", "Hollowheart",
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
        AddChild(_harbours = new Harbours(this));
        AddLegends();
    }

    Harbours? _harbours;

    (string Verb, Action Act)? ForestAction(Vector3 pos)
    {
        if (_harbours?.Pick(pos) is { } pick) return pick;
        if (BladeAction(pos) is { } blade) return blade;
        if (_forestNode?.NearClue(pos) != true || _forest.Turlin.Current is not { } c) return null;
        return ("look at what's lying here", () =>
        {
            _forest.Turlin.Find();
            _hud.Toast($"{char.ToUpperInvariant(c.Token[0])}{c.Token[1..]}. And round it, pressed deep in the mud, the marks of something huge. Take it back to Turlin at the camp; or to someone who knows the wood.", 10);
            Save();
        });
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
        if (id == TurlinMan) foreach (var o in TurlinTalk(person)) yield return o;
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
            else yield return ("Tell me about the guardians.", () => Say(person, "Four. They mended this wood before there was a Tristinwold. The Photosynthens pray to them. They'll leave you be, unless you've taken an axe to the trees. Then don't walk their ground."));
            if (c.LairOpen) yield return ("Something opened in the west.", () => Say(person, c.DragonFree ? "It's out. Everything we were afraid of is out." : "We felt it. There's a barrier on that place. The guardians ARE the barrier. Do not kill another. Do you hear me? Not one."));
        }
        if (id == Leader)
        {
            yield return ("Where are the other guardians?", () => Say(person, "Where they've always been, and that's all I'll say. Ask me again and I'll answer with the staff. There are some roads you don't walk a stranger down."));
            yield return ("Who is Raffkin?", () => Say(person, "We don't say that name. He was the wisest of us, and he listened to something under the mountains until there was no room left in him for anything else."));
        }
        if (id == Raffkin)
        {
            yield return ("What do you want?", () => Say(person, "To wake my lord! He sleeps under the western front, walled in by the four, and they will not let him rise. Kill the rotten one, and the door opens. Kill one more, a true one, and the wall comes down. Oh, it'll be wonderful. Everything will burn green."));
            yield return (_forest.Told ? "Remind me where they stand." : "Where are the true guardians?", () =>
            {
                _forest.Told = true;
                foreach (var g in Northwood.Guardians.Where(x => !x.Corrupted)) _hud.Log($"{g.Name}: {Direction(person.GlobalPosition, (g.X, g.Z))}");
                Say(person, string.Join(" ", Northwood.Guardians.Where(g => !g.Corrupted).Select(g => $"{g.Name}, {Direction(person.GlobalPosition, (g.X, g.Z))}.")) + " And they don't go empty-handed to the grave. Oh no. They've been growing treasures in their hearts since before your city had walls.");
                _standing.Evil(5, "bargained with Raffkin");
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
        if (id == Druid && _forest.Turlin.Stage is TurlinStage.Clue or TurlinStage.Blamed && _forest.Turlin.Current is { } lost)
            yield return ($"Look at this: {lost.Token}.", () =>
            {
                _forest.Turlin.Doubt();
                Say(person, $"Guardians don't carry things off. And these marks aren't root. Four fingers and a thumb, and more hand than you've got chest. There's a smell on it too, sweet and rotten: a red fruit that only grows at the foot of the hill in the far north-west. Whoever hired you, stop trusting them. {lost.Victim} is {Direction(person.GlobalPosition, (Northwood.DenX, Northwood.DenZ))} of here, if anywhere.");
                _hud.Log($"The den: {Direction(person.GlobalPosition, (Northwood.DenX, Northwood.DenZ))} of Hollowheart.");
                Save();
            });
        if (id == Leader) yield return ("Who is the pirate with your face?", () => Say(person, _city.WukongDeaths == 0
            ? "My brother. Lu Wukong went down to the sea with a gourd of what grows at the foot of the Maw, to prove the world was bigger than this wood. He robs pirates and sings about it, and he stands in Soren's road, and he thinks that's a joke. I don't speak to him. He sends me pipe-weed."
            : $"My brother. He's died {(_city.WukongDeaths == 1 ? "once" : $"{_city.WukongDeaths} times")} now, that I've heard of. It's the gourd brings him back, and it takes a verse each time. The pipe-weed still comes. The notes don't, any more."));
        if (id == Druid) yield return ("How do I find my way through the wood?", () => Say(person, "Keep to the road and nothing older than a bear will trouble you. Leave it and the paths will turn you round: they're meant to. The guardians are at the ends of the small ones."));
        if (id == Sprout) yield return ("Who do your people pray to?", () => Say(person, string.Join(", ", Northwood.Guardians.Select(g => g.Name)) + ". We'd thank you not to kill them. It wouldn't feel like thanks."));
    }

    /// <summary>Whoever Turlin's paying this time, who has no idea what they're being paid for.</summary>
    IEnumerable<(string, Action)> TurlinTalk(Person person)
    {
        var t = _forest.Turlin;
        var slain = _city.GuardiansSlain;
        var trueOnes = Northwood.Guardians.Where(g => !g.Corrupted).Select(g => g.Id).ToList();
        if (t.Stage == TurlinStage.Fooled)
            yield return ("It's dead.", () =>
            {
                var knew = t.Knows;
                var pay = t.Settle();
                _player.Crowns += pay;
                Sound.Ui("coin");
                Say(person, knew
                    ? $"I was told you'd say that. {pay} crowns. A big fellow with a black mane and a tail left it, and a message. 'Again.' I don't know what that means. I don't think I want to."
                    : $"Is it? A big fellow with a black mane and a tail paid me to find someone who'd do it. I didn't ask why. He left {pay} crowns for you, and said to tell you the one you were looking for will walk home by morning. I'm... I'm sorry. Should I not have done that?");
                _standing.Evil(5, "took Turlin's gold for a guardian's death");
                Save();
            });
        if (t.Current is not { } c) yield break;
        if (t.Stage == TurlinStage.None)
            yield return ("You look worried.", () =>
            {
                t.Hire();
                var (x, z) = Northwood.ClueFor(c.Guardian);
                Say(person, $"{c.Victim}, {c.Who}. Went into the trees two nights ago and didn't come out. {TurlinBook.FindPay} crowns to whoever brings word. A gentleman put up the money; he's very concerned. Look {Direction(person.GlobalPosition, (x, z))}, where the small paths run out. Be careful. There are things in that wood that don't like us.");
                _hud.Log($"Find {c.Victim}: {Direction(person.GlobalPosition, (x, z))} of the camp.");
                Save();
            });
        else if (t.Stage == TurlinStage.Hired)
            yield return ("Tell me again where to look.", () => Say(person, $"{Direction(person.GlobalPosition, Northwood.ClueFor(c.Guardian))}, where the small paths run out. Please hurry."));
        else if (t.Stage == TurlinStage.Clue)
            yield return ($"I found {c.Token}.", () =>
            {
                t.Report();
                var name = TurlinBook.Blame(c, slain, trueOnes) is { } g ? Northwood.Guardians.First(x => x.Id == g).Name : "the wood";
                Say(person, $"Gods. No. That's {name}'s ground, and everyone says nothing else is that strong. So much for the Photosynthens' holy trees. It takes our people and we pray to it? The gentleman said if it came to this he'd give {(t.Knows ? TurlinBook.ImmortalPay : TurlinBook.BlamePay)} crowns to the hero who brought it down. For {c.Victim}.");
                Save();
            });
        else if (t.Stage == TurlinStage.Blamed)
            yield return ("Is there no other way?", () => Say(person, "It took them. What other way is there? Or ask that druid woman at Hollowheart, I suppose. She'll tell you it's all very complicated."));
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
        main.PlayerNode.WentDown += OnHeroDown;
        main.HudNode.ShowRival(main.ForestLog.Turlin.Sheet());
        main.PlayerNode.RivalEdge = main.ForestLog.Turlin.EdgeBonus;
        main.PlayerNode.Fit();
        main.PlayerNode.Shapes = () => main.ForestLog.Shapes;
        BuildVillage();
        var lair = new Vector3((float)Northwood.LairX, 0, (float)Northwood.LairZ);
        Buildings.Place(this, "mine_portal", lair with { Y = City.FloorAt(lair) }, 0.7f, 3);
        Grove();
    }

    /// <summary>The trees at the foot of the lair, hung with the spiked red fruit that grows nowhere else.</summary>
    void Grove()
    {
        var fruit = new StandardMaterial3D { AlbedoColor = new Color("#8a2a1c"), Roughness = 0.5f, EmissionEnabled = true, Emission = new Color("#a02010"), EmissionEnergyMultiplier = 0.5f };
        var (bark, leaf) = (Bark(), new StandardMaterial3D { AlbedoColor = new Color("#2e3a24"), Roughness = 0.95f });
        for (var i = 0; i < 5; i++)
        {
            var at = new Vector3((float)Northwood.GroveX + Mathf.Cos(i * 1.3f) * (4 + i * 3), 0, (float)Northwood.GroveZ + Mathf.Sin(i * 1.3f) * (4 + i * 3));
            at.Y = City.FloorAt(at);
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.5f, Height = 5 }, MaterialOverride = bark, Position = at + Vector3.Up * 2.4f });
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 2.6f, Height = 3.6f }, MaterialOverride = leaf, Position = at + Vector3.Up * 5.4f });
            for (var f = 0; f < 6; f++)
                AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 8, Rings = 5 }, MaterialOverride = fruit, Position = at + new Vector3(Mathf.Cos(f * 2.1f) * 2.2f, 4.2f + f % 3 * 0.5f, Mathf.Sin(f * 2.1f) * 2.2f) });
        }
    }

    public override void _ExitTree()
    {
        Enemy.Slain -= OnSlain;
        Enemy.Landed -= OnLanded;
        main.PlayerNode.WentDown -= OnHeroDown;
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
            Buildings.Door(door, seed == 1 && i == 1 ? "tavern" : "hollow", seed * 7 + i); // the village's one tavern, up on the deck of the Heartwood
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
        WatchRival(delta);
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
        TendTurlin(p);
    }

    const float Faint = 0.3f, Burning = 1;
    Enemy? _ape;
    Node3D? _aura;
    Person? _asker, _taken;
    Node3D? _clue;
    double _fruitReady, _fury, _leaving;
    string? _clueFor;
    int _askerCase = -1;
    Vector3 _away;
    bool _fought;

    Vector3 HeroAt => main.PlayerNode.GlobalPosition;
    string Hero => main.PlayerNode.Company;

    /// <summary>The rival when he's out, the ones he's used, and the clue he left where it can be found.</summary>
    void TendTurlin(Vector3 p)
    {
        var t = main.ForestLog.Turlin;
        TendCase(t, p);

        var den = new Vector3((float)Northwood.DenX, 0, (float)Northwood.DenZ);
        var flat = new Vector2(p.X - den.X, p.Z - den.Z).Length();
        if (t.Stage == TurlinStage.Hunting && t.Current is { } c)
        {
            if (_taken is null && flat < 200)
                _taken = main.MeetAt(new Npc(Main.TakenId, c.Victim, Sex.Other, 20, $"{c.Who}, held in Turlin's den", "northwood", "the Northwood", $"{c.Victim} is tied to a root with vine as thick as your wrist, and very glad to see you.", [], "shaken", "Behind you! He's behind you!", Northwood.DenX + 4, Northwood.DenZ + 2), "peasant");
            if (_ape is null && flat < 80)
            {
                Rise(den, t);
                main.HudNode.Toast("Turlin is here, in his den, and he has had time to get ready for you.", 7);
            }
            if (_ape is not null && flat < 46 && _leaving <= 0 && t.Returned()) Reckon(t);
            if (_ape is not null && flat < 34 && _ape.State == EnemyState.Idle && !main.HudNode.Talking) Fight();
        }
        else if (_taken is not null) { main.Dismiss(_taken); _taken = null; }

        if (_ape is not null && !IsInstanceValid(_ape)) Gone();
        if (_ape is { } a)
        {
            var off = new Vector2(p.X - a.GlobalPosition.X, p.Z - a.GlobalPosition.Z).Length();
            if (off > 130 && _leaving <= 0)
            {
                // The hero's run from it, or walked off and left him standing.
                if (_fought)
                {
                    t.Fled(Hero, main.GuildBook.PlaySeconds);
                    main.HudNode.Toast("Turlin doesn't follow. He stands and watches you go, and marks it down.", 6);
                }
                a.QueueFree();
                Gone();
                Told();
            }
            else if (a.State != EnemyState.Dead) TendFruit(t, a);
            return;
        }

        // He comes when he's ready: the first time at the edge of the wood, before the hero's ever reached the village.
        var hero = main.PlayerNode;
        if (hero.Downed || hero.InFight || main.HudNode.Talking || City.Below is not null || City.Afloat is not null) return;
        if (!t.Met)
        {
            if (Northwood.Inside(p.X, p.Z) && !Northwood.NearVillage(p.X, p.Z)) Meet(p);
        }
        else if (main.GuildBook.PlaySeconds >= t.NextAt && !main.InTownForTurlin(p) && !Northwood.NearVillage(p.X, p.Z) && GD.Randf() < 1f / 45) Meet(p);
    }

    void TendCase(TurlinBook t, Vector3 p)
    {
        var camp = new Vector3((float)Northwood.TurlinX, 0, (float)Northwood.TurlinZ);
        var about = t.Met && t.AtTheCamp || t.Stage == TurlinStage.Fooled;
        if (_asker is not null && (!about || _askerCase != t.Case)) { main.Dismiss(_asker); _asker = null; }
        if (about && _asker is null && t.Current is { } a && new Vector2(p.X - camp.X, p.Z - camp.Z).Length() < 200)
        {
            _askerCase = t.Case;
            _asker = main.MeetAt(new Npc(Main.TurlinId, a.Asker, Sex.Other, 45, a.AskerIs, "northwood", "the Northwood",
                $"{a.Asker} has the look of someone who's been paid to worry, and is good at it.",
                ["the forest", "trade"], "warm", "Oh, thank goodness. You look like someone who can walk in a straight line.", Northwood.TurlinX, Northwood.TurlinZ), "hunter");
        }
        var want = t.Stage == TurlinStage.Hired ? t.Current?.Guardian : null;
        if (want == _clueFor) return;
        _clueFor = want;
        _clue?.QueueFree();
        _clue = null;
        if (want is null) return;
        var (x, z) = Northwood.ClueFor(want);
        var at = new Vector3((float)x, 0, (float)z);
        at.Y = City.FloorAt(at);
        AddChild(_clue = new Node3D { Position = at });
        _clue.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.5f, Height = 0.5f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#6a2a1a") }, Position = Vector3.Up * 0.2f });
        _clue.AddChild(new OmniLight3D { Position = Vector3.Up * 1.4f, LightColor = new Color("#ffc080"), LightEnergy = 0.8f, OmniRange = 6 });
        _clue.AddChild(new Label3D { Text = "?", Position = Vector3.Up * 2, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 96, OutlineSize = 12, Modulate = new Color("#ffd27a") });
    }

    /// <summary>He's there, in whatever's left of him, at what's fair: the deviant system's numbers, held near the hero's level.</summary>
    void Rise(Vector3 at, TurlinBook t)
    {
        var form = t.Form;
        var e = Enemy.Create(Bestiary.Get(form.Kind), at with { Y = City.FloorAt(at) + 1 }, Northwood.DangerFor(t.LevelAgainst(main.PlayerNode.Hero.Level)));
        e.HeroesRival = true;
        e.Roam = 20;
        main.AddEnemy(e);
        e.MakeDeviant(t.Self.Id, new DeviantWorld(1).ModifiersFor(t.Self), null);
        e.Crown("Turlin", t.Self.Id);
        e.Scale = Vector3.One * form.Size;
        Demonic(e, form.Demon);
        (_ape, _fought, _fury, _leaving) = (e, false, 0, 0);
        _aura = null;
        if (t.Awoke) AuraAt(_aura = Aura(e), Faint);
    }

    /// <summary>What's coming through him: horns, and embers where his eyes should be, the further gone he is.</summary>
    static void Demonic(Enemy e, float demon)
    {
        if (demon <= 0) return;
        var high = (float)e.Kind.Height * 0.98f;
        var horn = new StandardMaterial3D { AlbedoColor = new Color("#14100e"), Roughness = 0.35f };
        var ember = new StandardMaterial3D { AlbedoColor = new Color("#ff3a10"), EmissionEnabled = true, Emission = new Color("#ff3a10"), EmissionEnergyMultiplier = 1 + 3 * demon };
        foreach (var side in new[] { -1, 1 })
        {
            e.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = high * 0.035f, Height = high * 0.32f * demon, RadialSegments = 8, Rings = 1 }, MaterialOverride = horn,
                Position = new Vector3(side * high * 0.08f, high * (0.96f + 0.1f * demon), 0.05f), Rotation = new Vector3(0.25f, 0, -side * 0.45f),
            });
            e.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = high * 0.022f, Height = high * 0.044f }, MaterialOverride = ember, Position = new Vector3(side * high * 0.05f, high * 0.9f, high * 0.1f) });
        }
    }

    /// <summary>
    /// The power showing: a red aura round him, the same as the fruit's, that lies outside his body and leaves its colours
    /// alone. There's none at all until the first time it's woken.
    /// </summary>
    static Node3D Aura(Enemy e)
    {
        var high = (float)e.Kind.Height * (e.Kind.Id == "giant_ape" ? 1 : 1.1f);
        var aura = new Node3D { Name = "Aura" };
        var flame = new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 64, Height = 64,
            Gradient = new Gradient { Offsets = [0, 1], Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)] },
        };
        aura.AddChild(new CpuParticles3D
        {
            Amount = 110, Lifetime = 0.9, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(high * 0.2f, high * 0.42f, high * 0.2f), Position = Vector3.Up * high * 0.46f,
            Direction = Vector3.Up, Spread = 8, Gravity = Vector3.Up * high * 0.6f, InitialVelocityMin = high * 0.15f, InitialVelocityMax = high * 0.4f, Mesh = new QuadMesh { Size = Vector2.One * high * 0.16f },
            ScaleAmountMin = 0.5f, ScaleAmountMax = 1.4f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                AlbedoColor = new Color(1, 0.16f, 0.08f, 0.16f), AlbedoTexture = flame, NoDepthTest = false,
            },
        });
        e.AddChild(aura);
        return aura;
    }

    static void AuraAt(Node3D? aura, float strength)
    {
        if (aura?.GetChild<CpuParticles3D>(0) is not { } flames) return;
        if (flames.MaterialOverride is StandardMaterial3D m) m.AlbedoColor = m.AlbedoColor with { A = 0.3f * strength };
    }

    void Fight()
    {
        if (_ape is not { } e || !IsInstanceValid(e)) return;
        _fought = true;
        e.Provoke(main.PlayerNode);
    }

    void Gone() => (_ape, _fought, _fury, _leaving) = (null, false, 0, 0);

    void Told()
    {
        main.HudNode.ShowRival(main.ForestLog.Turlin.Sheet());
        main.PlayerNode.RivalEdge = main.ForestLog.Turlin.EdgeBonus;
        main.Save();
    }

    /// <summary>He steps out, says what he's come to say, and you can try to talk your way past. It doesn't often work.</summary>
    void Meet(Vector3 p)
    {
        var t = main.ForestLog.Turlin;
        var hero = main.PlayerNode;
        var ahead = p + (-hero.GlobalBasis.Z with { Y = 0 }).Normalized() * 11;
        Rise(ahead, t);
        var first = !t.Met;
        t.Met = true;
        if (t.Returned()) { Reckon(t); return; }
        main.HudNode.Talk("Turlin", t.Form.Hail,
            ("Fight.", () => { main.HudNode.EndTalk(); Fight(); }),
            ("\"There's no need for this.\"", () =>
            {
                main.HudNode.EndTalk();
                if (GD.Randf() < (hero.Class.Calling == Calling.Bard ? TurlinBook.BardTalkChance : TurlinBook.TalkChance))
                {
                    main.HudNode.Toast(first ? "He looks at you for a moment, and laughs. \"Not bad. You've talked your way out of more than you know. Another day.\" He's gone between one tree and the next." : "He weighs it, and something decides him. He backs into the trees without turning round. Not today.", 8);
                    t.NextAt = main.GuildBook.PlaySeconds + TurlinBook.TalkedOut;
                    _ape?.QueueFree();
                    Gone();
                    main.Save();
                }
                else
                {
                    main.HudNode.Toast("\"Good try,\" says Turlin, and he's already moving.", 5);
                    Fight();
                }
            }));
    }

    /// <summary>The hero he killed walks up to him, whole, and he works out what that means. It doesn't occur to him to wonder about himself.</summary>
    void Reckon(TurlinBook t)
    {
        var opts = new List<(string, Action)> { ("\"Let's see who gets up this time.\"", () => { main.HudNode.EndTalk(); Fight(); }) };
        if (t.Current is not null && t.Stage is TurlinStage.None or TurlinStage.Hunting)
            opts.Add(("\"Name your price.\"", () =>
            {
                main.HudNode.EndTalk();
                t.Stage = TurlinStage.Blamed;
                main.Standing.Evil(10, "took Turlin's offer");
                _ape?.QueueFree();
                Gone();
                main.HudNode.Toast($"Turlin wants a true guardian dead, and there's someone at the logging camp who'll pay you for it.", 8);
                main.Save();
            }));
        main.HudNode.Talk("Turlin", $"He stops dead, well out of reach, and for the first time he doesn't look pleased. \"I broke your neck. I heard it go. I stood there and watched you stop.\" He circles you, keeping his distance, and looks at your ring for a long time. \"Nobody comes back from that,\" he says, and doesn't seem to hear himself. \"A hero who can die at a guardian's feet a hundred times, and walk back a hundred and one. Do you understand what you're worth? {TurlinBook.ImmortalPay} crowns a head. To start.\"", [.. opts]);
    }

    /// <summary>
    /// When the fight turns he eats. For a few seconds he's ten times what he was and nothing can stand near him; then the
    /// stamina's gone and he stands there blowing, and that's the moment to hit him. It's two minutes before he can again.
    /// </summary>
    void TendFruit(TurlinBook t, Enemy ape)
    {
        if (_fury > 0)
        {
            if ((_fury -= 1) > 0) return;
            FruitDone(ape);
            ape.Stunned = TurlinBook.FruitWinded;
            main.HudNode.Toast("The fruit burns out of him all at once. Turlin stands swaying, blowing like a horse. Now!", 6);
            return;
        }
        if (!t.CanEat || !_fought || _leaving > 0 || main.Clock < _fruitReady || ape.Stats.Health > ape.Stats.MaxHealth * TurlinBook.FruitBelow) return;
        ape.Stats.DamageMult *= TurlinBook.FruitMight;
        ape.Stats.SpeedMult *= TurlinBook.FruitSpeed;
        ape.Stunned = 2; // the bite: all the warning there is
        t.Awoke = true;
        AuraAt(_aura ??= Aura(ape), Burning);
        _fury = TurlinBook.FruitSeconds + 2;
        main.HudNode.Toast("Turlin bites into a spiked red fruit, and his eyes go white. GET AWAY FROM HIM!", 5);
    }

    void FruitDone(Enemy ape)
    {
        AuraAt(_aura, Faint);
        ape.Stats.DamageMult /= TurlinBook.FruitMight;
        ape.Stats.SpeedMult /= TurlinBook.FruitSpeed;
        _fruitReady = main.Clock + TurlinBook.FruitCooldown;
        _fury = 0;
    }

    /// <summary>Every moment of the fight: he isn't reckless, and at a quarter of his blood he changes shape and takes to the air.</summary>
    void WatchRival(double delta)
    {
        if (_ape is not { } e || !IsInstanceValid(e) || e.State == EnemyState.Dead) return;
        if (_leaving > 0)
        {
            e.GlobalPosition += (_away * 13 + Vector3.Up * 5) * (float)delta;
            if ((_leaving -= delta) > 0) return;
            var t = main.ForestLog.Turlin;
            t.HeEscaped(Hero, main.Economy.Day, main.GuildBook.PlaySeconds);
            var who = Free(t);
            main.HudNode.Toast($"Turlin dwindles into the sky and he's gone. You beat him, and something of it stays with you: you're stronger for it.{who}", 9);
            e.QueueFree();
            Gone();
            Told();
            return;
        }
        if (!_fought || e.Stats.Health > e.Stats.MaxHealth * TurlinBook.FleeAt) return;
        if (_fury > 0) FruitDone(e);
        _leaving = TurlinBook.FlightSeconds;
        var away = e.GlobalPosition - HeroAt;
        _away = new Vector3(away.X, 0, away.Z).Normalized();
        e.Carried = true;
        e.Reshape("winged_ape");
        Demonic(e, main.ForestLog.Turlin.Form.Demon);
        main.HudNode.Toast("Turlin has no intention of dying here. His back splits, wings tear out of it, and he's going. Bring him down now, or he's gone!", 7);
    }

    /// <summary>If this was in his den, the one he took walks free: what to say about it.</summary>
    string Free(TurlinBook t)
    {
        var who = t.Current?.Victim ?? "the prisoner";
        if (!t.Won()) return "";
        main.PlayerNode.Crowns += TurlinBook.FindPay;
        main.Standing.Good(3, $"brought {who} out of Turlin's den");
        return $" {who} is free, and the camp sends {TurlinBook.FindPay} crowns of thanks.";
    }

    void OnHeroDown()
    {
        if (_ape is not { } e || !IsInstanceValid(e) || e.State == EnemyState.Dead || !_fought) return;
        if (e.GlobalPosition.DistanceTo(HeroAt) > 60) return;
        var t = main.ForestLog.Turlin;
        var said = t.Form.Won;
        t.HeWon(Hero, main.Economy.Day, main.GuildBook.PlaySeconds);
        main.HudNode.Toast($"{said} Turlin is stronger for it.", 11);
        if (_fury > 0) FruitDone(e);
        GetTree().CreateTimer(4).Timeout += () => { if (IsInstanceValid(e)) e.QueueFree(); };
        Gone();
        Told();
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
            // Asleep in the lair until the barrier breaks. After that it's wherever the wood's being cut: it hunts anyone
            // who's ever taken an axe to a tree, and it's in whichever town is burning the most timber.
            var town = city.DragonAt is { } at ? main.World.Find(at) : null;
            var seat = town is null ? lair : new Vector3((float)town.X, 0, (float)town.Z);
            var hunted = city.DragonFree && main.ForestLog.TreesCut > 0;
            var near = hunted ? p + new Vector3(30, 0, 30) : seat;
            if (!hunted && new Vector2(p.X - seat.X, p.Z - seat.Z).Length() > 240) return;
            _dragon = Enemy.Create(Bestiary.Get("dragon_green"), near with { Y = City.FloorAt(near) + 2 }, Northwood.DangerFor(14), size: DragonSize.Giant);
            _dragon.Stats.Name = Northwood.DragonName;
            _dragon.Stats.Grow(6);
            _dragon.RefreshPlate();
            main.AddEnemy(_dragon);
        }
        if (city.DragonFree && _dragon.State == EnemyState.Idle && (main.ForestLog.TreesCut > 0 || _dragon.GlobalPosition.DistanceTo(p) < 90)) _dragon.Provoke(main.PlayerNode);
    }

    void OnSlain(Enemy victim, Enemy? killer, bool byCompany)
    {
        if (byCompany && main.PlayerNode.Class.Id == "Shapeshifter" && main.ForestLog.Study(victim.Kind.Id) is { } shape)
            main.HudNode.Toast($"You've fought enough of them to know them from the inside. New shape: {shape}.", 8);
        if (victim == _ape)
        {
            var t = main.ForestLog.Turlin;
            var said = t.Form.Lost;
            t.HeLost(Hero, victim.LastBlow, main.Economy.Day, main.GuildBook.PlaySeconds);
            main.HudNode.Toast($"{said}{Free(t)} You're stronger for it.", 12);
            main.HudNode.Log("Turlin is dead. You've thought so before.");
            Gone();
            Told();
            return;
        }
        if (victim == _dragon)
        {
            main.ForestLog.DragonSlayer = true;
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
            main.ForestLog.Turlin.Slew();
            main.Take([(g.Loot, 1)]);
            main.PlayerNode.Crowns += ForestBook.Hoard;
            main.Standing.Evil(25, $"slew {g.Name}");
            main.HudNode.Toast($"{g.Name} comes down like a falling hill. In the heart of it: {Items.Get(g.Loot)!.Name}, and {ForestBook.Hoard} crowns of old gold. Every bird in the wood stops singing.", 11);
        }
        main.DoCity(CityAction.Guardian(id, g.Corrupted));
        main.Save();
    }

    public Enemy? RivalBody => _ape;

    /// <summary>Turlin as he'd be after so many deaths, stood somewhere to be looked at: for the screenshots.</summary>
    public void PoseAura(Enemy e) => AuraAt(Aura(e), Burning);

    public Enemy PoseRival(int deaths, Vector3 at)
    {
        var t = main.ForestLog.Turlin;
        (t.Deaths, _ape) = (deaths, null);
        Rise(at, t);
        var e = _ape!;
        _ape = null;
        t.Deaths = 0;
        e.Carried = true;
        e.GlobalPosition = at with { Y = City.FloorAt(at) };
        e.HidePlate();
        return e;
    }
    public void MeetForTest() => Meet(main.PlayerNode.GlobalPosition);

    /// <summary>The clue, when the hero's standing over it.</summary>
    public bool NearClue(Vector3 p) => _clue is not null && p.DistanceTo(_clue.GlobalPosition) < 4;

    /// <summary>The guardians' ground, with the heroes who've come to it: for the self-test.</summary>
    public void TendForTest() => _tick = 0;
}
