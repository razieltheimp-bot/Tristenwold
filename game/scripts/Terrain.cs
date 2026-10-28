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
            Biome.AutumnForest => new Color("#7d6e38"),
            Biome.PineForest => new Color("#4f6b3a"),
            Biome.Marsh => new Color("#56603a"),
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

    /// <summary>How thick the grass grows (1 is sparse; the better the graphics, the fuller the meadows).</summary>
    public float GrassDensity { get; set; } = 1;

    /// <summary>Wind in the trees and the wave shader on the water (set before the first chunk is built).</summary>
    public bool Fancy { get; set; }

    public int Count => _chunks.Count;
    public int GrassCount => _grass.Count;
    readonly Dictionary<(int, int), Node3D> _grass = [];
    Mesh? _tuft;
    Material? _grassSway;
    bool _cardGrass; // photographed grass on cards, rather than plain blades

    readonly Dictionary<(int, int), Node3D> _chunks = [];
    readonly MeshInstance3D _water = new();
    Material _ground = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    readonly StandardMaterial3D _leaves = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f };

    public override void _Ready()
    {
        var reach = ChunkSize * (2 * ViewChunks + 1); // no further than the land, or it reads as an endless sea
        _water.Mesh = new PlaneMesh { Size = new Vector2(reach, reach), SubdivideWidth = Fancy ? reach / 8 : 0, SubdivideDepth = Fancy ? reach / 8 : 0 };
        _water.MaterialOverride = Fancy
            ? Graphics.Water()
            : new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.25f, 0.33f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f, Metallic = 0.3f };
        AddChild(_water);
        if (Photographic) _ground = Graphics.Ground();
        // The simple renderer (old devices) draws the wind shader's grass black, so it gets plain grass.
        var sways = Fancy && RenderingServer.GetCurrentRenderingMethod() != "gl_compatibility";
        if (Foliage.HasGrass)
        {
            _cardGrass = true;
            _tuft = Foliage.GrassTuft();
            _grassSway = Foliage.GrassMaterial(sways);
        }
        else
        {
            if (sways) _grassSway = Graphics.Swaying(2.2f, heightScale: 1.6f);
            _tuft = Tuft();
        }
        if (Fancy && !City.PhotoSky) Clouds(); // a photographed sky has its own clouds
    }

    readonly List<Node3D> _clouds = [];
    Vector3 _wind = new(2.5f, 0, 1.2f);

    /// <summary>Clouds drifting overhead, kept in a wide ring around the player.</summary>
    void Clouds()
    {
        var rng = new Tristinwold.Deviant.Rng(world.Seed);
        var white = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1), DisableFog = true, Roughness = 1, EmissionEnabled = true, Emission = new Color("#c8d8e8"), EmissionEnergyMultiplier = 0.35f };
        for (var i = 0; i < 16; i++)
        {
            if (Models.Scene(i % 3 == 0 ? "medieval/nature/cloud_small.gltf" : "medieval/nature/cloud_big.gltf")?.Instantiate() is not Node3D cloud) continue;
            foreach (var m in cloud.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) { m.MaterialOverride = white; m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
            cloud.Scale = Vector3.One * (18 + (float)rng.NextDouble() * 22);
            cloud.Position = new Vector3((float)(rng.NextDouble() - 0.5) * 1400, 140 + (float)rng.NextDouble() * 80, (float)(rng.NextDouble() - 0.5) * 1400);
            cloud.RotationDegrees = new Vector3(0, (float)rng.NextDouble() * 360, 0);
            AddChild(cloud);
            _clouds.Add(cloud);
        }
    }

    void DriftClouds(Vector3 player, double delta)
    {
        foreach (var c in _clouds)
        {
            var p = c.Position + _wind * (float)delta;
            // Wrap around the player so the sky is never empty.
            if (p.X - player.X > 700) p.X -= 1400; else if (player.X - p.X > 700) p.X += 1400;
            if (p.Z - player.Z > 700) p.Z -= 1400; else if (player.Z - p.Z > 700) p.Z += 1400;
            c.Position = p;
        }
    }

    /// <summary>A tuft of grass: seven thin blades, each leaning its own way, darker at the root.</summary>
    static Mesh Tuft()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new Tristinwold.Deviant.Rng(7);
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
        DriftClouds(player, GetProcessDeltaTime());
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
        if (_cardGrass) count = count * 2 / 5; // each card tuft is a whole clump
        count = (int)(count * GrassDensity);
        var clear = world.SettlementsNear(x0 + ChunkSize / 2, z0 + ChunkSize / 2, ChunkSize).ToList();
        var lairs = LairsNear(x0, z0);
        var tufts = new List<(Transform3D, Color)>();
        for (var i = 0; i < count; i++)
        {
            var (lx, lz) = (rng.NextDouble() * ChunkSize, rng.NextDouble() * ChunkSize);
            var (wx, wz) = (x0 + lx, z0 + lz);
            // Grass grows in drifts and patches, not evenly: skip most of it where the patch noise is low.
            if (Noise.Value(world.Seed + 77, wx / 9, wz / 9) < -0.15 && rng.NextDouble() < 0.8) continue;
            if (wx * wx + wz * wz < 112 * 112 || clear.Any(s => s.DistanceTo(wx, wz) < s.Radius * 0.8) || lairs.Any(r => r.DistanceTo(wx, wz) < 22)) continue;
            var y = world.Height(wx, wz);
            if (y < WorldGen.WaterLevel + 0.4) continue;
            var tint = _cardGrass ? world.BiomeAt(wx, wz, y) switch
            {
                // The photo already has grass colours; these only warm or green it a little.
                Biome.AutumnForest => new Color("#f0dca0"), Biome.Marsh => new Color("#c8dca8"), Biome.Tundra => new Color("#e0dcb8"),
                Biome.Crags => new Color("#d0d8b8"), _ => new Color("#c8e0b0"),
            } : world.BiomeAt(wx, wz, y) switch
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

        var places = world.SettlementsNear(x0 + ChunkSize / 2, z0 + ChunkSize / 2, ChunkSize).ToList();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        if (Photographic) st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
            {
                var y = h[r * n + c];
                var dx = h[r * n + Math.Min(c + 1, n - 1)] - h[r * n + Math.Max(c - 1, 0)];
                var dz = h[Math.Min(r + 1, n - 1) * n + c] - h[Math.Max(r - 1, 0) * n + c];
                var slope = Math.Sqrt(dx * dx + dz * dz) / (2 * Step);
                var (wx, wz) = (x0 + c * Step, z0 + r * Step);
                var biome = world.BiomeAt(wx, wz, y);
                if (Photographic)
                {
                    var (lo, hi) = Layers(biome, wx, wz, y, slope, places);
                    st.SetColor(lo);
                    st.SetCustom(0, hi);
                }
                else
                {
                    var colour = Palette.Ground(biome, y, slope);
                    var jitter = (float)(Noise.Unit(Noise.Hash(world.Seed, (long)wx, (long)wz)) - 0.5) * 0.06f;
                    st.SetColor(new Color(colour.R + jitter, colour.G + jitter, colour.B + jitter));
                }
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
        body.SetMeta("ground", true); // props settle onto it, never away from it
        body.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = _ground });
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        Trees(body, cx, cz, h);
        AddChild(body);
        return body;
    }

    /// <summary>The photo-textured ground (Medium quality and up), when its textures are there.</summary>
    bool Photographic => Fancy && Graphics.GroundAlbedo is not null;

    // The ground shader's layers.
    const int AutumnFloor = 0, GrassLayer = 1, Mud = 2, TundraGrass = 3, Snow = 4, Rock = 5, Shore = 6, Dirt = 7;

    /// <summary>
    /// How much of each ground layer a point has, packed into two colours for the shader: the
    /// biome's own ground with a second one in patches, rock on steep or high ground, trodden dirt
    /// in settlements, and shingle along the water.
    /// </summary>
    (Color Lo, Color Hi) Layers(Biome biome, double x, double z, double y, double slope, List<Settlement> places)
    {
        Span<float> w = stackalloc float[8];
        var (main, second, share) = biome switch
        {
            Biome.AutumnForest => (AutumnFloor, GrassLayer, 0.4),
            Biome.PineForest => (GrassLayer, AutumnFloor, 0.3),
            Biome.Marsh => (Mud, GrassLayer, 0.35),
            Biome.Tundra => (TundraGrass, GrassLayer, 0.25),
            Biome.Snowfield => (Snow, TundraGrass, 0.15),
            _ => (TundraGrass, Rock, 0.5),
        };
        var patch = Noise.SmoothStep(0.45, 0.7, Noise.Value(world.Seed + 31, x / 28, z / 28)) * share * 1.6;
        w[main] = (float)(1 - patch);
        w[second] += (float)patch;

        var rock = Math.Clamp((slope - 0.5) * 2.2, 0, 1);
        if (y > 70 && biome != Biome.Snowfield) rock = Math.Max(rock, Math.Clamp((y - 70) / 30, 0, 0.8));
        if (biome == Biome.Snowfield) rock *= 0.7;
        Blend(w, Rock, rock);

        var dirt = 0.0;
        foreach (var s in places)
            dirt = Math.Max(dirt, 1 - Noise.SmoothStep(s.Radius * 0.55, s.Radius * 0.95, s.DistanceTo(x, z)));
        Blend(w, Dirt, dirt * 0.85);
        Blend(w, Shore, 1 - Noise.SmoothStep(WorldGen.WaterLevel + 0.1, WorldGen.WaterLevel + 1.1, y));
        return (new Color(w[0], w[1], w[2], w[3]), new Color(w[4], w[5], w[6], w[7]));
    }

    static void Blend(Span<float> w, int layer, double amount)
    {
        if (amount <= 0) return;
        for (var i = 0; i < w.Length; i++) w[i] *= (float)(1 - amount);
        w[layer] += (float)amount;
    }

    /// <summary>
    /// A kind of tree or rock: its model (or a tree built in code), how big to draw it, and how its
    /// height is measured for the wind. Poly Haven models ("ph:") keep their own photo materials.
    /// </summary>
    sealed record Plant(string Path, float MinScale, float MaxScale, float HeightScale, bool Sways = true, Foliage.Kind? Tree = null, int Variant = 0);

    static readonly Plant PineA = new("medieval/nature/tree_single_A.gltf", 5.5f, 8.5f, 6.5f);
    static readonly Plant PineB = new("medieval/nature/tree_single_B.gltf", 5.5f, 8.5f, 6.5f);
    static readonly Plant OrangeL = new("halloween/tree_pine_orange_large.gltf", 0.8f, 1.25f, 1);
    static readonly Plant YellowL = new("halloween/tree_pine_yellow_large.gltf", 0.8f, 1.25f, 1);
    static readonly Plant DeadL = new("halloween/tree_dead_large.gltf", 0.9f, 1.4f, 1);
    static readonly Plant DeadM = new("halloween/tree_dead_medium.gltf", 1f, 1.5f, 1);
    static readonly Plant[] KayKitRocks = [.. "ABCDE".Select(c => new Plant($"medieval/nature/rock_single_{c}.gltf", 6, 16, 1, Sways: false))];

    static Plant[] Grown(Foliage.Kind kind, float min, float max) =>
        [.. Enumerable.Range(0, Foliage.Variants).Select(v => new Plant($"tree:{kind}:{v}", min, max, Foliage.HeightScale, Tree: kind, Variant: v))];

    static readonly Plant[] Firs = Grown(Foliage.Kind.Fir, 0.75f, 1.25f);
    static readonly Plant[] Autumns = Grown(Foliage.Kind.Autumn, 0.8f, 1.2f);
    static readonly Plant[] Greens = Grown(Foliage.Kind.Green, 0.8f, 1.2f);
    static readonly Plant[] Bares = Grown(Foliage.Kind.Bare, 0.8f, 1.3f);

    static readonly Plant Boulder = new("ph:boulder.glb", 1, 3.5f, 1, Sways: false);
    static readonly Plant FlatBoulder = new("ph:boulder_flat.glb", 1, 2.6f, 1, Sways: false);
    static readonly Plant SmallBoulder = new("ph:boulder_small.glb", 1.2f, 3.5f, 1, Sways: false);
    static readonly Plant Stone = new("ph:rock.glb", 4, 9, 1, Sways: false);
    static readonly Plant Log = new("ph:dead_trunk.glb", 0.9f, 1.5f, 1, Sways: false);
    static readonly Plant Stump = new("ph:stump.glb", 0.9f, 1.4f, 1, Sways: false);

    /// <summary>Photo-real trees and rocks, when their textures are there; otherwise the KayKit ones.</summary>
    static bool Real => Foliage.Available;

    readonly Dictionary<Plant, (Mesh Mesh, Material? Material)> _plants = [];

    (Mesh Mesh, Material? Material)? Look(Plant p)
    {
        if (_plants.TryGetValue(p, out var known)) return known;
        if (p.Tree is { } kind) return _plants[p] = (Foliage.Tree(kind, p.Variant, Fancy), null);
        if (Models.MeshOf(p.Path) is not { } mesh) return null;
        if (p.Path.StartsWith("ph:")) return _plants[p] = (mesh, null);
        var texture = (mesh.SurfaceGetMaterial(0) as BaseMaterial3D)?.AlbedoTexture;
        Material? material = Fancy && p.Sways ? Graphics.Swaying(1, texture, p.HeightScale) : Fancy ? Graphics.Swaying(0, texture, p.HeightScale) : null;
        return _plants[p] = (mesh, material);
    }

    static (Plant Plant, double Weight)[] Each(Plant[] plants, double weight) => [.. plants.Select(p => (p, weight / plants.Length))];

    static (Plant Plant, double Weight)[] Woods(Biome b) => !Real
        ? b switch
        {
            Biome.AutumnForest => [(OrangeL, 3), (YellowL, 2), (PineB, 2)],
            Biome.PineForest => [(PineA, 5), (PineB, 4), (DeadM, 0.4)],
            Biome.Marsh => [(DeadM, 3), (DeadL, 1), (PineB, 2)],
            _ => [(PineA, 4), (DeadM, 1)],
        }
        : b switch
        {
            Biome.AutumnForest => [.. Each(Autumns, 6), .. Each(Greens, 1.5), .. Each(Firs, 1.5), .. Each(Bares, 0.3)],
            Biome.PineForest => [.. Each(Firs, 9), .. Each(Greens, 0.6), .. Each(Bares, 0.4)],
            Biome.Marsh => [.. Each(Bares, 3), .. Each(Greens, 2), .. Each(Firs, 1)],
            Biome.Tundra => [.. Each(Firs, 5), .. Each(Bares, 2)],
            Biome.Snowfield => [.. Each(Firs, 4), .. Each(Bares, 1)],
            _ => [.. Each(Firs, 3), .. Each(Bares, 1)],
        };

    /// <summary>What lies on the ground between the trees: boulders, and fallen logs and stumps in woods.</summary>
    static (Plant Plant, double Weight)[] Stones(Biome b) => !Real
        ? [.. KayKitRocks.Select(r => (r, 1.0))]
        : b is Biome.AutumnForest or Biome.PineForest or Biome.Marsh
            ? [(Boulder, 2), (SmallBoulder, 2), (Stone, 1), (Log, 2), (Stump, 2)]
            : [(Boulder, 3), (FlatBoulder, 2), (SmallBoulder, 3), (Stone, 2)];

    void Trees(Node3D chunk, int cx, int cz, float[] h)
    {
        var rng = Noise.Dice(world.Seed, 7, cx, cz);
        var (x0, z0) = (cx * ChunkSize, cz * ChunkSize);
        var mid = world.BiomeAt(x0 + ChunkSize / 2, z0 + ChunkSize / 2);
        var count = mid switch
        {
            Biome.AutumnForest => 18, Biome.PineForest => 24, Biome.Marsh => 7, Biome.Tundra => 5, Biome.Snowfield => 5, _ => 3,
        };
        var rocks = mid == Biome.Crags ? 6 : 2;
        var clear = world.SettlementsNear(x0 + ChunkSize / 2, z0 + ChunkSize / 2, ChunkSize).ToList();
        var lairs = LairsNear(x0, z0);
        var groups = new Dictionary<Plant, List<(Transform3D, Color, float)>>();
        for (var i = 0; i < count + rocks; i++)
        {
            var (lx, lz) = (rng.NextDouble() * (ChunkSize - 1), rng.NextDouble() * (ChunkSize - 1));
            var (wx, wz) = (x0 + lx, z0 + lz);
            if (wx * wx + wz * wz < 140 * 140) continue; // not inside Tristinwold
            if (clear.Any(s => s.DistanceTo(wx, wz) < s.Radius + (s.IsCity ? 30 : 8))) continue;
            if (lairs.Any(r => r.DistanceTo(wx, wz) < 26)) continue; // a lair stands in a clearing
            if (Clearings.Any(c => new Vector2((float)wx - c.At.X, (float)wz - c.At.Z).Length() < c.Radius)) continue;
            var y = h[(int)Math.Round(lz / Step) * (Steps + 1) + (int)Math.Round(lx / Step)];
            if (y < WorldGen.WaterLevel + 0.5) continue;
            var biome = world.BiomeAt(wx, wz, y);
            var plant = rng.PickWeighted(i >= count ? Stones(biome) : Woods(biome));
            var scale = plant.MinScale + (float)rng.NextDouble() * (plant.MaxScale - plant.MinScale);
            var basis = Basis.Identity.Scaled(Vector3.One * scale).Rotated(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau);
            var snow = biome switch { Biome.Snowfield => 0.95f, Biome.Tundra => 0.5f, Biome.Crags when y > 70 => 0.8f, _ => 0f };
            var shade = 0.9f + (float)rng.NextDouble() * 0.2f;
            (groups.TryGetValue(plant, out var list) ? list : groups[plant] = []).Add((new Transform3D(basis, new Vector3((float)lx, y - 0.1f, (float)lz)), new Color(shade, shade, shade), snow));
        }
        foreach (var (plant, items) in groups)
            if (Look(plant) is { } look) Multi(chunk, look.Mesh, items, look.Material);
    }

    /// <summary>Places kept clear of wild trees and rocks: the quarry floor, and the woodlot (its own trees are planted).</summary>
    /// <summary>The lairs near a chunk, the nine near home and the far country's, for the trees and grass to keep clear of.</summary>
    List<Raid> LairsNear(double x0, double z0) =>
        [.. Raids.All.Where(r => r.DistanceTo(x0 + ChunkSize / 2, z0 + ChunkSize / 2) < ChunkSize + 30),
            .. FarLairs.Near(world, x0 + ChunkSize / 2, z0 + ChunkSize / 2, ChunkSize + 30)];

    public static readonly (Vector3 At, float Radius)[] Clearings =
        [(Main.QuarryAt, 24), (Main.WoodlotAt, 18), (Main.AllotmentsAt, 18), (Main.HousePlotAt, 16), (Main.CourseAt + new Vector3(-2, 0, -14), 24)];

    static void Multi(Node3D parent, Mesh mesh, List<(Transform3D T, Color C)> items, Material material) =>
        Multi(parent, mesh, items.Select(i => (i.T, i.C, 0f)).ToList(), material);

    static void Multi(Node3D parent, Mesh mesh, List<(Transform3D T, Color C, float Snow)> items, Material? material)
    {
        if (items.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = mesh, InstanceCount = items.Count };
        for (var i = 0; i < items.Count; i++)
        {
            mm.SetInstanceTransform(i, items[i].T);
            mm.SetInstanceColor(i, items[i].C);
            mm.SetInstanceCustomData(i, new Color(items[i].Snow, 0, 0, 0));
        }
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material });
    }
}
