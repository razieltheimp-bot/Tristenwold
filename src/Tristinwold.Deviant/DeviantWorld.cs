namespace Tristinwold.Deviant;

/// <summary>Tuning knobs. Defaults aim for "a deviant shows up every few battles".</summary>
public sealed class DeviantRules
{
    public int MaxScourges { get; init; } = 5;
    public int MaxRingleadersPerScourge { get; init; } = 4;
    public int MaxUnassignedRingleaders { get; init; } = 6;
    /// <summary>No limit: there's always someone new who thinks they can climb.</summary>
    public int MaxStrays { get; init; } = int.MaxValue;
    /// <summary>How many strays can turn up for any one fight, whatever the size of the roster.</summary>
    public int StraysPerBattle { get; init; } = 4;
    /// <summary>What a stray gains in a day of scheming, the chance it picks a fight above it, and the chance a soldier breaks away to be a stray.</summary>
    public int AmbitionPerDay { get; init; } = 6;
    public double StrayChallengePerDay { get; init; } = 0.3;
    public double BreakawayPerDay { get; init; } = 0.25;

    public int PowerForScourge { get; init; } = 100;
    public int PowerForTyrant { get; init; } = 250;

    public double AppearanceChance { get; init; } = 0.35;
    public double HomeAreaAppearanceChance { get; init; } = 0.6;
    public int MaxDeviantsPerBattle { get; init; } = 2;

    public int MaxLevel { get; init; } = 20;
    public int MaxDisplayNameLength { get; init; } = 40;

    public double GrudgeChancePerDay { get; init; } = 0.2;
    public double GrudgeFightChancePerDay { get; init; } = 0.5;
    /// <summary>Oaths come from an underling rising, not from chance; this is only for a world that wants pacts as well.</summary>
    public double PactChancePerDay { get; init; }
}

/// <summary>
/// The whole deviant simulation. The game layer feeds it battle reports and asks it
/// which deviants to put into each new battle; everything else happens in here.
/// Not thread-safe: call it from the game thread (or under one lock).
/// </summary>
public sealed class DeviantWorld
{
    public const int SaveVersion = 1;

    readonly List<Deviant> _deviants;
    readonly Rng _rng;

    public DeviantRules Rules { get; }
    public NamePack Names { get; }
    public int Day { get; private set; }
    public IReadOnlyList<Deviant> All => _deviants;
    public IEnumerable<Deviant> Living => _deviants.Where(n => n.Alive);

    public DeviantWorld(ulong seed, DeviantRules? rules = null, NamePack? names = null)
        : this(new Rng(seed), [], 0, rules, names) { }

    DeviantWorld(Rng rng, List<Deviant> deviants, int day, DeviantRules? rules, NamePack? names)
    {
        _rng = rng;
        _deviants = deviants;
        Day = day;
        Rules = rules ?? new DeviantRules();
        Names = names ?? NamePack.Default;
    }

    public Deviant? Find(Guid id) => _deviants.FirstOrDefault(n => n.Id == id);

    // ---- Hierarchy -------------------------------------------------------------------------

    public Deviant? Tyrant => Living.FirstOrDefault(n => n.Rank == Rank.Tyrant);
    public IEnumerable<Deviant> Scourges => Living.Where(n => n.Rank == Rank.Scourge).OrderByDescending(n => n.Power);
    public IEnumerable<Deviant> RingleadersUnder(Guid warchiefId) =>
        Living.Where(n => n.Rank == Rank.Ringleader && n.ServesUnder == warchiefId).OrderByDescending(n => n.Power);
    public IEnumerable<Deviant> UnassignedRingleaders =>
        Living.Where(n => n.Rank == Rank.Ringleader && (n.ServesUnder is null || Find(n.ServesUnder.Value)?.Alive != true));
    public IEnumerable<Deviant> Strays => Living.Where(n => n.Rank == Rank.Stray).OrderByDescending(n => n.Power);

