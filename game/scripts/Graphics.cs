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
        env.FogDensity = q switch { Quality.Low => 0.005f, Quality.Medium => 0.003f, Quality.High => 0.0022f, _ => 0.0017f };
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

    /// <summary>
    /// Trees and grass that move in the wind: taller parts sway more, neighbours sway together.
    /// Given a model's texture it draws the model; each copy's custom data can dust it with snow.
    /// A cut-out texture (branch and leaf cards) is see-through where its alpha is low.
    /// </summary>
    public static ShaderMaterial Swaying(float strength, Texture2D? texture = null, float heightScale = 1, bool cutout = false, Texture2D? normal = null, float uvScale = 1)
    {
        var m = new ShaderMaterial { Shader = cutout ? CutoutSwayShader : normal is not null ? NormalSwayShader : SwayShader };
        m.SetShaderParameter("strength", strength);
        m.SetShaderParameter("height_scale", heightScale);
        m.SetShaderParameter("textured", texture is not null);
        m.SetShaderParameter("uv_scale", uvScale);
        if (texture is not null) m.SetShaderParameter("albedo_tex", texture);
        if (normal is not null) m.SetShaderParameter("normal_tex", normal);
        return m;
    }

    static readonly Shader SwayShader = new() { Code = SwayCode("") };
    static readonly Shader NormalSwayShader = new() { Code = SwayCode("#define NORMALS\n") };
    static readonly Shader CutoutSwayShader = new() { Code = SwayCode("#define CUTOUT\n") };

    static string SwayCode(string defines) => "shader_type spatial;\n" + defines + """
        render_mode cull_disabled;
        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool textured = false;
        uniform float strength = 1.0;
        uniform float height_scale = 1.0;
        uniform float uv_scale = 1.0;
        varying float snow;
        varying vec3 world_normal;
        vec3 to_linear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
        void vertex() {
            vec3 world = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
            float h = max(VERTEX.y, 0.0) * height_scale;
            float t = TIME * 1.4 + world.x * 0.06 + world.z * 0.045;
            float gust = 0.6 + 0.4 * sin(TIME * 0.35 + world.x * 0.01);
            VERTEX.x += sin(t) * 0.012 * h * h * strength * gust / height_scale;
            VERTEX.z += cos(t * 0.83) * 0.009 * h * h * strength * gust / height_scale;
            snow = INSTANCE_CUSTOM.r;
            world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
        }
        void fragment() {
            vec4 tex = textured ? texture(albedo_tex, UV * uv_scale) : vec4(1.0);
            vec3 base = tex.rgb * to_linear(COLOR.rgb);
        #ifdef CUTOUT
            // Thin needles fade away in the smaller mipmaps; strengthen them with distance so trees stay full.
            vec2 texels = UV * vec2(textureSize(albedo_tex, 0));
            float lod = max(0.0, 0.5 * log2(max(dot(dFdx(texels), dFdx(texels)), dot(dFdy(texels), dFdy(texels)))));
            ALPHA = tex.a * (1.0 + lod * 0.45);
            ALPHA_SCISSOR_THRESHOLD = 0.45;
        #endif
        #ifdef NORMALS
            NORMAL_MAP = texture(normal_tex, UV * uv_scale).rgb;
        #endif
            float cover = snow * smoothstep(0.15, 0.6, world_normal.y);
            ALBEDO = mix(base, vec3(0.9, 0.93, 0.98), cover);
            ROUGHNESS = 0.85;
            BACKLIGHT = base * 0.35;
        }
        """;

    /// <summary>The terrain's photo textures: eight layers of ground, each in one slice of these arrays.</summary>
    public static TextureLayered? GroundAlbedo => _groundAlbedo ??= Load<TextureLayered>("res://assets/polyhaven/ground/albedo.jpg");
    static TextureLayered? _groundAlbedo;

    static T? Load<T>(string path) where T : class => ResourceLoader.Exists(path) ? GD.Load<T>(path) : null;

    /// <summary>The ground layers' real sizes in metres (Poly Haven's measurements), a little enlarged to hide repeats.</summary>
    static readonly float[] GroundTile = [3.0f, 2.0f, 1.3f, 1.0f, 2.0f, 3.0f, 2.9f, 2.0f];

    /// <summary>
    /// The ground, from photographs: each vertex carries how much of each of eight layers it has
    /// (forest floor, grass, marsh, tundra, snow, rock, shore, trodden dirt) and the shader blends
    /// them, with real surface relief from the normal maps. Rock wraps around cliffs instead of
    /// stretching down them. Far away it samples a larger copy, so the repeats don't show.
    /// </summary>
    public static ShaderMaterial Ground()
    {
        var m = new ShaderMaterial { Shader = GroundShader };
        m.SetShaderParameter("albedo", GroundAlbedo);
        m.SetShaderParameter("normals", Load<TextureLayered>("res://assets/polyhaven/ground/normal.jpg"));
        m.SetShaderParameter("arm", Load<TextureLayered>("res://assets/polyhaven/ground/arm.jpg"));
        m.SetShaderParameter("tile", GroundTile);
        return m;
    }

    static readonly Shader GroundShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode cull_disabled;
            uniform sampler2DArray albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
            uniform sampler2DArray normals : filter_linear_mipmap_anisotropic, repeat_enable;
            uniform sampler2DArray arm : filter_linear_mipmap, repeat_enable;
            uniform float tile[8];
            varying vec3 world;
            varying vec3 wnormal;
            varying vec4 w_lo;
            varying vec4 w_hi;
            float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
            float noise(vec2 p) {
                vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
                return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
            }
            void vertex() {
                world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
                wnormal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
                w_lo = COLOR;
                w_hi = CUSTOM0;
            }
            void fragment() {
                float dist = length(VERTEX);
                float far = smoothstep(18.0, 70.0, dist);
                float near = 1.0 - smoothstep(60.0, 110.0, dist);
                // Blend edges follow the texture's own light and dark, not straight lines.
                float wobble = noise(world.xz * 0.6) * 0.5 + noise(world.xz * 2.3) * 0.25;
                float w[8] = float[8](w_lo.r, w_lo.g, w_lo.b, w_lo.a, w_hi.r, w_hi.g, w_hi.b, w_hi.a);
                float total = 0.0;
                for (int i = 0; i < 8; i++) { w[i] *= 0.75 + wobble; w[i] *= w[i]; total += w[i]; }
                vec3 col = vec3(0.0); vec3 bend = vec3(0.0); float rough = 0.0; float ao = 0.0;
                vec3 an = abs(wnormal); an = pow(an, vec3(4.0)); an /= an.x + an.y + an.z;
                for (int i = 0; i < 8; i++) {
                    float k = w[i] / max(total, 1e-4);
                    if (k < 0.01) continue;
                    float s = 1.0 / tile[i];
                    if (i == 5) {
                        // Rock: three projections, so cliffs are textured along their faces.
                        vec3 uvx = vec3(world.z * s, -world.y * s, 5.0), uvy = vec3(world.x * s, world.z * s, 5.0), uvz = vec3(world.x * s, -world.y * s, 5.0);
                        col += k * (texture(albedo, uvx).rgb * an.x + texture(albedo, uvy).rgb * an.y + texture(albedo, uvz).rgb * an.z);
                        if (near > 0.0) {
                            vec2 nx = texture(normals, uvx).xy * 2.0 - 1.0, ny = texture(normals, uvy).xy * 2.0 - 1.0, nz = texture(normals, uvz).xy * 2.0 - 1.0;
                            bend += k * near * (vec3(0.0, nx.y, nx.x) * an.x + vec3(ny.x, 0.0, -ny.y) * an.y + vec3(nz.x, nz.y, 0.0) * an.z);
                            vec3 a = texture(arm, uvy).rgb;
                            rough += k * a.g; ao += k * a.r;
                        } else { rough += k * 0.9; ao += k; }
                        continue;
                    }
                    vec3 uv = vec3(world.xz * s, float(i));
                    vec3 c = texture(albedo, uv).rgb;
                    if (far > 0.0) c = mix(c, texture(albedo, vec3(world.xz * s * 0.23 + 0.37, float(i))).rgb, far * 0.6);
                    col += k * c;
                    if (near > 0.0) {
                        vec2 n = texture(normals, uv).xy * 2.0 - 1.0;
                        bend += k * near * vec3(n.x, 0.0, -n.y);
                        vec3 a = texture(arm, uv).rgb;
                        rough += k * a.g; ao += k * a.r;
                    } else { rough += k * 0.9; ao += k; }
                }
                // Large, soft patches of light and shade, so whole hillsides aren't one even tone.
                col *= 0.82 + 0.36 * noise(world.xz * 0.025 + 3.0);
                vec3 n = normalize(wnormal + bend * 0.9);
                NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
                ALBEDO = col;
                ROUGHNESS = clamp(rough, 0.4, 1.0);
                AO = ao;
                AO_LIGHT_AFFECT = 0.4;
            }
            """,
    };

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

}
