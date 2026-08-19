using UnityEngine;

namespace Ocean
{
    /// <summary>
    /// Owns the ocean surface: builds the grid mesh, keeps it centered on the
    /// camera (snapped to the grid cell so vertices don't shimmer), and publishes
    /// the water level + wave parameters as global shader values so the underwater
    /// renderer feature, caustics and the C# buoyancy sampler all stay in sync
    /// with the visible surface.
    ///
    /// Put this on an empty GameObject; it creates its own MeshFilter/MeshRenderer.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OceanController : MonoBehaviour
    {
        [Header("Surface")]
        [Tooltip("World Y of the flat rest height of the ocean. Keep in sync with gameplay water level.")]
        [SerializeField] private float surfaceY = WaterLevel.SurfaceY;
        [SerializeField] private Material oceanMaterial;

        [Header("Grid")]
        [Tooltip("Quads per side. Higher = smoother waves, more cost. Keep cell size " +
                 "(Size / Resolution) well below the shortest wavelength to avoid faceting.")]
        [SerializeField] private int resolution = 400;
        [Tooltip("World size of the grid. Should comfortably exceed the view distance.")]
        [SerializeField] private float size = 800f;
        [SerializeField] private bool followCamera = true;
        [SerializeField] private bool snapToCell = true;

        [Header("Wave Sync (must match the material)")]
        [Tooltip("If assigned, wave params are read from this material and pushed to globals for buoyancy/caustics.")]
        [SerializeField] private bool publishWaveGlobals = true;

        // Global shader property ids shared with the underwater / caustics passes.
        private static readonly int IdSurfaceY = Shader.PropertyToID("_OceanSurfaceY");
        private static readonly int IdWaveA = Shader.PropertyToID("_OceanWaveA");
        private static readonly int IdWaveB = Shader.PropertyToID("_OceanWaveB");
        private static readonly int IdWaveC = Shader.PropertyToID("_OceanWaveC");
        private static readonly int IdWaveD = Shader.PropertyToID("_OceanWaveD");
        private static readonly int IdWaveSpeed = Shader.PropertyToID("_OceanWaveSpeed");

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private int builtResolution = -1;
        private float builtSize = -1f;

        /// <summary>Canonical rest height of the ocean surface.</summary>
        public float SurfaceY => surfaceY;

        private void OnEnable()
        {
            EnsureComponents();
            Rebuild();
        }

        private void EnsureComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            if (oceanMaterial != null) meshRenderer.sharedMaterial = oceanMaterial;
        }

        private void Rebuild()
        {
            if (mesh == null || builtResolution != resolution || builtSize != size)
            {
                mesh = OceanMeshBuilder.Build(resolution, size);
                meshFilter.sharedMesh = mesh;
                builtResolution = resolution;
                builtSize = size;
            }
        }

        private void Update()
        {
            EnsureComponents();
            Rebuild();
            FollowCamera();
            PublishGlobals();
        }

        private void FollowCamera()
        {
            Vector3 pos = transform.position;
            pos.y = surfaceY;

            if (followCamera)
            {
                Camera cam = GetActiveCamera();
                if (cam != null)
                {
                    Vector3 camPos = cam.transform.position;
                    if (snapToCell)
                    {
                        float cell = size / Mathf.Max(resolution, 1);
                        pos.x = Mathf.Round(camPos.x / cell) * cell;
                        pos.z = Mathf.Round(camPos.z / cell) * cell;
                    }
                    else
                    {
                        pos.x = camPos.x;
                        pos.z = camPos.z;
                    }
                }
            }

            transform.position = pos;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private Camera GetActiveCamera()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var sv = UnityEditor.SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null) return sv.camera;
            }
#endif
            return Camera.main;
        }

        private void PublishGlobals()
        {
            Shader.SetGlobalFloat(IdSurfaceY, surfaceY);

            if (publishWaveGlobals && oceanMaterial != null)
            {
                Shader.SetGlobalVector(IdWaveA, oceanMaterial.GetVector("_WaveA"));
                Shader.SetGlobalVector(IdWaveB, oceanMaterial.GetVector("_WaveB"));
                Shader.SetGlobalVector(IdWaveC, oceanMaterial.GetVector("_WaveC"));
                Shader.SetGlobalVector(IdWaveD, oceanMaterial.GetVector("_WaveD"));
                Shader.SetGlobalFloat(IdWaveSpeed, oceanMaterial.GetFloat("_WaveSpeed"));
            }
        }
    }
}