    /// <summary>Every pair with a grudge still open: what a hero might walk in on.</summary>
    public IEnumerable<(Deviant A, Deviant B)> Feuds =>
        Living.SelectMany(a => a.Rivals.Select(Find).Where(b => b is { Alive: true } && a.Id.CompareTo(b.Id) < 0).Select(b => (a, b!)));

    // ---- Battle setup ----------------------------------------------------------------------

    /// <summary>
    /// Decides which enemies in this fight become deviants. A deviant only takes the place of
    /// an enemy of its own kind, so it looks and fights the way players remember it.
    /// </summary>
    public IReadOnlyList<DeviantSpawn> PlanEncounter(string areaId, IReadOnlyList<FormationSlot> formation, IReadOnlyList<string> players)
    {
        var spawns = new List<DeviantSpawn>();
        var freeSlots = formation.ToList();
        var fitting = Living.Where(n => freeSlots.Any(s => s.Kind == n.Kind)).ToList();
        var strays = fitting.Where(n => n.Rank == Rank.Stray).OrderBy(_ => _rng.Next(int.MaxValue)).Take(Rules.StraysPerBattle);
        var candidates = fitting.Where(n => n.Rank != Rank.Stray).Concat(strays)
            .Select(n => (Deviant: n, Weight: AppearanceWeight(n, areaId, players)))
            .OrderByDescending(c => c.Weight)
            .ToList();

        foreach (var (deviant, weight) in candidates)
        {
            if (spawns.Count >= Rules.MaxDeviantsPerBattle) break;
            var chance = Math.Min(0.95, (deviant.HomeArea == areaId ? Rules.HomeAreaAppearanceChance : Rules.AppearanceChance) * weight);
            if (!_rng.Chance(chance)) continue;
            var slot = freeSlots.FirstOrDefault(s => s.Kind == deviant.Kind);
            if (slot is null) continue;
            freeSlots.Remove(slot);
            spawns.Add(new DeviantSpawn(slot.Slot, deviant.Id, ModifiersFor(deviant), Taunt(deviant, players)));
        }
        return spawns;
    }

    double AppearanceWeight(Deviant n, string areaId, IReadOnlyList<string> players)
    {
        var weight = 1.0 + 0.25 * (int)n.Rank;
        foreach (var p in players)
        {
            if (!n.Players.TryGetValue(p, out var rec)) continue;
            if (rec.Memory.HasFlag(Memory.KilledPlayer)) weight += 1.0;
            if (rec.Memory.HasFlag(Memory.PlayerFled)) weight += 0.5;
            if (rec.Memory.HasFlag(Memory.Vengeful)) weight += 2.0; // hunting this player
        }
        if (n.Memory.HasFlag(Memory.Returned)) weight += 0.5;
        return weight;
    }

    /// <summary>Rank, level and traits folded into one set of numbers for the game layer.</summary>
    public BattleModifiers ModifiersFor(Deviant n)
    {
        var rank = (int)n.Rank;
        var level = Math.Min(n.Level, Rules.MaxLevel);
        var stats = Enum.GetValues<Stat>().ToDictionary(s => s, s => s switch
        {
            Stat.Health => (1.0 + 0.6 * rank) * (1.0 + 0.08 * (level - 1)),
            Stat.Mana or Stat.Stamina => 1.0 + 0.3 * rank,
            _ => (1.0 + 0.1 * rank) * (1.0 + 0.03 * (level - 1)),
        });
        var affinities = new Dictionary<DamageKind, Affinity>();
        var autos = new List<AutoStatus>();
        foreach (var t in n.Traits)
        {
            if (t.StatMultipliers is not null)
                foreach (var (s, m) in t.StatMultipliers) stats[s] *= m;
            if (t.Affinities is not null)
                foreach (var (k, a) in t.Affinities)
                    affinities[k] = affinities.TryGetValue(k, out var existing) ? Stronger(existing, a) : a;
            if (t.AutoStatuses is not null)
                autos.AddRange(t.AutoStatuses.Where(s => !autos.Contains(s)));
        }
        var victories = n.Players.Values.Select(r => r.TimesKilledThem).DefaultIfEmpty(0).Max();
        return new BattleModifiers(DisplayName(n), stats, affinities, autos, n.Rank, victories);
    }

