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
}
