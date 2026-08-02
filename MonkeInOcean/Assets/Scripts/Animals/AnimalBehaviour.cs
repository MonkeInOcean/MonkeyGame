using UnityEngine;

// Shared brain for every animal in the world. Prey run away once the player gets
// close, predators close in and bite; everything else is aimless roaming.
// Subclasses only provide locomotion, the state machine lives here.
public abstract class AnimalBehaviour : MonoBehaviour
{
	protected enum AnimalState
	{
		Idle,
		Roam,
		Flee,
		Chase,
		Attack
	}

	[Header("Profile")]
	[SerializeField] protected AnimalProfile profile;

	[Header("References")]
	[SerializeField] protected Animator animator;

	[Header("World")]
	[SerializeField] protected Terrain terrain;
	[SerializeField] protected float waterSurfaceY = 97f;

	[Header("Sensing")]
	[SerializeField] private float senseInterval = 0.2f;
	[SerializeField] private float alertHoldDuration = 0.35f;

	// resolved once for the whole scene, every animal shares the same player
	private static Transform playerTransform;
	private static PlayerStats playerStats;
	private static bool playerLookupDone;

	protected Vector3 homePosition;
	protected Vector3 moveTarget;

	private float currentSpeed;
	private AnimalState state = AnimalState.Idle;
	private float stateTimer;
	private float senseTimer;
	private float attackTimer;
	private float distanceToPlayer = Mathf.Infinity;
	private string currentAnimState;
	private bool wasAlerted;

	protected AnimalState State => state;
	protected Transform Player => playerTransform;

	// ─────────────────────────────────────────
	// Lifecycle
	// ─────────────────────────────────────────
	protected virtual void Awake()
	{
		if (animator == null) animator = GetComponentInChildren<Animator>();
		if (terrain == null) terrain = Terrain.activeTerrain;
	}

	protected virtual void Start()
	{
		if (profile == null)
		{
			Debug.LogWarning($"[{GetType().Name}] '{name}' has no AnimalProfile assigned, disabling.");
			enabled = false;
			return;
		}

		if (homePosition == Vector3.zero) homePosition = transform.position;

		ResolvePlayer();

		// spread the sense ticks across frames so a herd never spikes on one frame
		senseTimer = Random.Range(0f, senseInterval);
		EnterIdle();
	}

	// Called by the seeder before Start so spawn placement drives the roam area
	// and the animal shares the seeder's idea of where the water line sits.
	public virtual void Initialize(AnimalProfile animalProfile, Vector3 home, Terrain worldTerrain, float waterLine)
	{
		profile = animalProfile;
		homePosition = home;
		waterSurfaceY = waterLine;

		if (worldTerrain != null) terrain = worldTerrain;
	}

	protected virtual void Update()
	{
		attackTimer -= Time.deltaTime;

		senseTimer -= Time.deltaTime;
		if (senseTimer <= 0f)
		{
			senseTimer = senseInterval;
			Sense();
		}

		TickState();
	}

	// ─────────────────────────────────────────
	// Sensing — cheap distance check on a staggered timer
	// ─────────────────────────────────────────
	private void Sense()
	{
		if (playerTransform == null)
		{
			distanceToPlayer = Mathf.Infinity;
			return;
		}

		distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
		bool playerDead = playerStats != null && playerStats.IsDead;

		if (state == AnimalState.Idle || state == AnimalState.Roam)
		{
			if (!playerDead && distanceToPlayer <= profile.detectRadius && CanReactToPlayer())
			{
				if (profile.isPredator) EnterChase();
				else EnterFlee();
			}

			return;
		}

		// already reacting: hold on until the player is far enough away
		if (distanceToPlayer > profile.giveUpRadius || playerDead || !CanReactToPlayer())
			EnterIdle();
	}

	// ─────────────────────────────────────────
	// State machine
	// ─────────────────────────────────────────
	private void TickState()
	{
		switch (state)
		{
			case AnimalState.Idle:
				currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, profile.acceleration * Time.deltaTime);
				stateTimer -= Time.deltaTime;
				if (stateTimer <= 0f) EnterRoam();
				break;

			case AnimalState.Roam:
				DriveTowards(moveTarget, profile.walkSpeed, profile.moveState);
				if (ReachedTarget(moveTarget)) EnterIdle();
				break;

			case AnimalState.Flee:
				// hold the startled pose for a beat before bolting
				if (stateTimer > 0f)
				{
					stateTimer -= Time.deltaTime;
					currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, profile.acceleration * Time.deltaTime);
					break;
				}

				DriveTowards(moveTarget, profile.fleeSpeed, profile.fastState);
				if (ReachedTarget(moveTarget)) PickFleeTarget();
				break;

			case AnimalState.Chase:
				if (playerTransform == null) { EnterIdle(); break; }

