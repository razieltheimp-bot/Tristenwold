using System.Text.Json;
using System.Text.Json.Serialization;
using Tristinwold.Deviant;

namespace Tristinwold.Shared;

/// <summary>
/// Everything that crosses the wire, one JSON object per line. Each friend runs their own
/// game with their own company; the server only shares the world between them.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
// client -> server
[JsonDerivedType(typeof(Hello), "hello")]
[JsonDerivedType(typeof(AccountRequest), "account_request")]
[JsonDerivedType(typeof(AccountReply), "account_reply")]
[JsonDerivedType(typeof(SaveProgress), "save_progress")]
[JsonDerivedType(typeof(Presence), "presence")]
[JsonDerivedType(typeof(ReportBattle), "report_battle")]
[JsonDerivedType(typeof(Rested), "rested")]
[JsonDerivedType(typeof(ClaimContract), "claim_contract")]
[JsonDerivedType(typeof(AbandonContract), "abandon_contract")]
[JsonDerivedType(typeof(FinishContract), "finish_contract")]
[JsonDerivedType(typeof(LeaveDrop), "leave_drop")]
[JsonDerivedType(typeof(Say), "say")]
[JsonDerivedType(typeof(EncounterStarted), "encounter_started")]
[JsonDerivedType(typeof(CityTrade), "city_trade")]
[JsonDerivedType(typeof(Moved), "moved")]
[JsonDerivedType(typeof(StrikeHero), "strike_hero")]
[JsonDerivedType(typeof(HeroFell), "hero_fell")]
[JsonDerivedType(typeof(SleepSpot), "sleep_spot")]
// server -> client
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(Rejected), "rejected")]
[JsonDerivedType(typeof(DeviantSnapshot), "deviant_snapshot")]
[JsonDerivedType(typeof(DeviantEvents), "deviant_events")]
[JsonDerivedType(typeof(Board), "board")]
[JsonDerivedType(typeof(Drops), "drops")]
[JsonDerivedType(typeof(Roster), "roster")]
[JsonDerivedType(typeof(Notice), "notice")]
[JsonDerivedType(typeof(Reward), "reward")]
[JsonDerivedType(typeof(KeystoneRevealed), "keystone_revealed")]
[JsonDerivedType(typeof(DeviantAppears), "deviant_appears")]
[JsonDerivedType(typeof(DeviantHunts), "deviant_hunts")]
[JsonDerivedType(typeof(CitySnapshot), "city_snapshot")]
[JsonDerivedType(typeof(Seen), "seen")]
[JsonDerivedType(typeof(HeroStruck), "hero_struck")]
[JsonDerivedType(typeof(HeroSpoils), "hero_spoils")]
[JsonDerivedType(typeof(Sleepers), "sleepers")]
[JsonDerivedType(typeof(WhileAway), "while_away")]
public abstract record Message;

// ---- client -> server ----------------------------------------------------------------------

/// <param name="Party">The three front-line characters' names, as this player has them.</param>
/// <param name="Account">Set when playing a character that belongs to an account; <paramref name="Token"/> proves the login.</param>
public sealed record Hello(string Player, string Password, int ProtocolVersion, IReadOnlyList<string> Party, string? Account = null, string? Token = null) : Message;

/// <summary>Sent whenever the group moves to another area or changes its line-up.</summary>
public sealed record Presence(string Area, IReadOnlyList<string> Party) : Message;

public sealed record ReportBattle(BattleReport Report) : Message;

/// <summary>The group saved or slept; the world advances a day.</summary>
public sealed record Rested : Message;

public sealed record ClaimContract(string ContractId) : Message;
public sealed record AbandonContract(string ContractId) : Message;
/// <summary>Only honoured when the server lets players close their own contracts.</summary>
public sealed record FinishContract(string ContractId) : Message;

public sealed record LeaveDrop(string Area, string Text) : Message;

public sealed record Say(string Text) : Message;

/// <summary>A fight just started in this company's game. The server says which enemies are deviants.</summary>
public sealed record EncounterStarted(string Area, IReadOnlyList<FormationSlot> Enemies) : Message;

