using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// The wrong side of every honest job (see <see cref="ShadowBook"/>). Where the Watch walks beats, the Ravenscrew
/// Tongs' man in the Warrens, Corvin Rask, sends a thug round the shops for the Tongs' due; where the hunter sells
/// pelts, a poacher sells Nell Corran what the farms keep; where the sailor sails, a smuggler runs her crates to the
/// ports; where the carter drives, a highwayman buys her tip on a wagon and waylays it on the road. Each pays, each
/// rises, and each costs a name in the city, with the Watch sent for when a shopkeeper won't pay.
/// </summary>
public partial class Main
{
    public const string TongMan = "tristinwold/tong";
    public static readonly Vector3 TongCorner = new(-26, 0, -78);
    ShadowBook Shadows => _standing.Shadows;

    void AddShadows()
    {
        Townsfolk(new Npc(TongMan, "Corvin Rask", Sex.Male, 46, "a captain of the Ravenscrew Tongs", "city:tristinwold", "Tristinwold",
            "Corvin collects for the Tongs in the Warrens and the docks, and has done since he was tall enough to lean on a door. He has never been inside the Watch House, and says it's because he's never been invited.",
            ["money", "the Tongs"], "dry", "You're standing in my shadow. Either you want work, or you want to move.", TongCorner.X, TongCorner.Z), "bandit");
    }

    IEnumerable<(string, Action)> ShadowTalk(Person person)
    {
        var id = person.Npc.Id;
        var sh = Shadows;
        if (id == TongMan)
        {
            if (!sh.In(ShadowLine.Thug)) yield return ("I want to work for the Tongs.", () => JoinTongs(person));
            else if (sh.Round.Count == 0 || sh.RoundDone) yield return ("Give me a round to collect.", () => StartRound(person));
            else yield return ("About the round...", () => Say(person, $"Still owed: {string.Join(", ", sh.Round.Where(sh.Owes).Select(ShopName))}. Lean on them."));
            if (sh.In(ShadowLine.Thug)) yield return ("How do I stand with the Tongs?", () => Say(person, $"You're {sh.TitleOf(ShadowLine.Thug)}: {sh.For(ShadowLine.Thug).Done} collected, {sh.For(ShadowLine.Thug).Earned} crowns of it yours. {sh.NextStep(ShadowLine.Thug)}"));
        }
        else if (sh.In(ShadowLine.Thug) && sh.Owes(id) && Shops.All.Any(s => s.Id == id))
            yield return ("The Tongs' due.", () => Collect(person));
        else if (id == Smuggler)
        {
            if (!sh.In(ShadowLine.Poacher)) yield return ("What would the farms' animals fetch, no questions asked?", () => JoinLine(person, ShadowLine.Poacher, "Pelts, meat, a horse if you can walk it here. I pay by the head, and I don't ask whose field it stood in."));
            else if (sh.Pelts > 0) yield return ($"Here's what I took off the farms ({sh.Pelts}).", () => SellPelts(person));
            if (!sh.In(ShadowLine.Smuggler)) yield return ("Anything that needs carrying where the Watch doesn't look?", () => JoinLine(person, ShadowLine.Smuggler, "Crates. Over the sea, to a man who'll be waiting on the quay. Sign on with the company if you haven't; their ships carry more than they know."));
            else if (sh.CrateFor is null) yield return ("A crate to run?", () => GiveCrate(person));
            else yield return ("About the crate...", () => Say(person, $"It's for {Seas.Get(sh.CrateFor)?.Name ?? sh.CrateFor}. Get it there, and don't open it."));
            if (!sh.In(ShadowLine.Highwayman)) yield return ("Heard of a wagon worth waylaying?", () => JoinLine(person, ShadowLine.Highwayman, $"There's always a wagon. {ShadowBook.TipPrice} crowns for which one and when, and the road's yours."));
            else if (_standing.Tracker is null || _standing.Tracker.Lost(_guilds.PlaySeconds)) yield return ($"A wagon worth waylaying? ({ShadowBook.TipPrice} crowns)", () => BuyTip(person));
            foreach (var line in new[] { ShadowLine.Poacher, ShadowLine.Smuggler, ShadowLine.Highwayman }.Where(sh.In))
                yield return ($"How do I stand as {sh.TitleOf(line)}?", () => Say(person, $"{sh.For(line).Done} done, {sh.For(line).Earned} crowns. {sh.NextStep(line)}"));
        }
        else if (_atSea?.Agent == person && sh.CrateFor is { } port && _sea.Voyage?.Port == port)
            yield return ("Nell Corran's crate.", () => DeliverCrate(person, port));
    }

    static string ShopName(string id) => Shops.All.FirstOrDefault(s => s.Id == id)?.Name ?? id;

    void JoinTongs(Person corvin)
    {
        if (_standing.Watch.In) { Say(corvin, "Not with the Watch's colours on you. Hand them in first."); return; }
        Shadows.Join(ShadowLine.Thug);
        _standing.Evil(2, "went to work for the Ravenscrew Tongs");
        Say(corvin, "Good. The Tongs keep the peace in their own way: the shops pay, and nothing happens to them. You collect. Come back when you want a round.");
        _hud.Toast("You run with the Ravenscrew Tongs now. Corvin Rask gives you rounds: shops to collect the Tongs' due from. The Watch won't have you while you do.", 10);
        Save();
    }

    void StartRound(Person corvin)
    {
        var round = Shadows.StartRound(Shops.All.Where(s => Locals.Any(p => p.Npc.Id == s.Id)).Select(s => s.Id).ToList(), _lootRng);
        Say(corvin, $"Three doors: {string.Join(", ", round.Select(ShopName))}. Tell them the Tongs sent you. If one won't pay, remember who.");
        _hud.Toast($"A round to collect: {string.Join(", ", round.Select(ShopName))}. The tracker shows who's still owed.", 8);
        Save();
    }

