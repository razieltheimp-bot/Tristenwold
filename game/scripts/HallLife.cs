using Godot;
using Tristinwold.Gameplay;
using Tristinwold.World;

namespace Tristinwold.Game;

/// <summary>
/// Halls heroes found (see <see cref="Hall"/>): a rogue's thieves' guild, a ranger's hunting lodge, a barbarian's fight
/// club, a martial artist's monastery, a paladin's chapel, a mage's spire, a bard's playhouse. A hero founds one where
/// they stand, takes people on under them, and the shared world keeps it working whether they're on or not; any other
/// hero who finds it can do business there.
/// </summary>
public partial class Main
{
    readonly Dictionary<string, (Node3D Root, Person Steward, int Members)> _hallFronts = [];
    const float HallsDrawnWithin = 300, HallYard = 17, HallRing = 12.5f;

    /// <summary>The people the shared world puts about the place: other heroes' shopkeepers and the stewards of every hall.</summary>
    IEnumerable<Person> SharedFolk => _storeFronts.Values.Select(f => f.Keeper).Concat(_hallFronts.Values.Select(f => f.Steward)).Where(IsInstanceValid);

    /// <summary>What a hero of this class can found, and theirs if they have.</summary>
    HallPlan? MyHallPlan => Halls.For(_player.Class.Id);

    /// <summary>What the pack can start for a founder and for a druid.</summary>
    IEnumerable<(string, Action)> HallPack()
    {
        if (MyHallPlan is { } plan && _city.HallOf(_player.Company, plan.Kind) is null)
            yield return ($"Found your own {plan.Word} here ({plan.FoundCost} crowns, from level 5)", () => FoundHall(plan));
        yield return ($"Found {(_city.ColoniesOf(_player.Company).Any() ? "another" : "a")} colony here ({Halls.PlanOf(HallKind.Colony).FoundCost} crowns, from level 5; it wants open land, as far from any town as Tristinwold is from its neighbours)", () => FoundHall(Halls.PlanOf(HallKind.Colony)));
        if (Groves.MayPlant(_player.Class.Id)) yield return ($"Plant a tree here ({Groves.Mana:0} mana)", () => { _hud.EndTalk(); PlantTree(); });
    }

    /// <summary>Why a hall can't stand here, if it can't: it wants open land of its own, out of the towns.</summary>
    /// <summary>Whether this is an island out in the ocean that nobody lives on.</summary>
    bool UninhabitedIsland(Vector3 at) => Northwood.Island(at.X, at.Z) > 0.5 && _world.Height(at.X, at.Z) > WorldGen.WaterLevel + 0.3 && !_world.SettlementsNear(at.X, at.Z, 500).Any();

    /// <summary>How much room a colony wants: as far from any town as Tristinwold is from the nearer of its two neighbours.</summary>
    double ColonySpacing => new[] { "city:druitt", "city:greywater" }.Select(_world.Find).OfType<Settlement>().Select(s => Math.Sqrt(s.X * s.X + s.Z * s.Z)).DefaultIfEmpty(1200).Min();

    /// <summary>Why a colony can't be founded here, if it can't: too near a town, a city or another colony, or no dry land round it.</summary>
    string? NoRoomForColony(Vector3 at)
    {
        if (_indoors is not null || _cave is not null || _atSea is not null || _isle is not null || City.Afloat is not null) return "Not here. A colony wants open land under it.";
        for (var a = 0; a < 8; a++)
        {
            var probe = at + new Vector3(Mathf.Cos(a * Mathf.Tau / 8), 0, Mathf.Sin(a * Mathf.Tau / 8)) * HallYard;
            if (_world.Height(probe.X, probe.Z) <= WorldGen.WaterLevel + 0.3) return "Too near the water. A colony wants dry land all round it.";
        }
        var room = ColonySpacing;
        if (Math.Sqrt(at.X * at.X + at.Z * at.Z) < room || _world.SettlementsNear(at.X, at.Z, room).Any(s => s.Kind is SettlementKind.City or SettlementKind.Town))
            return $"Too near a town. A colony wants {room:0} m of open country between it and any town or city.";
        if (_city.Halls.Values.Any(h => h.Kind == HallKind.Colony && Math.Sqrt((h.X - at.X) * (h.X - at.X) + (h.Z - at.Z) * (h.Z - at.Z)) < room))
            return $"Too near someone else's colony. Give it {room:0} m.";
        return _city.HallRoom(at.X, at.Z) ? null : "Someone's settled here already.";
    }

