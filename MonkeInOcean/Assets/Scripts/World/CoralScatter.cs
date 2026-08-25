using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally scatters underwater flora (corals, sea grass, kelp…) onto the
/// Terrain as <b>terrain tree instances</b> — not GameObjects. Terrain trees are
/// batched/instanced and LOD'd by the terrain system with no per-object overhead
/// and no colliders, so tens of thousands of them stay cheap. They're pure local
/// decoration: identical on every client from the same seed, so they never touch
/// the netcode in a multiplayer build.
///
/// <b>Biomes</b> are horizontal regions picked by a low-frequency noise field, so
/// the seabed breaks up into irregular patches (a reef here, a kelp forest there).
/// Each biome has its own palette of prefabs, and each prefab has its own depth
/// range, so you also get vertical zonation (shallow reef tops vs. deeper growth)
/// inside a biome.
///
/// This component OWNS the terrain's tree layer: Scatter rebuilds both the tree
/// prototypes and all tree instances from the biome definitions. Don't also hand-
/// paint trees on the same terrain, or Scatter will wipe them. Runs at edit time
/// via the right-click context menu — no Play mode needed.
///
/// Notes / limits:
///  - Terrain trees are always upright (they only rotate around Y), so they won't
///    tilt to the seabed slope. Fine for most corals; use a mesh with a flat base.
///  - Use prefabs WITHOUT colliders (these are decoration) to keep physics free.
/// </summary>
[DisallowMultipleComponent]
public class CoralScatter : MonoBehaviour
{
    [Serializable]
    public class CoralEntry
    {
        public GameObject prefab;
        [Tooltip("Relative chance of this prefab vs. others in the same biome.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Metres below the water surface this prefab is allowed (min, max).")]
        public Vector2 depthRange = new Vector2(1f, 20f);
        [Tooltip("Random uniform scale multiplier (min, max).")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.4f);
        [Tooltip("Base tint applied to instances (terrain trees are tinted per-instance).")]
        public Color tint = Color.white;
    }

    [Serializable]
    public class GroundCoverEntry
    {
        [Tooltip("Mesh detail (kelp/seagrass model). Leave empty to use a grass texture instead.")]
        public GameObject mesh;
        [Tooltip("Grass billboard texture (used when Mesh is empty).")]
        public Texture2D texture;
        [Tooltip("Metres below the water surface this cover is allowed (min, max).")]
        public Vector2 depthRange = new Vector2(1f, 25f);
        [Tooltip("Peak density per cell (higher = thicker). Clamped by Max Density Per Cell.")]
        [Min(0f)] public float density = 4f;
        public Vector2 widthRange = new Vector2(0.8f, 1.6f);
        public Vector2 heightRange = new Vector2(0.8f, 1.6f);
        public Color healthyColor = new Color(0.5f, 0.75f, 0.55f, 1f);
        public Color dryColor = new Color(0.35f, 0.6f, 0.45f, 1f);
    }

    [Serializable]
    public class Biome
    {
        public string name = "Reef";
        [Tooltip("Scales how densely this biome fills vs. the global density.")]
        [Range(0f, 2f)] public float densityMultiplier = 1f;
        [Tooltip("Coral / prop prefabs placed as terrain trees.")]
        public List<CoralEntry> entries = new List<CoralEntry>();
        [Tooltip("Kelp / grass placed as terrain detail (cheap GPU ground cover).")]
        public List<GroundCoverEntry> groundCover = new List<GroundCoverEntry>();
        [Tooltip("Editor-only colour for the biome preview gizmo.")]
        public Color gizmoColor = new Color(0.2f, 0.8f, 0.7f, 1f);
    }

    [Header("Terrain")]
    [SerializeField] private Terrain terrain;

    [Header("Biomes")]
    [SerializeField] private List<Biome> biomes = new List<Biome>();

    [Header("Biome Regions")]
    [Tooltip("World frequency of the biome patches. Smaller = larger regions.")]
    [SerializeField] private float biomeNoiseScale = 0.008f;
    [Tooltip("Warps the biome borders so they're organic rather than smooth blobs.")]
    [SerializeField] private float biomeBorderWarp = 40f;

