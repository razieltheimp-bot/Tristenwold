namespace Tristinwold.Gameplay;

/// <summary>
/// What a class is worth to the economy, besides its blade: every class leans on the markets its own way. The
/// numbers are multipliers (1 is no change) on what the shops and markets pay the hero for goods and loot
/// (<see cref="Sell"/>), on what the hero gathers, fishes, mines, cuts and hunts (<see cref="Gather"/>), on what
/// the town's work pays (<see cref="Pay"/>), on shop prices (<see cref="Shop"/>), and on what a load fetches
/// traded away from home, by caravan or ship (<see cref="Trade"/>).
/// </summary>
public sealed record TradePerk(string Discipline, string Name, string Text, double Sell = 1, double Gather = 1, double Pay = 1, double Shop = 1, double Trade = 1);

public static class Perks
{
    public static IReadOnlyList<TradePerk> All { get; } =
    [
        // Warriors
        new("MartialArtist", "Ascetic", "Needs little and is charged little: a tenth off in every shop.", Shop: 0.9),
        new("Barbarian", "Strong Back", "Hauls, cuts and digs like two men: more from the quarry, the woodlot and the loading, and the yard pays for it.", Gather: 1.25, Pay: 1.15),
        new("Paladin", "Good Name", "Honest dealing, and the shopkeepers know it: a better price for what you sell, and the temple's tithe comes off what you buy.", Sell: 1.1, Shop: 0.95),
        // Rogues
        new("Ranger", "Huntsman", "Takes more from every kill and every snare: pelts, meat and trophies.", Gather: 1.3),
        new("Thief", "Fence's Friend", "Knows who pays most: more for loot and trophies, and a thief's cut of every job.", Sell: 1.2, Pay: 1.1),
        new("Assassin", "Discreet", "Asks no questions and gets none: a tenth more for anything that needs a discreet buyer, and the Tongs' rates in the black market.", Sell: 1.15),
        // Mages
        new("Wizard", "Appraiser", "Reads what a thing is truly worth: a better price for gems, metals and curiosities.", Sell: 1.15),
        new("Warlock", "Hard Bargain", "The pact's patron whispers what a buyer will bear: a fifth more on every load traded away from home.", Trade: 1.2),
        new("Sorcerer", "Dragon's Hoard", "What's precious comes to you: more crowns and better gear out of every hoard and lair.", Sell: 1.1, Gather: 1.1),
        // Bards
        new("Poet", "Silver Tongue", "Talks a tenth off every price, and a better one into every sale.", Shop: 0.9, Sell: 1.1),
        new("Scribe", "Clerk", "Keeps the books: the Exchange, the yard and the company pay a scribe a fifth more for every job and contract.", Pay: 1.2),
        new("Musician", "Crowd-Pleaser", "Plays the markets as well as the Salt Barrel: loads sell a tenth better in every town and port, and the crowd pays for the song.", Trade: 1.1, Pay: 1.1),
        // Artificers
        new("Alchemist", "Refiner", "Refines what the land gives: a fifth more for everything gathered, fished and hunted, and preserving salts that keep loads sound on the road (a tenth more away from home). Once a day, steadies a town's market with reagents.", Gather: 1.2, Sell: 1.1, Trade: 1.1),
        new("EtherAeronaut", "Shipwright's Engineer", "Fits ships with modules at the shipyard; the company pays an engineer a fifth more a voyage, and the yard a fifth more a plank.", Pay: 1.2, Trade: 1.1),
        new("Saboteur", "Scrapper", "Breaks gear and wreckage down for the black market: a third more from Nell Corran for anything you sell her, and trackers planted on wagons for an ambush.", Sell: 1.3),
    ];

    public static TradePerk Of(string discipline) => All.FirstOrDefault(p => p.Discipline == discipline) ?? new TradePerk(discipline, "None", "No trade to speak of.");
}
