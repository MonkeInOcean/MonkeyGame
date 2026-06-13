using System;
using UnityEngine;

public class LifeProcess : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private PlayerStats playerStats;
	[SerializeField] private PlayerMovement playerMovement;

	[Header("Hunger")]
	[SerializeField] private float maxHunger = 100f;
	[SerializeField] private float hungerDrainRate = 1f;
	[SerializeField] private float hungerHealthDrain = 2f;   // health drain per second when starving

	[Header("Thirst")]
	[SerializeField] private float maxThirst = 100f;
	[SerializeField] private float thirstDrainRate = 1.5f;
	[SerializeField] private float thirstHealthDrain = 3f;   // health drain per second when dehydrated

	[Header("Pee & Poop")]
	[SerializeField] private float peeThreshold = 80f;  // how full bladder before needing pee
	[SerializeField] private float poopThreshold = 80f;
	[SerializeField] private float peeIgnoreDuration = 60f;  // seconds before slowness kicks in
	[SerializeField] private float poopIgnoreDuration = 90f;
	[SerializeField] private float peeSpeedPenalty = 0.6f; // multiplier on walkSpeed/sprintSpeed
	[SerializeField] private float poopSpeedPenalty = 0.5f;

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
	}

	private void Update()
	{
		DrainOverTime();
		UpdateBladderAndBowel();
		HandleIgnoreTimers();
		ApplySpeedPenalties();
	}

	// ─────────────────────────────────────────
	// Drain over time
	// ─────────────────────────────────────────
	private void DrainOverTime()
	{
		if (playerStats.IsDead) return;

		// hunger
		CurrentHunger = Mathf.Max(0f, CurrentHunger - hungerDrainRate * Time.deltaTime);
		OnHungerChanged?.Invoke(GetHungerPercent());

		if (CurrentHunger <= 0f)
		{
			OnStarving?.Invoke();
			playerStats.TakeDamage(hungerHealthDrain * Time.deltaTime);
		}

		// thirst
		CurrentThirst = Mathf.Max(0f, CurrentThirst - thirstDrainRate * Time.deltaTime);
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
		// bladder fills as thirst depletes (you drank, now it processes)
		float drankAmount = (maxThirst - CurrentThirst) / maxThirst;
		BladderLevel = Mathf.Min(100f, drankAmount * 100f);

		// bowel fills as hunger depletes
		float ateAmount = (maxHunger - CurrentHunger) / maxHunger;
		BowelLevel = Mathf.Min(100f, ateAmount * 100f);

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
	public void Eat(float hungerRestore, float bowelIncrease = 10f)
	{
		CurrentHunger = Mathf.Min(maxHunger, CurrentHunger + hungerRestore);
		BowelLevel = Mathf.Min(100f, BowelLevel + bowelIncrease);
		OnHungerChanged?.Invoke(GetHungerPercent());
	}

	public void Drink(float thirstRestore, float bladderIncrease = 10f)
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
		BladderLevel = 0f;
		ShouldPee = false;
		peeIgnoreTimer = 0f;
		peeSlownessActive = false;

		// restore speed immediately
		if (playerMovement != null)
			playerMovement.WalkSpeed = baseWalkSpeed;
	}

	public void Poop()
	{
		BowelLevel = 0f;
		ShouldPoop = false;
		poopIgnoreTimer = 0f;
		poopSlownessActive = false;

		if (playerMovement != null)
			playerMovement.SprintSpeed = baseSprintSpeed;
	}

	// ─────────────────────────────────────────
	// Getters for UI
	// ─────────────────────────────────────────
	public float GetHungerPercent() => CurrentHunger / maxHunger;
	public float GetThirstPercent() => CurrentThirst / maxThirst;
	public float GetBladderPercent() => BladderLevel / 100f;
	public float GetBowelPercent() => BowelLevel / 100f;
}
