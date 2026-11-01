using Tristinwold.Gameplay;

namespace Tristinwold.World.Tests;

/// <summary>The yard's wagon driven off with: the job over, the wagon and load the thief's, and the yard's gate shut to them for a while.</summary>
public class CaravanTheftTests
{
    [Fact]
    public void A_driver_who_keeps_the_yards_wagon_is_a_trader_by_the_back_door_and_banned_from_the_yard()
    {
        var book = new TradeBook { Guarding = new Escort { To = "s:1:1", ToName = "Millford", Pay = 50 } };
        var took = book.Stole(10, new Dictionary<Good, int> { [Good.Iron] = 5, [Good.Ale] = 5 });
        Assert.Equal(10, took);
        Assert.Null(book.Guarding);
        Assert.True(book is { OwnWagon: true, WagonOut: true, Stolen: 1 });
        Assert.Equal(TradeBook.PerHorse, book.Capacity); // the horse in the shafts came with it
        Assert.Equal(10, book.Loaded);
        Assert.True(book.Banned(10) && book.Banned(10 + TradeBook.BanDays - 1) && !book.Banned(10 + TradeBook.BanDays));
    }

    [Fact]
    public void A_run_for_the_yard_can_be_kept_too_but_only_what_the_wagon_holds()
    {
        var book = new TradeBook { Run = new CaravanRun { To = "s:1:1", ToName = "Millford", Out = Good.Timber, Load = 10 } };
        Assert.Equal(TradeBook.PerHorse, book.Stole(1, new Dictionary<Good, int> { [Good.Timber] = 30 }));
        Assert.Null(book.Run);
        Assert.Equal(TradeBook.PerHorse, book.Cargo[Good.Timber]);
        Assert.Equal(0, book.Room);
    }
}
