using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The Velvet Knives' job on the old storehouse in the Warrens. Behind an iron door at the back of it is the Magi's
/// strongroom: Korrin's tank, and on a pedestal by it the Starfall Diamond, worth five thousand crowns. Vesper Quill
/// wants it and gives whoever takes the job the vault's combination; anyone else has to pick the lock. Bring her the
/// diamond and she pays five hundred. Or keep it: sell it, or break it, once, and the sky comes down on whatever you
/// point it at, the way it does for Pride, in silver and violet.
/// </summary>
public partial class Main
{
    public const string Diamond = "starfall_diamond";
    const string Vesper = "guild/vesper";
    const int HeistPay = 500, PickAt = 4;

    /// <summary>Where the job stands: 0 not taken, 1 taken, 2 done (the diamond handed over).</summary>
    int Heist { get => _pursuits.Counters.GetValueOrDefault("heist"); set => _pursuits.Counters["heist"] = value; }
    bool VaultOpen { get => _pursuits.Counters.GetValueOrDefault("vault") > 0; set => _pursuits.Counters["vault"] = value ? 1 : 0; }
    bool DiamondTaken { get => _pursuits.Counters.GetValueOrDefault("diamond") > 0; set => _pursuits.Counters["diamond"] = value ? 1 : 0; }

    /// <summary>The vault's door, in its wall across the old storehouse's room; and the pedestal the diamond sits on behind it.</summary>
    static Vector3 VaultDoor => Indoors.StageAt + new Vector3(0, 0, Indoors.VaultWall);
    public static Vector3 Pedestal => Indoors.StageAt + new Vector3(-2.6f, 0, -3.6f);

    Node3D? _diamondShown;