    static Affinity Stronger(Affinity a, Affinity b) => (Affinity)Math.Max((int)a, (int)b);

    public string DisplayName(Deviant n)
    {
        var full = n.FullName;
        return full.Length <= Rules.MaxDisplayNameLength ? full : n.Name[..Math.Min(n.Name.Length, Rules.MaxDisplayNameLength)];
    }

    string? Taunt(Deviant n, IReadOnlyList<string> players)
    {
        string? target = null;
        string key = n.Encounters == 0 ? "first" : "generic";
        foreach (var p in players)
        {
            if (!n.Players.TryGetValue(p, out var rec)) continue;
            var k = rec.Memory.HasFlag(Memory.Vengeful) ? "vengeful"
                : n.Memory.HasFlag(Memory.Returned) && rec.TimesTheyBeatMe > 0 ? "returned"
                : rec.Memory.HasFlag(Memory.KilledPlayer) ? "killed"
                : rec.Memory.HasFlag(Memory.PlayerFled) ? "fled"
                : n.Memory.HasFlag(Memory.FledFromPlayer) ? "escaped"
                : null;
            if (k is null) continue;
            (key, target) = (k, p);
            if (k == "vengeful") break;
        }
        // In a shared world it may have killed one of your friends in their own game.
        if (target is null)
        {
            var victim = n.Players.FirstOrDefault(kv => !players.Contains(kv.Key) && kv.Value.Memory.HasFlag(Memory.KilledPlayer)).Key;
            if (victim is not null) (key, target) = ("friend", victim);
        }
        if (!Names.Taunts.TryGetValue(key, out var lines) || lines.Count == 0) return null;
        return _rng.Pick(lines).Replace("{name}", n.FullName).Replace("{player}", target ?? "hero");
    }

    // ---- Battle results --------------------------------------------------------------------

    public IReadOnlyList<WorldEvent> RecordBattle(BattleReport report)
    {
        var events = new List<WorldEvent>();
        var deviantsInBattle = report.Enemies
            .Where(e => e.DeviantId is not null)
            .Select(e => (Enemy: e, Deviant: Find(e.DeviantId!.Value)))
            .Where(x => x.Deviant is { Alive: true })
            .ToList();

        foreach (var (enemy, n) in deviantsInBattle)
        {
            n!.Encounters++;
            n.Memory |= Memory.MetPlayer;
            foreach (var p in report.Players) n.Player(p).Memory |= Memory.MetPlayer;

            if (report.Result == BattleResult.Defeat)
                Triumph(n, report, events);
            else if (report.Result == BattleResult.PartyEscaped)
                PlayerFled(n, report, events);
            else if (enemy.Escaped)
                Escaped(n, report, events);
            else if (enemy.Killed)
                Defeated(n, enemy, report, events);
        }

        // A plain monster that wipes the party, or that the party runs from or that runs
        // away itself, gets a name. This is how the roster fills up.
        if (deviantsInBattle.Count == 0)
        {
            var survivor = report.Enemies.Where(e => !e.Killed).OrderByDescending(e => e.MaxHealth).FirstOrDefault();
            if (survivor is not null)
            {
                var (rank, memory) = report.Result switch
                {
                    BattleResult.Defeat => (Rank.Ringleader, Memory.KilledPlayer),
                    BattleResult.PartyEscaped => (Rank.Stray, Memory.PlayerFled),
                    _ when survivor.Escaped => (Rank.Stray, Memory.FledFromPlayer),
                    _ => (Rank.Stray, Memory.None),
                };
                if (memory != Memory.None) Birth(survivor, rank, memory, report, events);
            }
        }

        UpdateHierarchy(events);
        return events;
    }

