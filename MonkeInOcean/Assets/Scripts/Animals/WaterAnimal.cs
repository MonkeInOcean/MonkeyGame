using UnityEngine;

// Ocean dweller: swims freely in 3D, pitching into its heading, and stays inside a
// depth band between the sea floor and the surface.
public class WaterAnimal : AnimalBehaviour
{
	[Header("Water Volume")]
	[SerializeField] private float minSubmergence = 1.5f;
	[SerializeField] private float floorClearance = 1f;

	[Header("Swimming")]
	[SerializeField] private string swimState = "Swim";
	[SerializeField] private float verticalRoamRange = 6f;
	[SerializeField] private float depthCorrectSpeed = 2f;

	// a fish has no gait: both the roaming and the panicked pace read as swimming,
	// only the speed differs
	protected override string ResolveState(string stateName)
	{
		if (string.IsNullOrEmpty(swimState)) return stateName;
		return stateName == profile.moveState || stateName == profile.fastState ? swimState : stateName;
	}

	protected override Vector3 PickRoamTarget()
	{
		Vector2 offset = Random.insideUnitCircle * profile.roamRadius;

		Vector3 candidate = new Vector3(
			homePosition.x + offset.x,
			homePosition.y + Random.Range(-verticalRoamRange, verticalRoamRange),
			homePosition.z + offset.y);

		return ClampTargetToHabitat(candidate);
	}

	protected override void MoveToward(Vector3 target, float speed)
	{
		Vector3 direction = target - transform.position;

		if (direction.sqrMagnitude > 0.001f)
		{
			FaceDirection(direction);
			transform.position += transform.forward * speed * Time.deltaTime;
		}

		HoldDepthBand();
	}

	protected override void FaceDirection(Vector3 direction)
	{
		if (direction.sqrMagnitude < 0.001f) return;

		Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);

		transform.rotation = Quaternion.Slerp(
			transform.rotation, look, 1f - Mathf.Exp(-profile.turnSpeed * Time.deltaTime));
	}

	protected override Vector3 ClampTargetToHabitat(Vector3 target)
	{
		float floor = SampleFloor(target) + floorClearance;
		float ceiling = waterSurfaceY - minSubmergence;

		target.y = ceiling > floor ? Mathf.Clamp(target.y, floor, ceiling) : floor;
		return target;
	}

	// a fish stops caring the moment the player climbs back onto dry land
	protected override bool CanReactToPlayer()
	{
		if (Player == null) return false;
		return Player.position.y < waterSurfaceY;
	}

	// Steering can still nose the fish through the sea bed or out of the water, so
	// the band is re-asserted after every move.
	private void HoldDepthBand()
	{
		float floor = SampleFloor(transform.position) + floorClearance;
		float ceiling = waterSurfaceY - minSubmergence;
		float clamped = ceiling > floor
			? Mathf.Clamp(transform.position.y, floor, ceiling)
			: floor;

		if (Mathf.Approximately(clamped, transform.position.y)) return;

		transform.position = new Vector3(
			transform.position.x,
			Mathf.MoveTowards(transform.position.y, clamped, depthCorrectSpeed * Time.deltaTime),
			transform.position.z);
	}

	private float SampleFloor(Vector3 position)
	{
		if (terrain == null) return waterSurfaceY - verticalRoamRange * 2f;
		return terrain.SampleHeight(position) + terrain.transform.position.y;
	}
}
