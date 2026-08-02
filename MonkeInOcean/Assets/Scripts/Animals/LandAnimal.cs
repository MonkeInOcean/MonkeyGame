using UnityEngine;

// Island dweller: walks the heightmap on the XZ plane, turns on its yaw only and
// refuses to wander past the shoreline.
public class LandAnimal : AnimalBehaviour
{
	[Header("Ground")]
	[SerializeField] private LayerMask groundMask;
	[SerializeField] private float groundRayHeight = 5f;
	[SerializeField] private float groundOffset = 0f;
	[SerializeField] private float groundSnapSpeed = 12f;

	[Header("Shoreline")]
	[SerializeField] private float shoreMargin = 1.5f;

	[Header("Slope")]
	[SerializeField] private bool alignToSlope = true;
	[SerializeField] private float slopeAlignSpeed = 6f;

	[Header("Obstacles")]
	[SerializeField] private float obstacleCheckDistance = 2f;
	[SerializeField] private float obstacleRadius = 0.4f;
	[SerializeField] private LayerMask obstacleMask;

	private float obstacleCheckTimer;

	protected override Vector3 PickRoamTarget()
	{
		for (int attempt = 0; attempt < 8; attempt++)
		{
			Vector2 offset = Random.insideUnitCircle * profile.roamRadius;
			Vector3 candidate = homePosition + new Vector3(offset.x, 0f, offset.y);
			candidate.y = SampleGround(candidate);

			if (candidate.y >= waterSurfaceY + shoreMargin) return candidate;
		}

		// every sample landed in the sea, stay put rather than swim
		return transform.position;
	}

	protected override void MoveToward(Vector3 target, float speed)
	{
		Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
		Vector3 direction = flatTarget - transform.position;
		direction.y = 0f;

		if (direction.sqrMagnitude > 0.001f)
		{
			FaceDirection(direction);

			Vector3 step = transform.forward * speed * Time.deltaTime;
			Vector3 next = transform.position + step;

			float groundY = SampleGround(next);

			// the shoreline is a wall: never step into water
			if (groundY >= waterSurfaceY + shoreMargin)
				transform.position = new Vector3(next.x, transform.position.y, next.z);
		}

		SnapToGround();
		CheckObstacle();
	}

	protected override void FaceDirection(Vector3 direction)
	{
		direction.y = 0f;
		if (direction.sqrMagnitude < 0.001f) return;

		Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);

		if (alignToSlope)
		{
			Vector3 normal = SampleNormal(transform.position);
			Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
			look = Quaternion.Slerp(look, tilt * look, 1f - Mathf.Exp(-slopeAlignSpeed * Time.deltaTime));
		}

		transform.rotation = Quaternion.Slerp(
			transform.rotation, look, 1f - Mathf.Exp(-profile.turnSpeed * Time.deltaTime));
	}

	protected override Vector3 ClampTargetToHabitat(Vector3 target)
	{
		target.y = SampleGround(target);
		return target;
	}

	// a land predator gives up the moment its prey is out over deep water
	protected override bool CanReactToPlayer()
	{
		if (Player == null) return false;
		return Player.position.y >= waterSurfaceY;
	}

	protected override bool ReachedTarget(Vector3 target)
	{
		Vector3 flat = target - transform.position;
		flat.y = 0f;
		return flat.sqrMagnitude < 1f;
	}

	// ─────────────────────────────────────────
	// Ground handling
	// ─────────────────────────────────────────
	private void SnapToGround()
	{
		float groundY = SampleGround(transform.position) + groundOffset;

		transform.position = new Vector3(
			transform.position.x,
			Mathf.Lerp(transform.position.y, groundY, 1f - Mathf.Exp(-groundSnapSpeed * Time.deltaTime)),
			transform.position.z);
	}

	private float SampleGround(Vector3 position)
	{
		if (terrain != null)
			return terrain.SampleHeight(position) + terrain.transform.position.y;

		if (Physics.Raycast(position + Vector3.up * groundRayHeight, Vector3.down,
			out RaycastHit hit, groundRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
			return hit.point.y;

		return position.y;
	}

	private Vector3 SampleNormal(Vector3 position)
	{
		if (terrain == null) return Vector3.up;

		TerrainData data = terrain.terrainData;
		Vector3 origin = terrain.transform.position;

		float normalizedX = Mathf.Clamp01((position.x - origin.x) / data.size.x);
		float normalizedZ = Mathf.Clamp01((position.z - origin.z) / data.size.z);

		return data.GetInterpolatedNormal(normalizedX, normalizedZ);
	}

	// Rocks and trees are not on the heightmap, so a short cast ahead re-rolls the
	// destination instead of letting the animal grind into them.
	private void CheckObstacle()
	{
		if (obstacleMask.value == 0) return;
		if (State == AnimalState.Chase || State == AnimalState.Attack) return;

		obstacleCheckTimer -= Time.deltaTime;
		if (obstacleCheckTimer > 0f) return;
		obstacleCheckTimer = 0.25f;

		Vector3 origin = transform.position + Vector3.up * obstacleRadius;

		if (Physics.SphereCast(origin, obstacleRadius, transform.forward, out _,
			obstacleCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore))
			moveTarget = ClampTargetToHabitat(PickRoamTarget());
	}
}