/// <summary>Something this company did to Tristinwold's economy (sold, bought, delivered, gave): the server does it to the shared city.</summary>
public sealed record CityTrade(Gameplay.CityAction Action) : Message;

/// <summary>
/// Where this player's hero is and what they're doing, a few times a second while it changes, so friends on
/// the same server see them walk by. <paramref name="Look"/> and <paramref name="Avatar"/> say what to draw
/// (the class look and the character page's choices); <paramref name="Clip"/> is the last move performed (a
/// swing, a spell, a jump) and <paramref name="Beat"/> counts them, so each is played once. <paramref name="Hidden"/>:
/// a rogue in the shadows, whom nobody else's game draws.
/// </summary>
public sealed record Moved(string Look, string? Avatar, string Name, float X, float Y, float Z, float Yaw, float Speed, string? Clip, int Beat, bool Hidden = false) : Message;

/// <summary>
/// This hero's blow landed on another player's hero (any blow, any reason: there's no asking). The harm is worked
/// out here, before the target's own armour and luck; the target's game takes it as they would any other blow.
/// </summary>
public sealed record StrikeHero(string Target, double Damage, DamageKind Kind, Gameplay.AttackKind Attack, bool Crit) : Message;

/// <summary>
/// This hero was put down by another player: what the winner takes. Nothing leaves the loser: the winner gets a copy
/// of everything carried but the gear, the gear's worth in crowns instead (<paramref name="GearCrowns"/>), a purse as
/// full as the loser's (<paramref name="Crowns"/>), and any bounty on the loser's head.
/// </summary>
public sealed record HeroFell(string Killer, IReadOnlyList<SpoilItem> Items, int Crowns, int GearCrowns, int Bounty) : Message;

public sealed record SpoilItem(string Id, int N);

/// <summary>
/// Where this hero would lie if the player left now, and what's on them: sent now and then while it changes. When the
/// player goes, the server leaves their hero there asleep (see <see cref="Sleepers"/>), where it's this spot that
/// counts, not the last step seen: a hero indoors sleeps at the door they came in by, as their save has it.
/// <paramref name="Safe"/>: in a tavern, where nobody fights.
/// </summary>
public sealed record SleepSpot(float X, float Y, float Z, float Yaw, int Crowns, double MaxHealth, bool Safe) : Message;

// ---- server -> client ----------------------------------------------------------------------

public sealed record Welcome(string Player, string Campaign, IReadOnlyList<string> Flags) : Message;
public sealed record Rejected(string Reason) : Message;

/// <summary>The whole deviant world. Clients replace their copy with it.</summary>
public sealed record DeviantSnapshot(string WorldJson) : Message;
public sealed record DeviantEvents(IReadOnlyList<WorldEvent> Events) : Message;

public sealed record Board(IReadOnlyList<PublicContract> Contracts) : Message;

/// <summary>Notes waiting in an area, sent when a group arrives there.</summary>
public sealed record Drops(string Area, IReadOnlyList<DeadDropView> Notes) : Message;

public sealed record Roster(IReadOnlyList<GroupView> Groups) : Message;

public enum NoticeKind { Chat, Dm, NearMiss, World, Contract, Error, Vann }
public sealed record Notice(NoticeKind Kind, string Text, string? From = null) : Message;

/// <summary>Contract pay, in coppers (a hundred to the crown), for the group that finished it.</summary>
public sealed record Reward(string ContractId, int Coppers) : Message;

public sealed record KeystoneRevealed(string Player, string Character) : Message;

/// <summary>Answer to <see cref="EncounterStarted"/>: these enemies are deviants; rename and strengthen them.</summary>
public sealed record DeviantAppears(IReadOnlyList<DeviantSpawn> Spawns) : Message;

/// <summary>Vann sent a deviant after this company: spawn it nearby and let it find them.</summary>
public sealed record DeviantHunts(Guid DeviantId, string Kind, string Species, string BaseForm, BattleModifiers Modifiers, string? Taunt) : Message;

/// <summary>
/// Tristinwold's economy as everyone on the server shares it: stock, prices, the day, events, contracts, the works.
/// Clients replace their copy with it, keeping their own name at the Exchange (renown is each hero's own).
/// <paramref name="Seconds"/> is the city's own clock, so a game can tell how long until the next day.
/// </summary>
public sealed record CitySnapshot(Gameplay.CityEconomy City, double Seconds) : Message;

