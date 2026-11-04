using Tristinwold.Deviant;

namespace Tristinwold.World;

/// <summary>Where a hero stands in Turlin's game: hired, holding the planted clue, sold on the lie, or on to him.</summary>
public enum TurlinStage { None, Hired, Clue, Blamed, Hunting, Fooled }

/// <summary>Someone Turlin has had taken, the dupe he pays to hire heroes, what he left to be found, and the guardian he means to hang it on.</summary>
public sealed record TurlinCase(string Asker, string AskerIs, string Victim, string Who, string Token, string Guardian);

/// <summary>What's left of him this time: the body he came back in, how far the demon's got into it (0 to 1), and how he talks now.</summary>
public sealed record TurlinForm(string Is, string Kind, float Size, Rank Rank, float Demon, string Hail, string Won, string Lost);

/// <summary>
/// Turlin, the hero's rival for life, in the same terms as every other deviant: scars, memory, wins and losses (his
/// <see cref="Self"/>). The fruit from the foot of the Maw has made him an incomplete immortal, and he doesn't know it.
/// He thinks every death was a narrow escape, so he fights like someone who can die: he's careful, he's cunning, and at a
/// quarter of his blood he grows wings and goes. But each time he's really killed, less of what made him Turlin comes back,
/// and more of something demonic and beast-like; and he's sharp enough to have noticed, without knowing why. He's kept
/// with the hero and not in the server's shared roster, so nobody else's fights can make him too much for someone just
/// reaching the wood; and he never strays far from the hero's own strength, so there's always a fight in it.
/// </summary>
public sealed class TurlinBook
{
    /// <summary>His record, as the deviant system keeps it.</summary>
    public Deviant.Deviant Self { get; set; } = new() { Name = "Turlin", Kind = "turlin", Species = "Shapeshifter", BaseForm = "turlin", HomeArea = "the Northwood", Rank = Rank.Stray, Level = 1 };
    /// <summary>The first meeting, at the wood's edge, is over.</summary>
    public bool Met { get; set; }
    /// <summary>How many times the hero has killed him. He came back from every one.</summary>
    public int Deaths { get; set; }
    /// <summary>
    /// How far gone he is: how many of those deaths took something of him. Only one death in four does (see
    /// <see cref="ChangeChance"/>). -1 in a save from before that, where every death did, so it follows the deaths.
    /// </summary>
    public int Fall { get; set; } = -1;
    /// <summary>How many steps he's taken from the man he was.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public int Changes => Fall < 0 ? Deaths : Fall;
    /// <summary>The chance a death costs him something of himself; the rest he comes back from as he was, only stronger.</summary>
    public const double ChangeChance = 0.25;
    /// <summary>For the self-test: the chance to use instead, so a test can be sure of the change.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double? ChangeChanceForTest { get; set; }
    /// <summary>What beating him has taught the hero, for good.</summary>
    public int Edge { get; set; }
    /// <summary>The deviant in him has shown itself: he's eaten the fruit in front of the hero, and from then on he burns with it.</summary>
    public bool Awoke { get; set; }
    /// <summary>When he'll come looking again: seconds of play.</summary>
    public double NextAt { get; set; }

    public TurlinStage Stage { get; set; }
    /// <summary>How many of his cases this hero has seen through to the end.</summary>
    public int Case { get; set; }
    /// <summary>He's put this hero down, and is waiting to see what happens next.</summary>
    public bool Waiting { get; set; }
    /// <summary>He has watched a hero he killed walk back: he knows the ring does whole what the fruit only half does.</summary>
    public bool Knows { get; set; }

