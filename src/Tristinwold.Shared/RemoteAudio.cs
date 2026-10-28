namespace Tristinwold.Shared;

/// <summary>
/// The game's sound for a phone playing remotely: mono, squeezed to a byte a sample (G.711 mu-law, which a
/// browser can undo with a 256-entry table), about 22 kilobytes a second at 22 kHz. Each packet is the letter
/// 'A', the sample rate in three bytes, then the samples; a picture always starts with a JPEG's 0xFF, so the
/// phone can tell them apart.
/// </summary>
public static class RemoteAudio
{
    public const byte Tag = (byte)'A';

    /// <summary>The rate sound goes to the phone at: plenty for music and effects, and light on a phone's data.</summary>
    public const int Rate = 22050;

    public static byte[] Packet(ReadOnlySpan<float> samples, int rate = Rate)
    {
        var packet = new byte[4 + samples.Length];
        packet[0] = Tag;
        packet[1] = (byte)rate;
        packet[2] = (byte)(rate >> 8);
        packet[3] = (byte)(rate >> 16);
        for (var i = 0; i < samples.Length; i++)
            packet[4 + i] = MuLaw((short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f));
        return packet;
    }

    /// <summary>What the phone hears from a packet (as the page decodes it), for the tests.</summary>
    public static (int Rate, float[] Samples) Read(byte[] packet)
    {
        if (packet.Length < 4 || packet[0] != Tag) throw new ArgumentException("Not a sound packet.");
        var rate = packet[1] | packet[2] << 8 | packet[3] << 16;
        var samples = new float[packet.Length - 4];
        for (var i = 0; i < samples.Length; i++) samples[i] = Linear(packet[4 + i]) / 32768f;
        return (rate, samples);
    }

    public static byte MuLaw(short sample)
    {
        const int Bias = 0x84, Clip = 32635;
        var sign = sample < 0 ? 0x80 : 0;
        var s = Math.Min(Math.Abs((int)sample), Clip) + Bias;
        var exponent = 7;
        for (var mask = 0x4000; (s & mask) == 0 && exponent > 0; exponent--, mask >>= 1) { }
        var mantissa = (s >> (exponent + 3)) & 0x0F;
        return (byte)~(sign | exponent << 4 | mantissa);
    }

    public static int Linear(byte mu)
    {
        var u = ~mu & 0xFF;
        var exponent = (u >> 4) & 7;
        var sample = (((u & 0x0F) << 3) + 0x84 << exponent) - 0x84;
        return (u & 0x80) != 0 ? -sample : sample;
    }
}
