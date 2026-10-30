using Godot;

namespace Tristinwold.Game;

/// <summary>
/// Nicer surfaces for the cartoon figures. KayKit paints a figure from one palette texture: each part of the body
/// maps to a swatch of flat colour with a gradient down it. The figures keep that look (it reads well on a tablet),
/// and get a material that treats each swatch as the stuff it stands for: the greys as brushed steel with a metal
/// sheen and scratches, the warm tones as skin with a little scattering, everything else as cloth and leather with
/// a fine weave and a bump to it. The detail is laid on in object space from every side (triplanar), since the
/// palette's own UVs are tiny squares that would stretch any texture. A tint, a glow and worn-armour paint still
/// apply, as uniforms rather than a copied material.
/// </summary>
public static class Styled
{
    static Shader? _shader;
    static Texture2D? _grain;
    static readonly Dictionary<Material, ShaderMaterial> Done = [];

    /// <summary>A fine seamless noise, laid over everything: the weave of cloth, the brushing of steel.</summary>
    internal static Texture2D Grain => _grain ??= new NoiseTexture2D
    {
        Width = 256, Height = 256, Seamless = true, GenerateMipmaps = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.06f, FractalOctaves = 4, FractalLacunarity = 2.4f },
    };

    /// <summary>The styled material for one of KayKit's (or a plain colour's), made once per original and shared.</summary>
    public static ShaderMaterial From(Material? original)
    {
        if (original is ShaderMaterial already && already.Shader == _shader) return already;
        if (original is not null && Done.TryGetValue(original, out var done)) return done;
        _shader ??= new Shader { Code = Code };
        var m = new ShaderMaterial { Shader = _shader };
        var b = original as BaseMaterial3D;
        m.SetShaderParameter("albedo_tex", b?.AlbedoTexture ?? (Texture2D)White);
        m.SetShaderParameter("tint", b?.AlbedoColor ?? Colors.White);
        m.SetShaderParameter("glow", b is { EmissionEnabled: true } ? b.Emission : Colors.Black);
        m.SetShaderParameter("glow_energy", b is { EmissionEnabled: true } ? b.EmissionEnergyMultiplier : 0f);
        m.SetShaderParameter("grain_tex", Grain);
        m.SetShaderParameter("force", 0f);
        m.SetShaderParameter("force_metal", 0f);
        if (original is not null) Done[original] = m;
        return m;
    }

    static Texture2D? _white;
    static Texture2D White => _white ??= ImageTexture.CreateFromImage(Image.CreateFromData(2, 2, false, Image.Format.Rgba8, [255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255]));

    /// <summary>Styles every surface of every mesh under a node.</summary>
    public static void Apply(Node model)
    {
        foreach (var m in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) Apply(m);
    }

    public static void Apply(MeshInstance3D m)
    {
        for (var i = 0; i < m.GetSurfaceOverrideMaterialCount(); i++)
        {
            var active = m.GetActiveMaterial(i);
            if (active is null) continue;
            m.SetSurfaceOverrideMaterial(i, From(active));
        }
    }

    /// <summary>Paints a styled surface: the tint multiplied in, a glow, and whether it's made metal (armour) or left as it reads.</summary>
    public static ShaderMaterial Painted(Material? original, Color tint, string? glow = null, float glowEnergy = 0, bool? metal = null)
    {
        var m = (ShaderMaterial)From(original).Duplicate();
        m.SetShaderParameter("tint", tint);
        if (glow is not null) { m.SetShaderParameter("glow", new Color(glow)); m.SetShaderParameter("glow_energy", glowEnergy); }
        if (metal is { } forced) { m.SetShaderParameter("force", 1f); m.SetShaderParameter("force_metal", forced ? 1f : 0f); }
        return m;
    }

    const string Code = """
        shader_type spatial;
        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
        uniform vec4 tint : source_color = vec4(1.0);
        uniform vec3 glow : source_color = vec3(0.0);
        uniform float glow_energy = 0.0;
        uniform sampler2D grain_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
        uniform float force = 0.0;        // 1: the stuff is told, not read from the swatch
        uniform float force_metal = 0.0;  // with force: 1 metal, 0 cloth
        varying vec3 obj_pos;
        varying vec3 obj_normal;

        void vertex() {
            obj_pos = VERTEX;
            obj_normal = NORMAL;
        }

        // Noise from every side, blended by the facing, so no part of a figure is stretched.
        float tri(vec3 p, vec3 n, float scale) {
            vec3 w = abs(n);
            w = w / (w.x + w.y + w.z + 0.0001);
            return texture(grain_tex, p.yz * scale).r * w.x + texture(grain_tex, p.xz * scale).r * w.y + texture(grain_tex, p.xy * scale).r * w.z;
        }

        void fragment() {
            vec4 a = texture(albedo_tex, UV) * tint;
            float lum = dot(a.rgb, vec3(0.3, 0.59, 0.11));
            float sat = max(a.r, max(a.g, a.b)) - min(a.r, min(a.g, a.b));
            float metal = step(sat, 0.09) * step(0.22, lum) * step(lum, 0.96);
            float skin = (1.0 - metal) * step(0.1, sat) * step(a.g, a.r) * step(a.b, a.g) * step(0.42, lum) * step(sat, 0.42);
            if (force > 0.5) { metal = force_metal; skin = 0.0; }
            float cloth = (1.0 - metal) * (1.0 - skin);
            float coarse = tri(obj_pos, obj_normal, 2.2);   // slow drift: wear and unevenness
            float fine = tri(obj_pos, obj_normal, 14.0);    // the weave, the brushing
            float brushed = tri(obj_pos, obj_normal, 30.0);
            vec3 albedo = a.rgb;
            albedo *= mix(1.0, 0.9 + 0.2 * coarse, cloth) * mix(1.0, 0.96 + 0.08 * fine, cloth);
            albedo *= mix(1.0, 0.94 + 0.12 * brushed, metal);
            albedo *= mix(1.0, 0.98 + 0.04 * coarse, skin);
            ALBEDO = albedo;
            METALLIC = 0.85 * metal;
            ROUGHNESS = metal * (0.28 + 0.22 * brushed) + skin * 0.55 + cloth * (0.78 + 0.18 * fine);
            SPECULAR = metal * 0.6 + skin * 0.35 + cloth * 0.22;
            SSS_STRENGTH = 0.3 * skin;
            // A little bump from the fine grain: cloth and steel aren't flat.
            NORMAL = normalize(NORMAL + (fine - 0.5) * (0.14 * cloth + 0.06 * metal) * TANGENT + (coarse - 0.5) * 0.06 * cloth * BINORMAL);
            RIM = 0.3;
            RIM_TINT = 0.65;
            EMISSION = glow * glow_energy;
        }
        """;
}
