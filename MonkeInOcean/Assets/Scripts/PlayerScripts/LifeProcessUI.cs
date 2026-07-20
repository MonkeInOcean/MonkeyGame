using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Drives the survival HUD: sliders for health/oxygen/hunger/thirst/bladder/bowel
// (with optional percentage labels) plus alert icons and a death overlay.
public class LifeProcessUI : MonoBehaviour
{
	[Header("Sources")]
	[SerializeField] private PlayerStats playerStats;
	[SerializeField] private LifeProcess lifeProcess;

	[Header("Bars (Slider, value 0..1)")]
	[SerializeField] private Slider healthSlider;
	[SerializeField] private Slider oxygenSlider;
	[SerializeField] private Slider hungerSlider;
	[SerializeField] private Slider thirstSlider;
	[SerializeField] private Slider bladderSlider;
	[SerializeField] private Slider bowelSlider;
	[SerializeField] private Slider staminaSlider;

	[Header("Percentage Labels (optional)")]
	[SerializeField] private TextMeshProUGUI healthText;
	[SerializeField] private TextMeshProUGUI oxygenText;
	[SerializeField] private TextMeshProUGUI hungerText;
	[SerializeField] private TextMeshProUGUI thirstText;
	[SerializeField] private TextMeshProUGUI bladderText;
	[SerializeField] private TextMeshProUGUI bowelText;
	[SerializeField] private TextMeshProUGUI staminaText;

	[Header("Oxygen Bar Visibility")]
	[SerializeField] private CanvasGroup oxygenGroup; // optional; hidden unless swimming
	[SerializeField] private PlayerMovement playerMovement;

	[Header("Alert Icons (toggled on/off)")]
	[SerializeField] private GameObject peeAlert;
	[SerializeField] private GameObject poopAlert;
	[SerializeField] private GameObject starvingAlert;
	[SerializeField] private GameObject dehydratedAlert;
	[SerializeField] private GameObject slowedAlert;

	[Header("Death")]
	[SerializeField] private GameObject deathOverlay;

	[Header("HUD Toggle (F1)")]
	[SerializeField] private CanvasGroup hudRoot; // wraps all bars/alerts; toggled with F1

	private float healthTarget = 1f;
	private float oxygenTarget = 1f;
	private float hungerTarget = 1f;
	private float thirstTarget = 1f;
	private float staminaTarget = 1f;
	private bool hudVisible = true;

	private void Awake()
	{
		ResolveReferences();
	}

	// Auto-find the data sources if they weren't assigned in the inspector.
	private void ResolveReferences()
	{
#if UNITY_2023_1_OR_NEWER
		if (playerStats == null) playerStats = FindFirstObjectByType<PlayerStats>();
		if (lifeProcess == null) lifeProcess = FindFirstObjectByType<LifeProcess>();
		if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
#else
		if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
		if (lifeProcess == null) lifeProcess = FindObjectOfType<LifeProcess>();
		if (playerMovement == null) playerMovement = FindObjectOfType<PlayerMovement>();
#endif
	}

	private void Start()
	{
		if (deathOverlay != null) deathOverlay.SetActive(false);

		// seed from current values
		if (playerStats != null)
		{
			healthTarget = playerStats.GetHealthPercent();
			oxygenTarget = playerStats.GetOxygenPercent();
		}
		if (lifeProcess != null)
		{
			hungerTarget = lifeProcess.GetHungerPercent();
			thirstTarget = lifeProcess.GetThirstPercent();
		}
		if (playerMovement != null)
		{
			staminaTarget = playerMovement.GetStaminaPercent();
		}

		SetBar(healthSlider, healthText, healthTarget);
		SetBar(oxygenSlider, oxygenText, oxygenTarget);
		SetBar(hungerSlider, hungerText, hungerTarget);
		SetBar(thirstSlider, thirstText, thirstTarget);
		SetBar(staminaSlider, staminaText, staminaTarget);
	}

	private void OnEnable()
	{
		if (playerStats != null)
		{
			playerStats.OnHealthChanged += OnHealthChanged;
			playerStats.OnOxygenChanged += OnOxygenChanged;
			playerStats.OnDeath += OnDeath;
		}

		if (lifeProcess != null)
		{
			lifeProcess.OnHungerChanged += OnHungerChanged;
			lifeProcess.OnThirstChanged += OnThirstChanged;
		}

		if (playerMovement != null)
			playerMovement.OnStaminaChanged += OnStaminaChanged;
	}

	private void OnDisable()
	{
		if (playerStats != null)
		{
			playerStats.OnHealthChanged -= OnHealthChanged;
			playerStats.OnOxygenChanged -= OnOxygenChanged;
			playerStats.OnDeath -= OnDeath;
		}

		if (lifeProcess != null)
		{
			lifeProcess.OnHungerChanged -= OnHungerChanged;
			lifeProcess.OnThirstChanged -= OnThirstChanged;
		}

		if (playerMovement != null)
			playerMovement.OnStaminaChanged -= OnStaminaChanged;
	}

	private void OnHealthChanged(float pct) => healthTarget = pct;
	private void OnOxygenChanged(float pct) => oxygenTarget = pct;
	private void OnHungerChanged(float pct) => hungerTarget = pct;
	private void OnThirstChanged(float pct) => thirstTarget = pct;
	private void OnStaminaChanged(float pct) => staminaTarget = pct;

	private void OnDeath()
	{
		if (deathOverlay != null) deathOverlay.SetActive(true);
	}

	private void Update()
	{
		// bars track the true values instantly (real-time)
		SetBar(healthSlider, healthText, healthTarget);
		SetBar(oxygenSlider, oxygenText, oxygenTarget);
		SetBar(hungerSlider, hungerText, hungerTarget);
		SetBar(thirstSlider, thirstText, thirstTarget);
		SetBar(staminaSlider, staminaText, staminaTarget);

		if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
			ToggleHud();

		// bladder/bowel have no change events, so read them directly
		if (lifeProcess != null)
		{
			SetBar(bladderSlider, bladderText, lifeProcess.GetBladderPercent());
			SetBar(bowelSlider, bowelText, lifeProcess.GetBowelPercent());

			Toggle(peeAlert, lifeProcess.ShouldPee);
			Toggle(poopAlert, lifeProcess.ShouldPoop);
			Toggle(starvingAlert, lifeProcess.GetHungerPercent() <= 0f);
			Toggle(dehydratedAlert, lifeProcess.GetThirstPercent() <= 0f);
			Toggle(slowedAlert, lifeProcess.IsSlowed);
		}

		// only show the oxygen bar while swimming
		if (oxygenGroup != null && playerMovement != null)
			oxygenGroup.alpha = playerMovement.isSwimming ? 1f : 0f;
	}

	private void ToggleHud()
	{
		hudVisible = !hudVisible;
		if (hudRoot != null)
		{
			hudRoot.alpha = hudVisible ? 1f : 0f;
			hudRoot.interactable = hudVisible;
			hudRoot.blocksRaycasts = hudVisible;
		}
	}

	private void SetBar(Slider slider, TextMeshProUGUI label, float value01)
	{
		if (slider != null) slider.value = value01 * slider.maxValue; // fills 0..100 sliders correctly
		if (label != null) label.text = FormatPercent(value01);
	}

	private static string FormatPercent(float value01) => Mathf.RoundToInt(value01 * 100f) + "%";

	private void Toggle(GameObject go, bool on)
	{
		if (go != null && go.activeSelf != on) go.SetActive(on);
	}
}