    public static readonly IReadOnlyList<TurlinForm> Forms =
    [
        new("a big, easy man, black-furred, with a mane of long black hair and a monkey's tail", "turlin", 1, Rank.Stray, 0,
            "A big man drops out of the first of the trees and lands easy, black fur from the neck down, a mane of black hair to the small of his back, a monkey's tail flicking behind him, a spiked red fruit in his hand. \"There you are. I've been told a lot about what walks into this wood, and I'd like to see for myself what you're made of. No hard feelings. Hands up.\"",
            "\"Better than most. Not enough.\" He rolls his shoulders, and you can see it: he's bigger than he was. \"Come again when you've grown. I'll have grown too.\"",
            "The grin goes, and what's under it is plain fear. \"No. Not here, I haven't...\" He falls. The fruit at his belt splits with a wet sound, and when you look back, the body's gone."),
        new("a man going wrong: more fur than there was, and a tail he keeps looking at", "turlin", 1.15f, Rank.Stray, 0.2f,
            "Turlin steps out of the trees and stops well short of you, and something's wrong with how he's standing. \"That was close. I don't know how I got out of it; I woke under the hill with the taste of the fruit in my mouth. And I can't remember my mother's name. I had it, the day before you. What did you do to me?\" His eyes don't leave your hands. \"Show me again. Slowly.\"",
            "\"There.\" He stands over you, breathing like something bigger than he is. \"That helps. I can't tell you how, but it helps.\"",
            "He goes down on his knees with one hand out. \"Wait. Wait, I can pay,\" he says, and he means it. Then the fruit splits, and he's gone."),
        new("half a beast: long-armed, thick-furred, and hardly a man at all", "giant_ape", 0.5f, Rank.Ringleader, 0.4f,
            "You've had the feeling for a while before you see him: he's been walking beside you, upwind, out of reach, watching how you favour your feet. He comes out on his knuckles, a head over a tall man. There are notches cut down one forearm. \"I've started keeping a tally,\" Turlin says. \"Every time you nearly finish me I wake with less. I laughed once. I remember that I did. I'd like to know what you're taking.\"",
            "He sits on his heels by you and cuts a different mark in his arm, with care. \"Stronger. That's one thing it doesn't take.\"",
            "He tries for the trees and doesn't make it. He looks at the tally on his arm as if there were something on it he'd meant to read. The fruit splits."),
        new("a horned thing the size of a cart, that still looks at you like a man doing sums", "giant_ape", 0.7f, Rank.Ringleader, 0.6f,
            "The ambush is a good one. You'd never have seen it if he hadn't wanted you to; the road's been cut behind you. Horn has started through the fur at his brow. \"Nobody kind ever lived in here,\" Turlin says, tapping his own chest, and it takes you a moment to see that he doesn't remember that isn't true. \"I don't want to die. Do you understand? There's so little left to do it with. Let's be careful, you and I.\"",
            "He doesn't gloat any more. He checks you're down, then checks again, and walks backward to the treeline.",
            "\"I had it worked out,\" he says, in a voice with a lot of other voices in it. \"I had it all...\" The fruit splits."),
        new("a black-horned beast with embers for eyes", "giant_ape", 0.85f, Rank.Scourge, 0.8f,
            "He's let you see him three times today, always at the same distance, and you've only now worked out why: to learn what you do when you're tired. His eyes burn. \"Turlin,\" he says, carefully, like a man reading a label. \"That's right, isn't it? That's who's going to kill you.\"",
            "It stands over you and makes a low, satisfied noise, and then, as an afterthought, it remembers to speak. \"Again. Soon.\"",
            "It goes down with a noise like a hill settling, still trying to crawl for cover, still not believing it. Then the fruit splits, somewhere inside it."),
        new("a demon, mostly: all horn and ember and black hide, and every bit as careful as he ever was", "giant_ape", 1, Rank.Tyrant, 1,
            "There's a shape on the rise, keeping the wind in its favour and the trees at its back. Whatever made Turlin laugh is gone, and whatever made him kind, and nearly all of what made him anyone. What's left knows its name, knows your ring, knows the way out of every clearing in the world, and very much doesn't want to die. \"Turlin,\" it says. \"You. Yes.\"",
            "It looks at you for a long time, and pats you twice on the chest, gently, the way you'd pat something you mean to keep.",
            "It falls, and its last look is a surprised one, as it always is. It has no idea. You know better than to think that's the end of it."),
    ];

    public TurlinForm Form => Forms[Math.Min(Changes, Forms.Count - 1)];

    public static readonly IReadOnlyList<TurlinCase> Cases =
    [
        new("Hob Tallow", "foreman of the logging camp", "Tolly Reed", "the camp cook's boy", "a child's boot, split down the sole, with sap dried on it", "mosswarden"),
        new("Wenna Sayle", "a carter at the logging camp", "Sister Anwen", "a healer who walked out to the camps", "a healer's satchel, its strap torn through and bark ground into the leather", "barkbreaker"),
        new("Pell Harrow", "a surveyor out of Tristinwold", "Jory Flint", "Dagna's nephew, up from the quarry", "a miner's lamp, crushed flat, with black water still in it", "drowned"),
    ];

