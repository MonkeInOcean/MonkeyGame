using System.Collections.Generic;
using UnityEngine;

// Adds a black inverted-hull outline to every renderer on this object and its children.
public class ItemOutline : MonoBehaviour
{
	[Header("Outline")]
	[SerializeField] private Material outlineMaterial; // optional; created from Custom/ItemOutline if null
	[SerializeField] private Color outlineColor = Color.black;
	[SerializeField, Range(0f, 0.1f)] private float outlineWidth = 0.03f;

	private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
	private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

	private bool applied;

	private void Awake()
	{
		ApplyOutline();
	}

	public void ApplyOutline()
	{
		if (applied) return;

		Material mat = GetOrCreateMaterial();
		if (mat == null)
		{
			Debug.LogWarning("[ItemOutline] Could not find shader 'Custom/ItemOutline'.");
			return;
		}

		Renderer[] renderers = GetComponentsInChildren<Renderer>();
		foreach (Renderer r in renderers)
		{
			List<Material> mats = new List<Material>();
			r.GetSharedMaterials(mats);

			// avoid double-adding if this runs twice
			if (mats.Contains(mat)) continue;

			mats.Add(mat);
			r.materials = mats.ToArray();
		}

		applied = true;
	}

	private Material GetOrCreateMaterial()
	{
		if (outlineMaterial != null)
			return outlineMaterial;

		Shader shader = Shader.Find("Custom/ItemOutline");
		if (shader == null) return null;

		outlineMaterial = new Material(shader);
		outlineMaterial.SetColor(OutlineColorId, outlineColor);
		outlineMaterial.SetFloat(OutlineWidthId, outlineWidth);
		return outlineMaterial;
	}
}
