using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Scatters collectable prefabs over the terrain at game start and keeps topping
// the world back up as the player picks them up.
public class ItemSeeder : MonoBehaviour
{
	[Serializable]
	public class SpawnEntry
	{
		public ItemData item;
		public int targetCount = 10;

		[NonSerialized] public List<GameObject> live = new List<GameObject>();
	}

	[Header("Terrain")]
	[SerializeField] private Terrain terrain;

	[Header("Items")]
	[SerializeField] private List<SpawnEntry> entries = new List<SpawnEntry>();

	[Header("Water Line")]
	// Single source of truth for the water line — see Ocean.WaterLevel.
	private float waterSurfaceY => Ocean.WaterLevel.SurfaceY;
	[SerializeField] private float shoreMargin = 1.5f;

	[Header("Placement")]
	[SerializeField] private float maxSlopeDegrees = 30f;
	[SerializeField] private float minSpacing = 3f;
	[SerializeField] private LayerMask blockingMask;
	[SerializeField] private float spawnHeightOffset = 0.3f;
	[SerializeField] private bool randomYRotation = true;
	[SerializeField] private int maxAttemptsPerItem = 60;

	[Header("Spawn Area (XZ only, world space)")]
	[SerializeField] private bool useSpawnArea;
	[SerializeField] private Vector3 spawnAreaCenter = new Vector3(221f, 100f, 207f);
	[SerializeField] private Vector3 spawnAreaSize = new Vector3(600f, 0f, 600f);

	[Header("Respawn")]
	[SerializeField] private float respawnInterval = 30f;
	[SerializeField] private float minDistanceFromPlayer = 25f;
	[SerializeField] private Transform player;

	[Header("Performance")]
	[SerializeField] private int spawnsPerFrame = 5;

	private void Start()
	{
		if (terrain == null) terrain = Terrain.activeTerrain;

		if (terrain == null)
		{
			Debug.LogError("[ItemSeeder] No terrain assigned and no active terrain in the scene, seeding disabled.");
			enabled = false;
			return;
		}

		if (player == null)
		{
			PlayerMovement found = FindAnyObjectByType<PlayerMovement>();
			if (found != null) player = found.transform;
		}

		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return SeedAll(false);

		if (respawnInterval <= 0f) yield break;

		WaitForSeconds wait = new WaitForSeconds(respawnInterval);

		while (true)
		{
			yield return wait;
			yield return SeedAll(true);
		}
	}

	// ─────────────────────────────────────────
	// Tops every entry back up to its target count
	// ─────────────────────────────────────────
	private IEnumerator SeedAll(bool avoidPlayer)
	{
		int budget = 0;

		foreach (SpawnEntry entry in entries)
		{
			if (entry.item == null) continue;

			if (entry.item.worldPrefab == null)
			{
				Debug.LogWarning($"[ItemSeeder] '{entry.item.itemName}' has no worldPrefab assigned, skipping.");
				continue;
			}

			// picked-up items were destroyed, so their slots read back as null
			entry.live.RemoveAll(go => go == null);

			int missing = entry.targetCount - entry.live.Count;

			for (int i = 0; i < missing; i++)
			{
				if (TryGetSpawnPoint(avoidPlayer, out Vector3 point))
					entry.live.Add(Spawn(entry.item.worldPrefab, point));

				if (++budget >= Mathf.Max(1, spawnsPerFrame))
				{
					budget = 0;
					yield return null;
				}
			}
		}
	}

	private GameObject Spawn(GameObject prefab, Vector3 position)
	{
		Quaternion rotation = prefab.transform.rotation;

		if (randomYRotation)
			rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * rotation;

		return Instantiate(prefab, position, rotation, transform);
	}

	// ─────────────────────────────────────────
	// Rejection sampling: random XZ, drop onto the heightmap, then filter out
	// anything below the water line, too steep, or already occupied
	// ─────────────────────────────────────────
	private bool TryGetSpawnPoint(bool avoidPlayer, out Vector3 point)
	{
		point = Vector3.zero;

		TerrainData data = terrain.terrainData;
		Vector3 origin = terrain.transform.position;
		Vector3 size = data.size;

		float minX = origin.x;
		float maxX = origin.x + size.x;
		float minZ = origin.z;
		float maxZ = origin.z + size.z;

		if (useSpawnArea)
		{
			minX = Mathf.Max(minX, spawnAreaCenter.x - spawnAreaSize.x * 0.5f);
			maxX = Mathf.Min(maxX, spawnAreaCenter.x + spawnAreaSize.x * 0.5f);
			minZ = Mathf.Max(minZ, spawnAreaCenter.z - spawnAreaSize.z * 0.5f);
			maxZ = Mathf.Min(maxZ, spawnAreaCenter.z + spawnAreaSize.z * 0.5f);

			if (minX >= maxX || minZ >= maxZ) return false;
		}

		float minGroundY = waterSurfaceY + shoreMargin;

		// the terrain would fail every clearance test, so it never counts as blocking
		int clearance = blockingMask.value & ~(1 << terrain.gameObject.layer);

		for (int attempt = 0; attempt < Mathf.Max(1, maxAttemptsPerItem); attempt++)
		{
			Vector3 candidate = new Vector3(
				UnityEngine.Random.Range(minX, maxX),
				0f,
				UnityEngine.Random.Range(minZ, maxZ));

			candidate.y = terrain.SampleHeight(candidate) + origin.y;

			if (candidate.y < minGroundY) continue;

			float normalizedX = (candidate.x - origin.x) / size.x;
			float normalizedZ = (candidate.z - origin.z) / size.z;

			Vector3 normal = data.GetInterpolatedNormal(normalizedX, normalizedZ);
			if (Vector3.Angle(normal, Vector3.up) > maxSlopeDegrees) continue;

			if (avoidPlayer && player != null &&
				Vector3.Distance(candidate, player.position) < minDistanceFromPlayer) continue;

			if (minSpacing > 0f && clearance != 0 &&
				Physics.CheckSphere(candidate + Vector3.up * minSpacing, minSpacing, clearance,
					QueryTriggerInteraction.Ignore)) continue;

			point = candidate + Vector3.up * spawnHeightOffset;
			return true;
		}

		return false;
	}

	// ─────────────────────────────────────────
	// Editor helpers
	// ─────────────────────────────────────────
	[ContextMenu("Reseed Now")]
	private void ReseedNow()
	{
		if (!Application.isPlaying)
		{
			Debug.LogWarning("[ItemSeeder] Reseed only works in play mode.");
			return;
		}

		foreach (SpawnEntry entry in entries)
		{
			foreach (GameObject spawned in entry.live)
				if (spawned != null) Destroy(spawned);

			entry.live.Clear();
		}

		StartCoroutine(SeedAll(false));
	}

	private void OnDrawGizmosSelected()
	{
		Terrain target = terrain != null ? terrain : Terrain.activeTerrain;
		float lineY = waterSurfaceY + shoreMargin;

		if (target != null)
		{
			Vector3 size = target.terrainData.size;
			Vector3 origin = target.transform.position;

			Gizmos.color = new Color(0.2f, 0.7f, 1f, 1f);
			Gizmos.DrawWireCube(
				new Vector3(origin.x + size.x * 0.5f, lineY, origin.z + size.z * 0.5f),
				new Vector3(size.x, 0f, size.z));
		}

		if (useSpawnArea)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(
				new Vector3(spawnAreaCenter.x, lineY, spawnAreaCenter.z),
				new Vector3(spawnAreaSize.x, 0f, spawnAreaSize.z));
		}
	}
}