    string? NoRoomForHall(Vector3 at, bool colony = false)
    {
        if (colony) return NoRoomForColony(at);
        if (_indoors is not null || _cave is not null || _atSea is not null || _isle is not null || City.Afloat is not null) return "Not here. A hall wants open land under it.";
        if (new Vector2(at.X, at.Z).Length() < City.OuterRadius + 60 || TownHere(at) is not null || _world.SettlementAt(at.X, at.Z) is not null) return "Not in a town. Find open land of your own, out past the walls.";
        for (var a = 0; a < 8; a++)
        {
            var probe = at + new Vector3(Mathf.Cos(a * Mathf.Tau / 8), 0, Mathf.Sin(a * Mathf.Tau / 8)) * HallYard;
            if (_world.Height(probe.X, probe.Z) <= WorldGen.WaterLevel + 0.3) return "Too near the water. A hall wants dry land all round it.";
        }
        return _city.HallRoom(at.X, at.Z) ? null : "Too near someone else's hall. Give it some room.";
    }

    void FoundHall(HallPlan plan)
    {
        _hud.EndTalk();
        var at = _player.GlobalPosition;
        if (_player.Hero.Level < 5) { Sound.Ui("error"); _hud.Toast($"Nobody follows a beginner. Make a name first (level 5), then found your {plan.Word}.", 6); return; }
        if (_player.Crowns < plan.FoundCost) { Sound.Ui("error"); _hud.Toast($"A {plan.Word} costs {plan.FoundCost} crowns to set up: timber, a roof, and a first {plan.Member}'s wages.", 6); return; }
        if (NoRoomForHall(at, plan.Kind == HallKind.Colony) is { } why) { Sound.Ui("error"); _hud.Toast(why, 6); return; }
        _player.Crowns -= plan.FoundCost;
        var number = plan.Kind == HallKind.Colony ? _city.NextColony(_player.Company) : 0; // as many colonies as a hero can find room for
        var hall = new Hall { Owner = _player.Company, Hero = _player.Company, Kind = plan.Kind, Name = plan.Kind == HallKind.Colony ? Halls.ColonyName(_player.Company, number) : $"{_player.Company}'s {plan.Word}", X = at.X, Z = at.Z, Number = number };
        DoCity(CityAction.HallFound(hall));
        Sound.Ui("quest");
        WellDone(2);
        _hud.Toast(plan.Kind == HallKind.Colony ? $"You've founded {hall.Name}, and you sit on its council. Your first settler's already cutting timber. Talk to your reeve to bring more out." : $"You've founded {hall.Name}. Your first {plan.Member} is already sweeping the yard. Talk to your {plan.Steward} there to take on more.", 10);
        Save();
        TendHallFronts();
    }

