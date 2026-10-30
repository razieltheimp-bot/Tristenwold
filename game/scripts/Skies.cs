using Godot;
using Tristinwold.Gameplay;

namespace Tristinwold.Game;

/// <summary>
/// The season and the weather overhead (see <see cref="Seasons"/>): the land's colours and its snow, cloud taking the
/// light down, fog rolling in, rain or snow falling round the hero, and lightning in a storm. Roofs and mines keep it
/// all out, and a voyage's own sky is left alone.
/// </summary>
public partial class Skies(Main main) : Node3D
{
    CpuParticles3D? _rain, _snow;
    float _sunBase, _ambientBase, _fogBase;
    float _light = 1, _haze = 1;
    double _tick, _flashIn = 12;
    bool _set;

    public Weather Sky { get; private set; }
    public Season Season => Seasons.Of(main.Economy.Day);

    /// <summary>Spring, summer, fall or winter, and what's overhead, to go under the name of the place.</summary>
    public string Line => City.Shut ? "" : $"{Season}, {(Sky switch { Weather.Clear => "clear skies", Weather.Cloudy => "overcast", Weather.Rain => "rain", Weather.Storm => "a storm", Weather.Snow => "snow", _ => "fog" })}";

    /// <summary>The land's season in the shaders: set before anything that reads them is drawn.</summary>
    public static void Prepare()
    {
        var known = RenderingServer.GlobalShaderParameterGetList();
        if (!known.Contains("season_snow")) RenderingServer.GlobalShaderParameterAdd("season_snow", RenderingServer.GlobalShaderParameterType.Float, 0f);
        if (!known.Contains("season_tint")) RenderingServer.GlobalShaderParameterAdd("season_tint", RenderingServer.GlobalShaderParameterType.Vec3, Vector3.One);
    }

    static CpuParticles3D Fall(int amount, float speed, Vector3 size, Color colour, float lifetime) => new()
    {
        Amount = amount, Lifetime = lifetime, Emitting = false, Position = new Vector3(0, 14, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
        EmissionBoxExtents = new Vector3(18, 1, 18), Direction = Vector3.Down, Spread = 4, Gravity = Vector3.Zero, InitialVelocityMin = speed, InitialVelocityMax = speed * 1.2f,
        LocalCoords = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Mesh = new BoxMesh
        {
            Size = size,
            Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = colour, Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
        },
    };

    public override void _Ready()
    {
        AddChild(_rain = Fall(420, 22, new Vector3(0.012f, 0.55f, 0.012f), new Color(0.72f, 0.8f, 0.9f, 0.5f), 0.8f));
        AddChild(_snow = Fall(320, 2.2f, new Vector3(0.05f, 0.05f, 0.05f), new Color(1, 1, 1, 0.9f), 6.5f));
    }

    /// <summary>The spell of the day it is now, from how far through the day the city's clock has got.</summary>
    int Spell()
    {
        var left = main.Economy.NextDayAt - main.GuildBook.PlaySeconds;
        return Math.Clamp((int)((1 - left / CityEconomy.DaySeconds) * Seasons.SpellsADay), 0, Seasons.SpellsADay - 1);
    }

    public override void _Process(double delta)
    {
        if (!_set)
        {
            (_sunBase, _ambientBase, _fogBase, _set) = (City.Sun.LightEnergy, City.Sky.AmbientLightEnergy, City.Sky.FogDensity, true);
        }
        var p = main.PlayerNode.GlobalPosition;
        GlobalPosition = p;
        var shut = City.Shut || main.AtSeaNode is not null;
        if (_rain is not null) _rain.Emitting = !shut && Sky is Weather.Rain or Weather.Storm;
        if (_snow is not null) _snow.Emitting = !shut && Sky == Weather.Snow;
        if (shut) return;
        if ((_tick -= delta) <= 0)
        {
            _tick = 2;
            var cold = main.World.BiomeAt(p.X, p.Z) is Tristinwold.World.Biome.Snowfield or Tristinwold.World.Biome.Tundra;
            Sky = Seasons.WeatherOn(main.Economy.Day, Spell(), cold);
            RenderingServer.GlobalShaderParameterSet("season_snow", (float)Seasons.SnowCover(main.Economy.Day));
            var (r, g, b) = Seasons.Tint(Season);
            RenderingServer.GlobalShaderParameterSet("season_tint", new Vector3((float)r, (float)g, (float)b));
        }
        var (light, haze) = Sky switch
        {
            Weather.Cloudy => (0.72f, 1.3f), Weather.Rain => (0.52f, 2f), Weather.Storm => (0.36f, 2.6f), Weather.Snow => (0.62f, 2.8f), Weather.Fog => (0.6f, 7f), _ => (1f, 1f),
        };
        var ease = (float)Math.Min(1, delta * 0.4);
        _light = Mathf.Lerp(_light, light, ease);
        _haze = Mathf.Lerp(_haze, haze, ease);
        City.Sun.LightEnergy = _sunBase * _light;
        City.Sky.AmbientLightEnergy = _ambientBase * Mathf.Lerp(0.72f, 1, _light);
        City.Sky.FogDensity = _fogBase * _haze;
        if (Sky != Weather.Storm) return;
        if ((_flashIn -= delta) > 0) return;
        _flashIn = 9 + GD.Randf() * 16;
        City.Sun.LightEnergy = _sunBase * 2.4f; // the eases bring it back down
        Sound.At("magic/fire_boom", p + new Vector3(GD.Randf() * 60 - 30, 30, GD.Randf() * 60 - 30), -3, 0.45f, 200);
    }
}