    void Triumph(Deviant n, BattleReport report, List<WorldEvent> events)
    {
        n.Wins++;
        n.Memory |= Memory.KilledPlayer;
        GainPower(n, 40, levels: 2);
        foreach (var p in report.Players)
        {
            var rec = n.Player(p);
            rec.Memory |= Memory.KilledPlayer;
            rec.TimesKilledThem++;
        }
        Log(n, $"Wiped out {Party(report)} in {report.AreaId}.");
        events.Add(new(WorldEventKind.Triumph, n.Id, $"{n.FullName} defeated {Party(report)} and grows stronger."));
    }

    void PlayerFled(Deviant n, BattleReport report, List<WorldEvent> events)
    {
        n.Memory |= Memory.PlayerFled;
        GainPower(n, 15, levels: 1);
        foreach (var p in report.Players)
        {
            var rec = n.Player(p);
            rec.Memory |= Memory.PlayerFled;
            rec.TimesTheyFled++;
        }
        Log(n, $"{Party(report)} ran from it in {report.AreaId}.");
        events.Add(new(WorldEventKind.Triumph, n.Id, $"{n.FullName} laughs as {Party(report)} flee."));
    }

    void Escaped(Deviant n, BattleReport report, List<WorldEvent> events)
    {
        n.Memory |= Memory.FledFromPlayer;
        GainPower(n, 10, levels: 0);
        Log(n, $"Escaped from {Party(report)} in {report.AreaId}.");
        events.Add(new(WorldEventKind.Escaped, n.Id, $"{n.FullName} escaped. It will be back."));
    }

    void Defeated(Deviant n, EnemyReport enemy, BattleReport report, List<WorldEvent> events)
    {
        n.Losses++;
        var killer = enemy.KilledBy ?? report.Players.FirstOrDefault();
        foreach (var p in report.Players) n.Player(p).TimesTheyBeatMe++;

        var cheatChance = n.Rank switch { Rank.Stray => 0.35, Rank.Ringleader => 0.55, Rank.Scourge => 0.65, _ => 0.75 }
            - 0.2 * n.Scars.Count;
        if (_rng.Chance(Math.Max(0.05, cheatChance)))
        {
            var cause = enemy.KillingBlow ?? DamageKind.Physical;
            AddScar(n, cause);
            n.Memory |= Memory.Returned;
            GainPower(n, 10, levels: 1);
            Log(n, $"Was cut down by {killer ?? "the party"} but survived, scarred by {cause}.");
            events.Add(new(WorldEventKind.Returned, n.Id, $"{n.FullName} was not as dead as it looked."));
            return;
        }

        n.Alive = false;
        Log(n, $"Slain by {killer ?? "the party"} in {report.AreaId}.");
        events.Add(new(WorldEventKind.Slain, n.Id, $"{n.FullName} has been slain by {killer ?? "the party"}!"));

        // Death has consequences: all who are sworn to it swear revenge on whoever did it.
        foreach (var bid in n.Oaths.Concat(n.PactSworn is { } pact ? [pact] : []).Distinct().ToList())
        {
            if (Find(bid) is not { Alive: true } sworn) continue;
            sworn.Memory |= Memory.Vengeful;
            if (killer is not null) sworn.Player(killer).Memory |= Memory.Vengeful;
            GainPower(sworn, 15, levels: 1);
            Log(sworn, $"Swore revenge on {killer ?? "the party"} for {n.FullName}.");
            events.Add(new(WorldEventKind.Vengeful, sworn.Id, $"{sworn.FullName} swears to avenge {n.FullName}!", n.Id));
        }
    }

