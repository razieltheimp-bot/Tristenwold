using Tristinwold.World;

namespace Tristinwold.World.Tests;

public class PortsTests
{
    static readonly WorldGen World = WorldGen.Default;

    static double ToWater(double x, double z, (double X, double Z) way)
    {
        for (var r = 30.0; r < 400; r += 4)
            if (World.Height(x + way.X * r, z + way.Z * r) < WorldGen.WaterLevel - 1) return r;
        return double.MaxValue;
    }

    [Fact]
    public void Both_dock_towns_stand_on_the_water()
    {
        Assert.True(ToWater(-900, -262, (-1, 0)) < 240, $"Druitt: {ToWater(-900, -262, (-1, 0))}");
        Assert.True(ToWater(640, -600, (1, 0)) < 240, $"Greywater: {ToWater(640, -600, (1, 0))}");
    }

    [Fact]
    public void Greywater_stands_clear_of_its_own_quay()
    {
        Assert.True(World.Height(640, -600) > WorldGen.WaterLevel + 1);
        Assert.True(World.Height(700, -600) > WorldGen.WaterLevel + 1);
    }

    [Fact]
    public void Greywater_can_be_reached_by_water_from_the_cove()
    {
        for (var z = -250.0; z > -600; z -= 8)
        {
            var x = 770 + (z + 250) / -390 * 30;
            Assert.True(World.Height(x, z) < WorldGen.WaterLevel - 1, $"aground at {x:0},{z:0}");
        }
    }
}