    [Header("Placement")]
    // Single source of truth for the water line — see Ocean.WaterLevel.
    private float waterSurfaceY => Ocean.WaterLevel.SurfaceY;
    [Tooltip("Instances attempted per 100 m² of seabed, before depth/slope rejection.")]
    [SerializeField] private float densityPer100m2 = 12f;
    [Tooltip("Don't place anything shallower than this depth (metres below surface).")]
    [SerializeField] private float minDepth = 0.5f;
    [Tooltip("Reject seabed steeper than this (degrees).")]
    [SerializeField] private float maxSlopeDegrees = 40f;
    [Tooltip("Random per-instance brightness variation (0 = uniform).")]
    [Range(0f, 1f)] [SerializeField] private float colorVariation = 0.15f;
    [Tooltip("Hard cap so a bad density value can't create millions of trees.")]
    [SerializeField] private int maxInstances = 200000;
    [SerializeField] private int seed = 12345;

    [Header("Ground Cover (terrain detail)")]
    [Tooltip("Detail map resolution. Higher = finer grass placement, more memory.")]
    [SerializeField] private int detailResolution = 512;
    [SerializeField] private int detailResolutionPerPatch = 16;
    [Tooltip("Upper clamp on per-cell grass density.")]
    [SerializeField] private int maxDensityPerCell = 8;
    [Tooltip("World frequency of the density patchiness within a biome.")]
    [SerializeField] private float groundCoverNoiseScale = 0.05f;
    [Tooltip("Gentle sway applied to grass/kelp (looks like an underwater current).")]
    [SerializeField] private bool applyGentleSway = true;

    [Header("Scatter Area (XZ only, world space)")]
    [SerializeField] private bool useScatterArea;
    [SerializeField] private Vector3 areaCenter = new Vector3(221f, 100f, 207f);
    [SerializeField] private Vector3 areaSize = new Vector3(600f, 0f, 600f);

    [ContextMenu("Scatter Corals")]
    public void Scatter()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[CoralScatter] No Terrain assigned or active in the scene.");
            return;
        }
        if (biomes.Count == 0)
        {
            Debug.LogWarning("[CoralScatter] No biomes defined; nothing to scatter.");
            return;
        }

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        // Collect the distinct prefabs across all biomes into the tree prototype list.
        var prototypeList = new List<TreePrototype>();
        var prefabToIndex = new Dictionary<GameObject, int>();
        foreach (Biome biome in biomes)
        {
            foreach (CoralEntry entry in biome.entries)
            {
                if (entry.prefab == null || prefabToIndex.ContainsKey(entry.prefab)) continue;
                prefabToIndex[entry.prefab] = prototypeList.Count;
                prototypeList.Add(new TreePrototype { prefab = entry.prefab });
            }
        }
        if (prototypeList.Count == 0)
        {
            Debug.LogWarning("[CoralScatter] No prefabs assigned in any biome entry.");
            return;
        }

        // Work out the XZ bounds to scatter over.
        float minX = origin.x, maxX = origin.x + size.x;
        float minZ = origin.z, maxZ = origin.z + size.z;
        if (useScatterArea)
        {
            minX = Mathf.Max(minX, areaCenter.x - areaSize.x * 0.5f);
            maxX = Mathf.Min(maxX, areaCenter.x + areaSize.x * 0.5f);
            minZ = Mathf.Max(minZ, areaCenter.z - areaSize.z * 0.5f);
            maxZ = Mathf.Min(maxZ, areaCenter.z + areaSize.z * 0.5f);
            if (minX >= maxX || minZ >= maxZ)
            {
                Debug.LogWarning("[CoralScatter] Scatter area doesn't overlap the terrain.");
                return;
            }
        }

        float area = (maxX - minX) * (maxZ - minZ);
        int attempts = Mathf.Clamp(
            Mathf.RoundToInt(area / 100f * Mathf.Max(0f, densityPer100m2)),
            0, maxInstances);

        var rng = new System.Random(seed);
        var instances = new List<TreeInstance>(attempts);

        for (int i = 0; i < attempts; i++)
        {
            float wx = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
            float wz = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());

            float groundY = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
            float depth = waterSurfaceY - groundY;
            if (depth < minDepth) continue; // above water or too shallow

            float nx = (wx - origin.x) / size.x;
            float nz = (wz - origin.z) / size.z;
            Vector3 normal = data.GetInterpolatedNormal(nx, nz);
            if (Vector3.Angle(normal, Vector3.up) > maxSlopeDegrees) continue;

            Biome biome = biomes[BiomeIndexAt(wx, wz)];
            // Density thinning per biome: skip some candidates in sparser biomes.
            if (biome.densityMultiplier < 1f && rng.NextDouble() > biome.densityMultiplier) continue;

            CoralEntry entry = PickEntry(biome, depth, rng);
            if (entry == null) continue; // nothing in this biome grows at this depth

            float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
            float shade = 1f - colorVariation * (float)rng.NextDouble();
            Color c = entry.tint * shade;
            c.a = 1f;

            instances.Add(new TreeInstance
            {
                position = new Vector3(nx, (groundY - origin.y) / size.y, nz),
                prototypeIndex = prefabToIndex[entry.prefab],
                widthScale = scale,
                heightScale = scale,
                rotation = (float)(rng.NextDouble() * Math.PI * 2.0),
                color = c,
                lightmapColor = Color.white
            });
        }

        data.treePrototypes = prototypeList.ToArray();
        data.RefreshPrototypes();
        data.SetTreeInstances(instances.ToArray(), true); // snap to heightmap
        terrain.Flush();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(data);
