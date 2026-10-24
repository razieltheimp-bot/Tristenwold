using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Graphics settings, from phones to big PCs. The ideas come from the Skyrim and Oblivion graphics
/// projects (Community Shaders' lighting, OptiScaler's upscaling, the water and wind overhauls),
/// done with Godot's own renderer: ambient occlusion and indirect light, volumetric fog,
/// AMD FSR 2 upscaling, water with waves, depth-tinting and shoreline foam, and wind in the trees
/// and grass.
/// </summary>
public static class Graphics
{
    public enum Quality { Low, Medium, High, Ultra }

    /// <summary>Phones start at Medium, everything else at High.</summary>
    public static Quality Default => OS.HasFeature("mobile") ? Quality.Medium : Quality.High;

    /// <summary>The Forward+ renderer: the full set of effects. Phones use the Mobile renderer, which has fewer.</summary>
    public static bool Full => RenderingServer.GetCurrentRenderingMethod() == "forward_plus";

    public static void Apply(Quality q, Viewport viewport, Godot.Environment env, DirectionalLight3D sun, Terrain terrain)
    {
        var mobile = OS.HasFeature("mobile");
        terrain.ViewChunks = q switch { Quality.Low => 4, Quality.Medium => mobile ? 4 : 6, Quality.High => 7, _ => 9 };
        terrain.GrassChunks = q switch { Quality.Low => 0, Quality.Medium => mobile ? 0 : 1, Quality.High => 2, _ => 3 };
        terrain.Fancy = q >= Quality.Medium;

        env.TonemapMode = q >= Quality.High ? Godot.Environment.ToneMapper.Agx : Godot.Environment.ToneMapper.Filmic;
        env.FogDensity = q switch { Quality.Low => 0.007f, Quality.Medium => 0.0045f, Quality.High => 0.0035f, _ => 0.0026f };
        env.GlowEnabled = q >= Quality.Medium;
        env.GlowIntensity = 0.6f;
        env.GlowBloom = 0.05f;
        env.SsaoEnabled = q >= Quality.High && Full;
        env.SsilEnabled = q >= Quality.Ultra && Full;
        env.SdfgiEnabled = false; // global illumination goes murky on land that streams in around you, and costs the most
        env.VolumetricFogEnabled = q >= Quality.High && Full;
        env.VolumetricFogDensity = 0.004f;
        env.VolumetricFogAlbedo = new Color("#d8cbb0");
        env.AdjustmentEnabled = true;
        env.AdjustmentSaturation = q >= Quality.High ? 1.12f : 1.05f;
        env.AdjustmentContrast = 1.06f;

        sun.ShadowEnabled = q >= Quality.Medium;
        sun.DirectionalShadowMaxDistance = q switch { Quality.Low => 0, Quality.Medium => 80, Quality.High => 140, _ => 220 };
        sun.ShadowBlur = q >= Quality.High ? 1.5f : 1f;
        sun.LightAngularDistance = q >= Quality.High ? 0.6f : 0; // soft shadows

        // Upscaling: phones render a little under screen size; Ultra uses FSR 2 as its anti-aliasing.
        viewport.Scaling3DMode = q switch
        {
            Quality.Ultra when Full => Viewport.Scaling3DModeEnum.Fsr2,
            Quality.Low or Quality.Medium when mobile => Viewport.Scaling3DModeEnum.Fsr,
            _ => Viewport.Scaling3DModeEnum.Bilinear,
        };
        viewport.Scaling3DScale = q switch { Quality.Low => 0.7f, Quality.Medium when mobile => 0.85f, _ => 1f };
        viewport.Msaa3D = q == Quality.High ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
        viewport.ScreenSpaceAA = q == Quality.Medium ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
    }

    // ---- shaders --------------------------------------------------------------------------------

    /// <summary>Leaves and grass that move in the wind. Taller parts sway more; neighbours sway together.</summary>
    public static ShaderMaterial Swaying(float strength) => new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode cull_disabled;
                uniform float strength = 1.0;
                uniform float base_height = 0.0;
                vec3 to_linear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
                void vertex() {
                    vec3 world = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
                    float h = max(VERTEX.y - base_height, 0.0);
                    float t = TIME * 1.4 + world.x * 0.06 + world.z * 0.045;
                    float gust = 0.6 + 0.4 * sin(TIME * 0.35 + world.x * 0.01);
                    VERTEX.x += sin(t) * 0.07 * h * strength * gust;
                    VERTEX.z += cos(t * 0.83) * 0.05 * h * strength * gust;
                }
                void fragment() {
                    ALBEDO = to_linear(COLOR.rgb);
                    ROUGHNESS = 0.85;
                    BACKLIGHT = ALBEDO * 0.35; // sunlight through the leaves
                }
                """,
        },
        RenderPriority = 0,
    }.Also(m => m.SetShaderParameter("strength", strength));

    /// <summary>Water: rolling waves, darker where it's deep, foam where it meets the shore, and a fresnel sheen.</summary>
    public static ShaderMaterial Water() => new()
    {
        Shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode blend_mix, depth_draw_always, cull_disabled, specular_schlick_ggx;
                uniform vec4 shallow : source_color = vec4(0.22, 0.40, 0.42, 1.0);
                uniform vec4 deep : source_color = vec4(0.05, 0.14, 0.20, 1.0);
                uniform sampler2D depth_tex : hint_depth_texture, filter_linear;
                varying vec3 world;
                float wave(vec2 p, float t) {
                    return sin(p.x * 0.11 + t * 1.1) * 0.16 + sin(p.y * 0.083 - t * 0.8) * 0.12 + sin((p.x + p.y) * 0.23 + t * 1.9) * 0.05;
                }
                void vertex() {
                    world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
                    VERTEX.y += wave(world.xz, TIME);
                    float e = 0.6;
                    float hx = wave(world.xz + vec2(e, 0.0), TIME) - wave(world.xz - vec2(e, 0.0), TIME);
                    float hz = wave(world.xz + vec2(0.0, e), TIME) - wave(world.xz - vec2(0.0, e), TIME);
                    NORMAL = normalize(vec3(-hx / (2.0 * e), 1.0, -hz / (2.0 * e)));
                }
                void fragment() {
                    float raw = texture(depth_tex, SCREEN_UV).r;
                #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
                    vec3 ndc = vec3(SCREEN_UV, raw) * 2.0 - 1.0;
                #else
                    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, raw);
                #endif
                    vec4 behind = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
                    behind.xyz /= behind.w;
                    float thickness = max(VERTEX.z - behind.z, 0.0);
                    float deepness = clamp(thickness / 5.0, 0.0, 1.0);
                    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
                    float foam = (1.0 - smoothstep(0.0, 0.3, thickness)) * (0.5 + 0.3 * sin(TIME * 2.3 + world.x * 0.9 + world.z * 0.7));
                    vec3 colour = mix(shallow.rgb, deep.rgb, deepness);
                    ALBEDO = mix(colour, vec3(0.85, 0.9, 0.9), foam * 0.7);
                    ROUGHNESS = mix(0.04, 0.6, foam);
                    SPECULAR = 0.6;
                    ALPHA = clamp(0.5 + deepness * 0.45 + fresnel * 0.3 + foam, 0.0, 1.0);
                }
                """,
        },
    };

    static T Also<T>(this T value, Action<T> act)
    {
        act(value);
        return value;
    }
}
