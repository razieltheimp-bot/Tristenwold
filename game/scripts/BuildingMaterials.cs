using Godot;

namespace Tristinwold.Game;

/// <summary>What a building surface is made of.</summary>
public enum Surface { Stone, DarkStone, Plaster, Timber, Planks, Glass, Slate, ClayTile, Shingle, Thatch }

/// <summary>
/// Materials for generated buildings, drawn entirely by a shader so they need no texture files:
/// mortared stone courses, weathered plaster, wood grain, overlapping slate or clay tiles, thatch,
/// leaded glass. Each one also computes its own surface relief, so light catches the edges of
/// stones and tiles. Coordinates come in metres, so patterns stay the same size on every wall.
/// </summary>
public static class BuildingMaterials
{
    static readonly Dictionary<Surface, ShaderMaterial> Cache = [];

    public static ShaderMaterial For(Surface s)
    {
        if (Cache.TryGetValue(s, out var m)) return m;
        m = new ShaderMaterial { Shader = Shader };
        var (pattern, a, b, c, rough) = s switch
        {
            Surface.Stone => (0, "#9a9384", "#7d776b", "#5e584f", 0.9f),
            Surface.DarkStone => (0, "#6f6a62", "#57534d", "#3e3a36", 0.92f),
            Surface.Plaster => (1, "#ddd3bc", "#c9bc9e", "#8a7e66", 0.95f),
            Surface.Timber => (2, "#4a3324", "#33231a", "#20160f", 0.8f),
            Surface.Planks => (5, "#6b4c33", "#563b27", "#2a1d13", 0.85f),
            Surface.Glass => (6, "#1d2a33", "#2f4452", "#151515", 0.15f),
            Surface.Slate => (3, "#4f5660", "#3c424b", "#24282e", 0.7f),
            Surface.ClayTile => (4, "#a8553a", "#8c4430", "#5a2a1e", 0.8f),
            Surface.Shingle => (3, "#6b5a46", "#584837", "#2e251c", 0.85f),
            _ => (7, "#b8975a", "#94753f", "#5c4826", 1f),
        };
        m.SetShaderParameter("pattern", pattern);
        m.SetShaderParameter("colour_a", new Color(a));
        m.SetShaderParameter("colour_b", new Color(b));
        m.SetShaderParameter("colour_c", new Color(c));
        m.SetShaderParameter("roughness", rough);
        return Cache[s] = m;
    }

