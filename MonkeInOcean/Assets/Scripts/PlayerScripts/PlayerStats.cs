using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
	[Header("Health")]
	[SerializeField] private float maxHealth = 100f;
	[SerializeField] private float drownDamageRate = 10f;

	[Header("Oxygen")]
	[SerializeField] private float maxOxygen = 100f;
	[SerializeField] private float oxygenDrainRate = 5f;
	[SerializeField] private float oxygenDrainRateWalking = 2f;
	[SerializeField] private float oxygenRegenRate = 10f;

	[Header("Life Process")]
	[SerializeField] private float hunger;
	[SerializeField] private float thirst;
	[SerializeField] private float hungerDrainRate = 1f;
	[SerializeField] private float thirstDrainRate = 1.5f;

	[SerializeField] private bool shouldPee = false;
	[SerializeField] private bool shouldPoop = false;

	[SerializeField] private float peeThreshold = 80f;
	[SerializeField] private float poopThreshold = 80f;
	/// <summary>
	/// ///Consts for the life process
	/// </summary>
	[SerializeField] private float maxHunger = 100f;
	[SerializeField] private float maxThirst = 100f;

	public float CurrentHealth { get; private set; }
	public float CurrentOxygen { get; private set; }
	public bool IsDead { get; private set; }

	public event Action OnDeath;
	public event Action<float> OnHealthChanged;
	public event Action<float> OnOxygenChanged;

	private void Awake()
	{
		CurrentHealth = maxHealth;
		CurrentOxygen = maxOxygen;
	}

	public void TakeDamage(float amount)
	{
		if (IsDead) return;

		CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
		OnHealthChanged?.Invoke(GetHealthPercent());

		if (CurrentHealth <= 0f)
		{
			IsDead = true;
			OnDeath?.Invoke();
			Debug.Log("[PlayerStats] Player died");
		}
	}

	public void Heal(float amount)
	{
		if (IsDead) return;
		CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
		OnHealthChanged?.Invoke(GetHealthPercent());
	}

	public void DrainOxygen(bool isWalking, float deltaTime)
	{
		float rate = isWalking ? oxygenDrainRateWalking : oxygenDrainRate;
		CurrentOxygen = Mathf.Max(0f, CurrentOxygen - rate * deltaTime);
		OnOxygenChanged?.Invoke(GetOxygenPercent());

		if (CurrentOxygen <= 0f)
		{
			TakeDamage(drownDamageRate * deltaTime);
		}
	}

	public void RegenOxygen(float deltaTime)
	{
		if (CurrentOxygen >= maxOxygen) return;
		CurrentOxygen = Mathf.Min(maxOxygen, CurrentOxygen + oxygenRegenRate * deltaTime);
		OnOxygenChanged?.Invoke(GetOxygenPercent());
	}

	public float GetHealthPercent() => CurrentHealth / maxHealth;
	public float GetOxygenPercent() => CurrentOxygen / maxOxygen;
}
