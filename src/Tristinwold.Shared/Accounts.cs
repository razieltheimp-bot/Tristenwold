using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tristinwold.Shared;

/// <summary>One of an account's characters, as the login screen lists it.</summary>
/// <param name="Look">The character's class.</param>
/// <param name="Avatar">How they look, apart from their class (see Tristinwold.Gameplay.Avatar); null for their class's own look.</param>
public sealed record CharacterSheet(string Name, string Look, string Element, DateTime Created, int Septims = 0, string Where = "", string? Avatar = null);

/// <summary>
/// Sent before joining: register, log in, or manage characters. Answered with one
/// <see cref="AccountReply"/>, over TCP (one request per connection) or POST /v1/account.
/// </summary>
/// <param name="Action">register, login, create, delete, load, save, or restyle (a character's new look).</param>
/// <param name="Secret">The account's password (register, and login without a token).</param>
/// <param name="Token">What login hands back, so the game can stay logged in without keeping the password.</param>
/// <param name="ServerPassword">The server's own password, if it has one.</param>
public sealed record AccountRequest(
    string Action,
    string Account,
    string? Secret = null,
    string? Token = null,
    string ServerPassword = "",
    CharacterSheet? Character = null,
    SaveProgress? Progress = null) : Message;

public sealed record AccountReply(bool Ok, string? Error, string? Token, IReadOnlyList<CharacterSheet> Characters, string? Save = null) : Message;

/// <summary>A logged-in character's progress (map, quests, purse), kept on the server so it follows them between devices.</summary>
public sealed record SaveProgress(string Save, int Septims, string Where) : Message;

/// <summary>
/// Accounts and the characters they own, kept in a JSON file next to the campaign. Passwords are
/// stored only as salted PBKDF2 hashes; login tokens only as SHA-256 hashes. Thread-safe.
/// </summary>
public sealed partial class AccountStore
{
    public const int MaxCharacters = 6;
    const int Iterations = 120_000;
    const int MaxFailures = 5;
    static readonly TimeSpan Lockout = TimeSpan.FromMinutes(1);

    sealed class Account
    {
        public string Name { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public List<string> Tokens { get; set; } = [];
        public List<CharacterSheet> Characters { get; set; } = [];
        public Dictionary<string, string> Saves { get; set; } = [];
        public int Failures { get; set; }
        public DateTime LockedUntil { get; set; }
    }

    readonly Dictionary<string, Account> _accounts = new(StringComparer.OrdinalIgnoreCase);
    readonly Lock _gate = new();
    readonly string? _path;
    readonly Func<DateTime> _clock;

    public AccountStore(string? path = null, Func<DateTime>? clock = null)
    {
        _path = path;
        _clock = clock ?? (() => DateTime.UtcNow);
        if (path is not null && File.Exists(path) && File.ReadAllText(path).Trim().Length > 0)
            foreach (var a in JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(path), Wire.Options) ?? [])
                _accounts[a.Name] = a;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _'\-]{1,22}[A-Za-z0-9]$")]
    private static partial Regex NamePattern();

    public static bool ValidName(string name) => NamePattern().IsMatch(name);

    /// <summary>The account that owns a character, if any.</summary>
    public string? OwnerOf(string character)
    {
        lock (_gate) return _accounts.Values.FirstOrDefault(a => a.Characters.Any(c => Same(c.Name, character)))?.Name;
    }

    /// <summary>Is this a live login token for the account?</summary>
    public bool Holds(string account, string? token)
    {
        lock (_gate) return token is not null && _accounts.TryGetValue(account, out var a) && a.Tokens.Contains(HashToken(token));
    }

