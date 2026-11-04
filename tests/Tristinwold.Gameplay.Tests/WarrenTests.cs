using Tristinwold.Gameplay;

namespace Tristinwold.Gameplay.Tests;

public class WarrenTests
{
    static readonly WarrenRoom[] Kept = [new("door", 2, 17, 2, 1), new("city", 0, 6, 6, 6), new("mouth", 2, 0, 2, 1)];

    [Fact]
    public void Every_tunnel_and_chamber_can_be_reached_from_the_way_in()
    {
        var w = Warren.Carve(6, 18, Kept, 7);
        for (var c = 0; c < w.Cols; c++)
            for (var r = 0; r < w.Rows; r++)
                Assert.True(w.Steps((3, 17), (c, r)) >= 0, $"cell {c},{r} is walled off");
    }

    [Fact]
    public void Chambers_are_open_inside_and_the_tunnels_wind()
    {
        var w = Warren.Carve(6, 18, Kept, 7);
        for (var c = 0; c < 5; c++)
            for (var r = 6; r < 12; r++)
            {
                Assert.True(w.OpenEast(c, r), "the city is one hall");
                if (r < 11) Assert.True(w.OpenSouth(c, r));
            }
        // From the way in to the tunnel down is a walk, not a straight line: more steps than rows between them.
        Assert.True(w.Steps((3, 17), (2, 0)) > 17, $"a winding way ({w.Steps((3, 17), (2, 0))} steps)");
        var walls = 0;
        for (var c = 0; c < 6; c++) for (var r = 12; r < 17; r++) if (!w.OpenEast(c, r) && c < 5) walls++;
        Assert.True(walls > 5, "there are walls between the tunnels");
    }

    [Fact]
    public void The_same_seed_cuts_the_same_warren_and_side_rooms_keep_clear_of_each_other()
    {
        var (a, b) = (Warren.Carve(8, 19, Kept, 99), Warren.Carve(8, 19, Kept, 99));
        for (var c = 0; c < 8; c++)
            for (var r = 0; r < 19; r++)
                Assert.Equal((a.OpenEast(c, r), a.OpenSouth(c, r)), (b.OpenEast(c, r), b.OpenSouth(c, r)));
        var side = Warren.SideRooms(8, 19, Kept, 4, 2, 2, 5);
        Assert.Equal(4, side.Count);
        Assert.All(side, s => Assert.DoesNotContain(Kept.Concat(side).Where(o => o != s), o => o.Overlaps(s)));
    }
}