    void Birth(EnemyReport enemy, Rank rank, Memory memory, BattleReport report, List<WorldEvent> events)
    {
        if (rank == Rank.Stray && Strays.Count() >= Rules.MaxStrays)
        {
            // Roster full: the newcomer replaces the weakest stray, who fades into the horde.
            var weakest = Strays.Last();
            weakest.Alive = false;
            Log(weakest, "Was pushed out of the ranks by a newcomer.");
        }

        var n = new Deviant
        {
            Id = _rng.NextGuid(),
            Name = MakeName(),
            Kind = enemy.Kind,
            BaseForm = enemy.BaseForm,
            Species = enemy.Species,
            Rank = rank,
            Level = Math.Max(1, report.PartyLevel / 5),
            HomeArea = report.AreaId,
            Memory = memory | Memory.MetPlayer,
            Encounters = 1,
            Wins = memory.HasFlag(Memory.KilledPlayer) ? 1 : 0,
        };
        foreach (var p in report.Players)
        {
            var rec = n.Player(p);
            rec.Memory = memory | Memory.MetPlayer;
            if (memory.HasFlag(Memory.KilledPlayer)) rec.TimesKilledThem = 1;
            if (memory.HasFlag(Memory.PlayerFled)) rec.TimesTheyFled = 1;
        }
        n.Power = rank == Rank.Ringleader ? 40 : 10;
        AddTrait(n, TraitKind.Strength);
        AddTrait(n, TraitKind.Weakness);
        if (rank >= Rank.Ringleader) AddTrait(n, TraitKind.Strength);
        n.Epithet = n.Traits.Select(t => t.Epithet).FirstOrDefault(e => e is not null) ?? _rng.Pick(Names.Epithets);
        _deviants.Add(n);

        var why = memory.HasFlag(Memory.KilledPlayer) ? $"after defeating {Party(report)}"
            : memory.HasFlag(Memory.PlayerFled) ? $"after {Party(report)} fled from it"
            : $"after escaping {Party(report)}";
        Log(n, $"Rose from the ranks of {n.Species} in {report.AreaId} {why}.");
        events.Add(new(WorldEventKind.Born, n.Id, $"A {n.Species} named {n.FullName} rose to {rank} {why}."));
    }

    // ---- Traits, scars, names ---------------------------------------------------------------

    void AddTrait(Deviant n, TraitKind kind)
    {
        var pool = (kind == TraitKind.Strength ? TraitCatalog.Strengths : TraitCatalog.Weaknesses)
            .Where(t => n.Traits.All(o => o.Id != t.Id) && !Conflicts(n, t))
            .ToList();
        if (pool.Count > 0) n.Traits.Add(_rng.Pick(pool));
    }

    /// <summary>A deviant can't both absorb fire and be weak to it.</summary>
    static bool Conflicts(Deviant n, Trait t) =>
        t.Affinities is not null && n.Traits.Any(o => o.Affinities is not null && o.Affinities.Keys.Intersect(t.Affinities.Keys).Any());

    void AddScar(Deviant n, DamageKind cause)
    {
        var descriptions = Names.ScarDescriptions.GetValueOrDefault(cause) ?? ["An ugly scar."];
        n.Scars.Add(new Scar(cause, _rng.Pick(descriptions), Day));

        // Surviving a death teaches resistance; surviving it twice, immunity. Any weakness
        // to that element is burned away.
        var id = $"scar_{cause}".ToLowerInvariant();
        var hadScar = n.Traits.Any(t => t.Id == id);
        n.Traits.RemoveAll(t => t.Id == id || (t.Kind == TraitKind.Weakness && t.Affinities?.ContainsKey(cause) == true));
        n.Traits.Add(TraitCatalog.ScarResistance(cause, hadScar ? Affinity.Immune : Affinity.Resist));

        if (Names.ScarEpithets.TryGetValue(cause, out var epithets) && epithets.Count > 0)
            n.Epithet = _rng.Pick(epithets);
    }

    string MakeName()
    {
        for (var i = 0; i < 20; i++)
        {
            var name = _rng.Pick(Names.FirstNames) + _rng.Pick(Names.NameSuffixes.Count > 0 ? Names.NameSuffixes : [""]);
            if (Living.All(n => n.Name != name)) return name;
        }
        return _rng.Pick(Names.FirstNames) + " " + (char)('A' + _rng.Next(26));
    }

    // ---- Power and promotion ----------------------------------------------------------------

    void GainPower(Deviant n, int power, int levels)
    {
        n.Power += power;
        n.Level = Math.Min(Rules.MaxLevel, n.Level + levels);
    }