    static readonly Shader Shader = new()
    {
        Code = """
            shader_type spatial;
            render_mode cull_disabled;
            uniform int pattern = 0;
            uniform vec4 colour_a : source_color;
            uniform vec4 colour_b : source_color;
            uniform vec4 colour_c : source_color;
            uniform float roughness = 0.9;
            varying float world_y;

            float hash11(float p) { return fract(sin(p * 127.1) * 43758.5453); }
            float hash21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
            float vnoise(vec2 p) {
                vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
                return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x), mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x), u.y);
            }
            float fbm(vec2 p) { return vnoise(p) * 0.5 + vnoise(p * 2.1) * 0.3 + vnoise(p * 4.3) * 0.2; }

            // Each surface: its height (for relief), its colour and how rough it is, at a point in metres.
            float surface(vec2 uv, out vec3 col, out float rough) {
                rough = roughness;
                vec3 a = colour_a.rgb, b = colour_b.rgb, c = colour_c.rgb;
                if (pattern == 0) { // stone courses with mortar
                    float rowH = 0.26;
                    float row = floor(uv.y / rowH);
                    float bw = 0.3 + hash11(row + 3.0) * 0.25 + hash11(floor((uv.x + hash11(row) * 2.0) / 0.45) + row * 13.0) * 0.2;
                    float x = uv.x + hash11(row) * 2.0;
                    float bi = floor(x / bw);
                    vec2 l = vec2(fract(x / bw) * bw, fract(uv.y / rowH) * rowH);
                    float edge = min(min(l.x, bw - l.x), min(l.y, rowH - l.y));
                    float mortar = 1.0 - smoothstep(0.012, 0.03, edge);
                    float id = hash21(vec2(bi, row));
                    col = mix(a, b, id) * (0.82 + 0.3 * fbm(uv * 5.0 + id * 7.0));
                    col = mix(col, c, mortar);
                    return (1.0 - mortar) * (0.55 + 0.45 * smoothstep(0.0, 0.07, edge)) + fbm(uv * 11.0) * 0.12 * (1.0 - mortar);
                }
                if (pattern == 1) { // plaster, stained, dirtier near the ground
                    float stains = smoothstep(0.45, 0.75, fbm(uv * 0.7));
                    col = mix(a, b, stains) * (0.92 + 0.1 * vnoise(uv * 9.0));
                    col = mix(col * 0.62, col, smoothstep(0.0, 1.1, world_y));
                    rough = roughness;
                    return fbm(uv * 14.0) * 0.18;
                }
                if (pattern == 2) { // timber: grain along the beam
                    float g = vnoise(vec2(uv.x * 1.3, uv.y * 22.0)) * 0.6 + vnoise(vec2(uv.x * 4.0, uv.y * 60.0)) * 0.4;
                    col = mix(a, b, g) * (0.85 + 0.2 * vnoise(uv * 2.0));
                    return g * 0.35;
                }
                if (pattern == 3 || pattern == 4) { // slate, shingle or clay tiles, overlapping in rows up the slope
                    float rowH = pattern == 4 ? 0.26 : 0.22;
                    float w = pattern == 4 ? 0.3 : 0.26;
                    float row = floor(uv.y / rowH);
                    float x = uv.x + mod(row, 2.0) * w * 0.5;
                    float ti = floor(x / w);
                    vec2 l = vec2(fract(x / w), fract(uv.y / rowH));
                    float id = hash21(vec2(ti, row));
                    float gap = 1.0 - smoothstep(0.0, 0.06, min(l.x, 1.0 - l.x));
                    float h = (1.0 - l.y) * 0.8 + 0.2;                  // each row is thickest at its lower edge
                    if (pattern == 4) h += 0.35 * sin(3.14159 * l.x);   // rounded clay tiles
                    h *= 1.0 - gap * 0.7;
                    col = mix(a, b, id) * (0.85 + 0.25 * vnoise(uv * 7.0 + id)) * (1.0 - gap * 0.5);
                    col = mix(col, c, smoothstep(0.85, 1.0, l.y) * 0.5);
                    return h;
                }
                if (pattern == 5) { // planks
                    float pw = 0.19;
                    float lx = fract(uv.x / pw);
                    float id = hash11(floor(uv.x / pw));
                    float gap = 1.0 - smoothstep(0.0, 0.05, min(lx, 1.0 - lx));
                    float g = vnoise(vec2(uv.x * 3.0 + id * 9.0, uv.y * 18.0));
                    col = mix(a, b, id * 0.6 + g * 0.4) * (1.0 - gap * 0.6);
                    return (1.0 - gap) * (0.6 + g * 0.3);
                }
                if (pattern == 6) { // leaded glass: diamond panes
                    vec2 d = vec2(uv.x + uv.y, uv.x - uv.y) / 0.16;
                    vec2 l = fract(d);
                    float lead = 1.0 - smoothstep(0.0, 0.08, min(min(l.x, 1.0 - l.x), min(l.y, 1.0 - l.y)));
                    col = mix(mix(a, b, hash21(floor(d))), c, lead);
                    rough = mix(0.08, 0.7, lead);
                    return 1.0 - lead;
                }
                // thatch: straw combed down the slope, in bundles
                float s = vnoise(vec2(uv.x * 38.0, uv.y * 2.5)) * 0.6 + vnoise(vec2(uv.x * 90.0, uv.y * 6.0)) * 0.4;
                float bundle = fract(uv.y / 0.45);
                col = mix(b, a, s) * (0.75 + 0.35 * fbm(uv * 1.5));
                col = mix(col, c, (1.0 - bundle) * 0.35);
                return s * 0.6 + (1.0 - bundle) * 0.5;
            }

            void vertex() { world_y = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y - MODEL_MATRIX[3].y; }

            void fragment() {
                vec3 col; float rough;
                float h = surface(UV, col, rough);
                vec3 c1; float r1;
                float e = 0.006;
                float hx = surface(UV + vec2(e, 0.0), c1, r1);
                float hy = surface(UV + vec2(0.0, e), c1, r1);
                float strength = pattern == 1 ? 0.015 : pattern == 6 ? 0.01 : 0.035;
                vec3 n = normalize(vec3(-(hx - h) / e * strength, -(hy - h) / e * strength, 1.0));
                NORMAL_MAP = n * 0.5 + 0.5;
                ALBEDO = col;
                ROUGHNESS = rough;
                METALLIC = pattern == 6 ? 0.2 : 0.0;
                AO = mix(0.75, 1.0, clamp(h, 0.0, 1.0));
            }
            """,
    };
}
