using Godot;
using Tristinwold.World;
using Noise = Tristinwold.World.Noise;

namespace Tristinwold.Game;

/// <summary>Colours for the land, shared by the ground and the map so the map looks like the world.</summary>
public static class Palette
{
    public static readonly Color Water = new("#2b4a5a");

    public static Color Ground(Biome biome, double height, double slope = 0)
    {
        if (height < WorldGen.WaterLevel + 0.3) return new Color("#5a5040"); // shore mud
        var c = biome switch
        {
            Biome.AutumnForest => new Color("#6e5a2c"),
            Biome.PineForest => new Color("#3f5233"),
            Biome.Marsh => new Color("#4a5038"),
            Biome.Tundra => new Color("#7d7a5f"),
            Biome.Snowfield => new Color("#c4ccd4"),
            _ => new Color("#6e6a64"),
        };
        var rock = new Color("#6e6a64");
        if (slope > 0.6 && biome != Biome.Snowfield) c = c.Lerp(rock, Mathf.Clamp((float)(slope - 0.6) * 2, 0, 1));
        if (height > 70 && biome != Biome.Snowfield) c = c.Lerp(rock, Mathf.Clamp((float)(height - 70) / 30, 0, 0.8f));
        return c;
    }

    public static Color Map(WorldGen world, double x, double z)
    {
        var h = world.Height(x, z);
        if (h < WorldGen.WaterLevel) return Water;
        var c = Ground(world.BiomeAt(x, z, h), h);
        return c.Lightened(Mathf.Clamp((float)h / 250, 0, 0.35f)); // higher ground reads lighter
    }
}

/// <summary>
/// The land around the player, streamed in 64 m chunks: levelled ground with collision, trees by
/// biome, and the water level. Chunks appear ahead and are dropped behind, so the world has no edge.
/// </summary>
public partial class Terrain(WorldGen world) : Node3D
{
    public const int ChunkSize = 64;
    const int Steps = 32;
    const float Step = ChunkSize / (float)Steps;

    public int ViewChunks { get; set; } = 7;

    /// <summary>How many chunks around the player get grass (0 for none).</summary>
    public int GrassChunks { get; set; }

    /// <summary>Wind in the trees and the wave shader on the water (set before the first chunk is built).</summary>
    public bool Fancy { get; set; }

    public int Count => _chunks.Count;
    public int GrassCount => _grass.Count;
    readonly Dictionary<(int, int), Node3D> _grass = [];
    Mesh? _tuft;
    Material? _sway, _grassSway;