    void UpdateHierarchy(List<WorldEvent> events)
    {
        // Strays with enough renown become ringleaders.
        foreach (var g in Strays.Where(g => g.Power >= 40).ToList())
        {
            if (UnassignedRingleaders.Count() >= Rules.MaxUnassignedRingleaders && !HasRingleaderVacancy()) break;
            Promote(g, Rank.Ringleader, events);
        }

        // Ringleaders under nobody look for a scourge with room.
        foreach (var c in UnassignedRingleaders.ToList())
        {
            var chief = Scourges.Where(w => RingleadersUnder(w.Id).Count() < Rules.MaxRingleadersPerScourge)
                .OrderBy(w => RingleadersUnder(w.Id).Count()).FirstOrDefault();
            if (chief is not null) c.ServesUnder = chief.Id;
        }

        // The strongest ringleaders rise when there's room at the top.
        while (Scourges.Count() < Rules.MaxScourges
               && Living.Where(n => n.Rank == Rank.Ringleader && n.Power >= Rules.PowerForScourge)
                   .OrderByDescending(n => n.Power).FirstOrDefault() is { } rising)
        {
            var oldChief = rising.ServesUnder;
            Promote(rising, Rank.Scourge, events);
            // Its old scourge's other ringleaders stay put; the new scourge starts with none.
            if (oldChief is not null) Log(rising, "Left its scourge's service to lead its own band.");
            if (oldChief is { } chiefId && Find(chiefId) is { Alive: true } chiefSworn) BloodOath(rising, chiefSworn, events);
        }

        if (Tyrant is null && Scourges.FirstOrDefault(w => w.Power >= Rules.PowerForTyrant) is { } heir)
        {
            Promote(heir, Rank.Tyrant, events);
            foreach (var c in RingleadersUnder(heir.Id).ToList()) c.ServesUnder = null; // they need a new chief
        }

        // With no vacancy, an ambitious ringleader challenges the weakest scourge.
        if (Scourges.Count() >= Rules.MaxScourges)
        {
            var weakest = Scourges.Last();
            var challenger = Living.Where(n => n.Rank == Rank.Ringleader && n.Power > weakest.Power + 20 && !Sworn(n, weakest))
                .OrderByDescending(n => n.Power).FirstOrDefault();
            if (challenger is not null && challenger.Rivals.Add(weakest.Id))
            {
                weakest.Rivals.Add(challenger.Id);
                events.Add(new(WorldEventKind.GrudgeStarted, challenger.Id,
                    $"{challenger.FullName} challenges Scourge {weakest.FullName} for its title.", weakest.Id));
            }
        }
    }

    /// <summary>Bound never to hunt each other.</summary>
    public static bool Sworn(Deviant a, Deviant b) => a.Oaths.Contains(b.Id) || a.PactSworn == b.Id || b.PactSworn == a.Id;

    /// <summary>An underling has risen: it and the one it rose under are bound by blood, and any quarrel between them is over.</summary>
    void BloodOath(Deviant risen, Deviant master, List<WorldEvent> events)
    {
        risen.Oaths.Add(master.Id);
        master.Oaths.Add(risen.Id);
        risen.Rivals.Remove(master.Id);
        master.Rivals.Remove(risen.Id);
        Log(risen, $"Swore a blood oath with {master.FullName}, who it rose under.");
        Log(master, $"Swore a blood oath with {risen.FullName}, who rose under it.");
        events.Add(new(WorldEventKind.Pact, risen.Id, $"{risen.FullName} and {master.FullName} are bound by a blood oath. They'll never hunt one another.", master.Id));
    }

    bool HasRingleaderVacancy() =>
        Scourges.Any(w => RingleadersUnder(w.Id).Count() < Rules.MaxRingleadersPerScourge);

    void Promote(Deviant n, Rank rank, List<WorldEvent> events)
    {
        var from = n.Rank;
        n.Rank = rank;
        if (rank >= Rank.Scourge) n.ServesUnder = null;
        AddTrait(n, TraitKind.Strength);
        Log(n, $"Promoted from {from} to {rank}.");
        events.Add(new(WorldEventKind.Promoted, n.Id, $"{n.FullName} has been promoted to {rank}!"));
    }

