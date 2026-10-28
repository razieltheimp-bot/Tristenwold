using System.Text.Json;
using System.Text.Json.Serialization;
using Tristinwold.Nemesis;

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
// server -> client
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(Rejected), "rejected")]
[JsonDerivedType(typeof(NemesisSnapshot), "nemesis_snapshot")]
[JsonDerivedType(typeof(NemesisEvents), "nemesis_events")]
[JsonDerivedType(typeof(Board), "board")]
[JsonDerivedType(typeof(Drops), "drops")]
[JsonDerivedType(typeof(Roster), "roster")]
[JsonDerivedType(typeof(Notice), "notice")]
[JsonDerivedType(typeof(Reward), "reward")]
[JsonDerivedType(typeof(KeystoneRevealed), "keystone_revealed")]
[JsonDerivedType(typeof(NemesisAppears), "nemesis_appears")]
[JsonDerivedType(typeof(NemesisHunts), "nemesis_hunts")]
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

/// <summary>A fight just started in this company's game. The server says which enemies are nemeses.</summary>
public sealed record EncounterStarted(string Area, IReadOnlyList<FormationSlot> Enemies) : Message;

// ---- server -> client ----------------------------------------------------------------------

public sealed record Welcome(string Player, string Campaign, IReadOnlyList<string> Flags) : Message;
public sealed record Rejected(string Reason) : Message;

/// <summary>The whole nemesis world. Clients replace their copy with it.</summary>
public sealed record NemesisSnapshot(string WorldJson) : Message;
public sealed record NemesisEvents(IReadOnlyList<WorldEvent> Events) : Message;

public sealed record Board(IReadOnlyList<PublicContract> Contracts) : Message;

/// <summary>Notes waiting in an area, sent when a group arrives there.</summary>
public sealed record Drops(string Area, IReadOnlyList<DeadDropView> Notes) : Message;

public sealed record Roster(IReadOnlyList<GroupView> Groups) : Message;

public enum NoticeKind { Chat, Dm, NearMiss, World, Contract, Error, Vann }
public sealed record Notice(NoticeKind Kind, string Text, string? From = null) : Message;

/// <summary>Contract pay, in coppers (a hundred to the crown), for the group that finished it.</summary>
public sealed record Reward(string ContractId, int Coppers) : Message;

public sealed record KeystoneRevealed(string Player, string Character) : Message;

/// <summary>Answer to <see cref="EncounterStarted"/>: these enemies are nemeses; rename and strengthen them.</summary>
public sealed record NemesisAppears(IReadOnlyList<NemesisSpawn> Spawns) : Message;

/// <summary>Vann sent a nemesis after this company: spawn it nearby and let it find them.</summary>
public sealed record NemesisHunts(Guid NemesisId, string Kind, string Species, string BaseForm, BattleModifiers Modifiers, string? Taunt) : Message;

// ---- views (what players may see) ------------------------------------------------------------

public enum ContractStatus { Open, Claimed, Done }

/// <summary>A contract as players see it: no secret.</summary>
public sealed record PublicContract(string Id, string Title, string PostedBy, int Pay, string? Where, ContractStatus Status, string? ClaimedBy);

public sealed record DeadDropView(string From, string Text, int Day);

public sealed record GroupView(string Player, string Area, IReadOnlyList<string> Party, bool Online);

public static class Wire
{
    public const int ProtocolVersion = 1;
    public const int DefaultPort = 7777;
    public const int MaxLineBytes = 4 * 1024 * 1024; // a big nemesis world is still well under this

    public static JsonSerializerOptions Options { get; } = new(NemesisJson.Options) { WriteIndented = false };

    public static string Encode(Message message) => JsonSerializer.Serialize(message, Options);

    public static Message Decode(string line) =>
        JsonSerializer.Deserialize<Message>(line, Options) ?? throw new JsonException("empty message");
}
