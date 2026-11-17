using System.Collections.Generic;
using System.Linq;
using Tristinwold.Shared;
using Xunit;

namespace Tristinwold.Shared.Tests;

public class StaleSessionTests
{
    sealed class Sink : IClientSink
    {
        public readonly List<Message> Got = [];
        public bool Closed;
        public void Send(Message message) => Got.Add(message);
        public void Close() => Closed = true;
    }

    static Hello Hello(string player) => new(player, "", Wire.ProtocolVersion, [player]);

    [Fact]
    public void A_new_login_replaces_a_session_left_open_instead_of_being_turned_away()
    {
        var log = new List<string>();
        var core = new ServerCore(CampaignState.SavedMan(), new ServerOptions { VannEnabled = false, RewindChance = 0, KeystoneMeetChance = 0 }, seed: 1);
        var host = new SharedWorldHost(core, () => { }, log: log.Add);
        var (old, fresh) = (new Sink(), new Sink());

        Assert.Equal("Ashgrip", host.Join(Hello("Ashgrip"), old));
        // The game closed or lost its connection, and the server hasn't noticed: the same character logs in again.
        Assert.Equal("Ashgrip", host.Join(Hello("Ashgrip"), fresh));
        Assert.DoesNotContain(fresh.Got, m => m is Rejected);
        Assert.Contains(fresh.Got, m => m is Welcome);
        Assert.True(old.Closed, "the old session is closed");
        Assert.Contains(log, l => l.Contains("older session was still open"));

        // The old connection finally dropping must not log out the new one.
        host.Leave("Ashgrip", old);
        Assert.DoesNotContain(log, l => l == "Ashgrip left.");
        host.Leave("Ashgrip", fresh);
        Assert.Contains(log, l => l == "Ashgrip left.");
    }
}