    /// <summary>
    /// Outside meddling (Vann whispering to a deviant): grants power and levels and lets the
    /// hierarchy react. Returns the resulting events.
    /// </summary>
    public IReadOnlyList<WorldEvent> Empower(Guid id, int power, string why)
    {
        var events = new List<WorldEvent>();
        if (Find(id) is not { Alive: true } n) return events;
        GainPower(n, power, levels: 1);
        Log(n, why);
        UpdateHierarchy(events);
        return events;
    }

    // ---- Time passing ------------------------------------------------------------------------

    /// <summary>
    /// The world moves while the party rests: grudges are fought, oaths sworn, titles change
    /// hands. Call it when the party saves, sleeps at an inn, or every N battles.
    /// </summary>
    public IReadOnlyList<WorldEvent> AdvanceDay()
    {
        Day++;
        var events = new List<WorldEvent>();
        var living = Living.Where(n => n.Rank >= Rank.Ringleader).ToList();

        if (living.Count >= 2 && _rng.Chance(Rules.GrudgeChancePerDay))
        {
            var a = _rng.Pick(living);
            var b = _rng.Pick(living.Where(x => x != a && !Sworn(a, x)).ToList() is { Count: > 0 } others ? others : [a]);
            if (a != b && a.Rivals.Add(b.Id))
            {
                b.Rivals.Add(a.Id);
                events.Add(new(WorldEventKind.GrudgeStarted, a.Id, $"{a.FullName} and {b.FullName} are now bitter rivals.", b.Id));
            }
        }

        if (living.Count >= 2 && _rng.Chance(Rules.PactChancePerDay))
        {
            var a = _rng.Pick(living);
            var b = living.FirstOrDefault(x => x != a && x.PactSworn is null && a.PactSworn is null && !a.Rivals.Contains(x.Id));
            if (b is not null)
            {
                a.PactSworn = b.Id;
                b.PactSworn = a.Id;
                Log(a, $"Swore a pact with {b.FullName}.");
                Log(b, $"Swore a pact with {a.FullName}.");
                events.Add(new(WorldEventKind.Pact, a.Id, $"{a.FullName} and {b.FullName} swore a pact.", b.Id));
            }
        }

        Ambition(events);

        foreach (var a in Living.ToList())
        {
            foreach (var rivalId in a.Rivals.ToList())
            {
                var b = Find(rivalId);
                if (b is null || !b.Alive) { a.Rivals.Remove(rivalId); continue; }
                if (a.Id.CompareTo(b.Id) > 0 || !_rng.Chance(Rules.GrudgeFightChancePerDay)) continue; // each pair once
                Duel(a, b, events);
            }
        }

        UpdateHierarchy(events);
        return events;
    }

    /// <summary>Nobody stays a stray by choice: they scheme, pick fights above them, and soldiers break away to try it themselves.</summary>
    void Ambition(List<WorldEvent> events)
    {
        var strays = Strays.ToList();
        foreach (var s in strays) GainPower(s, 1 + _rng.Next(Rules.AmbitionPerDay), levels: 0);
        if (strays.Count > 0 && _rng.Chance(Rules.StrayChallengePerDay))
        {
            var climber = _rng.Pick(strays);
            var above = Living.Where(n => n != climber && n.Rank <= Rank.Ringleader && !Sworn(climber, n) && !climber.Rivals.Contains(n.Id)).ToList();
            if (above.Count > 0)
            {
                var target = _rng.Pick(above);
                climber.Rivals.Add(target.Id);
                target.Rivals.Add(climber.Id);
                events.Add(new(WorldEventKind.GrudgeStarted, climber.Id, $"{climber.FullName} has picked a fight with {target.FullName}, and means to climb over them.", target.Id));
            }
        }
        if (Living.ToList() is { Count: > 0 } all && _rng.Chance(Rules.BreakawayPerDay))
        {
            var from = _rng.Pick(all);
            var n = new Deviant
            {
                Id = _rng.NextGuid(), Name = MakeName(), Kind = from.Kind, BaseForm = from.BaseForm, Species = from.Species,
                Rank = Rank.Stray, Level = 1, HomeArea = from.HomeArea, Power = 10,
            };
            AddTrait(n, TraitKind.Strength);
            AddTrait(n, TraitKind.Weakness);
            n.Epithet = n.Traits.Select(t => t.Epithet).FirstOrDefault(e => e is not null) ?? _rng.Pick(Names.Epithets);
            _deviants.Add(n);
            Log(n, $"Broke away from {from.FullName}'s soldiers to make a name of its own.");
            events.Add(new(WorldEventKind.Born, n.Id, $"{n.FullName} has broken away from {from.FullName}'s band. Another stray, looking to climb."));
            BloodOath(n, from, events);
        }
    }

