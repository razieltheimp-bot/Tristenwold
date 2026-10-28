using Tristinwold.Shared;

namespace Tristinwold.Shared.Tests;

/// <summary>The game's sound on its way to a phone: squeezed to a byte a sample, and back.</summary>
public class RemoteAudioTests
{
    [Fact]
    public void A_tone_comes_through_close_to_how_it_went_in()
    {
        var tone = Enumerable.Range(0, RemoteAudio.Rate / 10).Select(i => 0.6f * MathF.Sin(i * 2 * MathF.PI * 440 / RemoteAudio.Rate)).ToArray();
        var packet = RemoteAudio.Packet(tone);
        Assert.Equal(RemoteAudio.Tag, packet[0]);
        Assert.Equal(4 + tone.Length, packet.Length); // a byte a sample
        var (rate, heard) = RemoteAudio.Read(packet);
        Assert.Equal(RemoteAudio.Rate, rate);
        Assert.All(tone.Zip(heard), p => Assert.InRange(p.Second - p.First, -0.02f, 0.02f));
    }

    [Fact]
    public void Silence_and_the_loudest_sounds_survive()
    {
        Assert.Equal(0, RemoteAudio.Linear(RemoteAudio.MuLaw(0)));
        Assert.InRange(RemoteAudio.Linear(RemoteAudio.MuLaw(short.MaxValue)), 31000, 32767);
        Assert.InRange(RemoteAudio.Linear(RemoteAudio.MuLaw(short.MinValue)), -32767, -31000);
        // Every byte decodes to a different level on its own side (bar the two zeros).
        Assert.Equal(255, Enumerable.Range(0, 256).Select(b => RemoteAudio.Linear((byte)b)).Distinct().Count());
    }

    [Fact]
    public void A_sound_packet_never_looks_like_a_picture() => Assert.NotEqual(0xFF, RemoteAudio.Packet([0f])[0]);
}
