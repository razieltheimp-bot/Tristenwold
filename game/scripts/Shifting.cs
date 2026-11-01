using Godot;

namespace Tristinwold.Game;

/// <summary>A shapeshifter in a beast's body: harder hitting and harder to hurt, until they change again.</summary>
public partial class Player
{
    /// <summary>The shapes this hero knows (see <see cref="Tristinwold.World.ForestBook"/>): the game fills this in.</summary>
    public Func<IReadOnlyList<string>>? Shapes { get; set; }

    string? _form;
    Body? _own, _beast;
    double _formMight = 1;

    public string? Form => _form;

    /// <summary>On to the next shape studied, and after the last, back to their own body.</summary>
    void Shift(double might)
    {
        var shapes = Shapes?.Invoke() ?? [];
        var next = _form is null ? shapes.FirstOrDefault() : shapes.SkipWhile(s => s != _form).Skip(1).FirstOrDefault();
        Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, Tristinwold.Deviant.DamageKind.Poison, 1.6f);
        if (_form is not null)
        {
            Stats.DamageMult /= _formMight;
            Stats.Armor -= 20;
            _beast?.QueueFree();
            _beast = null;
            if (_own is not null) Wear(_own);
            _own = null;
        }
        _form = next;
        if (next is null) { Said?.Invoke("You come back to yourself."); return; }
        _own = Figure;
        _beast = Body.For(next);
        AddChild(_beast);
        Wear(_beast);
        _formMight = 1 + might;
        Stats.DamageMult *= _formMight;
        Stats.Armor += 20;
        Said?.Invoke($"You take the shape: {Tristinwold.World.ForestBook.ShapeName(next)}.");
    }

    /// <summary>Back to their own body from a beast's, if they're in one (before a half-form, which is built on their own).</summary>
    void LeaveBeastForm()
    {
        if (_form is null) return;
        Stats.DamageMult /= _formMight;
        Stats.Armor -= 20;
        _beast?.QueueFree();
        _beast = null;
        if (_own is not null) Wear(_own);
        _own = null;
        _form = null;
    }

    /// <summary>
    /// The half-form: the beast they wear (or the first they learned) and their own people in one body, standing tall,
    /// furred, with the beast's head and tail, stronger than either, for a while; then back to themselves, with the
    /// long rest the move asks between. The beast forms stay as long as they like; this one doesn't.
    /// </summary>
    bool HalfForm(double seconds, double might)
    {
        if (_possessing) return false;
        var kind = _form ?? Shapes?.Invoke().FirstOrDefault();
        if (kind is null) { Said?.Invoke("You know no beast's shape yet."); return false; }
        LeaveBeastForm();
        var (race, sex) = (Appearance?.Race ?? "human", Appearance?.Sex == "Female" ? Tristinwold.World.Sex.Female : Tristinwold.World.Sex.Male);
        var seed = (ulong)Company.GetHashCode();
        var boost = 1 + might * 1.6;
        Stats.DamageMult *= boost;
        Stats.Armor += 30;
        Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, Tristinwold.Deviant.DamageKind.Poison, 2.2f);
        Possess(() => (Body?)Tristinwold.Game.Figure.Human($"hybrid:{kind}", race, sex, Tristinwold.World.LifeStage.Adult, seed, Hybrids.Fur(kind)) ?? Body.For(kind), 1.12f, seconds, () =>
        {
            Stats.DamageMult /= boost;
            Stats.Armor -= 30;
            Effects.Burst(GetParent(), GlobalPosition + Vector3.Up, Tristinwold.Deviant.DamageKind.Poison, 1.6f);
            Said?.Invoke("You come back to yourself.");
        });
        Said?.Invoke($"The half-form: {Hybrids.Name(kind)}!");
        return true;
    }
}

/// <summary>The half-forms a shapeshifter can hold: what each beast lends the body, by name and by colour of coat.</summary>
public static class Hybrids
{
    public static string Name(string kind) => kind switch
    {
        "wolf" => "the Wolf-Blooded", "ice_wolf" => "the Frost-Blooded", "bear" => "the Bear-Blooded", "boar" => "the Boar-Blooded", "giant_rat" => "the Rat-Blooded",
        "fox" => "the Fox-Blooded", "mud_crab" => "the Shell-Blooded", "deathcrest_ape" => "the Crest-Blooded", _ => kind,
    };

    public static Color Fur(string kind) => new(kind switch
    {
        "wolf" => "#5a5a60", "ice_wolf" => "#d8e4f0", "bear" => "#4a3222", "boar" => "#4a3a30", "giant_rat" => "#6a5848",
        "fox" => "#c0602a", "mud_crab" => "#6a7a5a", "deathcrest_ape" => "#3a2a24", _ => "#5a5a60",
    });
}
