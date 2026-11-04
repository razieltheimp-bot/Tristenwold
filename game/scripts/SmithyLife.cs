using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The work of a smithy, inside it: the forge and the anvil, where a hero forges what they make (the smiths keep their
/// fires indoors, and send anyone who asks in to them), and the smith's own work by the piece for anyone who wants it.
/// </summary>
public partial class Main
{
    /// <summary>A shift at the anvil: the pieces still to hammer out.</summary>
    int? _anvilShift;
    double _hammerUntil;

    const int ShiftPieces = 5, ShiftBonus = 10, PracticePerPiece = 8;

    bool InSmithy => _indoors?.Kind == "smithy";

    /// <summary>Where the anvil stands in a smithy's room: across from the counter, so the smith isn't in the way of it.</summary>
    public static Vector3 Anvil => Indoors.StageAt + new Vector3(-3, 0, 2);

    /// <summary>What a piece of the smith's work pays: more the better a smith the hero is, and their calling's hand in the market.</summary>
    int AnvilWage => (int)Math.Round((4 + _player.Pack.Level(Job.Smithing)) * PerkPay);

    static readonly string[] Pieces = ["a horseshoe", "a door hinge", "a fistful of nails", "a billhook", "a spearhead", "a pot hook", "a plough-share", "a set of barrel hoops"];

    (string Verb, Action Act)? SmithyAction(Vector3 pos)
    {
        if (!InSmithy || pos.DistanceTo(Anvil with { Y = pos.Y }) > 2.4f) return null;
        if (_anvilShift is { } left) return ($"hammer out a piece at the anvil ({left} to go)", Hammer);
        return ("work at the forge", () => ShowCraft(Job.Smithing, _hud.EndTalk));
    }

    bool IsSmith(Person person) => Shops.Of(person.Npc.Id) is { Forge: true } || person.Npc.Occupation.Contains("smith", StringComparison.OrdinalIgnoreCase);

    /// <summary>What a smith says to "Can I use your forge?": come in to it, or there it is.</summary>
    void OfferForge(Person person, string mind)
    {
        if (!InSmithy) { Say(person, "The fire's inside, at the back. Step in and it's yours. Mind the tongs."); return; }
        ShowCraft(Job.Smithing, () => Say(person, mind));
    }

    /// <summary>A smith, in their own smithy, asked for work: a shift at the anvil, by the piece.</summary>
    IEnumerable<(string, Action)> SmithyTalk(Person person)
    {
        if (!InSmithy || !IsSmith(person)) yield break;
        if (_anvilShift is { } left)
        {
            yield return ("About that work...", () => Say(person, $"{left} more and we're square. The anvil's not going to hammer itself."));
            yield break;
        }
        yield return ($"Any work at the forge? ({AnvilWage} crowns a piece)", () =>
        {
            _anvilShift = ShiftPieces;
            Say(person, $"Always. {ShiftPieces} pieces off the anvil there, whatever's on the bench: shoes, hinges, nails. {AnvilWage} a piece, {ShiftBonus} more when you're done. Keep your fingers.");
        });
    }

    /// <summary>A piece hammered out: a shower of sparks, the pay for it, and a little more of the trade in the hero's hands.</summary>
    void Hammer()
    {
        if (_anvilShift is not { } left || _guilds.PlaySeconds < _hammerUntil) return;
        _hammerUntil = _guilds.PlaySeconds + 0.6;
        var wage = AnvilWage;
        _player.Crowns += wage;
        var rose = _player.Pack.Practise(Job.Smithing, PracticePerPiece);
        Effects.Burst(this, Anvil + Vector3.Up * 1.0f, Tristinwold.Deviant.DamageKind.Fire, 0.8f);
        Sound.At("melee/hit_metal", Anvil, 2);
        Sound.Ui("anvil");
        var piece = _lootRng.Pick(Pieces);
        left--;
        if (left > 0)
        {
            _anvilShift = left;
            _hud.Toast($"You hammer out {piece}. +{wage} crowns.{(rose is { } l ? $" Smithing rises to {l}." : "")} {left} to go.", 3);
        }
        else
        {
            _anvilShift = null;
            _player.Crowns += ShiftBonus;
            _hud.Toast($"You hammer out {piece}, and that's the lot. +{wage} crowns, and {ShiftBonus} more for a full shift.{(rose is { } l ? $" Smithing rises to {l}." : "")}", 5);
        }
        Save();
    }

    // ---- for the self-test -------------------------------------------------------------------------------------------

    public (string Verb, Action Act)? SmithyActionForTest(Vector3 at) => SmithyAction(at);
    public int? AnvilShiftForTest => _anvilShift;
    public void HammerNowForTest() { _hammerUntil = 0; Hammer(); }
}