#endif
        Debug.Log($"[CoralScatter] Placed {instances.Count} instances " +
                  $"({prototypeList.Count} prototypes) across {biomes.Count} biomes.");
    }

    [ContextMenu("Scatter Ground Cover")]
    public void ScatterGroundCover()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[CoralScatter] No Terrain assigned or active in the scene.");
            return;
        }

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        // Collect the distinct detail prototypes across all biomes (one layer each).
        var prototypeList = new List<DetailPrototype>();
        var keyToLayer = new Dictionary<UnityEngine.Object, int>();
        foreach (Biome biome in biomes)
        {
            foreach (GroundCoverEntry gc in biome.groundCover)
            {
                UnityEngine.Object key = gc.mesh != null ? (UnityEngine.Object)gc.mesh : gc.texture;
                if (key == null || keyToLayer.ContainsKey(key)) continue;

                var proto = new DetailPrototype
                {
                    minWidth = gc.widthRange.x,
                    maxWidth = gc.widthRange.y,
                    minHeight = gc.heightRange.x,
                    maxHeight = gc.heightRange.y,
                    healthyColor = gc.healthyColor,
                    dryColor = gc.dryColor,
                    noiseSpread = 0.3f
                };
                if (gc.mesh != null)
                {
                    proto.usePrototypeMesh = true;
                    proto.prototype = gc.mesh;
                    proto.renderMode = DetailRenderMode.VertexLit;
                    proto.useInstancing = true;
                }
                else
                {
                    proto.usePrototypeMesh = false;
                    proto.prototypeTexture = gc.texture;
                    proto.renderMode = DetailRenderMode.GrassBillboard;
                }

                keyToLayer[key] = prototypeList.Count;
                prototypeList.Add(proto);
            }
        }
        if (prototypeList.Count == 0)
        {
            Debug.LogWarning("[CoralScatter] No ground cover assigned in any biome.");
            return;
        }

        // Per biome, map a layer index back to its entry so we can look up depth/density.
        var biomeLayerEntry = new Dictionary<int, GroundCoverEntry>[biomes.Count];
        for (int b = 0; b < biomes.Count; b++)
        {
            biomeLayerEntry[b] = new Dictionary<int, GroundCoverEntry>();
            foreach (GroundCoverEntry gc in biomes[b].groundCover)
            {
                UnityEngine.Object key = gc.mesh != null ? (UnityEngine.Object)gc.mesh : gc.texture;
                if (key != null && keyToLayer.TryGetValue(key, out int layer))
                    biomeLayerEntry[b][layer] = gc;
            }
        }

        if (data.detailWidth != detailResolution || data.detailHeight != detailResolution)
            data.SetDetailResolution(detailResolution, Mathf.Max(8, detailResolutionPerPatch));
        data.detailPrototypes = prototypeList.ToArray();
        data.RefreshPrototypes();

        int res = detailResolution;

        // Precompute depth and biome per detail cell once (shared across all layers).
        var depthGrid = new float[res * res];
        var biomeGrid = new int[res * res];
        for (int y = 0; y < res; y++)
        {
            float wz = origin.z + (y + 0.5f) / res * size.z;
            for (int x = 0; x < res; x++)
            {
                float wx = origin.x + (x + 0.5f) / res * size.x;
                float groundY = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
                depthGrid[y * res + x] = waterSurfaceY - groundY;
                biomeGrid[y * res + x] = BiomeIndexAt(wx, wz);
            }
        }

        // Fill each detail layer from the biome/entry that owns it.
        for (int layer = 0; layer < prototypeList.Count; layer++)
        {
            var map = new int[res, res];
            for (int y = 0; y < res; y++)
            {
                float wz = origin.z + (y + 0.5f) / res * size.z;
                for (int x = 0; x < res; x++)
                {
                    int idx = y * res + x;
                    float depth = depthGrid[idx];
                    if (depth < minDepth) continue;

                    Biome biome = biomes[biomeGrid[idx]];
                    if (!biomeLayerEntry[biomeGrid[idx]].TryGetValue(layer, out GroundCoverEntry gc)) continue;
                    if (depth < gc.depthRange.x || depth > gc.depthRange.y) continue;

                    float wx = origin.x + (x + 0.5f) / res * size.x;
                    float n = Mathf.PerlinNoise(wx * groundCoverNoiseScale + 3.7f,
                                                wz * groundCoverNoiseScale + 8.1f);
                    float d = gc.density * biome.densityMultiplier * n;
                    map[y, x] = Mathf.Clamp(Mathf.RoundToInt(d), 0, maxDensityPerCell);
                }
            }
            data.SetDetailLayer(0, 0, layer, map);
        }

        if (applyGentleSway)
        {
            data.wavingGrassStrength = 0.25f;
            data.wavingGrassAmount = 0.2f;
            data.wavingGrassSpeed = 0.3f;
            data.wavingGrassTint = new Color(0.6f, 0.75f, 0.6f, 1f);
        }

        terrain.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(data);
