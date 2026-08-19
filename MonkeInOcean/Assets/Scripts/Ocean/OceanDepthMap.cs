using UnityEngine;

namespace Ocean
{
    /// <summary>
    /// Bakes a top-down water-depth lookup from the scene Terrain and publishes it
    /// as global shader values. The Ocean shader samples it per-vertex/pixel by
    /// world XZ to:
    ///   - flatten waves in shallow water (so islands/shorelines don't get flooded),
    ///   - keep full-size waves out in deep water,
    ///   - tint the surface shallow vs deep based on the seabed below.
    ///
    /// Each texel stores (surfaceY - terrainHeight): positive = underwater depth,
    /// negative/zero = land at or above the waterline.
    ///
    /// Put this on the same object as (or near) the OceanController and assign the
    /// Terrain, or leave it empty to auto-find Terrain.activeTerrain. Re-bake via
    /// the context menu if the terrain changes at edit time.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OceanDepthMap : MonoBehaviour
    {
        [Tooltip("Terrain to bake seabed/island heights from. Empty = Terrain.activeTerrain.")]
        [SerializeField] private Terrain terrain;
        [Tooltip("Depth texture resolution. 256 is plenty for wave/colour shaping.")]
        [SerializeField] private int resolution = 256;
        [Tooltip("Water surface height used to compute depth. Match OceanController / WaterLevel.")]
        [SerializeField] private float surfaceY = WaterLevel.SurfaceY;
        [SerializeField] private bool bakeOnEnable = true;

        private Texture2D depthTex;

        private static readonly int IdTex = Shader.PropertyToID("_OceanDepthTex");
        private static readonly int IdArea = Shader.PropertyToID("_OceanDepthArea");

        private void OnEnable()
        {
            if (bakeOnEnable) Bake();
        }

        [ContextMenu("Rebake Depth Map")]
        public void Bake()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("[OceanDepthMap] No Terrain assigned or active; waves stay full-size everywhere.");
                return;
            }

            TerrainData td = terrain.terrainData;
            Vector3 tpos = terrain.transform.position;
            Vector3 size = td.size;
            resolution = Mathf.Clamp(resolution, 16, 1024);

            if (depthTex == null || depthTex.width != resolution)
            {
                depthTex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false, true)
                {
                    name = "OceanDepthMap",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var px = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;
                    float v = (y + 0.5f) / resolution;
                    float wx = tpos.x + u * size.x;
                    float wz = tpos.z + v * size.z;
                    float floorY = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + tpos.y;
                    px[y * resolution + x] = new Color(surfaceY - floorY, 0f, 0f, 0f);
                }
            }

            depthTex.SetPixels(px);
            depthTex.Apply(false, false);

            Shader.SetGlobalTexture(IdTex, depthTex);
            // xy = world-space min corner (x,z), zw = world-space span (x,z)
            Shader.SetGlobalVector(IdArea, new Vector4(tpos.x, tpos.z, size.x, size.z));
        }
    }
}
