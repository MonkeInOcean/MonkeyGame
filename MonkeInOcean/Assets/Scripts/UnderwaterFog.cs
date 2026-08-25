using UnityEngine;

/// <summary>
/// Above-water atmospheric fog only. The underwater look is now owned by the
/// per-camera <see cref="Rendering.UnderwaterRendererFeature"/> on the URP
/// renderer, so this no longer drives the heavy depth-based fog underwater —
/// that global toggle can't work once there are multiple player cameras.
///
/// When the local player submerges this simply switches Unity's global
/// <c>RenderSettings.fog</c> OFF so it doesn't tint the underwater scene and
/// fight the renderer feature. On land it restores the light distance fog.
/// </summary>
public class UnderwaterFog : MonoBehaviour
{
    [Header("Land Fog")]
    [SerializeField] private Color landFogColor = new Color(0.7f, 0.8f, 0.9f, 1f);
    [SerializeField] private float landFogDensity = 0.002f;

    [Header("Transition")]
    [SerializeField] private float transitionSpeed = 2f;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = landFogColor;
        RenderSettings.fogDensity = landFogDensity;
    }

    private void Update()
    {
        if (playerMovement != null && playerMovement.isSwimming)
        {
            // Underwater is handled by the renderer feature; keep global fog out of it.
            if (RenderSettings.fog) RenderSettings.fog = false;
        }
        else
        {
            if (!RenderSettings.fog) RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, landFogColor, transitionSpeed * Time.deltaTime);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, landFogDensity, transitionSpeed * Time.deltaTime);
        }
    }
}