    void Duel(Deviant a, Deviant b, List<WorldEvent> events)
    {
        double Score(Deviant n) => n.Power + n.Level * 5 + (int)n.Rank * 30 + _rng.Next(60);
        var (winner, loser) = Score(a) >= Score(b) ? (a, b) : (b, a);
        winner.Rivals.Remove(loser.Id);
        loser.Rivals.Remove(winner.Id);
        GainPower(winner, 30, levels: 1);

        // Taking a scourge's place: the winner inherits the title.
        if (loser.Rank > winner.Rank)
        {
            (winner.Rank, loser.Rank) = (loser.Rank, winner.Rank);
            winner.ServesUnder = null;
            foreach (var c in Living.Where(c => c.ServesUnder == loser.Id).ToList()) c.ServesUnder = winner.Id;
            loser.ServesUnder = winner.Rank == Rank.Scourge && RingleadersUnder(winner.Id).Count() < Rules.MaxRingleadersPerScourge ? winner.Id : null;
            events.Add(new(WorldEventKind.Succession, winner.Id, $"{winner.FullName} took {loser.FullName}'s place as {winner.Rank}!", loser.Id));
        }

        if (_rng.Chance(loser.Rank <= Rank.Ringleader ? 0.5 : 0.25))
        {
            loser.Alive = false;
            Log(loser, $"Killed settling a grudge with {winner.FullName}.");
            Log(winner, $"Killed its rival {loser.FullName}.");
            events.Add(new(WorldEventKind.GrudgeWon, winner.Id, $"{winner.FullName} killed its rival {loser.FullName}.", loser.Id));
        }
        else
        {
            loser.Memory |= Memory.Humiliated;
            loser.Power = Math.Max(0, loser.Power - 20);
            AddScar(loser, DamageKind.Physical);
            Log(loser, $"Beaten and humiliated by {winner.FullName}.");
            Log(winner, $"Humiliated its rival {loser.FullName}.");
            events.Add(new(WorldEventKind.GrudgeLost, loser.Id, $"{loser.FullName} was humiliated by {winner.FullName}.", winner.Id));
        }
    }

    // ---- Helpers and persistence --------------------------------------------------------------

    void Log(Deviant n, string text) => n.History.Add(new HistoryEntry(Day, text));

    static string Party(BattleReport r) => r.Players.Count switch
    {
        0 => "the party",
        1 => r.Players[0],
        _ => string.Join(", ", r.Players.Take(r.Players.Count - 1)) + " and " + r.Players[^1],
    };

    public sealed record State(int Version, int Day, ulong RngState, List<Deviant> Deviants);

    public State Snapshot() => new(SaveVersion, Day, _rng.State, _deviants);

    public static DeviantWorld Restore(State state, DeviantRules? rules = null, NamePack? names = null)
    {
        if (state.Version > SaveVersion)
            throw new InvalidDataException($"Deviant save version {state.Version} is newer than this mod ({SaveVersion}).");
        return new DeviantWorld(new Rng(state.RngState), state.Deviants, state.Day, rules, names);
    }

    public string ToJson() => DeviantJson.Serialize(Snapshot());

    public static DeviantWorld FromJson(string json, DeviantRules? rules = null, NamePack? names = null) =>
        Restore(DeviantJson.Deserialize<State>(json), rules, names);
}
