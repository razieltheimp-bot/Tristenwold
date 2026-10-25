using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The hero as they'll look, turning slowly on the character page: a little stage of its own (its own world,
/// light and backdrop) so it can sit among the buttons.
/// </summary>
public partial class AvatarPreview : SubViewportContainer
{
    readonly SubViewport _view = new() { OwnWorld3D = true, Size = new Vector2I(300, 380), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
    readonly Node3D _turntable = new();
    Node3D? _figure;

    public override void _Ready()
    {
        Stretch = true;
        CustomMinimumSize = new Vector2(300, 380);
        AddChild(_view);
        _view.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#2a241c"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#e8dcc8"), AmbientLightEnergy = 0.55f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });
        _view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, 35, 0), LightEnergy = 1.3f });
        _view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 200, 0), LightEnergy = 0.4f, LightColor = new Color("#a8c0ff") });
        _view.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.95f, BottomRadius = 1.0f, Height = 0.08f, RadialSegments = 40 },
            Position = new Vector3(0, -0.04f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#4a4034"), Roughness = 0.9f },
        });
        _view.AddChild(_turntable);
        var camera = new Camera3D { Fov = 38 };
        _view.AddChild(camera);
        camera.Position = new Vector3(0, 1.3f, 4.3f);
        camera.LookAt(new Vector3(0, 1.05f, 0), Vector3.Up);
    }

    /// <summary>Shows a hero of this class (what they carry) in this look.</summary>
    public void Show(string classLook, Avatar avatar)
    {
        _figure?.QueueFree();
        _figure = Figure.Create(classLook, avatar);
        _turntable.AddChild(_figure);
    }

    public override void _Process(double delta) => _turntable.RotateY((float)delta * 0.7f);
}
