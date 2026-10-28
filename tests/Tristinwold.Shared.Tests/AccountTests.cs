using System.Net;
using System.Net.Sockets;
using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>Logging in, characters that belong to accounts, and progress that follows a character between devices.</summary>
public class AccountTests
{
    static CharacterSheet Sheet(string name) => new(name, "#c9a46a", "Frost", DateTime.UtcNow);

    [Fact]
    public void Register_log_in_and_make_characters()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new AccountStore(path);
            var joined = store.Handle(new AccountRequest("register", "Joe", Secret: "hunter22"));
            Assert.True(joined.Ok);
            Assert.NotNull(joined.Token);
            Assert.False(store.Handle(new AccountRequest("register", "joe", Secret: "another1")).Ok); // names are case-blind
            Assert.False(store.Handle(new AccountRequest("register", "Mira", Secret: "short")).Ok);

            var made = store.Handle(new AccountRequest("create", "Joe", Token: joined.Token, Character: Sheet("Bram Ashmane")));
            Assert.True(made.Ok, made.Error);
            Assert.Equal("Bram Ashmane", made.Characters.Single().Name);
            Assert.Equal("Joe", store.OwnerOf("bram ashmane"));

            store.Handle(new AccountRequest("register", "Mira", Secret: "pinewood"));
            Assert.False(store.Handle(new AccountRequest("create", "Mira", Secret: "pinewood", Character: Sheet("Bram Ashmane"))).Ok); // taken
            Assert.False(store.Handle(new AccountRequest("create", "Joe", Token: "forged", Character: Sheet("Second"))).Ok);