    void Collect(Person keeper)
    {
        var shop = Shops.All.First(s => s.Id == keeper.Npc.Id);
        if (Shadows.Collect(shop.Id, _lootRng, sure: ShadowsSure) is { } pay)
        {
            _player.Crowns += pay;
            _standing.Evil(1, $"collected the Tongs' due from {shop.Name}");
            Sound.Ui("coin", -3);
            Say(keeper, $"{keeper.Npc.First} counts it out without looking at you. \"{pay} crowns. Tell Rask the roof's still leaking.\"");
            _hud.Toast($"The Tongs' due from {shop.Name}: {pay} crowns.{(Shadows.RoundDone ? " The round's done; Corvin Rask will want to hear it." : "")}", 6);
        }
        else
        {
            _standing.Evil(2, $"leaned on {shop.Name} for the Tongs");
            _standing.Wanted(ShadowBook.RefusedBounty);
            Sound.Ui("error");
            Say(keeper, $"\"Not a copper. Get out of my shop.\" {keeper.Npc.First}'s boy is already out of the back door and running for the Watch.");
            _hud.Toast($"{shop.Name} won't pay, and the Watch is sent for: {ShadowBook.RefusedBounty} crowns on your head ({_standing.Bounty} in all).", 8);
        }
        Save();
    }

    void JoinLine(Person nell, ShadowLine line, string said)
    {
        Shadows.Join(line);
        _standing.Evil(1, $"took up with Nell Corran as {Shadows.TitleOf(line)}");
        Say(nell, said);
        _hud.Toast(line switch
        {
            ShadowLine.Poacher => "A poacher now: the farms' animals you kill are pelts for Nell, not fines, as long as nobody sees. Bring her the tally.",
            ShadowLine.Smuggler => "A smuggler now: Nell has crates for the ports. Sail with one aboard a company ship, and her man comes to the rail at the quay.",
            _ => "A highwayman now: Nell sells you which wagon is leaving and when; the tracker shows it on the road. Meet it there (E) and bring its guards down.",
        }, 10);
        Save();
    }

    void SellPelts(Person nell)
    {
        var pay = Shadows.SellPelts();
        _player.Crowns += pay;
        Sound.Ui("coin", -2);
        Say(nell, $"{pay} crowns, and we never had this conversation.");
        _hud.Toast($"Nell pays for the poached animals: +{pay} crowns.", 6);
        Save();
    }

    void GiveCrate(Person nell)
    {
        if (_sea.Rank == SeaRank.Landsman) { Say(nell, "Sign on with the company first. A crate needs a ship under it."); return; }
        var port = _sea.Ports.OrderBy(p => p.Leagues).Skip(Shadows.Rank(ShadowLine.Smuggler)).FirstOrDefault() ?? _sea.Ports.Last();
        Shadows.CrateFor = port.Id;
        Say(nell, $"This one's for {port.Name}. A man will come to the rail when you're in. It's heavy, it's nailed, and it's none of your business.");
        _hud.Toast($"A crate for {port.Name}: sail there, and her man comes to the rail in port.", 8);
        Save();
    }

    void DeliverCrate(Person agent, string port)
    {
        if (Shadows.Deliver(port) is not { } pay) return;
        _player.Crowns += pay;
        _standing.Evil(2, $"ran a crate for Nell Corran to {Seas.Get(port)?.Name ?? port}");
        Sound.Ui("coin", -2);
        Say(agent, $"A man in a tarred coat takes the crate off you at the rail, and leaves {pay} crowns in your hand without a word.");
        _hud.Toast($"The crate's delivered: +{pay} crowns. Nell will have another.", 6);
        Save();
    }

    void BuyTip(Person nell)
    {
        if (_player.Crowns < ShadowBook.TipPrice) { Say(nell, $"{ShadowBook.TipPrice} crowns. Tips aren't free."); return; }
        if (NextBound() is not { } bound) { Say(nell, "Nothing leaving today. Ask tomorrow."); return; }
        _player.Crowns -= ShadowBook.TipPrice;
        Plant(bound);
        Say(nell, $"A wagon's leaving the yard for {bound.Name}. Be on the road. Not too near the walls.");
        Save();
    }

    /// <summary>The self-test's shopkeepers always pay.</summary>
    public bool ShadowsSure { get; set; }

    /// <summary>A poacher's kill of somebody's animal: no fine, as nobody saw; a pelt for Nell.</summary>
    bool Poaching(Enemy victim)
    {
        if (!Shadows.In(ShadowLine.Poacher)) return false;
        Shadows.Pelts++;
        _standing.Evil(1, $"poached somebody's {victim.Kind.Name.ToLowerInvariant()}");
        _hud.Toast($"Somebody's {victim.Kind.Name.ToLowerInvariant()}, and nobody saw: a pelt for Nell ({Shadows.Pelts} owed).", 5);
        return true;
    }

    IEnumerable<string> ShadowLines(Vector3 pos)
    {
        var sh = Shadows;
        if (sh.In(ShadowLine.Thug) && sh.Round.Count > 0 && !sh.RoundDone) yield return $"The Tongs: collect from {string.Join(", ", sh.Round.Where(sh.Owes).Select(ShopName))}";
        else if (sh.In(ShadowLine.Thug) && sh.RoundDone) yield return $"The Tongs: the round's done; tell Corvin Rask in the Warrens ({Direction(pos, (TongCorner.X, TongCorner.Z))})";
        if (sh.CrateFor is { } port) yield return $"Nell's crate: for {Seas.Get(port)?.Name ?? port}";
    }
}