    /// <summary>The old storehouse's room is built: the vault as the hero left it, and the diamond on its pedestal if it's still there.</summary>
    void FurnishVault(Indoors room)
    {
        room.OpenVault(VaultOpen);
        if (DiamondTaken) return;
        var gem = new Node3D { Position = Pedestal + Vector3.Up * 1.25f };
        gem.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.36f, RadialSegments = 4, Rings = 2 }, // cut, eight-faced
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.8f, 1f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Metallic = 0.6f, Roughness = 0.05f, EmissionEnabled = true, Emission = new Color("#b890ff"), EmissionEnergyMultiplier = 1.6f },
        });
        gem.AddChild(Placed(Lairfolk.Light("#c8a8ff", 1.2f, 4, flicker: 0.1f), Vector3.Zero));
        gem.AddChild(Lairfolk.Motes(new Color(0.85f, 0.8f, 1f, 0.8f), 0.03f, 16, new Vector3(0.3f, 0.3f, 0.3f), 0.3f));
        var spin = gem.CreateTween().SetLoops();
        spin.TweenProperty(gem, "rotation:y", Mathf.Tau, 6).AsRelative();
        room.AddChild(gem);
        _diamondShown = gem;
    }

    (string Verb, Action Act)? VaultAction(Vector3 pos)
    {
        if (_indoors?.Kind != "storehouse") return null;
        if (!VaultOpen && pos.DistanceTo((VaultDoor + Vector3.Back * 0.8f) with { Y = pos.Y }) < 2)
            return Heist == 1 ? ("open the vault (the Knives' combination)", () => UnlockVault("You turn the dials the way Vesper told you. Something heavy inside the door gives, and it swings open on oiled hinges."))
                : _player.Pack.Level(Job.Thieving) >= PickAt ? ("pick the vault's lock", PickVault)
                : ("try the vault door", () => _hud.Toast($"An iron door with a dial lock and the Magi's seal on it. You'd need the combination, or a far better hand at locks than yours (thieving {PickAt}).", 5));
        if (VaultOpen && !DiamondTaken && pos.DistanceTo(Pedestal with { Y = pos.Y }) < 1.8f)
            return ("take the Starfall Diamond", TakeDiamond);
        return null;
    }

    void UnlockVault(string how)
    {
        VaultOpen = true;
        _indoors?.OpenVault(true);
        Sound.Ui("open");
        _hud.Toast(how, 7);
        Save();
    }

    void PickVault()
    {
        var level = _player.Pack.Level(Job.Thieving);
        var chance = Math.Min(0.9, 0.25 + 0.05 * level + (_player.Class.Id == "Thief" ? 0.25 : 0));
        _player.Pack.Practise(Job.Thieving, 4);
        if (_lootRng.Chance(chance)) { UnlockVault("Tumbler by tumbler, the dials give, and the iron door swings open."); return; }
        Sound.Ui("error");
        _hud.Toast("A tumbler slips back into place. Nothing yet. Try again.", 3);
    }

    void TakeDiamond()
    {
        DiamondTaken = true;
        _player.Pack.Add(Diamond);
        if (_diamondShown is not null && IsInstanceValid(_diamondShown)) _diamondShown.QueueFree();
        _diamondShown = null;
        Sound.Ui("pickup");
        _hud.Toast("You lift the Starfall Diamond off its pedestal. It's warm, and heavier than a stone that size should be, and something turns slowly inside it like weather. In the tank beside you, nobody stirs.", 9);
        Save();
    }

    /// <summary>Vesper Quill's job, and what she says when you come back.</summary>
    IEnumerable<(string, Action)> HeistTalk(Person person)
    {
        if (person.Npc.Id != Vesper) yield break;
        if (Heist == 0)
            yield return ("Any work for a light pair of hands?", () => _hud.Talk("Vesper Quill",
                "\"There's a storehouse at the back of the Warrens, door to the wall, that nobody goes into. Inside it the Magi built themselves a strongroom, and in the strongroom there's a diamond the size of a hen's egg. Five thousand crowns to the right buyer, and I'm the right buyer. " +
                $"Bring it to me and you'll have {HeistPay}, and the Knives' thanks, which is worth more. I'll give you the combination. Don't touch anything else in there.\"",
                ("Take the job.", () => { Heist = 1; Save(); Say(person, "\"Good. The combination's three turns left, two right, and the seal's for show. Mind what's in the tank. It's been sleeping a long time.\""); }),
                ("Not today.", () => Say(person, "\"Then forget I asked. You will, if you're wise.\""))));
        else if (Heist == 1 && _player.Pack.Count(Diamond) > 0)
            yield return ("I have your diamond.", () =>
            {
                _player.Pack.Take(Diamond);
                _player.Crowns += HeistPay;
                Heist = 2;
                Sound.Ui("coin");
                Save();
                Say(person, $"She holds it up to the light, and for once her face gives something away. \"Well. You're a Knife now, whatever they call you.\" {HeistPay} crowns, counted out.");
            });
        else if (Heist == 1)
            yield return ("About the storehouse...", () => Say(person, "\"Back of the Warrens, door to the wall. Three left, two right. I'm not getting any younger, and neither's the thing in the tank.\""));
    }

    /// <summary>The diamond's spell, from the pack: the sky called down on whatever's in front of the hero. Once.</summary>
    IEnumerable<(string, Action)> DiamondPack()
    {
        if (_player.Pack.Count(Diamond) > 0)
            yield return ("Break the Starfall Diamond (its spell brings the sky down, once)", () => { _hud.EndTalk(); BreakDiamond(); });
    }

    void BreakDiamond(Vector3? at = null)
    {
        if (!_player.Pack.Take(Diamond)) return;
        var p = _player.GlobalPosition;
        var ahead = (-_player.GlobalBasis.Z with { Y = 0 }).Normalized();
        var target = at ?? Enemies().Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead && !e.Friendly && e.Bond is null && !e.Allied && e.GlobalPosition.DistanceTo(p) < 40)
            .OrderBy(e => e.GlobalPosition.DistanceTo(p + ahead * 10)).Select(e => (Vector3?)e.GlobalPosition).FirstOrDefault() ?? p + ahead * 12;
        target.Y = City.FloorAt(target);
        _hud.Toast("You close your fist on the diamond and it breaks like ice. Light runs out of it and up, and the clouds over you turn silver.", 6);
        CallMeteor(target, new Color("#c8a8ff"), new Color("#d8d8ec"), 1.6f, DamageKind.Shock, () =>
        {
            foreach (var e in Enemies().Where(e => IsInstanceValid(e) && e.State != EnemyState.Dead && !e.Friendly && e.Bond is null && !e.Allied && e.GlobalPosition.DistanceTo(target) < 6).ToList())
                e.TakeHit(300 + 25 * _player.Hero.Level, DamageKind.Shock, AttackKind.Spell, false, _player);
        });
        Save();
    }

    /// <summary>On the tracker: the Knives' job, while it's open.</summary>
    IEnumerable<string> HeistLines(Vector3 pos)
    {
        if (Heist == 1)
            yield return _player.Pack.Count(Diamond) > 0
                ? "The Knives' job: bring Vesper Quill the Starfall Diamond, at the old tannery yard"
                : $"The Knives' job: the diamond in the vault of the old storehouse at the back of the Warrens ({Direction(pos, (City.OldStorehouse.X, City.OldStorehouse.Z))})";
    }

    // ---- for the self-test -------------------------------------------------------------------------------------------

    public (string Verb, Action Act)? VaultActionForTest(Vector3 at) => VaultAction(at);
    public int HeistForTest => Heist;
    public bool VaultOpenForTest => VaultOpen;
    public void OpenVaultForTest() => UnlockVault("(test)");
    public void BreakDiamondForTest(Vector3 at) => BreakDiamond(at);
    public IEnumerable<string> HeistLinesForTest(Vector3 pos) => HeistLines(pos);
}