    readonly Dictionary<(int, int), Node3D> _chunks = [];
    readonly MeshInstance3D _water = new();
    readonly StandardMaterial3D _ground = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    readonly StandardMaterial3D _leaves = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f };
    readonly Mesh _cone = new CylinderMesh { TopRadius = 0, BottomRadius = 1.7f, Height = 6, RadialSegments = 7 };
    readonly Mesh _crown = new SphereMesh { Radius = 2.2f, Height = 3.6f, RadialSegments = 8, Rings = 4 };
    readonly Mesh _trunk = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.3f, Height = 2.4f, RadialSegments = 5 };

    public override void _Ready()
    {
        _water.Mesh = new PlaneMesh { Size = new Vector2(ChunkSize * 40, ChunkSize * 40), SubdivideWidth = Fancy ? 240 : 0, SubdivideDepth = Fancy ? 240 : 0 };
        _water.MaterialOverride = Fancy
            ? Graphics.Water()
            : new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.25f, 0.33f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f, Metallic = 0.3f };
        AddChild(_water);
        if (Fancy)
        {
            _sway = Graphics.Swaying(1);
            _grassSway = Graphics.Swaying(2.2f);
        }
        _tuft = Tuft();
    }

    /// <summary>A tuft of grass: seven thin blades, each leaning its own way, darker at the root.</summary>
    static Mesh Tuft()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new Tristinwold.Nemesis.Rng(7);
        for (var i = 0; i < 7; i++)
        {
            var a = (float)(rng.NextDouble() * Mathf.Tau);
            var foot = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (float)(rng.NextDouble() * 0.18);
            var side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.035f;
            var height = 0.35f + (float)rng.NextDouble() * 0.4f;
            var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * height * 0.35f;
            var tip = foot + lean + new Vector3(0, height, 0);
            // Root, root, tip: one tapered blade, shaded dark at the bottom.
            st.SetColor(new Color(0.55f, 0.55f, 0.55f)); st.SetNormal(Vector3.Up); st.AddVertex(foot - side);
            st.SetColor(new Color(0.55f, 0.55f, 0.55f)); st.SetNormal(Vector3.Up); st.AddVertex(foot + side);
            st.SetColor(Colors.White); st.SetNormal(Vector3.Up); st.AddVertex(tip);
        }
        return st.Commit();
    }

    public static (int, int) ChunkOf(Vector3 p) => ((int)Mathf.Floor(p.X / ChunkSize), (int)Mathf.Floor(p.Z / ChunkSize));

    public bool HasGround(Vector3 p) => _chunks.ContainsKey(ChunkOf(p));

    /// <summary>Builds the chunks right around a point at once (after a teleport, or at the start).</summary>
    public void EnsureAround(Vector3 p, int radius = 1)
    {
        var (cx, cz) = ChunkOf(p);
        for (var i = -radius; i <= radius; i++)
            for (var j = -radius; j <= radius; j++)
                if (!_chunks.ContainsKey((cx + i, cz + j))) _chunks[(cx + i, cz + j)] = Build(cx + i, cz + j);
    }

    /// <summary>Each frame: build up to <paramref name="budget"/> missing chunks, nearest first, and drop far ones.</summary>
    public void Update(Vector3 player, int budget)
    {
        var (pcx, pcz) = ChunkOf(player);
        _water.Position = new Vector3(Mathf.Round(player.X / ChunkSize) * ChunkSize, (float)WorldGen.WaterLevel, Mathf.Round(player.Z / ChunkSize) * ChunkSize);

        for (var ring = 0; ring <= ViewChunks && budget > 0; ring++)
            for (var i = -ring; i <= ring && budget > 0; i++)
                for (var j = -ring; j <= ring && budget > 0; j++)
                {
                    if (Math.Max(Math.Abs(i), Math.Abs(j)) != ring || i * i + j * j > ViewChunks * ViewChunks + 1) continue;
                    var key = (pcx + i, pcz + j);
                    if (_chunks.ContainsKey(key)) continue;
                    _chunks[key] = Build(key.Item1, key.Item2);
                    budget--;
                }

        foreach (var key in _chunks.Keys.Where(k => Math.Max(Math.Abs(k.Item1 - pcx), Math.Abs(k.Item2 - pcz)) > ViewChunks + 2).ToList())
        {
            _chunks[key].QueueFree();
            _chunks.Remove(key);
        }

        // Grass only close by, where it can be seen.
        var grassBudget = 1;
        for (var i = -GrassChunks; i <= GrassChunks && grassBudget > 0; i++)
            for (var j = -GrassChunks; j <= GrassChunks && grassBudget > 0; j++)
            {
                var key = (pcx + i, pcz + j);
                if (_grass.ContainsKey(key) || !_chunks.ContainsKey(key)) continue;
                _grass[key] = Grass(key.Item1, key.Item2);
                grassBudget--;
            }
        foreach (var key in _grass.Keys.Where(k => Math.Max(Math.Abs(k.Item1 - pcx), Math.Abs(k.Item2 - pcz)) > GrassChunks + 1).ToList())
        {
            _grass[key].QueueFree();
            _grass.Remove(key);
        }
    }

    Node3D Grass(int cx, int cz)
    {
        var node = new Node3D { Position = new Vector3(cx * ChunkSize, 0, cz * ChunkSize) };
        AddChild(node);
        var rng = Noise.Dice(world.Seed, 9, cx, cz);
        var (x0, z0) = (cx * ChunkSize, cz * ChunkSize);
        var biome = world.BiomeAt(x0 + ChunkSize / 2, z0 + ChunkSize / 2);
        var count = biome switch { Biome.Marsh => 1100, Biome.AutumnForest => 900, Biome.PineForest => 700, Biome.Tundra => 380, Biome.Crags => 160, _ => 0 };
        var clear = world.SettlementsNear(x0 + ChunkSize / 2, z0 + ChunkSize / 2, ChunkSize).ToList();
        var tufts = new List<(Transform3D, Color)>();
        for (var i = 0; i < count; i++)
        {
            var (lx, lz) = (rng.NextDouble() * ChunkSize, rng.NextDouble() * ChunkSize);
            var (wx, wz) = (x0 + lx, z0 + lz);
            if (wx * wx + wz * wz < 112 * 112 || clear.Any(s => s.DistanceTo(wx, wz) < s.Radius * 0.8)) continue;
            var y = world.Height(wx, wz);
            if (y < WorldGen.WaterLevel + 0.4) continue;
            var tint = world.BiomeAt(wx, wz, y) switch
            {
                Biome.AutumnForest => new Color("#9a8a3e"), Biome.Marsh => new Color("#5e7434"), Biome.Tundra => new Color("#8e8c62"),
                Biome.Crags => new Color("#6e7848"), _ => new Color("#55783a"),
            };
            var shade = (float)rng.NextDouble() * 0.12f - 0.06f;
            var scale = 0.7f + (float)rng.NextDouble() * 0.8f;
            var basis = Basis.Identity.Scaled(Vector3.One * scale).Rotated(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau);
            tufts.Add((new Transform3D(basis, new Vector3((float)lx, (float)y - 0.05f, (float)lz)), new Color(tint.R + shade, tint.G + shade, tint.B + shade)));
        }
        Multi(node, _tuft!, tufts, _grassSway ?? _leaves);
        return node;
    }

    Node3D Build(int cx, int cz)
    {
        const int n = Steps + 1;
        var (x0, z0) = (cx * ChunkSize, cz * ChunkSize);
        var h = world.HeightField(x0, z0, n, Step);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
            {
                var y = h[r * n + c];
                var dx = h[r * n + Math.Min(c + 1, n - 1)] - h[r * n + Math.Max(c - 1, 0)];
                var dz = h[Math.Min(r + 1, n - 1) * n + c] - h[Math.Max(r - 1, 0) * n + c];
                var slope = Math.Sqrt(dx * dx + dz * dz) / (2 * Step);
                var (wx, wz) = (x0 + c * Step, z0 + r * Step);
                var colour = Palette.Ground(world.BiomeAt(wx, wz, y), y, slope);
                var jitter = (float)(Noise.Unit(Noise.Hash(world.Seed, (long)wx, (long)wz)) - 0.5) * 0.06f;
                st.SetColor(new Color(colour.R + jitter, colour.G + jitter, colour.B + jitter));
                st.AddVertex(new Vector3(c * Step, y, r * Step));
            }
        for (var r = 0; r < Steps; r++)
            for (var c = 0; c < Steps; c++)
            {
                var (a, b, d, e) = (r * n + c, r * n + c + 1, (r + 1) * n + c, (r + 1) * n + c + 1);
                st.AddIndex(a); st.AddIndex(b); st.AddIndex(d);
                st.AddIndex(b); st.AddIndex(e); st.AddIndex(d);
            }
        st.GenerateNormals();
        var mesh = st.Commit();

        var body = new StaticBody3D { Position = new Vector3(x0, 0, z0), Name = $"Chunk{cx}_{cz}" };
        body.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = _ground });
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        Trees(body, cx, cz, h);
        AddChild(body);
        return body;
    }

    void Trees(Node3D chunk, int cx, int cz, float[] h)
    {
        var rng = Noise.Dice(world.Seed, 7, cx, cz);
        var (x0, z0) = (cx * ChunkSize, cz * ChunkSize);
        var mid = world.BiomeAt(x0 + ChunkSize / 2, z0 + ChunkSize / 2);
        var count = mid switch
        {
            Biome.AutumnForest => 22, Biome.PineForest => 30, Biome.Marsh => 7, Biome.Tundra => 4, Biome.Snowfield => 5, _ => 3,
        };
        var clear = world.SettlementsNear(x0 + ChunkSize / 2, z0 + ChunkSize / 2, ChunkSize).ToList();
        var pines = new List<(Transform3D, Color)>();
        var broad = new List<(Transform3D, Color)>();
        var trunks = new List<Transform3D>();
        for (var i = 0; i < count; i++)
        {
            var (lx, lz) = (rng.NextDouble() * (ChunkSize - 1), rng.NextDouble() * (ChunkSize - 1));
            var (wx, wz) = (x0 + lx, z0 + lz);
            if (wx * wx + wz * wz < 140 * 140) continue; // not inside Tristinwold
            if (clear.Any(s => s.DistanceTo(wx, wz) < s.Radius + (s.IsCity ? 30 : 8))) continue;
            var y = h[(int)Math.Round(lz / Step) * (Steps + 1) + (int)Math.Round(lx / Step)];
            if (y < WorldGen.WaterLevel + 0.5) continue;
            var biome = world.BiomeAt(wx, wz, y);
            var scale = 0.75f + (float)rng.NextDouble() * 0.7f;
            var basis = Basis.Identity.Scaled(Vector3.One * scale).Rotated(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau);
            trunks.Add(new Transform3D(basis, new Vector3((float)lx, y + 1.1f * scale, (float)lz)));
            if (biome == Biome.AutumnForest && rng.Chance(0.75))
            {
                Color[] leaves = [new("#b5541f"), new("#d08a2a"), new("#8a3b1c"), new("#c9a227"), new("#7a6a2a")];
                broad.Add((new Transform3D(basis, new Vector3((float)lx, y + 3.4f * scale, (float)lz)), rng.Pick(leaves)));
            }
            else
            {
                var colour = biome switch { Biome.Snowfield => new Color("#dfe8e4"), Biome.Tundra => new Color("#3a4a34"), Biome.Marsh => new Color("#4b5a2a"), _ => new Color("#24402a") };
                pines.Add((new Transform3D(basis, new Vector3((float)lx, y + 4.2f * scale, (float)lz)), colour));
            }
        }
        Multi(chunk, _trunk, trunks.Select(t => (t, new Color("#4a3426"))).ToList(), _leaves);
        Multi(chunk, _cone, pines, _sway ?? _leaves);
        Multi(chunk, _crown, broad, _sway ?? _leaves);
    }

    static void Multi(Node3D parent, Mesh mesh, List<(Transform3D T, Color C)> items, Material material)
    {
        if (items.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = items.Count };
        for (var i = 0; i < items.Count; i++)
        {
            mm.SetInstanceTransform(i, items[i].T);
            mm.SetInstanceColor(i, items[i].C);
        }
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material });
    }
}
