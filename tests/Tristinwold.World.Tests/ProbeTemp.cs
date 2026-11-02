using Xunit;
using Xunit.Abstractions;

namespace Tristinwold.World.Tests;

public class ProbeTemp(ITestOutputHelper o)
{
    [Fact]
    public void Probe()
    {
        var w = WorldGen.Default;
        var isles = 0;
        foreach (var s in w.SettlementsNear(0, 0, 6500))
        {
            if (Northwood.Island(s.X, s.Z) > 0.5) { isles++; if (isles < 12) o.WriteLine($"ISLE {s.Name} {s.Kind} {s.X:0},{s.Z:0}"); }
            if (Math.Abs(s.X) > 5600 || Math.Abs(s.Z) > 5600) o.WriteLine($"FAR {s.Name} {s.Kind} {s.X:0},{s.Z:0}");
        }
        o.WriteLine($"ISLES {isles}");
        var land = 0;
        for (var x = -6000; x <= 6000; x += 900) for (var z = -6000; z <= 6000; z += 900) if (Northwood.Island(x + 450, z + 450) > 0.5) land++;
        o.WriteLine($"ISLAND CELLS {land}");
    }
}