            // Everything survives a restart, and the password logs in again from another device.
            var again = new AccountStore(path);
            var phone = again.Handle(new AccountRequest("login", "Joe", Secret: "hunter22"));
            Assert.True(phone.Ok);
            Assert.NotEqual(joined.Token, phone.Token);
            Assert.True(again.Holds("Joe", joined.Token));
            Assert.True(again.Holds("Joe", phone.Token));
            Assert.Equal("Bram Ashmane", phone.Characters.Single().Name);
            Assert.DoesNotContain("hunter22", File.ReadAllText(path));
            Assert.DoesNotContain(joined.Token!, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Saving_through_the_account_is_answered_and_only_for_your_own_character()
    {
        var store = new AccountStore();
        var joe = store.Handle(new AccountRequest("register", "Joe", Secret: "hunter22"));
        store.Handle(new AccountRequest("create", "Joe", Token: joe.Token, Character: Sheet("Bram")));
        var mira = store.Handle(new AccountRequest("register", "Mira", Secret: "pinewood"));

        var saved = store.Handle(new AccountRequest("save", "Joe", Token: joe.Token, Character: Sheet("Bram"), Progress: new SaveProgress("{\"crowns\":12}", 12, "the wilds")));
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(12, saved.Characters.Single().Crowns);
        Assert.Equal("{\"crowns\":12}", store.Handle(new AccountRequest("load", "Joe", Token: joe.Token, Character: Sheet("Bram"))).Save);

        Assert.False(store.Handle(new AccountRequest("save", "Mira", Token: mira.Token, Character: Sheet("Bram"), Progress: new SaveProgress("{}", 0, ""))).Ok);
        Assert.False(store.Handle(new AccountRequest("save", "Joe", Token: "forged", Character: Sheet("Bram"), Progress: new SaveProgress("{}", 0, ""))).Ok);
        Assert.False(store.Handle(new AccountRequest("save", "Joe", Token: joe.Token, Character: Sheet("Bram"))).Ok);
    }

    [Fact]
    public void A_character_keeps_its_look_and_can_change_it()
    {
        var store = new AccountStore();
        var joe = store.Handle(new AccountRequest("register", "Joe", Secret: "hunter22"));
        var made = store.Handle(new AccountRequest("create", "Joe", Token: joe.Token, Character: Sheet("Bram") with { Avatar = "head=Mage" }));
        Assert.Equal("head=Mage", made.Characters.Single().Avatar);
        var changed = store.Handle(new AccountRequest("restyle", "Joe", Token: joe.Token, Character: Sheet("Bram") with { Avatar = "head=Knight" }));
        Assert.True(changed.Ok, changed.Error);
        Assert.Equal("head=Knight", changed.Characters.Single().Avatar);
        Assert.Equal("#c9a46a", changed.Characters.Single().Look); // only the look changes
        var mira = store.Handle(new AccountRequest("register", "Mira", Secret: "pinewood"));
        Assert.False(store.Handle(new AccountRequest("restyle", "Mira", Token: mira.Token, Character: Sheet("Bram"))).Ok);
    }

    [Fact]
    public void Wrong_passwords_lock_the_account_for_a_minute()
    {
        var now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var store = new AccountStore(clock: () => now);
        store.Handle(new AccountRequest("register", "Joe", Secret: "hunter22"));
        for (var i = 0; i < 5; i++) Assert.Equal("Wrong password.", store.Handle(new AccountRequest("login", "Joe", Secret: "guess" + i)).Error);
        Assert.Contains("Wait a minute", store.Handle(new AccountRequest("login", "Joe", Secret: "hunter22")).Error);
        now += TimeSpan.FromMinutes(2);
        Assert.True(store.Handle(new AccountRequest("login", "Joe", Secret: "hunter22")).Ok);
    }

    [Fact]
    public async Task A_character_plays_only_from_its_account_and_its_progress_follows_it()
    {
        var core = new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false, RewindChance = 0, KeystoneMeetChance = 0, Password = "table" }, seed: 1);
        var host = new SharedWorldHost(core, () => { }, accounts: new AccountStore());
        var httpPort = FreePort();
        using var gateway = new HttpGateway(host, $"http://127.0.0.1:{httpPort}/");
        gateway.Start();
        var tcpPort = FreePort();
        var server = new TcpListener(IPAddress.Loopback, tcpPort);
        server.Start();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var tcp = await server.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    using var conn = new LineConnection(tcp.GetStream());
                    var first = await conn.ReadAsync(CancellationToken.None);
                    if (first is AccountRequest r) { await conn.WriteAsync(host.Account(r), CancellationToken.None); return; }
                    if (first is not Hello hello || host.Join(hello, new Sink(conn)) is not { } player) { await Task.Delay(200); return; }
                    while (await conn.ReadAsync(CancellationToken.None) is { } m) host.Receive(player, m);
                });
            }
        });
        var (phoneAddress, pcAddress) = ($"http://127.0.0.1:{httpPort}", $"127.0.0.1:{tcpPort}");

        // Make an account and a character from the phone, through the gateway.
        Assert.Equal("Wrong server password.", (await AccountClient.AskAsync(phoneAddress, new AccountRequest("register", "Joe", Secret: "hunter22"))).Error);
        var login = await AccountClient.AskAsync(phoneAddress, new AccountRequest("register", "Joe", Secret: "hunter22", ServerPassword: "table"));
        Assert.True(login.Ok, login.Error);
        var made = await AccountClient.AskAsync(phoneAddress, new AccountRequest("create", "Joe", Token: login.Token, ServerPassword: "table", Character: Sheet("Bram")));
        Assert.True(made.Ok, made.Error);

        // Someone else can't join as Bram; the account can.
        using (var thief = IGameLink.Open(pcAddress, () => new Hello("Bram", "table", Wire.ProtocolVersion, ["Bram"])))
        {
            thief.Start();
            Assert.Contains("belongs to an account", (await Next<Rejected>(thief)).Reason);
        }
        using (var phone = IGameLink.Open(phoneAddress, () => new Hello("Bram", "table", Wire.ProtocolVersion, ["Bram"], "Joe", login.Token)))
        {
            phone.Start();
            await Next<Welcome>(phone);
            phone.Send(new SaveProgress("{\"crowns\":88}", 88, "Hawkwake Mine"));
            for (var i = 0; i < 100; i++)
            {
                var check = await AccountClient.AskAsync(pcAddress, new AccountRequest("load", "Joe", Token: login.Token, ServerPassword: "table", Character: Sheet("Bram")));
                if (check.Save is not null) break;
                await Task.Delay(25);
            }
        }

        // Later, on the PC over TCP: log in with the password and pick up where the phone left off.
        var pc = await AccountClient.AskAsync(pcAddress, new AccountRequest("login", "Joe", Secret: "hunter22", ServerPassword: "table"));
        Assert.True(pc.Ok, pc.Error);
        var bram = pc.Characters.Single();
        Assert.Equal((88, "Hawkwake Mine"), (bram.Crowns, bram.Where));
        var loaded = await AccountClient.AskAsync(pcAddress, new AccountRequest("load", "Joe", Token: pc.Token, ServerPassword: "table", Character: bram));
        Assert.Equal("{\"crowns\":88}", loaded.Save);
        server.Stop();
    }

    static async Task<T> Next<T>(IGameLink link) where T : Message
    {
        for (var i = 0; i < 200; i++)
        {
            while (link.TryReceive(out var m)) if (m is T t) return t;
            await Task.Delay(25);
        }
        throw new TimeoutException($"no {typeof(T).Name}");
    }

    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    sealed class Sink(LineConnection conn) : IClientSink
    {
        public void Send(Message message) => _ = conn.WriteAsync(message, CancellationToken.None);
    }
}