#endif
        Debug.Log($"[CoralScatter] Filled {prototypeList.Count} ground-cover layer(s) at {res}x{res}.");
    }

    [ContextMenu("Scatter All (Corals + Ground Cover)")]
    public void ScatterAll()
    {
        Scatter();
        ScatterGroundCover();
    }

    [ContextMenu("Clear Corals")]
    public void Clear()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        data.SetTreeInstances(Array.Empty<TreeInstance>(), false);
        terrain.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(data);
#endif
        Debug.Log("[CoralScatter] Cleared all terrain tree instances.");
    }

    [ContextMenu("Clear Ground Cover")]
    public void ClearGroundCover()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        // Zero every existing detail layer, then drop the prototypes.
        var empty = new int[data.detailWidth, data.detailHeight];
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            data.SetDetailLayer(0, 0, layer, empty);
        data.detailPrototypes = Array.Empty<DetailPrototype>();
        terrain.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(data);
#endif
        Debug.Log("[CoralScatter] Cleared all terrain detail (ground cover).");
    }

    // Biome region via warped low-frequency noise, split into equal bands.
    private int BiomeIndexAt(float wx, float wz)
    {
        if (biomes.Count == 1) return 0;

        // domain-warp the sample point so biome borders wander organically
        float warpX = (Mathf.PerlinNoise(wx * 0.02f + 13.1f, wz * 0.02f + 7.7f) - 0.5f) * biomeBorderWarp;
        float warpZ = (Mathf.PerlinNoise(wx * 0.02f + 91.3f, wz * 0.02f + 41.9f) - 0.5f) * biomeBorderWarp;

        float n = Mathf.PerlinNoise(
            (wx + warpX) * biomeNoiseScale + seed * 0.001f,
            (wz + warpZ) * biomeNoiseScale - seed * 0.001f);

        // Perlin clusters around 0.5, so stretch a little before banding.
        n = Mathf.Clamp01((n - 0.5f) * 1.6f + 0.5f);
        return Mathf.Clamp(Mathf.FloorToInt(n * biomes.Count), 0, biomes.Count - 1);
    }

    // Weighted random among the biome's entries whose depth range contains this depth.
    private CoralEntry PickEntry(Biome biome, float depth, System.Random rng)
    {
        float total = 0f;
        foreach (CoralEntry e in biome.entries)
        {
            if (e.prefab == null || e.weight <= 0f) continue;
            if (depth < e.depthRange.x || depth > e.depthRange.y) continue;
            total += e.weight;
        }
        if (total <= 0f) return null;

        float pick = (float)(rng.NextDouble() * total);
        foreach (CoralEntry e in biome.entries)
        {
            if (e.prefab == null || e.weight <= 0f) continue;
            if (depth < e.depthRange.x || depth > e.depthRange.y) continue;
            pick -= e.weight;
            if (pick <= 0f) return e;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Terrain target = terrain != null ? terrain : Terrain.activeTerrain;
        if (target == null) return;

        Vector3 size = target.terrainData.size;
        Vector3 origin = target.transform.position;

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireCube(
            new Vector3(origin.x + size.x * 0.5f, waterSurfaceY, origin.z + size.z * 0.5f),
            new Vector3(size.x, 0f, size.z));

        if (useScatterArea)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                new Vector3(areaCenter.x, waterSurfaceY, areaCenter.z),
                new Vector3(areaSize.x, 0f, areaSize.z));
        }
    }
}