    public const int FindPay = 300, BlamePay = 800, ImmortalPay = 1600;
    /// <summary>How long between his visits, in seconds of play; sooner if you only talked him out of it.</summary>
    public const double Between = 25 * 60, TalkedOut = 12 * 60;
    /// <summary>Talking him out of a fight: it can be done, but not often. Bards have the better of it.</summary>
    public const double TalkChance = 0.2, BardTalkChance = 0.35;
    /// <summary>He isn't reckless: at this share of his blood he takes wing, and you've this long to bring him down before he's gone.</summary>
    public const double FleeAt = 0.25, FlightSeconds = 7;
    /// <summary>How far ahead of the hero he can get, how far behind he'll fall, and how much each win teaches the hero.</summary>
    public const int MostAhead = 2, MostBehind = 1, MostEdge = 15;
    public const double EdgeGain = 0.02;
    /// <summary>The fruit: ten times his strength, twice his speed, and the stamina's gone before he's done chewing.</summary>
    public const double FruitMight = 10, FruitSpeed = 2, FruitSeconds = 12, FruitWinded = 8, FruitCooldown = 120, FruitBelow = 0.8;
    /// <summary>He doesn't touch it until he's fought this often: it isn't for the first meetings.</summary>
    public const int FruitAfter = 3;

    public bool CanEat => Self.Encounters >= FruitAfter;

    /// <summary>The level he fights at: he climbs with every win and every death, held close to the hero's own.</summary>
    public int LevelAgainst(int hero) => Math.Clamp(2 + Self.Wins * 2 + Deaths, Math.Max(1, hero - MostBehind), hero + MostAhead);

    /// <summary>What the rivalry has made of the hero: a share more health and damage.</summary>
    public double EdgeBonus => 1 + EdgeGain * Math.Min(Edge, MostEdge);

    /// <summary>He beat the hero: he's stronger for it, and if he thinks that's the end of them, he's about to learn otherwise.</summary>
    public void HeWon(string hero, int day, double now)
    {
        var rec = Self.Player(hero);
        rec.Memory |= Memory.KilledPlayer;
        rec.TimesKilledThem++;
        Self.Memory |= Memory.KilledPlayer;
        Self.Wins++;
        Self.Encounters++;
        Self.Level++; // an incomplete immortal has no ceiling
        Self.Power += 40;
        Self.History.Add(new(day, $"Beat {hero}, and took something from it."));
        NextAt = now + Between;
        if (!Knows) Waiting = true;
    }

    /// <summary>The hero beat him and he got away on the wing, whole: they've still won, and they're stronger for it.</summary>
    public void HeEscaped(string hero, int day, double now)
    {
        Self.Player(hero).TimesTheyBeatMe++;
        Self.Memory |= Memory.FledFromPlayer;
        Self.Losses++;
        Self.Encounters++;
        Edge = Math.Min(MostEdge, Edge + 1);
        Self.History.Add(new(day, $"Lost to {hero}, and got away."));
        NextAt = now + Between;
        Waiting = false;
    }

    /// <summary>
    /// The hero caught him and killed him. He'll be back, and one time in four less himself (<paramref name="changes"/>
    /// says, or the dice do); they're stronger for it, and that part they keep.
    /// </summary>
    public void HeLost(string hero, DamageKind blow, int day, double now, bool? changes = null)
    {
        if (Fall < 0) Fall = Deaths;
        var changed = changes ?? Random.Shared.NextDouble() < (ChangeChanceForTest ?? ChangeChance);
        if (changed) Fall++;
        var rec = Self.Player(hero);
        rec.TimesTheyBeatMe++;
        Self.Losses++;
        Self.Encounters++;
        Deaths++;
        Edge = Math.Min(MostEdge, Edge + 1);
        Self.Memory |= Memory.Returned;
        Self.Scars.Add(new(blow, $"{blow} wound from {hero}, that closed and didn't heal", day));
        // What's killed him, he's learned to stand: a deviant is hard to beat the same way twice.
        var id = $"learned_{blow}".ToLowerInvariant();
        if (blow != DamageKind.Unknown && Self.Traits.All(t => t.Id != id))
            Self.Traits.Add(new(id, TraitKind.Strength, $"has learned to stand {blow.ToString().ToLowerInvariant()}", Affinities: new() { [blow] = Affinity.Resist }));
        Self.Rank = Form.Rank;
        Self.History.Add(new(day, changed ? $"Killed by {hero}. Came back, and doesn't know he died: {Form.Is}." : $"Killed by {hero}. Came back as he was, and stronger for it."));
        NextAt = now + Between;
        Waiting = false;
    }

