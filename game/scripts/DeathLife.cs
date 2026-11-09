using Godot;
using Tristinwold.Deviant;
using Tristinwold.Gameplay;
using Tristinwold.Shared;

namespace Tristinwold.Game;

/// <summary>
/// A hero struck down by an enemy wakes with half their crowns and half of every stack of goods they'd gathered (see
/// <see cref="Forfeit"/>), and the one that killed them carries the other half off. On a server the shared world keeps
/// it on that deviant's record, for whoever brings it down; playing alone, it carries it itself (and, if it takes a
/// name and roams, it keeps it as long as it lives). Bring it down and it's yours again.
/// </summary>
public partial class Main
{
    /// <summary>How recent a blow must be to count as the one that killed: a fall long after a fight isn't the fight's doing.</summary>
    const double KillingBlowSeconds = 15;

    void StruckDown()
    {
        var killer = _player.LastHitBy is { } k && IsInstanceValid(k) && k.Hero is null && !k.Friendly && k.Bond is null
            && Time.GetTicksMsec() / 1000.0 - _player.LastHitAt < KillingBlowSeconds ? k : null;
        Hoard? lost = null;
        if (killer is not null)
        {
            lost = Forfeit.Halve(_player.Pack, _player.Crowns);
            _player.Crowns -= lost.Crowns;
            if (lost.Empty) lost = null;
        }
        // On a server the fight's report carries it to that deviant's record; otherwise the killer carries it itself.
        var onRecord = lost is not null && _tracker.InFight && _client?.Status == ClientStatus.Connected;
        if (lost is not null)
        {
            if (!onRecord) killer!.Plunder.Add(lost);
            _hud.Toast($"{killer!.Stats.Name} takes half of what you carried: {Described(lost)}. Bring it down to take it back.", 12);
            _hud.Log($"Lost to {killer.Stats.Name}: {Described(lost)}.");
            Callable.From(Save).CallDeferred();
        }
        _tracker.Downed(killer?.Id, onRecord ? lost : null);
        _hud.Center("You have fallen.");
    }

    /// <summary>What a fallen foe carried off from you, back in your pack and purse.</summary>
    void TakeBack(string from, Hoard hoard)
    {
        if (hoard.Empty) return;
        _player.Crowns += Forfeit.Recover(_player.Pack, hoard);
        Sound.Ui("coin");
        _hud.Toast($"You take back what {from} was carrying: {Described(hoard)}.", 10);
        _hud.Log($"Taken back from {from}: {Described(hoard)}.");
        Save();
    }

    static string Described(Hoard h) => h.Describe((id, n) => Items.Count(id, n));
}
