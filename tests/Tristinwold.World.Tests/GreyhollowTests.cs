using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>The mine: every chamber reachable and sloping sensibly, and Dagna's work from hiring to payment.</summary>
public class GreyhollowTests
{
    static HashSet<string> Reachable(bool deep)
    {
        var seen = new HashSet<string> { "adit" };
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var t in Greyhollow.Tunnels.Where(t => deep || !t.Deep))
                if (seen.Contains(t.From) != seen.Contains(t.To) && seen.Add(seen.Contains(t.From) ? t.To : t.From)) grew = true;
        }
        return seen;
    }

    [Fact]
    public void The_upper_workings_join_up_and_the_deep_lies_past_the_old_face_alone()
    {
        Assert.Equal(Greyhollow.Rooms.Where(r => !r.Deep).Select(r => r.Id).ToHashSet(), Reachable(deep: false));
        Assert.Equal(Greyhollow.Rooms.Select(r => r.Id).ToHashSet(), Reachable(deep: true));
        Assert.Equal("old_face", Assert.Single(Greyhollow.Tunnels, t => t.Deep && !Greyhollow.Room(t.From).Deep).From);
    }

    [Fact]
    public void You_can_walk_every_drift_and_the_floor_never_jumps()
    {
        foreach (var t in Greyhollow.Tunnels)
        {
            var (a, b) = (Greyhollow.Room(t.From), Greyhollow.Room(t.To));
            var last = a.Floor;
            for (var i = 0; i <= 400; i++)
            {
                var (x, z) = (a.X + (b.X - a.X) * i / 400, a.Z + (b.Z - a.Z) * i / 400);
                Assert.True(Greyhollow.Open(x, z), $"{t.From} to {t.To} is blocked {i / 4.0:0}% along");
                var floor = Greyhollow.Floor(x, z);
                Assert.True(Math.Abs(floor - last) < 0.4, $"a step of {Math.Abs(floor - last):0.0} m on {t.From} to {t.To}");
                last = floor;
            }
        }
    }

    [Fact]
    public void Drifts_are_a_walkable_slope_and_the_mine_goes_down()
    {
        foreach (var t in Greyhollow.Tunnels)
        {
            var (a, b) = (Greyhollow.Room(t.From), Greyhollow.Room(t.To));
            var run = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Z - a.Z, 2)) - a.Radius - b.Radius;
            Assert.True(run > 3, $"{t.From} to {t.To}: the rooms overlap");
            Assert.True(Math.Abs(b.Floor - a.Floor) / run < 0.7, $"{t.From} to {t.To} is too steep to climb");
        }
        Assert.True(Greyhollow.Room("gold_seam").Floor < Greyhollow.Room("old_face").Floor && Greyhollow.Room("old_face").Floor < Greyhollow.Room("adit").Floor);
    }

    [Fact]
    public void Solid_rock_is_rock_and_a_chamber_has_more_headroom_than_a_drift()
    {
        Assert.False(Greyhollow.Open(70, 10));
        var room = Greyhollow.Room("gold_seam");
        Assert.True(Greyhollow.Open(room.X, room.Z));
        Assert.True(Greyhollow.Headroom(room.X, room.Z) > Greyhollow.Headroom(room.X + room.Radius - 0.5, room.Z));
        Assert.Equal("The Gold Seam", Greyhollow.NameAt(room.X, room.Z));
    }

    [Fact]
    public void Every_seam_is_a_real_vein_in_the_open_and_the_gold_is_only_in_the_deep()
    {
        foreach (var s in Greyhollow.Seams)
        {
            Assert.Contains(Gathering.Veins, v => v.Id == s.Kind);
            var (x, z, _) = Greyhollow.SeamAt(s);
            Assert.True(Greyhollow.Open(x, z), $"{s.Id} is in the rock");
        }
        Assert.All(Greyhollow.Seams.Where(s => s.Kind == "gold"), s => Assert.True(Greyhollow.Room(s.Room).Deep));
        Assert.Equal(Greyhollow.Seams.Count, Greyhollow.Seams.Select(s => s.Id).Distinct().Count());
        Assert.All(Enumerable.Range(0, 11), i => Assert.Contains(Greyhollow.Seams, s => s.Id == $"quarry_{i}")); // the quarry's veins all moved in
    }

    [Fact]
    public void What_lives_down_there_exists_and_the_dead_stay_past_the_breach()
    {
        foreach (var d in Greyhollow.Dens) Assert.NotNull(Bestiary.Get(d.Kind));
        foreach (var wave in Enumerable.Range(0, MineBook.WaveCount))
            Assert.All(MineBook.Wave(wave), w => Assert.NotNull(Bestiary.Get(w.Kind)));
        Assert.All(Greyhollow.Dens.Where(d => d.Kind != "giant_rat"), d => Assert.True(Greyhollow.Room(d.Room).Deep));
    }

    [Fact]
    public void The_face_stands_in_the_drift_to_the_deep()
    {
        var (x, z, floor) = Greyhollow.Face;
        Assert.True(Greyhollow.Open(x, z));
        Assert.InRange(z, Greyhollow.Room("breach").Z, Greyhollow.Room("old_face").Z);
        Assert.InRange(floor, Greyhollow.Room("breach").Floor, Greyhollow.Room("old_face").Floor);
    }

    [Fact]
    public void The_ground_rises_into_a_mound_with_a_level_cutting_through_to_the_door()
    {
        Assert.Equal(0, Greyhollow.Hill(Greyhollow.QuarryX, Greyhollow.QuarryZ)); // the quarry floor stays flat
        var door = Greyhollow.DoorZ;
        Assert.True(Greyhollow.Hill(Greyhollow.QuarryX, door - 9) > 10, "the mound stands behind the doorway");
        Assert.True(Greyhollow.Hill(Greyhollow.QuarryX + 9, door + 1) > 5, "and rises either side of the cutting");
        for (var z = door - 5; z < door + 14; z++) Assert.True(Greyhollow.Hill(Greyhollow.QuarryX, z) < 0.3, $"the way in is level {z - door:0} m from the doorway");
        Assert.Equal(0, Greyhollow.Hill(-108, 34)); // clear of the caravan yard
        Assert.True(Math.Abs(Greyhollow.Hill(0, 0)) < 1e-9);
    }

    [Fact]
    public void Dagnas_work_runs_from_hiring_through_the_fight_to_the_pay()
    {
        var book = new MineBook();
        Assert.False(book.VeinWorked()); // nobody's counting until she's hired you
        Assert.False(book.Breach());
        Assert.True(book.Hire());
        Assert.False(book.Hire());
        for (var i = 1; i < MineBook.VeinsToProve; i++) Assert.False(book.VeinWorked());
        Assert.Equal(MineStage.Hired, book.Stage);
        Assert.True(book.VeinWorked());
        Assert.Equal(MineStage.AtTheFace, book.Stage);
        Assert.False(book.Broken);
        Assert.Null(book.Collect());

        Assert.True(book.Breach());
        Assert.True(book.Broken);
        Assert.False(book.DeepOpen);
        for (var i = 1; i < MineBook.WaveCount; i++) Assert.False(book.WaveBeaten());
        Assert.True(book.WaveBeaten());
        Assert.True(book.DeepOpen);

        Assert.Equal(MineBook.Pay, book.Collect());
        Assert.Null(book.Collect()); // once
        Assert.True(book.DeepOpen);
    }

    [Fact]
    public void Losing_Tam_or_falling_walls_it_up_again_to_be_tried_again()
    {
        var book = new MineBook { Stage = MineStage.AtTheFace };
        book.Breach();
        book.WaveBeaten();
        Assert.False(book.Harm(40));
        Assert.True(book.Harm(60));
        book.Lost();
        Assert.Equal(MineStage.AtTheFace, book.Stage);
        Assert.Equal(1, book.Failed);
        Assert.False(book.Broken);

        Assert.True(book.Breach());
        Assert.Equal(0, book.Waves);
        Assert.Equal(MineBook.TamHealth, book.Tam);
        book.Reload(); // saved mid-fight: back at the wall, no harm done
        Assert.Equal(MineStage.AtTheFace, book.Stage);
        Assert.Equal(1, book.Failed);
    }

    [Fact]
    public void Dagnas_work_is_saved_with_the_company()
    {
        var book = new MineBook { Stage = MineStage.AtTheFace, Worked = 4, Failed = 2 };
        var saved = CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), new QuestLog(), mine: book).ToJson());
        Assert.Equal(MineStage.AtTheFace, saved.Mine!.Stage);
        Assert.Equal(2, saved.Mine.Failed);
        Assert.Null(CompanySave.FromJson(CompanySave.Capture(1, 0, new ExploredMap(), new QuestLog()).ToJson()).Mine); // an older save has none
    }
}
