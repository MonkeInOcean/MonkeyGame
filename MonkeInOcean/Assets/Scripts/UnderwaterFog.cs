using UnityEngine;

public class UnderwaterFog : MonoBehaviour
{
	[Header("Land Fog")]
	[SerializeField] private Color landFogColor = new Color(0.7f, 0.8f, 0.9f, 1f);
	[SerializeField] private float landFogDensity = 0.002f;

	[Header("Underwater Fog")]
	[SerializeField] private Color shallowFogColor = new Color(0.1f, 0.5f, 0.6f, 1f);
	[SerializeField] private Color deepFogColor = new Color(0.01f, 0.05f, 0.15f, 1f);
	[SerializeField] private float shallowFogDensity = 0.02f;
	[SerializeField] private float deepFogDensity = 0.12f;

	[Header("Transition")]
	[SerializeField] private float transitionSpeed = 2f;

	[Header("References")]
	[SerializeField] private PlayerMovement playerMovement;

	private const float waterSurfaceY = 95f;
	private const float maxDepth = 95f; // 95 - 0

	private void Start()
	{
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Exponential;
		RenderSettings.fogColor = landFogColor;
		RenderSettings.fogDensity = landFogDensity;
	}

	private void Update()
	{
		if (playerMovement.isSwimming)
			ApplyUnderwaterFog();
		else
			ApplyLandFog();
	}

	// lerps fog color and density based on how deep the player is
	private void ApplyUnderwaterFog()
	{
		float depth = Mathf.Clamp(waterSurfaceY - playerMovement.transform.position.y, 0f, maxDepth);
		float depthRatio = depth / maxDepth;

		Color targetColor = Color.Lerp(shallowFogColor, deepFogColor, depthRatio);
		float targetDensity = Mathf.Lerp(shallowFogDensity, deepFogDensity, depthRatio);

		RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetColor, transitionSpeed * Time.deltaTime);
		RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetDensity, transitionSpeed * Time.deltaTime);
	}

	// smoothly returns to land fog when exiting water
	private void ApplyLandFog()
	{
		RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, landFogColor, transitionSpeed * Time.deltaTime);
		RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, landFogDensity, transitionSpeed * Time.deltaTime);
	}
}