    /// <param name="taken">Names already in use in the campaign by players without an account.</param>
    public AccountReply Handle(AccountRequest r, Func<string, bool>? taken = null)
    {
        lock (_gate)
        {
            var name = r.Account.Trim();
            if (r.Action == "register")
            {
                if (!ValidName(name)) return Fail("Account names are 3-24 letters, digits, spaces, _ - or '.");
                if (_accounts.ContainsKey(name)) return Fail("That account name is taken.");
                if ((r.Secret ?? "").Length < 6) return Fail("Pick a password of at least 6 characters.");
                var salt = RandomNumberGenerator.GetBytes(16);
                var account = new Account { Name = name, Salt = Convert.ToBase64String(salt), Hash = HashSecret(r.Secret!, salt) };
                _accounts[name] = account;
                return Ok(account, NewToken(account));
            }

            if (!_accounts.TryGetValue(name, out var a)) return Fail("No account by that name. Create one?");
            if (a.LockedUntil > _clock()) return Fail("Too many wrong passwords. Wait a minute and try again.");
            string? token;
            if (r.Token is not null && a.Tokens.Contains(HashToken(r.Token))) token = r.Token;
            else if (r.Secret is not null && CryptographicOperations.FixedTimeEquals(
                         Convert.FromBase64String(HashSecret(r.Secret, Convert.FromBase64String(a.Salt))), Convert.FromBase64String(a.Hash)))
                token = r.Action == "login" ? NewToken(a) : null;
            else
            {
                if (++a.Failures >= MaxFailures) { a.LockedUntil = _clock() + Lockout; a.Failures = 0; }
                Persist();
                return Fail(r.Token is not null && r.Secret is null ? "Your login has expired. Enter your password." : "Wrong password.");
            }
            a.Failures = 0;

            switch (r.Action)
            {
                case "login":
                    return Ok(a, token);
                case "create":
                {
                    if (r.Character is not { } c) return Fail("No character given.");
                    var cname = c.Name.Trim();
                    if (!ValidName(cname)) return Fail("Character names are 3-24 letters, digits, spaces, _ - or '.");
                    if (a.Characters.Count >= MaxCharacters) return Fail($"An account holds at most {MaxCharacters} characters.");
                    if (_accounts.Values.Any(x => x.Characters.Any(y => Same(y.Name, cname))) || (taken?.Invoke(cname) ?? false))
                        return Fail($"Someone already plays as {cname}.");
                    a.Characters.Add(c with { Name = cname, Created = _clock(), Septims = 0, Where = "" });
                    return Ok(a, token);
                }
                case "delete":
                {
                    if (r.Character is not { } c) return Fail("No character given.");
                    a.Characters.RemoveAll(x => Same(x.Name, c.Name));
                    a.Saves.Remove(c.Name);
                    return Ok(a, token);
                }
                case "load":
                {
                    if (r.Character is not { } c || !a.Characters.Any(x => Same(x.Name, c.Name))) return Fail("That isn't one of your characters.");
                    return Ok(a, token) with { Save = a.Saves.GetValueOrDefault(a.Characters.First(x => Same(x.Name, c.Name)).Name) };
                }
                case "restyle":
                {
                    if (r.Character is not { } c) return Fail("No character given.");
                    var i = a.Characters.FindIndex(x => Same(x.Name, c.Name));
                    if (i < 0) return Fail("That isn't one of your characters.");
                    a.Characters[i] = a.Characters[i] with { Avatar = c.Avatar };
                    return Ok(a, token);
                }
                case "save":
                {
                    // The same as the game's SaveProgress, but answered, so logging out or quitting can wait for it.
                    if (r.Character is not { } c || r.Progress is not { } progress) return Fail("Nothing to save.");
                    return Store(a.Name, c.Name, progress) ? Ok(a, token) : Fail("That isn't one of your characters.");
                }
                default:
                    return Fail($"Unknown account action '{r.Action}'.");
            }
        }
    }

    /// <summary>Keeps a character's progress. Only for a character the account owns.</summary>
    public bool Store(string account, string character, SaveProgress save)
    {
        lock (_gate)
        {
            if (!_accounts.TryGetValue(account, out var a)) return false;
            var i = a.Characters.FindIndex(c => Same(c.Name, character));
            if (i < 0) return false;
            a.Saves[a.Characters[i].Name] = save.Save;
            a.Characters[i] = a.Characters[i] with { Septims = save.Septims, Where = save.Where };
            Persist();
            return true;
        }
    }

    AccountReply Ok(Account a, string? token)
    {
        Persist();
        return new AccountReply(true, null, token, [.. a.Characters]);
    }

    static AccountReply Fail(string error) => new(false, error, null, []);

    string NewToken(Account a)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        a.Tokens.Add(HashToken(token));
        if (a.Tokens.Count > 10) a.Tokens.RemoveAt(0); // the ten most recent devices stay logged in
        return token;
    }

    static string HashSecret(string secret, byte[] salt) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, Iterations, HashAlgorithmName.SHA256, 32));

    static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    void Persist()
    {
        if (_path is null) return;
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_accounts.Values.ToList(), Wire.Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