/// <summary>Another player's hero, where they were last seen. Sent to everyone else whenever a player moves, and to a newcomer for everyone already there.</summary>
public sealed record Seen(string Player, Moved Where) : Message;

/// <summary>Another player's hero struck this one: the target's game takes the blow, if it can.</summary>
public sealed record HeroStruck(string From, double Damage, DamageKind Kind, Gameplay.AttackKind Attack, bool Crit) : Message;

/// <summary>This hero put another player's hero down: what's theirs for it (see <see cref="HeroFell"/>).</summary>
public sealed record HeroSpoils(string From, IReadOnlyList<SpoilItem> Items, int Crowns, int GearCrowns, int Bounty) : Message;

/// <summary>
/// Every player who's gone, asleep where they left: drawn where they stand, shifting about a little, and there to be
/// struck by anyone ready to fight (B). Sent whole to a newcomer and to everyone whenever it changes.
/// </summary>
public sealed record Sleepers(IReadOnlyList<SleeperView> All) : Message;

/// <param name="Health">How much fight is left in them, 0 to 1.</param>
/// <param name="Fallen">Cut down: they lie where they fell until their player comes back.</param>
public sealed record SleeperView(string Player, string Name, string Look, string? Avatar, float X, float Y, float Z, float Yaw, double Health, bool Safe, bool Fallen);

/// <summary>
/// Back from being away: who went for this hero while they slept, and who cut them down and what that one won by it
/// (a share of the purse, as a copy: nothing left this hero). They wake where they lay all the same.
/// </summary>
public sealed record WhileAway(IReadOnlyList<string> Attackers, string? Killer, int Taken) : Message;

// ---- views (what players may see) ------------------------------------------------------------

public enum ContractStatus { Open, Claimed, Done }

/// <summary>A contract as players see it: no secret.</summary>
public sealed record PublicContract(string Id, string Title, string PostedBy, int Pay, string? Where, ContractStatus Status, string? ClaimedBy);

public sealed record DeadDropView(string From, string Text, int Day);

public sealed record GroupView(string Player, string Area, IReadOnlyList<string> Party, bool Online);

public static class Wire
{
    public const int ProtocolVersion = 14; // 2: the shared city; 3: deviants; 4: their new ranks; 5: players see each other; 6: the new map; 7: longer piers at Tristinwold, and Lu at the sieges; 8: the king's roads cut through the land; 9: spur roads to every trading place; 10: every town's own shops and council; 11: buildings moved apart, Korrin's storehouse, the dock warehouse; 12: council chambers in every town, refuges that grow, more dock thugs; 13: many colonies to a hero, and heroes fighting heroes; 14: heroes left asleep where their players go
    public const int DefaultPort = 7777;

    /// <summary>
    /// A server's refusal in plain words. A server too old to say which side is behind ("Version mismatch: server speaks
    /// 7, you sent 10. Update the mod.", worded from long ago) is read here and told the right way round.
    /// </summary>
    public static string Explain(string reason)
    {
        var m = System.Text.RegularExpressions.Regex.Match(reason, @"server speaks (\d+), you sent (\d+)");
        return m.Success ? Mismatch(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : reason;
    }

    /// <summary>What to tell a game whose version isn't the server's: which of the two is behind, and what to do about it.</summary>
    public static string Mismatch(int server, int game) => game > server
        ? $"This server is running an older version of Tristinwold than your game (the server is on {server}, your game on {game}). Whoever runs the server needs to update it, or join a server that's up to date."
        : $"Your game is older than this server (your game is on {game}, the server on {server}). Update the game: on Windows, start it again and let it update itself; on a phone or a Mac, download the new version.";
    public const int MaxLineBytes = 4 * 1024 * 1024; // a big deviant world is still well under this

    public static JsonSerializerOptions Options { get; } = new(DeviantJson.Options) { WriteIndented = false };

    public static string Encode(Message message) => JsonSerializer.Serialize(message, Options);

    public static Message Decode(string line) =>
        JsonSerializer.Deserialize<Message>(line, Options) ?? throw new JsonException("empty message");
}
