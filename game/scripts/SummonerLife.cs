using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The Summoner's road (see <see cref="Summons"/>). Braz Algún keeps the Binding Stones west of the walls, below the
/// Eyrie's hill, with Bubbles, her corrupted shadow dragon, at her side, and her book of flesh at her belt. Nobody tells a
/// new hero about her. At level 8 she'll mark a hero's ring and send them after the great ones of the road, one at a time,
/// each in a lair of its own out in the wilds; each beaten with the hero there is bound to their ring, and G calls it.
/// The last of them is Draxon, Bubbles' son, the King of Beasts: the only dragon a summoner ever calls.
/// </summary>
public partial class Main
{
    const string BrazId = "summoner/braz";
    readonly Dictionary<string, Enemy> _greatOnes = [];
    readonly Dictionary<Enemy, double> _greatMoves = [];
    readonly HashSet<Enemy> _draxonSpoke = [], _draxonStung = [];
    double _summonerTick;

    static Vector3 Stones => new((float)Summons.StonesX, 0, (float)Summons.StonesZ);

    /// <summary>Braz, Bubbles beside her, and her ring of standing stones.</summary>
    void AddBraz()
    {
        Townsfolk(new Npc(BrazId, "Braz Algún", Sex.Female, 23, "summoner, keeper of the Binding Stones", "city:tristinwold", "Tristinwold",
            "Braz Algún keeps the Binding Stones west of the walls. She carries a book bound in something that isn't leather, and a black dragon of the Rift follows at her heel like a dog. She calls it Bubbles. It does whatever she says.",
            ["the great beasts", "her book", "Bubbles"], "quiet", "Don't mind Bubbles. She only eats what I tell her to.", Summons.StonesX, Summons.StonesZ), "braz");
        var ring = new Node3D { Name = "BindingStones" };
        AddChild(ring);
        var stone = new StandardMaterial3D { AlbedoColor = new Color("#5a5a58"), Roughness = 1 };
        var rune = new StandardMaterial3D { AlbedoColor = new Color("#5aff6a"), EmissionEnabled = true, Emission = new Color("#5aff6a"), EmissionEnergyMultiplier = 1.4f };
        for (var i = 0; i < 7; i++)
        {
            var a = Mathf.Tau * i / 7 + 0.3f;
            var at = Stones + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7.5f;
            var tall = 2.6f + (i * 37 % 10) * 0.08f;
            var s = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, tall, 0.55f) }, MaterialOverride = stone };
            ring.AddChild(s);
            s.GlobalPosition = at with { Y = City.FloorAt(at) + tall / 2 - 0.2f };
            s.LookAt(Stones with { Y = s.GlobalPosition.Y }, Vector3.Up);
            s.RotateObjectLocal(Vector3.Forward, (i % 3 - 1) * 0.06f); // leaning, as old stones do
            var glyph = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.5f, 0.02f) }, MaterialOverride = rune, Position = new Vector3(0, 0.2f, -0.29f) };
            s.AddChild(glyph);
        }
        var bubbles = Body.For("bubbles");
        ring.AddChild(bubbles);
        var beside = Stones + new Vector3(3.2f, 0, 2.4f);
        bubbles.GlobalPosition = beside with { Y = City.FloorAt(beside) };
        bubbles.LookAt(new Vector3(0, bubbles.GlobalPosition.Y, 0), Vector3.Up, true); // watching the city
        bubbles.AddChild(new Label3D { Text = "Bubbles", Position = Vector3.Up * 3.6f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 30, OutlineSize = 8, PixelSize = 0.008f, Modulate = new Color("#b8f0b8") });
        _player.BeastStruck += MendCompany;
        _player.BeastFell += BeastLost;
    }

    /// <summary>A called beast struck down: torn out of the ring, and that step of the road to walk again.</summary>
    void BeastLost(Summon s)
    {
        if (!Summons.Lose(_player.Hero, s.Id)) return;
        Sound.Ui("error");
        _hud.Toast($"{s.Name} is struck down, and torn out of your ring. It's gone. Go back to Braz Algún: you'll have to find it and beat it again.", 12);
        _hud.Log($"{s.Name} was killed, and is no longer bound to you.");
        Save();
    }

    /// <summary>Lumira's dawn and Aegrath's guard reach the whole company, not only the summoner.</summary>
    void MendCompany(Summon s)
    {
        foreach (var c in CompanionBodies.Where(c => c.State != EnemyState.Dead && c.GlobalPosition.DistanceTo(_player.GlobalPosition) < 25))
        {
            if (s.Move == SummonMove.Dawn) { c.Stats.Heal(c.Stats.MaxHealth * 0.2); Effects.Rise(c, DamageKind.Sun); }
            if (s.Move == SummonMove.Bulwark) Effects.Rise(c, DamageKind.Physical);
        }
    }

    // ---- Braz ----

    IEnumerable<(string, Action)> Braz(Person person)
    {
        var hero = _player.Hero;
        yield return ("Who are you? And what is that?", () => Say(person,
            "Braz Algún. And that is Bubbles. The Rift took her, and I took her back: she's bound to me, and she does what I say. " +
            "This book is how. Every great beast I've beaten is written in it, in its own skin. A summoner beats a great beast with their own hands, and its spirit catches on their ring. Then they can call it."));
        if (hero.Summoner == SummonerStep.None)
        {
            yield return ("Teach me to summon.", () =>
            {
                if (!Summons.MayBegin(hero)) { Say(person, $"You'd be eaten. Come back when you've lived a while out there (level {Summons.MinLevel})."); return; }
                Summons.Begin(hero);
                var t = Summons.Send(hero)!;
                Save();
                _hud.Toast($"New quest: the Summoner's road. Braz has marked your ring. Beat {Summons.Get(t.Id)!.Name} at {t.Lair} (recommended level {t.Recommended}).", 9);
                _hud.Talk(Who(person.Npc), $"Give me your hand. (She opens the book, and the page is warm.) There. Your ring's marked. {t.Send} It's {Direction(_player.GlobalPosition, LairAt(t))}. {Ready(t)}",
                    ("I'll find it.", () => Say(person, "Bubbles says good luck. She doesn't, but I'll say it for her.")));
            });
            yield break;
        }
        if (Summons.TrialOf(hero.Seeking) is { } seeking)
            yield return ($"Where is {Summons.Get(seeking.Id)!.Name} again?", () => Say(person, $"At {seeking.Lair}, {Direction(_player.GlobalPosition, LairAt(seeking))}. It hasn't moved. They never do."));
        if (hero.Summoner == SummonerStep.Marked && hero.Bound.Count > 0)
            yield return ($"I beat {Summons.Get(hero.Bound[0])!.Name}. It's in my ring.", () =>
            {
                Summons.Name(hero);
                Save();
                _hud.Toast($"You are a Summoner. Press G to call {Summons.Get(hero.Calls)!.Name} (half your mana; your ring needs {Summons.Cooldown:0} seconds after).", 12);
                Say(person, "So it is. (She writes in the book; the page takes it like a wound.) You're a summoner now. Call it when you need it. It'll come.");
            });
        if (Summons.ReadyFor(hero) is { } next && hero.Summoner == SummonerStep.Summoner)
            yield return ("I'm ready for the next one.", () =>
            {
                Summons.Send(hero);
                Save();
                _hud.Toast($"The Summoner's road, step {next.Step}: beat {Summons.Get(next.Id)!.Name} at {next.Lair} (recommended level {next.Recommended}).", 9);
                _hud.Talk(Who(person.Npc), $"{next.Send} It's {Direction(_player.GlobalPosition, LairAt(next))}. {Ready(next)}", ("I'll go.", () => Say(person, "Don't die. Well. You know what I mean.")));
            });
        if (hero.Summoner == SummonerStep.Summoner && hero.Bound.Count > 1)
            yield return ("Let me choose who answers my ring.", ChooseSummon);
        if (hero.Summoner == SummonerStep.Summoner && Summons.NextTrial(hero) is null)
            yield return ("Is there anything left to bind?", () => Say(person, "Not that I know of. You've Draxon in your ring. Bubbles won't look at you now. I think she's proud."));
    }

    /// <summary>Braz's word on whether they're ready for it.</summary>
    string Ready(Trial t) => _player.Hero.Level >= t.Recommended
        ? "You're ready for it, I think."
        : $"And don't go yet. Not before level {t.Recommended}, if you want to come back. Bubbles won't fetch what's left of you.";

    /// <summary>Which bound beast G calls.</summary>
    void ChooseSummon()
    {
        var hero = _player.Hero;
        var options = hero.Bound.Select(Summons.Get).OfType<Summon>().Select(s => ($"{(hero.Calls == s.Id ? "(called now) " : "")}{s.FullName}: {s.Text}", (Action)(() =>
        {
            Summons.Choose(hero, s.Id);
            Save();
            _hud.EndTalk();
            _hud.Toast($"G calls {s.Name} now.", 4);
        }))).ToList();
        options.Add(("Leave it.", _hud.EndTalk));
        _hud.Talk("Your ring", "Who answers when you call?", [.. options]);
    }

    /// <summary>G: the bound beast answers.</summary>
    void CallSummon()
    {
        var hero = _player.Hero;
        if (Summons.Callable(hero) is not { } s)
        {
            if (hero.Summoner != SummonerStep.None) _hud.Toast(hero.Bound.Count == 0 ? "Nothing in your ring yet." : "Show Braz what you've bound first.", 4);
            return;
        }
        if (_player.CantSummon(s) is { } why) { Sound.Ui("error"); _hud.Toast(why, 3); return; }
        _player.CallBeast(s, Summons.Blow(s, hero.Level));
    }

    (double X, double Z) LairAt(Trial t) => Summons.LairOf(_world, t);

    // ---- the great ones, in their lairs ----

    /// <summary>The great one the hero's been sent after is waiting in its lair when they come near; gone again when they leave.</summary>
    void TendSummoner(double delta)
    {
        if ((_summonerTick -= delta) > 0) return;
        _summonerTick = 1;
        var pos = _player.GlobalPosition;
        foreach (var (id, e) in _greatOnes.ToList())
            if (!IsInstanceValid(e) || e.State == EnemyState.Dead && e.GlobalPosition.DistanceTo(pos) > 200 || Summons.TrialOf(id) is { } t && new Vector2((float)LairAt(t).X - pos.X, (float)LairAt(t).Z - pos.Z).Length() > 600)
            {
                if (IsInstanceValid(e) && e.State != EnemyState.Dead) e.QueueFree();
                _greatOnes.Remove(id);
            }
        if (Summons.TrialOf(_player.Hero.Seeking) is not { } seeking || _greatOnes.ContainsKey(seeking.Id)) return;
        var (lx, lz) = LairAt(seeking);
        if (new Vector2((float)lx - pos.X, (float)lz - pos.Z).Length() > 300) return;
        _greatOnes[seeking.Id] = Rouse(seeking);
    }

    /// <summary>A great one of the road in its lair, its name, its strength, and its own ways of fighting.</summary>
    Enemy Rouse(Trial t)
    {
        var s = Summons.Get(t.Id)!;
        var (x, z) = LairAt(t);
        var at = new Vector3((float)x, 0, (float)z);
        TerrainNode.EnsureAround(at, 1);
        var e = Enemy.Create(Bestiary.Get(t.Kind), at with { Y = City.FloorAt(at) + 0.5f }, size: t.Id == Summons.Draxon ? DragonSize.Giant : DragonSize.Normal, look: t.Id);
        e.Stats.Grow(t.Health / e.Stats.MaxHealth);
        e.Stats.Name = s.FullName;
        e.Think = GreatOneMoves;
        e.RefreshPlate();
        AddEnemy(e);
        _hud.Toast($"{s.Name} is here, at {t.Lair}.", 5);
        return e;
    }

    /// <summary>Each great one's own way of fighting, every few seconds while it fights.</summary>
    void GreatOneMoves(Enemy e, double delta)
    {
        var id = _greatOnes.FirstOrDefault(kv => kv.Value == e).Key;
        if (id is null || _player.Downed) return;
        if (id == Summons.Draxon && _draxonSpoke.Add(e)) DraxonRises(e);
        if (id == Summons.Draxon && e.Stats.Health < e.Stats.MaxHealth / 2 && _draxonStung.Add(e)) _hud.Toast("Draxon: \"You hit like my mother. Good. I'd not bow to anything less.\"", 6);
        var wait = _greatMoves.GetValueOrDefault(e, 3) - delta;
        if (wait > 0) { _greatMoves[e] = wait; return; }
        var me = e.GlobalPosition;
        var you = _player.GlobalPosition;
        var near = me.DistanceTo(you);
        void Hit(double dmg, DamageKind kind) => _player.TakeHit(dmg, kind, AttackKind.Spell, false, e);
        switch (id)
        {
            case Summons.Orrogh: // a stamp that splits the ground round it
                _greatMoves[e] = 7;
                Effects.Burst(this, me + Vector3.Up * 0.3f, DamageKind.Physical, 8);
                if (near < 9) Hit(32, DamageKind.Physical);
                break;
            case Summons.Lumira: // she mends herself, and stings you with light
                _greatMoves[e] = 6;
                e.Stats.Heal(e.Stats.MaxHealth * 0.05);
                Effects.Rise(e, DamageKind.Sun);
                if (near < 22) { Effects.Bolt(this, me + Vector3.Up * 4, you + Vector3.Up, DamageKind.Sun, 0.3f); Hit(24, DamageKind.Sun); }
                break;
            case Summons.Quvirn: // lightning where you stand: move, or it finds you
            case Summons.Shicene: // ice falling from her wings where you stand
                {
                    _greatMoves[e] = id == Summons.Quvirn ? 5 : 6;
                    var mark = you with { Y = City.FloorAt(you) };
                    var frost = id == Summons.Shicene;
                    Effects.Glyph(this, mark, new Color(frost ? "#bfe8ff" : "#8a9aff"), 3.5f, 1.0, frost ? DamageKind.Frost : DamageKind.Shock);
                    GetTree().CreateTimer(1.0).Timeout += () =>
                    {
                        if (!IsInstanceValid(e) || e.State == EnemyState.Dead) return;
                        if (frost) Effects.Bolt(this, mark + new Vector3(0.5f, 16, 0.5f), mark, DamageKind.Frost, 0.6f);
                        else Effects.Lightning(this, mark + new Vector3(1, 30, 1), mark, new Color("#c8d0ff"), 0.35, 0.22f);
                        if (_player.GlobalPosition.DistanceTo(mark) < 3.5f) Hit(frost ? 46 : 42, frost ? DamageKind.Frost : DamageKind.Shock);
                    };
                }
                break;
            case Summons.Aegrath: // it shuts itself up in its guard, and stamps
                _greatMoves[e] = 8;
                e.Stats.Statuses.Add(AutoStatus.Protect);
                e.Stats.Statuses.Add(AutoStatus.Shell);
                GetTree().CreateTimer(4).Timeout += () => { if (IsInstanceValid(e)) { e.Stats.Statuses.Remove(AutoStatus.Protect); e.Stats.Statuses.Remove(AutoStatus.Shell); } };
                Effects.Rise(e, DamageKind.Physical);
                Effects.Burst(this, me + Vector3.Up * 0.3f, DamageKind.Physical, 7);
                if (near < 8) Hit(38, DamageKind.Physical);
                break;
            case Summons.Blaze: // a ring of fire out from it
                _greatMoves[e] = 6;
                Effects.Burst(this, me + Vector3.Up, DamageKind.Fire, 9);
                if (near < 10) Hit(42, DamageKind.Fire);
                break;
            case Summons.Draxon: // the King of Beasts calls more of his corrupted to him
                _greatMoves[e] = 20;
                if (_greatOnes.Count < 2 || Enemies().Count(o => o.Stats.Name == "Draxon's Thrall" && o.State != EnemyState.Dead) < 3) Thralls(e, 2);
                break;
        }
    }

    /// <summary>Draxon speaks: his mind is his own. And the beasts of his roost come to him, corrupted, and obey.</summary>
    void DraxonRises(Enemy e)
    {
        _hud.Toast("Draxon: \"My mother's little summoner. She sent you to be bound, or to bind? Let's see which.\"", 8);
        Thralls(e, 3);
    }

    void Thralls(Enemy king, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var a = Mathf.Tau * i / n;
            var at = king.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9;
            var t = Enemy.Create(Bestiary.Get(i % 2 == 0 ? "wolf" : "bear"), at with { Y = City.FloorAt(at) + 0.5f }, danger: 3);
            t.Stats.Name = "Draxon's Thrall";
            Models.Tint(t.Figure, new Color("#1c2a1c"), 0.5f);
            t.RefreshPlate();
            AddEnemy(t);
            t.Provoke(_player);
            Effects.Burst(this, at + Vector3.Up, DamageKind.Poison, 2);
        }
    }

    /// <summary>A great one of the road brought down with the hero there: bound to their ring.</summary>
    void SummonerSlain(Enemy victim)
    {
        if (_greatOnes.FirstOrDefault(kv => kv.Value == victim).Key is not { } id) return;
        if (victim.GlobalPosition.DistanceTo(_player.GlobalPosition) > 90) return;
        if (Summons.Bind(_player.Hero, id) is not { } s) return;
        Effects.Limit(this, victim.GlobalPosition, new Color("#5aff6a"));
        Sound.Ui("quest");
        if (id == Summons.Draxon) _hud.Toast("Draxon: \"...Then I'm yours. Tell my mother I said nothing.\"", 8);
        _hud.Toast(_player.Hero.Summoner == SummonerStep.Marked
            ? $"{s.Name}'s spirit catches on your ring. Go back to Braz Algún at the Binding Stones."
            : $"{s.Name} is bound to your ring. {(_player.Hero.Bound.Count > 1 ? "Ask Braz to choose who answers your call (or keep the one you have). " : "")}Braz will want to hear of it.", 10);
        Callable.From(Save).CallDeferred();
    }

    /// <summary>The tracker's line for the road.</summary>
    IEnumerable<string> SummonerLines(Vector3 pos)
    {
        var hero = _player.Hero;
        if (Summons.TrialOf(hero.Seeking) is { } t)
            yield return $"The Summoner's road, step {t.Step}: beat {Summons.Get(t.Id)!.Name} at {t.Lair}, recommended level {t.Recommended} ({Direction(pos, LairAt(t))})";
        else if (hero.Summoner == SummonerStep.Marked && hero.Bound.Count > 0)
            yield return $"The Summoner's road: show Braz Algún what you've bound ({Direction(pos, (Summons.StonesX, Summons.StonesZ))})";
        else if (Summons.ReadyFor(hero) is { } next && hero.Summoner == SummonerStep.Summoner)
            yield return $"The Summoner's road, step {next.Step}: Braz Algún has more for you ({Direction(pos, (Summons.StonesX, Summons.StonesZ))})";
    }

    // ---- for the self-test and the pictures ----

    public Enemy? GreatOneForTest(string id) => _greatOnes.GetValueOrDefault(id);
    public (double X, double Z) LairForTest(string id) => LairAt(Summons.TrialOf(id)!);
    public void TendSummonerForTest() { _summonerTick = 0; TendSummoner(0); }
    public void CallSummonForTest() => CallSummon();
    public IEnumerable<(string, Action)> BrazForTest(Person p) => Braz(p);
}
