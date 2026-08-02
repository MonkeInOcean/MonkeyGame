using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class LifeProcess : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private PlayerStats playerStats;
	[SerializeField] private PlayerMovement playerMovement;

	[Header("Hunger")]
	[SerializeField] private float maxHunger = 100f;
	[SerializeField] private float hungerDrainRate = 0.2f;
	[SerializeField] private float hungerHealthDrain = 2f;   // health drain per second when starving

	[Header("Thirst")]
	[SerializeField] private float maxThirst = 100f;
	[SerializeField] private float thirstDrainRate = 0.35f;
	[SerializeField] private float thirstHealthDrain = 3f;   // health drain per second when dehydrated

	[Header("Exertion")]
	[SerializeField] private float sprintDrainMultiplier = 2f; // hunger/thirst drain faster while running

	[Header("Pee & Poop")]
	[SerializeField] private float bladderFillRate = 0.4f;   // passive bladder fill per second
	[SerializeField] private float bowelFillRate = 0.15f;    // passive bowel fill per second
	[SerializeField] private float peeThreshold = 60f;  // how full bladder before needing pee
	[SerializeField] private float poopThreshold = 60f;
	[SerializeField] private float peeIgnoreDuration = 60f;  // seconds before slowness kicks in
	[SerializeField] private float poopIgnoreDuration = 90f;
	[SerializeField] private float peeSpeedPenalty = 0.6f; // multiplier on walkSpeed/sprintSpeed
	[SerializeField] private float poopSpeedPenalty = 0.5f;
	[SerializeField] private float peeDrainRate = 40f;   // bladder units drained per second while peeing
	[SerializeField] private float poopDrainRate = 45f;  // bowel units drained per second while pooping

	[Header("Relief FX")]
	[SerializeField] private ParticleSystem peeParticle;   // child of player, assigned in Inspector
	[SerializeField] private GameObject poopPrefab;        // spawned on poop, assigned in Inspector
	[SerializeField] private Transform poopSpawnOrigin;    // usually the player transform
	[SerializeField] private float poopSpawnBackDistance = 0.6f;
	[SerializeField] private float poopGroundRayHeight = 2f;
	[SerializeField] private LayerMask poopGroundMask = ~0;

	[Header("Ocean Water")]
	[SerializeField] private float oceanWaterThirstRestore = 15f;
	[SerializeField] private float oceanWaterHealthPenalty = 10f;
	[SerializeField] private float oceanWaterHungerPenalty = 5f;

	// ── current values ────────────────────────────────────────────
	public float CurrentHunger { get; private set; }
	public float CurrentThirst { get; private set; }
	public float BladderLevel { get; private set; } // fills as you eat/drink
	public float BowelLevel { get; private set; } // fills as you eat

	// ── flags UI reads ────────────────────────────────────────────
	public bool ShouldPee { get; private set; }
	public bool ShouldPoop { get; private set; }

	// true while any need is being ignored long enough to slow the player
	public bool IsSlowed => peeSlownessActive || poopSlownessActive;

	// ── active relief processes ───────────────────────────────────
	private bool isPeeing;
	private bool isPooping;
	public bool IsPooping => isPooping; // read by PlayerMovement to freeze

	// ── ignore timers ─────────────────────────────────────────────
	private float peeIgnoreTimer;
	private float poopIgnoreTimer;
	private bool peeSlownessActive;
	private bool poopSlownessActive;

	// ── base speeds cached from PlayerMovement ────────────────────
	private float baseWalkSpeed;
	private float baseSprintSpeed;

	// ── events ────────────────────────────────────────────────────
	public event Action<float> OnHungerChanged;
	public event Action<float> OnThirstChanged;
	public event Action OnNeedPee;
	public event Action OnNeedPoop;
	public event Action OnStarving;
	public event Action OnDehydrated;

	// ─────────────────────────────────────────
	// Lifecycle
	// ─────────────────────────────────────────
	private void Awake()
	{
		CurrentHunger = maxHunger;
		CurrentThirst = maxThirst;
		BladderLevel = 0f;
		BowelLevel = 0f;
	}

	private void Start()
	{
		if (playerMovement != null)
		{
			baseWalkSpeed = playerMovement.WalkSpeed;
			baseSprintSpeed = playerMovement.SprintSpeed;
		}

		peeParticle.Stop();
	}

	private void Update()
	{
		if (Keyboard.current != null)
		{
			if (Keyboard.current.gKey.wasPressedThisFrame && ShouldPee) Pee();
			if (Keyboard.current.hKey.wasPressedThisFrame && ShouldPoop) Poop();
		}

		DrainOverTime();
		UpdateBladderAndBowel();
		HandleRelief();
		HandleIgnoreTimers();
		ApplySpeedPenalties();
	}

	// ─────────────────────────────────────────
	// Drain over time
	// ─────────────────────────────────────────
	private void DrainOverTime()
	{
		if (playerStats.IsDead) return;

		// running burns through hunger/thirst faster
		float exertion = (playerMovement != null && playerMovement.IsSprinting) ? sprintDrainMultiplier : 1f;

		// hunger
		CurrentHunger = Mathf.Max(0f, CurrentHunger - hungerDrainRate * exertion * Time.deltaTime);
		OnHungerChanged?.Invoke(GetHungerPercent());

		if (CurrentHunger <= 0f)
		{
			OnStarving?.Invoke();
			playerStats.TakeDamage(hungerHealthDrain * Time.deltaTime);
		}

		// thirst
		CurrentThirst = Mathf.Max(0f, CurrentThirst - thirstDrainRate * exertion * Time.deltaTime);
		OnThirstChanged?.Invoke(GetThirstPercent());

		if (CurrentThirst <= 0f)
		{
			OnDehydrated?.Invoke();
			playerStats.TakeDamage(thirstHealthDrain * Time.deltaTime);
		}
	}

	// ─────────────────────────────────────────
	// Bladder and bowel fill passively
	// ─────────────────────────────────────────
	private void UpdateBladderAndBowel()
	{
		if (playerStats.IsDead) return;

		// independent accumulators: fill passively over time, drained by Pee()/Poop(),
		// topped up by Drink()/Eat(). Skip refill while actively relieving so the meter reaches zero.
		if (!isPeeing)
			BladderLevel = Mathf.Min(100f, BladderLevel + bladderFillRate * Time.deltaTime);
		if (!isPooping)
			BowelLevel = Mathf.Min(100f, BowelLevel + bowelFillRate * Time.deltaTime);

		bool newShouldPee = BladderLevel >= peeThreshold;
		bool newShouldPoop = BowelLevel >= poopThreshold;

		if (newShouldPee && !ShouldPee)
		{
			ShouldPee = true;
			OnNeedPee?.Invoke();
		}

		if (newShouldPoop && !ShouldPoop)
		{
			ShouldPoop = true;
			OnNeedPoop?.Invoke();
		}
	}

	// ─────────────────────────────────────────
	// Active relief — drain the meters gradually while peeing/pooping
	// ─────────────────────────────────────────
	private void HandleRelief()
	{
		if (isPeeing)
		{
			BladderLevel = Mathf.Max(0f, BladderLevel - peeDrainRate * Time.deltaTime);
			if (BladderLevel <= 0f)
			{
				isPeeing = false;
				ShouldPee = false;
				peeIgnoreTimer = 0f;
				peeSlownessActive = false;
				if (playerMovement != null) playerMovement.WalkSpeed = baseWalkSpeed;
				if (peeParticle != null) peeParticle.Stop();
			}
		}

		if (isPooping)
		{
			BowelLevel = Mathf.Max(0f, BowelLevel - poopDrainRate * Time.deltaTime);
			if (BowelLevel <= 0f)
			{
				isPooping = false;
				ShouldPoop = false;
				poopIgnoreTimer = 0f;
				poopSlownessActive = false;
				if (playerMovement != null) playerMovement.SprintSpeed = baseSprintSpeed;
			}
		}
	}

	// ─────────────────────────────────────────
	// Ignore timers — slowness kicks in if ignored too long
	// ─────────────────────────────────────────
	private void HandleIgnoreTimers()
	{
		if (ShouldPee)
		{
			peeIgnoreTimer += Time.deltaTime;
			peeSlownessActive = peeIgnoreTimer >= peeIgnoreDuration;
		}

		if (ShouldPoop)
		{
			poopIgnoreTimer += Time.deltaTime;
			poopSlownessActive = poopIgnoreTimer >= poopIgnoreDuration;
		}
	}

	// ─────────────────────────────────────────
	// Speed penalties — writes directly to PlayerMovement exposed props
	// ─────────────────────────────────────────
	private void ApplySpeedPenalties()
	{
		if (playerMovement == null) return;

		float multiplier = 1f;

		if (peeSlownessActive) multiplier *= peeSpeedPenalty;
		if (poopSlownessActive) multiplier *= poopSpeedPenalty;

		playerMovement.WalkSpeed = baseWalkSpeed * multiplier;
		playerMovement.SprintSpeed = baseSprintSpeed * multiplier;
	}

	// ─────────────────────────────────────────
	// Food and drink — call these from item interaction later
	// ─────────────────────────────────────────
	public void Eat(float hungerRestore, float bowelIncrease = 8f)
	{
		CurrentHunger = Mathf.Min(maxHunger, CurrentHunger + hungerRestore);
		BowelLevel = Mathf.Min(100f, BowelLevel + bowelIncrease);
		OnHungerChanged?.Invoke(GetHungerPercent());
	}

	public void Drink(float thirstRestore, float bladderIncrease = 8f)
	{
		CurrentThirst = Mathf.Min(maxThirst, CurrentThirst + thirstRestore);
		BladderLevel = Mathf.Min(100f, BladderLevel + bladderIncrease);
		OnThirstChanged?.Invoke(GetThirstPercent());
	}

	public void DrinkOceanWater()
	{
		Drink(oceanWaterThirstRestore, 20f);
		playerStats.TakeDamage(oceanWaterHealthPenalty);
		CurrentHunger = Mathf.Max(0f, CurrentHunger - oceanWaterHungerPenalty);
		OnHungerChanged?.Invoke(GetHungerPercent());
	}

	// ─────────────────────────────────────────
	// Pee and poop — call from UI/animation later
	// ─────────────────────────────────────────
	public void Pee()
	{
		if (isPeeing) return;
		isPeeing = true;

		if (peeParticle != null) peeParticle.Play();
	}

	public void Poop()
	{
		if (isPooping) return;
		isPooping = true;

		if (poopPrefab != null)
		{
			Transform origin = poopSpawnOrigin != null ? poopSpawnOrigin : transform;

			Vector3 pos = origin.position - origin.forward * poopSpawnBackDistance;
			
			if (Physics.Raycast(pos + Vector3.up * poopGroundRayHeight, Vector3.down,
				out RaycastHit hit, poopGroundRayHeight * 2f, poopGroundMask))
				pos = hit.point;
			
			Instantiate(poopPrefab, pos, poopPrefab.transform.rotation);
		}
	}

	// ─────────────────────────────────────────
	// Getters for UI
	// ─────────────────────────────────────────
	public float GetHungerPercent() => CurrentHunger / maxHunger;
	public float GetThirstPercent() => CurrentThirst / maxThirst;
	public float GetBladderPercent() => BladderLevel / 100f;
	public float GetBowelPercent() => BowelLevel / 100f;
}