    /// <summary>Every hall near the hero, built: its buildings round a fenced yard, its sign, its steward, and its people about the place.</summary>
    void TendHallFronts()
    {
        var pos = _player.GlobalPosition;
        foreach (var key in _hallFronts.Keys.ToList())
            if (!_city.Halls.TryGetValue(key, out var h) || h.Members != _hallFronts[key].Members || new Vector2((float)h.X - pos.X, (float)h.Z - pos.Z).Length() > HallsDrawnWithin * 1.2f)
            {
                _hallFronts[key].Root.QueueFree();
                _hallFronts.Remove(key);
            }
        foreach (var h in _city.Halls.Values.Where(h => !_hallFronts.ContainsKey(h.Key)))
        {
            var centre = new Vector3((float)h.X, 0, (float)h.Z);
            if (new Vector2(centre.X - pos.X, centre.Z - pos.Z).Length() > HallsDrawnWithin || !_terrain.HasGround(centre)) continue;
            centre.Y = City.FloorAt(centre);
            var root = new Node3D { Name = $"Hall_{h.Owner.GetHashCode():x}" };
            AddChild(root);
            HallCompound(root, h, centre);
            var plan = h.Plan;
            var spot = centre + new Vector3(2.5f, 0, 2.5f);
            var steward = new Person
            {
                Npc = new Npc($"hall:{h.Key}", $"{h.Hero}'s {plan.Steward}", Sex.Other, 40, $"{plan.Steward} of {h.Name}", "city:tristinwold", h.Name,
                    $"Keeps {h.Name} for {h.Hero}, in or out.", ["the hall", "trade"], "warm", $"Welcome to {h.Name}.", spot.X, spot.Z),
                Look = plan.Look, Position = spot with { Y = City.FloorAt(spot) },
            };
            root.AddChild(steward);
            _hallFronts[h.Key] = (root, steward, h.Members);
        }
    }