    /// <summary>The hero got clear of him. He notes it.</summary>
    public void Fled(string hero, double now)
    {
        Self.Player(hero).Memory |= Memory.PlayerFled;
        Self.Player(hero).TimesTheyFled++;
        Self.Encounters++;
        NextAt = now + Between;
    }

    public bool Over => Case >= Cases.Count;
    public TurlinCase? Current => Over ? null : Cases[Case];
    /// <summary>The one he's paying is at the camp, until the hero's on to it.</summary>
    public bool AtTheCamp => !Over && Stage != TurlinStage.Hunting;

    public bool Hire()
    {
        if (Over || Stage != TurlinStage.None) return false;
        Stage = TurlinStage.Hired;
        return true;
    }

    /// <summary>The clue found, where he left it.</summary>
    public bool Find()
    {
        if (Stage != TurlinStage.Hired) return false;
        Stage = TurlinStage.Clue;
        return true;
    }

    /// <summary>Taken back to the camp, where the guilty guardian's named and its death is offered for.</summary>
    public bool Report()
    {
        if (Stage != TurlinStage.Clue) return false;
        Stage = TurlinStage.Blamed;
        return true;
    }

    /// <summary>Shown to a druid who knows what made those marks: the trail turns round and goes to his den.</summary>
    public bool Doubt()
    {
        if (Stage is not (TurlinStage.Clue or TurlinStage.Blamed)) return false;
        Stage = TurlinStage.Hunting;
        return true;
    }

    /// <summary>A true guardian killed on his word. The lie has done its work.</summary>
    public bool Slew()
    {
        if (Stage != TurlinStage.Blamed) return false;
        Stage = TurlinStage.Fooled;
        return true;
    }

    /// <summary>What's paid when the guardian's dead, closing the case: more once he knows what he's got.</summary>
    public int Settle()
    {
        if (Stage != TurlinStage.Fooled) return 0;
        Stage = TurlinStage.None;
        Case++;
        return Knows ? ImmortalPay : BlamePay;
    }

    /// <summary>He's killed in his den, and the one he took walks free.</summary>
    public bool Won()
    {
        if (Stage != TurlinStage.Hunting) return false;
        Stage = TurlinStage.None;
        Case++;
        return true;
    }

    /// <summary>The hero he killed comes back. True if that's how he finds out.</summary>
    public bool Returned()
    {
        if (!Waiting) return false;
        (Waiting, Knows) = (false, true);
        return true;
    }

    /// <summary>The guardian this case hangs on: the one it was meant for, or the next still standing.</summary>
    public static string? Blame(TurlinCase c, IReadOnlySet<string> slain, IReadOnlyList<string> guardians) =>
        slain.Contains(c.Guardian) ? guardians.FirstOrDefault(g => !slain.Contains(g)) : c.Guardian;

    /// <summary>His entry in the deviants tab.</summary>
    public string Sheet()
    {
        var hero = Self.Players.Values.FirstOrDefault();
        var last = Self.History.TakeLast(3).Select(h => "   " + h.Text);
        return string.Join("\n", new[]
        {
            "YOUR RIVAL  Turlin",
            $"   {char.ToUpperInvariant(Form.Is[0])}{Form.Is[1..]}. He has beaten you {hero?.TimesKilledThem ?? 0} time{(hero?.TimesKilledThem == 1 ? "" : "s")}; you have killed him {Deaths} time{(Deaths == 1 ? "" : "s")}, and he came back from every one{(Changes == 0 ? "" : Changes == Deaths ? ", less" : $", {Changes} of them less")} himself.",
            Edge > 0 ? $"   What he's taught you: {EdgeGain * Math.Min(Edge, MostEdge):P0} more health and damage." : "",
            Self.Traits.Count > 0 ? "   " + string.Join("; ", Self.Traits.Select(t => t.Label)) : "",
        }.Where(l => l != "").Concat(last));
    }
}