				moveTarget = ClampTargetToHabitat(playerTransform.position);
				DriveTowards(moveTarget, profile.fleeSpeed, profile.fastState);

				// distance is polled on the sense tick, but a bite is too tight a
				// window to lag behind by that much
				if (attackTimer <= 0f &&
					Vector3.Distance(transform.position, playerTransform.position) <= profile.attackRange)
					EnterAttack();
				break;

			case AnimalState.Attack:
				currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, profile.acceleration * Time.deltaTime);

				if (playerTransform != null)
					FaceDirection(playerTransform.position - transform.position);

				stateTimer -= Time.deltaTime;
				if (stateTimer <= 0f) EnterChase();
				break;
		}
	}

	private void EnterIdle()
	{
		state = AnimalState.Idle;
		wasAlerted = false;
		stateTimer = Random.Range(profile.idleDurationRange.x, profile.idleDurationRange.y);
		PlayState(profile.idleState);
	}

	private void EnterRoam()
	{
		moveTarget = ClampTargetToHabitat(PickRoamTarget());
		state = AnimalState.Roam;
		PlayState(profile.moveState);
	}

	private void EnterFlee()
	{
		state = AnimalState.Flee;
		PickFleeTarget();

		// only the first sighting gets the startled reaction
		stateTimer = wasAlerted ? 0f : alertHoldDuration;
		PlayState(wasAlerted ? profile.fastState : profile.alertState);
		wasAlerted = true;
	}

	private void EnterChase()
	{
		state = AnimalState.Chase;
		PlayState(profile.fastState);
	}

	private void EnterAttack()
	{
		state = AnimalState.Attack;
		attackTimer = profile.attackCooldown;
		stateTimer = profile.attackCooldown;
		PlayState(profile.attackState);

		if (playerStats != null && !playerStats.IsDead)
			playerStats.TakeDamage(profile.attackDamage);
	}

	private void PickFleeTarget()
	{
		Vector3 away = playerTransform != null
			? transform.position - playerTransform.position
			: transform.forward;

		if (away.sqrMagnitude < 0.001f) away = Random.insideUnitSphere;

		moveTarget = ClampTargetToHabitat(transform.position + away.normalized * profile.fleeDistance);
	}

	// ─────────────────────────────────────────
	// Locomotion glue
	// ─────────────────────────────────────────
	private void DriveTowards(Vector3 target, float speed, string animState)
	{
		currentSpeed = Mathf.MoveTowards(currentSpeed, speed, profile.acceleration * Time.deltaTime);
		MoveToward(target, currentSpeed);
		PlayState(animState);
	}

	protected abstract Vector3 PickRoamTarget();
	protected abstract void MoveToward(Vector3 target, float speed);
	protected abstract void FaceDirection(Vector3 direction);

	// Keeps chase/flee targets inside the habitat: fish stay wet, land animals stay dry.
	protected virtual Vector3 ClampTargetToHabitat(Vector3 target) => target;

	// Predators lose interest when the player leaves their element (e.g. a boar
	// cannot follow into deep water), prey have no such restriction by default.
	protected virtual bool CanReactToPlayer() => true;

	protected virtual bool ReachedTarget(Vector3 target) =>
		(target - transform.position).sqrMagnitude < 1f;

	// Lets a subclass remap a profile state name onto the clip it actually uses.
	protected virtual string ResolveState(string stateName) => stateName;

	// ─────────────────────────────────────────
	// Animation — the Quirky controllers expose no parameters, states are
	// played by name, so anything missing has to fail quietly
	// ─────────────────────────────────────────
	protected void PlayState(string stateName)
	{
		if (animator == null || string.IsNullOrEmpty(stateName)) return;

		stateName = ResolveState(stateName);
		if (currentAnimState == stateName) return;

		int hash = Animator.StringToHash(stateName);
		if (!animator.HasState(0, hash)) return;

		animator.CrossFade(hash, 0.15f);
		currentAnimState = stateName;
	}

	private static void ResolvePlayer()
	{
		if (playerLookupDone && playerTransform != null) return;

		PlayerMovement found = FindAnyObjectByType<PlayerMovement>();
		playerTransform = found != null ? found.transform : null;
		playerStats = found != null ? found.GetComponent<PlayerStats>() : null;
		playerLookupDone = true;
	}

	// domain reload can be disabled in the editor, so statics need clearing by hand
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		playerTransform = null;
		playerStats = null;
		playerLookupDone = false;
	}

	protected virtual void OnDrawGizmosSelected()
	{
		if (profile == null) return;

		Vector3 center = Application.isPlaying ? homePosition : transform.position;

		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(center, profile.roamRadius);

		Gizmos.color = profile.isPredator ? Color.red : Color.cyan;
		Gizmos.DrawWireSphere(transform.position, profile.detectRadius);

		if (Application.isPlaying)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawLine(transform.position, moveTarget);
		}
	}
}