    /// <summary>A hall: its buildings round the yard with their doors in, a fence with an open gate, the sign, and what its people do in the yard.</summary>
    void HallCompound(Node3D root, Hall h, Vector3 centre)
    {
        var plan = h.Plan;
        var rng = new Tristinwold.Deviant.Rng((ulong)WorldGen.StableHash(h.Key) ^ 0x4A11);
        var gate = Mathf.Atan2(-centre.Z, -centre.X); // facing home
        var roof = rng.Chance(0.5) ? Surface.Slate : Surface.Shingle;
        // A colony has its tavern from the start, and what it's grown into since (see Settling).
        var kinds = plan.Buildings.ToList();
        if (h.Kind == HallKind.Colony)
        {
            var grown = _city.GrownOf(h.Town);
            kinds.Insert(0, "tavern");
            if (grown >= Settling.Market) kinds.Add("store");
            if (grown >= Settling.Chamber) kinds.Add("longhall");
            if (grown >= Settling.Walls) kinds.AddRange(["house", "house"]);
        }
        // Round the yard, each building given as much of the ring as it's wide (and a gap), so none runs into the next;
        // the ring and the fence let out as a colony grows, rather than its buildings crowding into each other.
        var (ring, yard, angles) = HallLayout(kinds, roof);
        var cursor = gate + angles[^1] / 2; // the last is the gap left for the gate
        for (var i = 0; i < kinds.Count; i++)
        {
            var a = cursor + angles[i] / 2;
            cursor += angles[i];
            var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring;
            Buildings.Place(root, kinds[i], at with { Y = City.FloorAt(at) }, Mathf.Atan2(centre.X - at.X, centre.Z - at.Z), i + 7, roof, inside: kinds[i] == "longhall" && h.Kind == HallKind.Colony ? "council" : null);
        }
        var panels = (int)(Mathf.Tau * yard / FencePanel);
        for (var i = 0; i < panels; i++)
        {
            var a = gate + (i + 0.5f) / panels * Mathf.Tau;
            if (Math.Abs(Mathf.Wrap(a - gate, -Mathf.Pi, Mathf.Pi)) < 0.26f) continue;
            var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * yard;
            Models.Place(root, "medieval/neutral/fence_wood_straight.gltf", at with { Y = City.FloorAt(at) }, 1.8f * yard / HallYard * (int)(Mathf.Tau * HallYard / FencePanel) / panels, -a + Mathf.Pi / 2);
        }
        var gateAt = centre + new Vector3(Mathf.Cos(gate), 0, Mathf.Sin(gate)) * (yard + 1.5f);
        root.AddChild(new Label3D { Text = h.Name, Position = gateAt with { Y = City.FloorAt(gateAt) + 3.4f }, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12, PixelSize = 0.012f, Modulate = new Color("#ffd27a") });
        if (h.Kind == HallKind.FightClub) // the pit: a ring of posts and rope in the middle of the yard
            for (var i = 0; i < 12; i++)
            {
                var a = i * Mathf.Tau / 12;
                var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5.5f;
                root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.1f, Height = 1.2f }, MaterialOverride = BuildingMaterials.For(Surface.Timber), Position = at with { Y = City.FloorAt(at) + 0.6f } });
            }
        if (h.Kind == HallKind.Chapel || h.Kind == HallKind.Monastery)
            root.AddChild(new OmniLight3D { Position = centre + Vector3.Up * 3, LightColor = new Color("#ffe8b0"), LightEnergy = 0.7f, OmniRange = 14 });
        // Its people, about the yard.
        for (var i = 0; i < Math.Min(h.Members, 8); i++)
        {
            var a = gate + 0.6f + i * Mathf.Tau / Math.Max(4, Math.Min(h.Members, 8));
            var at = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (h.Kind == HallKind.FightClub ? 7 : 4 + i % 3 * 1.5f);
            var body = Body.For(plan.Look);
            root.AddChild(body);
            body.GlobalPosition = at with { Y = City.FloorAt(at) };
            body.Rotation = new Vector3(0, Mathf.Atan2(centre.X - at.X, centre.Z - at.Z), 0);
        }
    }

    /// <summary>
    /// How a hall's buildings go round its yard: the ring their middles stand on, the fence round the lot, and the share
    /// of the circle each one takes (as wide as it is at its inner face, plus a gap), the gate's gap last. The ring lets out
    /// till they all fit.
    /// </summary>
    public static (float Ring, float Yard, float[] Angles) HallLayout(IReadOnlyList<string> kinds, Surface roof)
    {
        const float gap = 3f, gateWidth = 9f;
        var sizes = kinds.Select((k, i) => Buildings.Extent(k, i + 7, roof)).ToList();
        var ring = HallRing;
        float[] angles;
        while (true)
        {
            // Each takes the angle its inner corners need (they're the ones that meet), with nothing nearer the middle than the yard's heart.
            var inner = sizes.Select(s => ring - s.Front).ToList();
            angles = [.. sizes.Select((s, i) => 2 * Mathf.Atan((s.HalfWidth + gap / 2) / Mathf.Max(1, inner[i]))), 2 * Mathf.Atan(gateWidth / 2 / Mathf.Max(1, ring))];
            if (angles.Sum() <= Mathf.Tau && inner.All(r => r >= 6.5f) || ring > 80) break;
            ring += 0.5f;
        }
        var spare = (Mathf.Tau - angles.Sum()) / angles.Length;
        for (var i = 0; i < angles.Length; i++) angles[i] += spare;
        var yard = Mathf.Max(HallYard, ring + sizes.Select(s => s.Back).DefaultIfEmpty(0).Max() + 2.5f);
        return (ring, yard, angles);
    }

    /// <summary>What a hall's steward can do: business for its founder, and its service for anyone.</summary>
    IEnumerable<(string, Action)> HallTalk(Person person)
    {
        if (!person.Npc.Id.StartsWith("hall:") || !_city.Halls.TryGetValue(person.Npc.Id["hall:".Length..], out var h)) yield break;
        var plan = h.Plan;
        var mine = h.Owner == _player.Company;
        if (mine)
        {
            yield return ("How do we stand?", () => Say(person, HallReport(h)));
            if (h.Kind == HallKind.Colony)
            {
                foreach (var o in GrowthOptions(person, h.Town, h.Name)) yield return o; // building the colony up
                if (_story.Home?.Id != h.Town)
                    yield return ($"Make {h.Name} my hometown.", () => { _story.Home = new Hometown(h.Town, h.Name, h.X, h.Z); Settled(h.Name, bar: false); });
            }
            if (h.Members < Halls.MostMembers(_player.Hero.Level))
                yield return ($"Take on another {plan.Member} ({plan.RecruitCost} crowns)", () => Recruit(person, h));
            if (h.Till > 0) yield return ($"Empty the till ({h.Till} crowns)", () =>
            {
                var take = h.Till;
                DoCity(CityAction.HallTill(h.Key, take));
                _player.Crowns += take;
                Sound.Ui("coin");
                Save();
                Say(person, $"{take} crowns. Don't spend it all at once.");
            });
            if (h.Chest.Count > 0 && h.Kind is HallKind.HuntingLodge or HallKind.Spire or HallKind.Colony) yield return ("Take what's in the stores", () =>
            {
                foreach (var (id, n) in h.Chest.ToList())
                    for (var i = 0; i < n; i++) { DoCity(CityAction.HallTake(h.Key, id, 0)); _player.Pack.Add(id); }
                Sound.Ui("pickup");
                Save();
                Say(person, "All yours. There'll be more by the end of the week.");
            });
            if (h.Kind == HallKind.ThievesGuild && _city.Day >= h.LowUntil) yield return ("Lie low for a day", () =>
            {
                DoCity(CityAction.HallLow(h.Key));
                Save();
                Say(person, "Doors shut, hoods down. The Watch will have forgotten us by tomorrow.");
            });
            yield return ($"Shut {h.Name} down for good", () => _hud.Talk(h.Name, $"Close it for good? The {plan.Members} go their own ways, and whatever's in the till goes with them.",
                ("Yes, shut it down.", () => { DoCity(CityAction.HallClose(h.Key)); Save(); _hud.EndTalk(); _hud.Toast($"{h.Name} is no more.", 6); TendHallFronts(); }),
                ("No.", () => Say(person, "Thought not."))));
        }
        if (HallService(person, h, mine) is { } service) yield return service;
    }

    string HallReport(Hall h)
    {
        var plan = h.Plan;
        var lines = new List<string> { $"{h.Members} {(h.Members == 1 ? plan.Member : plan.Members)}, room for {Halls.MostMembers(_player.Hero.Level)}. {h.Till} crowns in the till." };
        if (h.Kind == HallKind.ThievesGuild) lines.Add(_city.Day < h.LowUntil ? "We're lying low." : h.Heat > Halls.CaughtAt ? "The Watch is hot on us. Lie low, or lose someone." : "The Watch hasn't noticed us.");
        if (h.Kind is HallKind.FightClub or HallKind.Monastery) lines.Add($"Our {(h.Kind == HallKind.FightClub ? "fighters" : "teaching")}: {h.Prowess:0.0} of 10.");
        if (h.Chest.Count > 0) lines.Add("In the stores: " + string.Join(", ", h.Chest.Select(kv => Items.Count(kv.Key, kv.Value))) + ".");
        return string.Join(" ", lines);
    }

    void Recruit(Person person, Hall h)
    {
        var plan = h.Plan;
        if (_player.Crowns < plan.RecruitCost) { Sound.Ui("error"); Say(person, $"Nobody works for nothing. {plan.RecruitCost} crowns."); return; }
        _player.Crowns -= plan.RecruitCost;
        DoCity(CityAction.HallRecruit(h.Key, _player.Hero.Level));
        Sound.Ui("coin");
        Save();
        TendHallFronts();
        Say(person, $"Found one. They start in the morning. That's {h.Members} of us.");
    }

    /// <summary>What a hall does for anyone who walks in, if it does anything today.</summary>
    (string, Action)? HallService(Person person, Hall h, bool mine)
    {
        var plan = h.Plan;
        var price = mine ? 0 : plan.ServicePrice;
        var cost = price > 0 ? $" ({price} crowns)" : "";
        var today = h.Served.GetValueOrDefault(_player.Company, -1) == _city.Day;
        switch (h.Kind)
        {
            case HallKind.ThievesGuild:
                if (mine) return null; // its own founder sends them out by running it
                return ($"{plan.Service}{cost}", () => ThievesJob(person, h));
            case HallKind.HuntingLodge or HallKind.Spire or HallKind.Colony:
                return mine ? null : ($"{plan.Service}", () => HallShelves(person, h));
            case HallKind.FightClub:
                return ($"{plan.Service}{cost}", () =>
                {
                    if (!PaidAt(person, h, price)) return;
                    StartPitFight(person, h);
                });
            default:
                if (today) return ($"{plan.Service}", () => Say(person, "Once a day. Come back tomorrow."));
                return ($"{plan.Service}{cost}", () =>
                {
                    if (!PaidAt(person, h, price)) return;
                    DoCity(CityAction.HallServed(h.Key, _player.Company));
                    switch (h.Kind)
                    {
                        case HallKind.Monastery:
                            var xp = Halls.Lesson(h, _player.Hero.Level);
                            GainXp(xp);
                            _player.Stats.Restore(1e9, 1e9);
                            Say(person, $"A morning of forms with the disciples, and a hard lesson in the afternoon. (+{xp} experience)");
                            break;
                        case HallKind.Chapel:
                            _player.Stats.Heal(1e9);
                            _player.Bless(600, Tristinwold.Deviant.AutoStatus.Protect, Tristinwold.Deviant.AutoStatus.Regen);
                            if (_forest.Blighted) { _forest.Blighted = false; _hud.Toast("The blight goes out of you.", 5); }
                            Say(person, "Go with the light. It'll keep you a while: Protect and Regen, for ten minutes.");
                            break;
                        default:
                            _pursuits.RestedUntil = _guilds.PlaySeconds + Housing.RestedSeconds;
                            Say(person, "You laughed in all the right places. Go on: you'll sleep the better for it, and learn the better tomorrow. (Well rested for half an hour.)");
                            break;
                    }
                    Save();
                });
        }
    }

    /// <summary>Pays a hall for its service, into its till: false if the hero can't.</summary>
    bool PaidAt(Person person, Hall h, int price)
    {
        if (price <= 0) return true;
        if (_player.Crowns < price) { Sound.Ui("error"); Say(person, $"That's {price} crowns, and you haven't got it."); return false; }
        _player.Crowns -= price;
        DoCity(CityAction.HallPay(h.Key, price));
        Sound.Ui("coin");
        return true;
    }

    /// <summary>Another hero's thieves, for hire: the stores of a town the wagons reach, robbed, and a cut for whoever paid.</summary>
    void ThievesJob(Person person, Hall h)
    {
        if (h.Members < 2) { Say(person, "There's not enough of us yet for that kind of work."); return; }
        if (_city.Day < h.LowUntil) { Say(person, "We're lying low. Try tomorrow."); return; }
        var cut = Math.Min(300, 30 * h.Members);
        var options = JobTowns.Select(town => ($"Rob the stores at {town.Name} ({h.Plan.ServicePrice} crowns; your cut about {cut})", (Action)(() =>
        {
            if (!PaidAt(person, h, h.Plan.ServicePrice)) return;
            DoCity(CityAction.Plundered(town.Id));
            _player.Crowns += cut;
            if (_lootRng.NextDouble() < 0.15) { Repute.Deed(-3, town.Id); _hud.Toast($"Word got round {town.Name} who paid for it.", 6); }
            Save();
            Say(person, $"In and out of {town.Name} before the lamps were lit. Your cut: {cut} crowns.");
        }))).ToList();
        options.Add(("Not today.", () => Say(person, "You know where we are.")));
        _hud.Talk($"{h.Name} ({_player.Crowns} crowns)", "Crowns first. We don't ask why.", [.. options]);
    }

    /// <summary>What a lodge's hunters or a spire's apprentices have brought in, for anyone to buy, a crown under the shops in town.</summary>
    void HallShelves(Person person, Hall h)
    {
        var options = h.Chest.Where(kv => kv.Value > 0 && Items.Get(kv.Key) is not null).OrderBy(kv => kv.Key).Select(kv =>
        {
            var item = Items.Get(kv.Key)!;
            var price = _city.StorePrice(item);
            return ($"{item.Name} ({kv.Value}), {price} crowns: {item.Text}", (Action)(() =>
            {
                if (_player.Crowns < price) { Sound.Ui("error"); _hud.Toast("You can't afford that.", 3); return; }
                DoCity(CityAction.HallTake(h.Key, kv.Key, price));
                _player.Crowns -= price;
                _player.Pack.Add(kv.Key);
                Sound.Ui("coin");
                Save();
                HallShelves(person, h);
            }));
        }).ToList();
        options.Add(("That's all.", () => Say(person, "Come again.")));
        _hud.Talk($"{h.Name} ({_player.Crowns} crowns)", options.Count == 1 ? "Nothing in just now. Give them a day." : "A crown under what you'd pay in town.", [.. options]);
    }

    /// <summary>A bout in a fight club's pit, against its champion: to the yield, and the purse comes out of the till.</summary>
    void StartPitFight(Person steward, Hall h)
    {
        if (_duel is not null) return;
        _hud.EndTalk();
        var (health, damage) = Halls.Champion(h, _player.Hero.Level);
        var centre = new Vector3((float)h.X, 0, (float)h.Z);
        var foe = Enemy.Create(Bestiary.Get("bandit_chief"), centre with { Y = City.FloorAt(centre) + 0.3f }, look: "warmaster");
        foe.Spar($"{h.Hero}'s champion", health, damage);
        AddEnemy(foe);
        foe.Provoke(_player);
        _duel = (foe, steward, won =>
        {
            if (!won) { _hud.Toast("You go down in the sawdust, and the crowd loves it.", 6); return; }
            var purse = Math.Min(Halls.PitPurse(h), h.Owner == _player.Company ? 0 : h.Till);
            if (purse > 0) { DoCity(CityAction.HallTill(h.Key, purse)); _player.Crowns += purse; }
            GainXp(25 + 6 * _player.Hero.Level);
            _hud.Toast(purse > 0 ? $"The champion yields! The purse is yours: {purse} crowns." : "The champion yields, and the yard roars.", 7);
        });
        _player.Face(centre);
        _hud.Toast("Into the pit! First to yield loses.", 5);
    }

    /// <summary>A pit fight over: the champion steps out of the ring, and the steward stands where they did.</summary>
    void HallDuelOver(Enemy foe) =>
        GetTree().CreateTimer(2.5).Timeout += () => { if (IsInstanceValid(foe)) foe.QueueFree(); };

    // ---- the druids' planting ----------------------------------------------------------------------

    readonly Dictionary<string, (Node3D Body, int Stage)> _shownGrove = [];

    static string Spot(Planted p) => FormattableString.Invariant($"{p.X:0.00},{p.Z:0.00}");
    const float GroveDrawnWithin = 220;

    void PlantTree()
    {
        var at = _player.GlobalPosition;
        if (_indoors is not null || _cave is not null || _atSea is not null || City.Afloat is not null) { Sound.Ui("error"); _hud.Toast("A tree wants the open sky and earth under it.", 5); return; }
        if (_world.Height(at.X, at.Z) <= WorldGen.WaterLevel + 0.3) { Sound.Ui("error"); _hud.Toast("Not in the water.", 4); return; }
        if (!_city.RoomToPlant(_player.Company, at.X, at.Z)) { Sound.Ui("error"); _hud.Toast("There's no room for another here, or you've planted all the land will take from one druid just now.", 6); return; }
        if (!_player.Stats.Spend(AttackKind.Spell, Groves.Mana)) { Sound.Ui("error"); _hud.Toast("You haven't the mana for it. Rest a little.", 4); return; }
        DoCity(CityAction.Plant(_player.Company, at.X, at.Z));
        Effects.Burst(this, at + Vector3.Up * 0.3f, Tristinwold.Deviant.DamageKind.Poison, 1.2f);
        Sound.Ui("equip", -6);
        var mended = Northwood.Inside(at.X, at.Z) && _forest.TreesCut > 0;
        if (mended) _forest.TreesCut--;
        WellDone(1);
        _hud.Toast(mended ? "A sapling stirs out of the earth. The wood feels it: one tree you cut is made good." : "A sapling stirs out of the earth under your hands. It'll be a young tree tomorrow and grown in three days.", 6);
        Save();
        TendGrove();
    }

    /// <summary>The druids' trees near the hero, at the size they've grown to.</summary>
    void TendGrove()
    {
        var pos = _player.GlobalPosition;
        var day = _city.Day;
        var near = _city.Grove.Where(p => new Vector2((float)p.X - pos.X, (float)p.Z - pos.Z).Length() < GroveDrawnWithin).GroupBy(Spot).ToDictionary(g => g.Key, g => g.First());
        foreach (var spot in _shownGrove.Keys.Where(s => !near.TryGetValue(s, out var p) || _shownGrove[s].Stage != Groves.Stage(p, day)).ToList())
        {
            _shownGrove[spot].Body.QueueFree();
            _shownGrove.Remove(spot);
        }
        foreach (var (spot, p) in near.Where(kv => !_shownGrove.ContainsKey(kv.Key)))
        {
            var at = new Vector3((float)p.X, 0, (float)p.Z);
            if (!_terrain.HasGround(at)) continue;
            var body = Sapling(p, day);
            AddChild(body);
            body.GlobalPosition = at with { Y = City.FloorAt(at) };
            _shownGrove[spot] = (body, Groves.Stage(p, day));
        }
    }

    /// <summary>A planted tree as it stands today: a sapling, a young tree, or a grown one you can't walk through.</summary>
    static Node3D Sapling(Planted p, int day)
    {
        var size = (float)Groves.Size(p, day);
        var seed = (int)(WorldGen.StableHash($"{p.X:0.0},{p.Z:0.0}") % 1000);
        var body = new StaticBody3D { Rotation = new Vector3(0, seed * 0.37f, 0) };
        if (Foliage.Available)
            body.AddChild(new MeshInstance3D { Mesh = Foliage.Tree(seed % 3 == 0 ? Foliage.Kind.Fir : Foliage.Kind.Green, seed % Foliage.Variants, false), Scale = Vector3.One * size });
        else
        {
            body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f * size, BottomRadius = 0.25f * size, Height = 6 * size }, Position = Vector3.Up * 3 * size, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#5a4030") } });
            body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 2.4f * size, Height = 4 * size }, Position = Vector3.Up * 6.5f * size, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#3f6a32") } });
        }
        if (Groves.Stage(p, day) > 0)
            body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.3f * size + 0.1f, Height = 4 }, Position = Vector3.Up * 2 });
        return body;
    }

    /// <summary>For the self-test.</summary>
    public void TendHallsForTest() { TendHallFronts(); TendGrove(); }
    public void RedrawHallsForTest() { foreach (var f in _hallFronts.Values) f.Root.Free(); _hallFronts.Clear(); TendHallFronts(); }
    public Vector3 ColonyStallSpotForTest(Hall h) => ColonyStall(h).At;
    public int ShownGroveForTest => _shownGrove.Count;
    public IEnumerable<Person> SharedFolkForTest => SharedFolk;
    public void FoundHallForTest() { if (MyHallPlan is { } plan) FoundHall(plan); }
    public void PlantForTest() => PlantTree();
    public string? NoRoomForHallForTest(Vector3 at) => NoRoomForHall(at);
}
