using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Populates the island with land animals and the ocean with fish, then keeps both
// populations topped up as they get killed off or culled.
public class AnimalSeeder : MonoBehaviour
{
	[Serializable]
	public class SpawnEntry
	{
		public AnimalProfile profile;
		public int targetCount = 5;

		[NonSerialized] public List<GameObject> live = new List<GameObject>();
	}

	[Header("Terrain")]
	[SerializeField] private Terrain terrain;

	[Header("Animals")]
	[SerializeField] private List<SpawnEntry> entries = new List<SpawnEntry>();

	[Header("Water Line")]
	// Single source of truth for the water line — see Ocean.WaterLevel.
	private float waterSurfaceY => Ocean.WaterLevel.SurfaceY;
	[SerializeField] private float shoreMargin = 1.5f;

	[Header("Land Placement")]
	[SerializeField] private float maxSlopeDegrees = 35f;
	[SerializeField] private float minSpacing = 2f;
	[SerializeField] private LayerMask blockingMask;
	[SerializeField] private float spawnHeightOffset = 0.1f;
	[SerializeField] private int maxAttemptsPerAnimal = 60;

	[Header("Water Placement")]
	[SerializeField] private float minSubmergence = 1.5f;
	[SerializeField] private float floorClearance = 1f;

	[Header("Spawn Area (XZ only, world space)")]
	[SerializeField] private bool useSpawnArea;
	[SerializeField] private Vector3 spawnAreaCenter = new Vector3(221f, 100f, 207f);
	[SerializeField] private Vector3 spawnAreaSize = new Vector3(600f, 0f, 600f);

	[Header("Respawn")]
	[SerializeField] private float respawnInterval = 45f;
	[SerializeField] private float minDistanceFromPlayer = 40f;
	[SerializeField] private Transform player;

	[Header("Performance")]
	[SerializeField] private int spawnsPerFrame = 3;

	private void Start()
	{
		if (terrain == null) terrain = Terrain.activeTerrain;

		if (terrain == null)
		{
			Debug.LogError("[AnimalSeeder] No terrain assigned and no active terrain in the scene, seeding disabled.");
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
			if (entry.profile == null) continue;

			if (entry.profile.prefab == null)
			{
				Debug.LogWarning($"[AnimalSeeder] '{entry.profile.name}' has no prefab assigned, skipping.");
				continue;
			}

			// killed or destroyed animals read back as null slots
			entry.live.RemoveAll(go => go == null);

			int missing = entry.targetCount - entry.live.Count;

			for (int i = 0; i < missing; i++)
			{
				if (TryGetSpawnPoint(entry.profile.habitat, avoidPlayer, out Vector3 point))
					entry.live.Add(Spawn(entry.profile, point));

				if (++budget >= Mathf.Max(1, spawnsPerFrame))
				{
					budget = 0;
					yield return null;
				}
			}
		}
	}

	private GameObject Spawn(AnimalProfile profile, Vector3 position)
	{
		Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
		GameObject spawned = Instantiate(profile.prefab, position, rotation, transform);

		Vector2 range = profile.scaleRange;
		if (range.x > 0f && range.y > 0f)
			spawned.transform.localScale *= UnityEngine.Random.Range(range.x, range.y);

		// the art prefabs ship bare, so the brain gets attached on the way in
		AnimalBehaviour behaviour = spawned.GetComponent<AnimalBehaviour>();

		if (behaviour == null)
		{
			behaviour = profile.habitat == AnimalHabitat.Water
				? spawned.AddComponent<WaterAnimal>()
				: spawned.AddComponent<LandAnimal>();
		}

		behaviour.Initialize(profile, position, terrain, waterSurfaceY);
		return spawned;
	}

	// ─────────────────────────────────────────
	// Rejection sampling: random XZ, drop onto the heightmap, then filter by habitat
	// ─────────────────────────────────────────
	private bool TryGetSpawnPoint(AnimalHabitat habitat, bool avoidPlayer, out Vector3 point)
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

		// the terrain would fail every clearance test, so it never counts as blocking
		int clearance = blockingMask.value & ~(1 << terrain.gameObject.layer);

		for (int attempt = 0; attempt < Mathf.Max(1, maxAttemptsPerAnimal); attempt++)
		{
			Vector3 candidate = new Vector3(
				UnityEngine.Random.Range(minX, maxX),
				0f,
				UnityEngine.Random.Range(minZ, maxZ));

			float groundY = terrain.SampleHeight(candidate) + origin.y;

			if (habitat == AnimalHabitat.Water)
			{
				float floor = groundY + floorClearance;
				float ceiling = waterSurfaceY - minSubmergence;

				// too shallow to hold a fish
				if (ceiling <= floor) continue;

				candidate.y = UnityEngine.Random.Range(floor, ceiling);
			}
			else
			{
				if (groundY < waterSurfaceY + shoreMargin) continue;

				float normalizedX = (candidate.x - origin.x) / size.x;
				float normalizedZ = (candidate.z - origin.z) / size.z;

				Vector3 normal = data.GetInterpolatedNormal(normalizedX, normalizedZ);
				if (Vector3.Angle(normal, Vector3.up) > maxSlopeDegrees) continue;

				candidate.y = groundY + spawnHeightOffset;

				if (minSpacing > 0f && clearance != 0 &&
					Physics.CheckSphere(candidate + Vector3.up * minSpacing, minSpacing, clearance,
						QueryTriggerInteraction.Ignore)) continue;
			}

			if (avoidPlayer && player != null &&
				Vector3.Distance(candidate, player.position) < minDistanceFromPlayer) continue;

			point = candidate;
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
			Debug.LogWarning("[AnimalSeeder] Reseed only works in play mode.");
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

		if (target != null)
		{
			Vector3 size = target.terrainData.size;
			Vector3 origin = target.transform.position;

			Gizmos.color = new Color(0.2f, 0.7f, 1f, 1f);
			Gizmos.DrawWireCube(
				new Vector3(origin.x + size.x * 0.5f, waterSurfaceY, origin.z + size.z * 0.5f),
				new Vector3(size.x, 0f, size.z));
		}

		if (useSpawnArea)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(
				new Vector3(spawnAreaCenter.x, waterSurfaceY, spawnAreaCenter.z),
				new Vector3(spawnAreaSize.x, 0f, spawnAreaSize.z));
		}
	}
}
